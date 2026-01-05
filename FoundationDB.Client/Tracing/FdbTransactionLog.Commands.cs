#region Copyright (c) 2023-2026 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Filters.Logging
{
	using FoundationDB.Client;

	[PublicAPI]
	public partial class FdbTransactionLog
	{

		private static JsonValue EncodeKeyToJson(Slice value) => value.IsNull ? JsonNull.Null : value.Count == 0 ? JsonString.Empty : JsonString.Return(value.ToString());

		private static JsonArray EncodeKeysToJson(ReadOnlySpan<Slice> keys)
		{
			return JsonArray.FromValues(keys, EncodeKeyToJson);
		}

		private static Slice DecodeKeyFromJson(JsonValue json) => json switch
		{
			JsonNull => Slice.Nil,
			JsonString str => (str.Value is "" or "<empty>" ? Slice.Empty : Slice.Unescape(str.Value)),
			_ => Slice.Empty
		};

		private static JsonObject EncodeKeySelectorToJson(KeySelector selector) => JsonObject.Create(
		[
			("key", EncodeKeyToJson(selector.Key)),
			("orEqual", selector.OrEqual),
			("offset", selector.Offset),
		]);

		private static KeySelector DecodeKeySelectorFromJson(JsonObject json) => new(DecodeKeyFromJson(json["key"]), json.Get<bool>("orEqual"), json.Get<int>("offset"));

		private static Slice[] DecodeKeysFromJson(JsonArray keys)
		{
			if (keys.Count == 0) return [ ];
			var result = new Slice[keys.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = DecodeKeyFromJson(keys.Get<string?>(i, null));
			}
			return result;
		}

		private static Slice DecodeValueFromJson(JsonValue json) => json switch
		{
			JsonNull => Slice.Nil,
			JsonString str => (str.Value is "" or "<empty>" ? Slice.Empty : Slice.Unescape(str.Value)),
			_ => Slice.Empty
		};

		private static JsonValue EncodeValueToJson(Slice value) => value.IsNull ? JsonNull.Null : value.Count == 0 ? JsonString.Empty : JsonString.Return(value.ToString());

		private static JsonArray EncodeValuesToJson(ReadOnlySpan<Slice> values)
		{
			return JsonArray.FromValues(values, EncodeValueToJson);
		}

		private static JsonArray EncodeKeyValuePairsToJson(ReadOnlySpan<KeyValuePair<Slice, Slice>> kvs)
		{
			var arr = new JsonArray(kvs.Length);
			foreach (var kv in kvs)
			{
				arr.Add(JsonArray.ReadOnly.Create(EncodeKeyToJson(kv.Key), EncodeValueToJson(kv.Value)));
			}
			CrystalJsonMarshall.FreezeTopLevel(arr.Freeze());
			return arr;
		}

		/// <summary>Base class of all types of operations performed on a transaction</summary>
		[DebuggerDisplay("{ToString(),nq}")]
		public abstract class Command
		{

			/// <summary>Return the type of operation</summary>
			public abstract Operation Op { get; }

			/// <summary>If true, the operation was executed in Snapshot mode</summary>
			public bool Snapshot { get; internal set; }

			/// <summary>Return the step number of this command</summary>
			/// <remarks>All commands with the same step number where started in parallel</remarks>
			public int Step { get; internal set; }

			/// <summary>Return the end step number of this command</summary>
			public int EndStep { get; internal set; }

			/// <summary>Number of ticks, since the start of the transaction, when the operation was started</summary>
			public TimeSpan StartOffset { get; internal set; }

			/// <summary>Number of ticks, since the start of the transaction, when the operation completed (or null if it did not complete)</summary>
			public TimeSpan? EndOffset { get; internal set; }

			/// <summary>Exception thrown by this operation</summary>
			public Exception? Error { get; internal set; }

			/// <summary>Total size (in bytes) of the arguments</summary>
			/// <remarks>For selectors, only include the size of the keys</remarks>
			public virtual int? ArgumentBytes => null;

			/// <summary>Total size (in bytes) of the result, or null if this operation does not produce a result</summary>
			/// <remarks>Includes the keys and values for range reads</remarks>
			public virtual int? ResultBytes => null;

			/// <summary>Id of the thread that started the command</summary>
			public int ThreadId { get; internal set; }

			/// <summary>StackTrace of the method that started this operation</summary>
			/// <remarks>Only if the <see cref="FdbLoggedFields.RecordOperationStackTrace"/> option is set</remarks>
			public StackTrace? CallSite { get; internal set; }

			/// <summary>Total duration of the command, or TimeSpan.Zero if the command is not yet completed</summary>
			public TimeSpan Duration
			{
				get
				{
					var start = this.StartOffset;
					var end = this.EndOffset;
					return start == TimeSpan.Zero || !end.HasValue ? TimeSpan.Zero : (end.Value - start);
				}
			}

			/// <summary>Returns a formatted representation of the arguments, for logging purpose</summary>
			public virtual string GetArguments(KeyResolver resolver)
			{
				return string.Empty;
			}

			/// <summary>Returns a formatted representation of the results, for logging purpose</summary>
			public virtual string GetResult(KeyResolver resolver)
			{
				if (this.Error != null)
				{
					return this.Error is FdbException fdbEx
						? "[" + fdbEx.Code.ToString() + "] " + fdbEx.Message
						: "[" + this.Error.GetType().Name + "] " + this.Error.Message;
				}
				return string.Empty;
			}

			/// <summary>Return the mode of the operation (Read, Write, Metadata, Watch, ...)</summary>
			public virtual Mode Mode
			{
				get
				{
					switch (this.Op)
					{
						case Operation.Invalid:
							return Mode.Invalid;

						case Operation.Set:
						case Operation.Clear:
						case Operation.ClearRange:
						case Operation.Atomic:
							return Mode.Write;

						case Operation.Get:
						case Operation.GetKey:
						case Operation.GetValues:
						case Operation.GetKeys:
						case Operation.GetRange:
						case Operation.CheckValue:
						case Operation.GetVersionStamp:
						case Operation.GetAddressesForKey:
						case Operation.GetRangeSplitPoints:
						case Operation.GetEstimatedRangeSizeBytes:
						case Operation.GetApproximateSize:
							return Mode.Read;

						case Operation.GetReadVersion:
						case Operation.Reset:
						case Operation.Cancel:
						case Operation.AddConflictRange:
						case Operation.Commit:
						case Operation.OnError:
						case Operation.SetOption:
							return Mode.Meta;

						case Operation.Watch:
							return Mode.Watch;

						case Operation.Log:
							return Mode.Annotation;

						default:
						{ 
#if DEBUG
							//FIXME: we probably forgot to add a case for a new type of command !
							if (Debugger.IsAttached) Debugger.Break();
#endif
							return Mode.Invalid;
						}
					}
				}
			}

			/// <summary>Returns the short version of the command name (up to two characters)</summary>
			public virtual string ShortName
			{
				get
				{
					switch (this.Op)
					{
						case Operation.Commit: return "Co";
						case Operation.Reset: return "Rz";
						case Operation.Cancel: return "Cn";
						case Operation.OnError: return "Er";
						case Operation.GetReadVersion: return "rv";
						case Operation.SetOption: return "op";

						case Operation.Get: return "G ";
						case Operation.GetValues: return "G*";
						case Operation.GetKey: return "K ";
						case Operation.GetKeys: return "K*";
						case Operation.GetRange: return "R ";
						case Operation.Set: return "s ";
						case Operation.Clear: return "c ";
						case Operation.ClearRange: return "cr";
						case Operation.AddConflictRange: return "rc";
						case Operation.Atomic: return "a ";

						case Operation.Log: return "//";
						case Operation.Watch: return "W ";
					}
					return "??";
				}
			}

			public sealed override string ToString()
			{
				return ToString(null);
			}

			public virtual string ToString(KeyResolver? resolver)
			{
				resolver ??= KeyResolver.Default;
				var arg = GetArguments(resolver);
				var res = GetResult(resolver);
				var sb = new StringBuilder(255);
				if (this.Snapshot) sb.Append("Snapshot.");
				sb.Append(this.Op.ToString());
				if (!string.IsNullOrEmpty(arg)) sb.Append(' ').Append(arg);
				if (!string.IsNullOrEmpty(res)) sb.Append(" => ").Append(res);
				return sb.ToString();
			}

			protected virtual string ResolveKey(Slice key, Func<Slice, string>? resolver)
			{
				return resolver == null ? FdbKey.Dump(key) : resolver(key);
			}

			/// <summary>Serializes this <see cref="Command"/> into a <see cref="JsonObject"/></summary>
			/// <remarks>The object can be deserialized back into a <see cref="Command"/> by calling <see cref="FromJson(JsonObject)"/></remarks>
			public JsonObject ToJson(bool readOnly = true)
			{
				var obj = new JsonObject();
				obj["op"] = this.Op.ToString();
				obj["startOffset"] = this.StartOffset;
				obj["endOffset"] = this.EndOffset;
				obj["duration"] = this.Duration;
				obj["step"] = this.Step;
				obj.AddIfTrue("endStep", this.EndStep != this.Step, this.EndStep);
				obj.AddIfTrue("snapshot", this.Snapshot);
				OnJsonSerialize(obj);
				obj.AddIfNonZero("threadId", this.ThreadId);
				if (readOnly) obj.Freeze();
				return obj;
			}

			/// <summary>Deserializes a <see cref="Command"/> previously serialized with a call to <see cref="ToJson"/></summary>
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public static Command FromJson(JsonObject obj)
			{
				Contract.Debug.Requires(obj is not null);
				return new DeserializedCommand(obj);
			}

			/// <summary>Deserializes an array of <see cref="Command"/></summary>
			/// <param name="arr">Array with one or more commands serialized into JSON Objects</param>
			public static List<Command> FromJson(JsonArray arr)
			{
				var res = new List<Command>(arr.Count);
				foreach (var obj in arr.AsObjects())
				{
					res.Add(FromJson(obj));
				}
				return res;
			}

			protected abstract void OnJsonSerialize(JsonObject obj);

		}

		private sealed class DeserializedCommand : Command
		{

			public DeserializedCommand(JsonObject obj)
			{
				this.Data = obj;
				this.Op = obj.Get<Operation>("op");
				this.StartOffset = obj.Get<TimeSpan>("startOffset");
				this.EndOffset = obj.Get<TimeSpan?>("endOffset", null);
				this.Step = obj.Get<int>("step", 0);
				this.EndStep = obj.Get<int>("endStep", 0);
				this.Snapshot = obj.Get<bool>("snapshot", false);
				this.ThreadId = obj.Get<int>("threadId", 0);
				//TODO: more?
			}

			private JsonObject Data { get; }

			/// <inheritdoc />
			public override Operation Op { get; }

			/// <inheritdoc />
			public override string GetArguments(KeyResolver resolver)
			{
				switch (this.Op)
				{
					case Operation.Log:
					{
						return this.Data["message"].ToString();
					}
					case Operation.Clear:
					case Operation.Get:
					case Operation.Watch:
					{
						return FdbKey.Dump(DecodeKeyFromJson(this.Data["key"]));
					}
					case Operation.Set:
					{
						return FdbKey.Dump(DecodeKeyFromJson(this.Data["key"])) + " = " + Slice.Dump(DecodeValueFromJson(this.Data["value"]));
					}
					case Operation.ClearRange:
					case Operation.GetRangeSplitPoints:
					{
						return FdbKey.Dump(DecodeKeyFromJson(this.Data["begin"])) + " = " + FdbKey.Dump(DecodeKeyFromJson(this.Data["end"]));
					}
					case Operation.GetRange:
					{
						return DecodeKeySelectorFromJson(this.Data.GetObject("begin")) + " ... " + DecodeKeySelectorFromJson(this.Data.GetObject("end"));
					}
					case Operation.GetValues:
					{
						return this.Data["keys"].ToString();
					}
					case Operation.GetKey:
					{
						return DecodeKeySelectorFromJson(this.Data.GetObject("selector")).ToString();
					}
					case Operation.CheckValue:
					{
						return $"{FdbKey.Dump(DecodeKeyFromJson(this.Data["key"]))} =? {this.Data["expected"]}";
					}
					case Operation.Atomic:
					{
						return $"{this.Data["mutation"].ToString()}({FdbKey.Dump(DecodeKeyFromJson(this.Data["key"]))}, {Slice.Dump(DecodeValueFromJson(this.Data["param"]))})";
					}
					default:
					{
						return "<???>";
					}
				}
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj) => throw new NotSupportedException();

		}

		/// <summary>Base class of all types of operations performed on a transaction, that return a result</summary>
		public abstract class Command<TResult> : Command
		{
			private const int MAX_LENGTH = 160;

			/// <summary>Optional result of the operation</summary>
			public Maybe<TResult> Result { get; internal set; }

			public override string GetResult(KeyResolver resolver)
			{
				if (this.Error != null) return base.GetResult(resolver);

				if (this.Result.Failed) return "<error>";
				if (!this.Result.HasValue) return "<n/a>";
				if (this.Result.Value is null) return "<null>";

				string res = Dump(this.Result.Value, resolver);
				if (res.Length > MAX_LENGTH)
				{
					if (res.Length > MAX_LENGTH)
					{
						res = string.Concat(res.AsSpan(0, MAX_LENGTH / 2), "...", res.AsSpan(res.Length - (MAX_LENGTH / 2), MAX_LENGTH / 2));
					}
				}

				return res;
			}

			protected virtual string Dump(TResult value, KeyResolver resolver)
			{
				return value?.ToString() ?? "<default>";
			}
		}

		public class KeyResolver
		{

			public static readonly KeyResolver Default = new();

			public virtual string Resolve(Slice key)
			{
				return FdbKey.PrettyPrint(key, FdbKey.PrettyPrintMode.Single);
			}

			public virtual string ResolveBegin(Slice key)
			{
				return FdbKey.PrettyPrint(key, FdbKey.PrettyPrintMode.Begin);
			}

			public virtual string ResolveEnd(Slice key)
			{
				return FdbKey.PrettyPrint(key, FdbKey.PrettyPrintMode.End);
			}

		}

		/// <summary>Key resolver which replace key prefixes by the name of their enclosing directory subspace</summary>
		public class DirectoryKeyResolver : KeyResolver
		{

			public readonly Slice[] Prefixes;
			public readonly string[] Paths;

			public DirectoryKeyResolver(Dictionary<Slice, string> knownSubspaces)
			{
				Contract.Debug.Requires(knownSubspaces != null);
				var prefixes = new Slice[knownSubspaces.Count];
				var paths = new string[knownSubspaces.Count];
				int p = 0;
				foreach (var kv in knownSubspaces)
				{
					prefixes[p] = kv.Key;
					paths[p] = kv.Value;
					p++;
				}

				Array.Sort(prefixes, paths, Slice.Comparer.Default);
				this.Prefixes = prefixes;
				this.Paths = paths;
			}

			/// <summary>Create a key resolver using the content of a DirectoryLayer as the map</summary>
			/// <returns>Resolver that replace each directory prefix by its name</returns>
			public static async Task<DirectoryKeyResolver> BuildFromDirectoryLayer(IFdbReadOnlyTransaction tr, FdbDirectoryLayer directory)
			{
				var metadata = await directory.Resolve(tr).ConfigureAwait(false);
				var location = metadata.Partition.Nodes;

				//HACKHACK: for now, we will simply poke inside the node subspace of the directory layer, which is brittle (if the structure changes in future versions!)
				// Entries that correspond to subfolders have the form: NodeSubspace.Pack( (parent_prefix, 0, "child_name") ) = child_prefix
				var keys = await tr.GetRange(location.ToRange()).ToListAsync().ConfigureAwait(false);

				var map = new Dictionary<Slice, string>(Slice.Comparer.Default);

				foreach (var entry in keys)
				{
					var t = TuPack.Unpack(location.GetSuffix(entry.Key));
					// look for a tuple of size 3 with 0 as the second element...
					if (t.Count != 3 || t.Get<int>(1) != 0) continue;

					//var parent = t.Get<Slice>(0); //TODO: use this to construct the full materialized path of this directory? (would need more than one pass)
					string name = t.Get<string?>(2) ?? string.Empty;

					map[entry.Value] = name;
				}

				return new DirectoryKeyResolver(map);
			}

			private bool TryLookup(Slice key, out Slice prefix, out string? path)
			{
				prefix = default;
				path = null;

				if (key.IsNullOrEmpty) return false;

				int p = Array.BinarySearch(this.Prefixes, key, Slice.Comparer.Default);
				if (p >= 0)
				{ // direct match!
					prefix = this.Prefixes[p];
					path = this.Paths[p];
					return true;
				}

				p = ~p;
				if (p > 0)
				{
					// check if the previous prefix matches
					p = p - 1;
					if (key.StartsWith(this.Prefixes[p]))
					{
						prefix = this.Prefixes[p];
						path = this.Paths[p];
						return true;
					}
				}

				return false;
			}

			public override string Resolve(Slice key)
			{
				if (!TryLookup(key, out Slice prefix, out string? path))
				{
					return base.Resolve(key);
				}

				var s = base.Resolve(key.Substring(prefix.Count));
				if (s != null! && s.Length >= 3 && s[0] == '(' && s[^1] == ')')
				{ // that was a tuple
					return string.Concat("([", path, "], ", s.AsSpan(1));
				}
				return string.Concat("[", path, "]:", s);
			}
		}

		public sealed class LogCommand : Command
		{
			public string Message { get; }

			public override Operation Op => Operation.Log;

			public LogCommand(string message)
			{
				this.Message = message;
			}

			public override string ToString(KeyResolver? resolver)
			{
				return "// " + this.Message;
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["message"] = this.Message;
			}

		}

		public sealed class SetOptionCommand : Command
		{
			/// <summary>Option that is set on the transaction</summary>
			public FdbTransactionOption Option { get; }

			/// <summary>Integer value (if not null)</summary>
			public long? IntValue { get; }

			/// <summary>String value (if not null)</summary>
			public string? StringValue { get; }

			public override Operation Op => Operation.SetOption;

			public SetOptionCommand(FdbTransactionOption option)
			{
				this.Option = option;
			}

			public SetOptionCommand(FdbTransactionOption option, long value)
			{
				this.Option = option;
				this.IntValue = value;
			}

			public SetOptionCommand(FdbTransactionOption option, string value)
			{
				this.Option = option;
				this.StringValue = value;
			}

			public override string GetArguments(KeyResolver resolver)
			{
				if (this.IntValue.HasValue)
				{
					return $"{this.Option.ToString()} = {this.IntValue.Value}";
				}
				if (this.StringValue != null)
				{
					return $"{this.Option.ToString()} = '{this.StringValue}'";
				}
				return this.Option.ToString();
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["message"] = this.Option.ToString();
				obj.AddIfNotNull("intValue", this.IntValue);
				obj.AddIfNotNull("strValue", this.StringValue);
			}

		}

		public sealed class SetCommand : Command
		{
			/// <summary>Key modified in the database</summary>
			public Slice Key { get; }

			/// <summary>Value written to the key</summary>
			public Slice Value { get; }

			public override Operation Op => Operation.Set;

			public SetCommand(Slice key, Slice value)
			{
				this.Key = key;
				this.Value = value;
			}

			public override int? ArgumentBytes => this.Key.Count + this.Value.Count;

			public override string GetArguments(KeyResolver resolver)
			{
				return string.Concat(resolver.Resolve(this.Key), " = ", this.Value.ToString("V"));
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["key"] = EncodeKeyToJson(this.Key);
				obj["value"] = EncodeValueToJson(this.Value);
			}

		}

		public sealed class ClearCommand : Command
		{
			/// <summary>Key cleared from the database</summary>
			public Slice Key { get; }

			public override Operation Op => Operation.Clear;

			public ClearCommand(Slice key)
			{
				this.Key = key;
			}

			public override int? ArgumentBytes => this.Key.Count;

			public override string GetArguments(KeyResolver resolver)
			{
				return resolver.Resolve(this.Key);
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj.Add("key", EncodeKeyToJson(this.Key));
			}

		}

		public sealed class ClearRangeCommand : Command
		{
			/// <summary>Begin of the range cleared</summary>
			public Slice Begin { get; }

			/// <summary>End of the range cleared</summary>
			public Slice End { get; }

			public override Operation Op => Operation.ClearRange;

			public ClearRangeCommand(Slice begin, Slice end)
			{
				this.Begin = begin;
				this.End = end;
			}

			public override int? ArgumentBytes => this.Begin.Count + this.End.Count;

			public override string GetArguments(KeyResolver resolver)
			{
				return string.Concat(resolver.ResolveBegin(this.Begin), " <= k < ", resolver.ResolveEnd(this.End));
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj.Add("begin", EncodeKeyToJson(this.Begin));
				obj.Add("end", EncodeKeyToJson(this.End));
			}

		}

		public class AtomicCommand : Command
		{
			/// <summary>Type of mutation performed on the key</summary>
			public FdbMutationType Mutation { get; }

			/// <summary>Key modified in the database</summary>
			public Slice Key { get; set; }

			/// <summary>Parameter depending on the type of mutation</summary>
			public Slice Param { get; }

			public override Operation Op => Operation.Atomic;

			public AtomicCommand(Slice key, Slice param, FdbMutationType mutation)
			{
				this.Key = key;
				this.Param = param;
				this.Mutation = mutation;
			}

			public override int? ArgumentBytes => this.Key.Count + this.Param.Count;

			private Slice GetUserKey()
			{
				var key = this.Key;

				//TODO: FIXME: the ApiVersion should be stored with the command or log, not read from a static variable, because it will prevent us from loading a log from a file, produced by another server with a different Api Version!
				if (this.Mutation == FdbMutationType.VersionStampedKey)
				{ // we must remove the stamp offset at the end
					if (Fdb.ApiVersion >= 520)
					{ // 4 bytes
						key = key.Substring(0, key.Count - 4);
					}
					else if(Fdb.ApiVersion >= 400)
					{ // 2 bytes
						key = key.Substring(0, key.Count - 2);
					}
				}

				return key;
			}

			private Slice GetUserValue()
			{
				var val = this.Param;

				//TODO: FIXME: the ApiVersion should be stored with the command or log, not read from a static variable, because it will prevent us from loading a log from a file, produced by another server with a different Api Version!
				if (this.Mutation == FdbMutationType.VersionStampedValue)
				{ // we must remove the stamp offset at the end
					if (Fdb.ApiVersion >= 520)
					{ // 4 bytes
						val = val.Substring(0, val.Count - 4);
					}
				}

				return val;
			}

			public override string GetArguments(KeyResolver resolver)
			{
				return string.Concat(resolver.Resolve(GetUserKey()), " ", this.Mutation.ToString(), " ", GetUserValue().ToString("V"));
			}

			public override string ToString(KeyResolver? resolver)
			{
				resolver ??= KeyResolver.Default;
				var sb = new StringBuilder();
				if (this.Snapshot) sb.Append("Snapshot.");

				// Depending on the type of mutations, the value is either known to be binary, or could be anything...
				var value = GetUserValue();
				string str;
				string? suffix = null;
				switch (this.Mutation)
				{
					case FdbMutationType.VersionStampedKey:
					{
						str = value.ToString("V");
						break;
					}
					default:
					{
						str = "<" + value.ToHexString(' ') + ">";
						switch (value.Count)
						{
							case 4:
								suffix = " (" + value.ToInt32() + ")";
								break;
							case 8:
								suffix = " (" + value.ToInt64() + ")";
								break;
						}
						break;
					}
				}
				sb.Append("Atomic_").Append(this.Mutation.ToString()).Append(' ').Append(resolver.Resolve(GetUserKey())).Append(", ").Append(str).Append(suffix);
				return sb.ToString();
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj.Add("mutation", this.Mutation.ToString());
				obj.Add("key", EncodeKeyToJson(this.Key));
				obj.Add("param", EncodeValueToJson(this.Param));
			}

		}

		public sealed class AddConflictRangeCommand : Command
		{
			/// <summary>Type of conflict</summary>
			public FdbConflictRangeType Type { get; }

			/// <summary>Begin of the conflict range</summary>
			public Slice Begin { get; }

			/// <summary>End of the conflict range</summary>
			public Slice End { get; }

			public override Operation Op => Operation.AddConflictRange;

			public AddConflictRangeCommand(Slice begin, Slice end, FdbConflictRangeType type)
			{
				this.Begin = begin;
				this.End = end;
				this.Type = type;
			}

			public override int? ArgumentBytes => this.Begin.Count + this.End.Count;

			public override string GetArguments(KeyResolver resolver)
			{
				return string.Concat(this.Type.ToString(), "! ", resolver.ResolveBegin(this.Begin), " <= k < ", resolver.ResolveEnd(this.End));
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj.Add("conflictType", this.Type.ToString());
				obj.Add("begin", EncodeKeyToJson(this.Begin));
				obj.Add("end", EncodeKeyToJson(this.End));
			}

		}

		/// <summary>Represents a <see cref="IFdbReadOnlyTransaction.GetAsync"/> operation</summary>
		/// <typeparam name="TResult">Type of the decoded value (or <see cref="Slice"/> for raw reads)</typeparam>
		public sealed class GetCommand<TResult> : Command<TResult>
		{
			/// <summary>Key read from the database</summary>
			public Slice Key { get; }

			/// <inheritdoc />
			public override Operation Op => Operation.Get;

			public GetCommand(Slice key, bool snapshot)
			{
				this.Key = key;
				this.Snapshot = snapshot;
			}

			/// <inheritdoc />
			public override int? ArgumentBytes => this.Key.Count;

			/// <inheritdoc />
			public override int? ResultBytes
			{
				get
				{
					if (typeof(TResult) == typeof(Slice))
					{
						return this.Result.HasValue ? ((Slice) (object) this.Result.Value!).Count : null;
					}
					return null;
					//BUGBUG: how to track the actual number of bytes?
				}
			}

			/// <inheritdoc />
			public override string GetArguments(KeyResolver resolver)
			{
				return resolver.Resolve(this.Key);
			}

			/// <inheritdoc />
			public override string GetResult(KeyResolver resolver)
			{
				if (this.Result.HasValue)
				{
					if (typeof(TResult) == typeof(Slice))
					{
						if (((Slice) (object) this.Result.Value!).IsNull) return "not_found";
						if (((Slice) (object) this.Result.Value!).IsEmpty) return "''";
					}
					else
					{
						return STuple.Formatter.Stringify(this.Result.Value);
					}
				}
				return base.GetResult(resolver);
			}

			/// <inheritdoc />
			protected override string Dump(TResult value, KeyResolver resolver)
			{
				if (typeof(TResult) == typeof(Slice))
				{
					return ((Slice) (object) value!).ToString("P");
				}

				return STuple.Formatter.Stringify(value);
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["key"] = EncodeKeyToJson(this.Key);
				if (this.Result.HasValue)
				{
					if (typeof(TResult) == typeof(Slice))
					{
						obj["value"] = EncodeValueToJson((Slice) (object) this.Result.Value!);
					}
					else
					{
						obj["value"] = JsonValue.FromValue(this.Result.Value);
					}
				}
			}

		}

		public sealed class GetKeyCommand : Command<Slice>
		{
			/// <summary>Selector to a key in the database</summary>
			public KeySelector Selector { get; }

			public override Operation Op => Operation.GetKey;

			public GetKeyCommand(KeySelector selector, bool snapshot)
			{
				this.Selector = selector;
				this.Snapshot = snapshot;
			}

			public override int? ArgumentBytes => this.Selector.Key.Count;

			public override int? ResultBytes => !this.Result.HasValue ? null : this.Result.Value.Count;

			public override string GetArguments(KeyResolver resolver)
			{
				//TODO: use resolver!
				return this.Selector.ToString();
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["selector"] = EncodeKeySelectorToJson(this.Selector);
				if (this.Result.HasValue)
				{
					obj["result"] = EncodeKeyToJson(this.Result.Value);
				}
			}

		}

		public sealed class GetValuesCommand : Command<Slice[]>
		{
			/// <summary>List of keys read from the database</summary>
			public Slice[] Keys { get; }

			public override Operation Op => Operation.GetValues;

			public GetValuesCommand(Slice[] keys, bool snapshot)
			{
				Contract.Debug.Requires(keys != null);
				this.Keys = keys;
				this.Snapshot = snapshot;
			}

			public override int? ArgumentBytes
			{
				get
				{
					int sum = 0;
					for (int i = 0; i < this.Keys.Length; i++) sum += this.Keys[i].Count;
					return sum;
				}
			}

			public override int? ResultBytes
			{
				get
				{
					var array = this.Result.GetValueOrDefault();
					if (array == null) return null;
					int sum = 0;
					for (int i = 0; i < array.Length; i++) sum += array[i].Count;
					return sum;
				}
			}

			public override string GetArguments(KeyResolver resolver)
			{
				string s = string.Concat("[", this.Keys.Length.ToString(), "] {");
				if (this.Keys.Length > 0) s += resolver.Resolve(this.Keys[0]);
				if (this.Keys.Length > 1) s += " ... " + resolver.Resolve(this.Keys[^1]);
				return s + " }";
			}

			protected override string Dump(Slice[] res, KeyResolver resolver)
			{
				return res.Length switch
				{
					0 => "<empty>",
					1 => $"[1] {{ {res[0]:P} }}",
					2 => $"[2] {{ {res[0]:P}, {res[1]:P} }}",
					3 => $"[3] {{ {res[0]:P}, {res[1]:P}, {res[2]:P} }}",
					_ => $"[{res.Length:N0}] {{ {res[0]:P}, {res[1]:P}, ..., {res[^1]:P} }}"
				};
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["keys"] = EncodeKeysToJson(this.Keys);
				if (this.Result.HasValue)
				{
					obj["values"] = EncodeValuesToJson(this.Result.Value);
				}
			}

		}

		public sealed class GetValuesCommand<TValue> : Command<long>
		{

			/// <summary>List of keys read from the database</summary>
			public Slice[] Keys { get; }

			/// <summary>Buffer where the decoded values will be stored</summary>
			public TValue[] Values { get; }

			public override Operation Op => Operation.GetValues;

			public GetValuesCommand(Slice[] keys, TValue[] values, bool snapshot)
			{
				Contract.Debug.Requires(keys != null && values.Length >= keys.Length);
				this.Keys = keys;
				this.Values = values;
				this.Snapshot = snapshot;
			}

			public override int? ArgumentBytes
			{
				get
				{
					long sum = 0;
					for (int i = 0; i < this.Keys.Length; i++)
					{
						sum += this.Keys[i].Count;
					}
					return sum <= int.MaxValue ? unchecked((int) sum) : null;
				}
			}

			public override int? ResultBytes
			{
				get
				{
					if (typeof(TValue) == typeof(Slice))
					{
						if (this.Result.HasValue)
						{
							var array = (Slice[]) (object) this.Values;
							long sum = 0;
							for (int i = 0; i < array.Length; i++) sum += array[i].Count;
							return sum <= int.MaxValue ? unchecked((int) sum) : null;
						}
					}
					//TODO: how to account for the number of bytes received?
					return null;
				}
			}

			public override string GetArguments(KeyResolver resolver)
			{
				string s = string.Concat("[", this.Keys.Length.ToString(), "] {");
				if (this.Keys.Length > 0) s += resolver.Resolve(this.Keys[0]);
				if (this.Keys.Length > 1) s += " ... " + resolver.Resolve(this.Keys[^1]);
				return s + " }";
			}

			protected override string Dump(long res, KeyResolver resolver)
			{
				var values = this.Values.AsSpan();
				if (typeof(TValue) == typeof(Slice))
				{
					return values.Length switch
					{
						0 => "<empty>",
						1 => string.Create(CultureInfo.InvariantCulture, $"[1] {{ {values[0]:P} }}"),
						2 => string.Create(CultureInfo.InvariantCulture, $"[2] {{ {values[0]:P}, {values[1]:P} }}"),
						3 => string.Create(CultureInfo.InvariantCulture, $"[3] {{ {values[0]:P}, {values[1]:P}, {values[2]:P} }}"),
						_ => string.Create(CultureInfo.InvariantCulture, $"[{values.Length:N0}] {{ {values[0]:P}, {values[1]:P}, ..., {values[^1]:P} }}")
					};
				}
				else
				{
					return values.Length switch
					{
						0 => "<empty>",
						1 => string.Create(CultureInfo.InvariantCulture, $"[1] {{ {values[0]} }}"),
						2 => string.Create(CultureInfo.InvariantCulture, $"[2] {{ {values[0]}, {values[1]} }}"),
						3 => string.Create(CultureInfo.InvariantCulture, $"[3] {{ {values[0]}, {values[1]}, {values[2]} }}"),
						_ => string.Create(CultureInfo.InvariantCulture, $"[{values.Length:N0}] {{ {values[0]}, {values[1]}, ..., {values[^1]} }}")
					};
				}
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["keys"] = EncodeKeysToJson(this.Keys);
				if (this.Result.HasValue)
				{
					if (typeof(TValue) == typeof(Slice))
					{
						obj["values"] = EncodeValuesToJson((Slice[]) (object) this.Values);
					}
					else
					{
						obj["values"] = JsonArray.FromValues(this.Values);
					}
				}
			}

		}

		public sealed class GetKeysCommand : Command<Slice[]>
		{
			/// <summary>List of selectors looked up in the database</summary>
			public KeySelector[] Selectors { get; }

			public override Operation Op => Operation.GetKeys;

			public GetKeysCommand(KeySelector[] selectors, bool snapshot)
			{
				Contract.Debug.Requires(selectors != null);
				this.Selectors = selectors;
				this.Snapshot = snapshot;
			}

			public override int? ArgumentBytes
			{
				get
				{
					int sum = 0;
					for (int i = 0; i < this.Selectors.Length; i++) sum += this.Selectors[i].Key.Count;
					return sum;
				}
			}

			public override int? ResultBytes
			{
				get
				{
					var array = this.Result.GetValueOrDefault();
					if (array == null) return null;
					int sum = 0;
					for (int i = 0; i < array.Length; i++) sum += array[i].Count;
					return sum;
				}
			}

			public override string GetArguments(KeyResolver resolver)
			{
				string s = string.Concat("[", this.Selectors.Length.ToString(), "] {");
				//TODO: use resolver!
				if (this.Selectors.Length > 0) s += this.Selectors[0].ToString();
				if (this.Selectors.Length > 1) s += " ... " + this.Selectors[^1].ToString();
				return s + " }";
			}

			protected override string Dump(Slice[] res, KeyResolver resolver)
			{
				return res.Length switch
				{
					0 => "<empty>",
					1 => $"[1] {{ {resolver.Resolve(res[0])} }}",
					2 => $"[2] {{ {resolver.Resolve(res[0])}, {resolver.Resolve(res[1])} }}",
					3 => $"[3] {{ {resolver.Resolve(res[0])}, {resolver.Resolve(res[1])}, {resolver.Resolve(res[2])} }}",
					_ => $"[{res.Length:N0}] {{ {resolver.Resolve(res[0])}, {resolver.Resolve(res[1])}, ..., {resolver.Resolve(res[^1])} }}"
				};
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["selectors"] = JsonArray.FromValues(this.Selectors, EncodeKeySelectorToJson);
				if (this.Result.HasValue)
				{
					obj["values"] = EncodeKeysToJson(this.Result.Value);
				}
			}

		}

		public sealed class GetRangeCommand : Command<FdbRangeChunk>
		{
			/// <summary>Selector to the start of the range</summary>
			public KeySelector Begin { get; }

			/// <summary>Selector to the end of the range</summary>
			public KeySelector End { get; }

			/// <summary>Options of the range read</summary>
			public FdbRangeOptions Options { get; }

			/// <summary>Iteration number</summary>
			public int Iteration { get; }

			public override Operation Op => Operation.GetRange;

			public GetRangeCommand(KeySelector begin, KeySelector end, bool snapshot, FdbRangeOptions options, int iteration)
			{
				this.Begin = begin;
				this.End = end;
				this.Snapshot = snapshot;
				this.Options = options;
				this.Iteration = iteration;
			}

			public override int? ArgumentBytes => this.Begin.Key.Count + this.End.Key.Count;

			public override int? ResultBytes => this.Result.GetValueOrDefault()?.TotalBytes;

			public override string GetArguments(KeyResolver resolver)
			{
				//TODO: use resolver!
				string s = this.Begin.PrettyPrint(FdbKey.PrettyPrintMode.Begin) + " <= k < " + this.End.PrettyPrint(FdbKey.PrettyPrintMode.End);
				if (this.Iteration > 1) s += ", #" + this.Iteration.ToString();
				if (this.Options.Limit != null && this.Options.Limit.Value > 0) s += ", limit(" + this.Options.Limit.Value.ToString() + ")";
				if (this.Options.IsReversed) s += ", reverse";
				if (this.Options.Streaming.HasValue) s += ", " + this.Options.Streaming.Value.ToString();
				if (this.Options.Fetch.HasValue) s += ", " + this.Options.Fetch.Value.ToString();
				return s;
			}

			public override string GetResult(KeyResolver resolver)
			{
				var chunk = this.Result.GetValueOrDefault();
				if (chunk != null)
				{
					string s = $"{chunk.Count:N0} result(s)";
					if (chunk.HasMore) s += ", has_more";
					return s;
				}
				return base.GetResult(resolver);
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["begin"] = EncodeKeySelectorToJson(this.Begin);
				obj["end"] = EncodeKeySelectorToJson(this.End);
				obj["iteration"] = this.Iteration;
				obj["options"] = JsonObject.FromObject(this.Options);
				if (this.Result.HasValue)
				{
					obj["values"] = EncodeKeyValuePairsToJson(this.Result.Value!.Items);
				}
			}

		}

		public sealed class GetRangeCommand<TResult> : Command<FdbRangeChunk<TResult>>
		{
			/// <summary>Selector to the start of the range</summary>
			public KeySelector Begin { get; }

			/// <summary>Selector to the end of the range</summary>
			public KeySelector End { get; }

			/// <summary>Options of the range read</summary>
			public FdbRangeOptions Options { get; }

			/// <summary>Iteration number</summary>
			public int Iteration { get; }

			public override Operation Op => Operation.GetRange;

			public GetRangeCommand(KeySelector begin, KeySelector end, bool snapshot, FdbRangeOptions options, int iteration)
			{
				this.Begin = begin;
				this.End = end;
				this.Snapshot = snapshot;
				this.Options = options;
				this.Iteration = iteration;
			}

			public override int? ArgumentBytes => this.Begin.Key.Count + this.End.Key.Count;

			public override int? ResultBytes => this.Result.GetValueOrDefault()?.TotalBytes;

			public override string GetArguments(KeyResolver resolver)
			{
				//TODO: use resolver!
				string s = $"{this.Begin.PrettyPrint(FdbKey.PrettyPrintMode.Begin)} <= k < {this.End.PrettyPrint(FdbKey.PrettyPrintMode.End)}";
				if (this.Iteration > 1) s += $", #{this.Iteration:N0}";
				if (this.Options.Limit != null && this.Options.Limit.Value > 0) s += $", limit({this.Options.Limit.Value:N0})";
				if (this.Options.IsReversed) s += ", reverse";
				if (this.Options.Streaming.HasValue) s += $", {this.Options.Streaming.Value}";
				if (this.Options.Fetch.HasValue) s += $", {this.Options.Fetch.Value}";
				return s;
			}

			public override string GetResult(KeyResolver resolver)
			{
				var chunk = this.Result.GetValueOrDefault();
				if (chunk != null)
				{
					return string.Create(CultureInfo.InvariantCulture, $"{chunk.Count:N0} result(s){(chunk.HasMore ? ", has_more" : "")}");
				}
				return base.GetResult(resolver);
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["begin"] = EncodeKeySelectorToJson(this.Begin);
				obj["end"] = EncodeKeySelectorToJson(this.End);
				obj["iteration"] = this.Iteration;
				obj["options"] = JsonObject.FromObject(this.Options);
				if (this.Result.HasValue)
				{
					if (typeof(TResult) == typeof(KeyValuePair<Slice, Slice>))
					{
						obj["values"] = EncodeKeyValuePairsToJson(((FdbRangeChunk<KeyValuePair<Slice, Slice>>) (object) this.Result.Value!).Items.Span);
					}
					else if (typeof(TResult) == typeof(Slice))
					{
						if (this.Options.Fetch == FdbFetchMode.ValuesOnly)
						{
							obj["values"] = EncodeValuesToJson(((FdbRangeChunk<Slice>) (object) this.Result.Value!).Items.Span);
						}
						else
						{
							obj["values"] = EncodeKeysToJson(((FdbRangeChunk<Slice>) (object) this.Result.Value!).Items.Span);
						}
					}
					else
					{
						obj["values"] = JsonArray.FromValues(this.Result.Value!.Items.Span);
					}
				}
			}

		}

		public sealed class VisitRangeCommand : Command<FdbRangeResult>
		{
			/// <summary>Selector to the start of the range</summary>
			public KeySelector Begin { get; }

			/// <summary>Selector to the end of the range</summary>
			public KeySelector End { get; }

			/// <summary>Options of the range read</summary>
			public FdbRangeOptions Options { get; }

			/// <summary>Iteration number</summary>
			public int Iteration { get; }

			public override Operation Op => Operation.GetRange;

			public VisitRangeCommand(KeySelector begin, KeySelector end, bool snapshot, FdbRangeOptions options, int iteration)
			{
				this.Begin = begin;
				this.End = end;
				this.Snapshot = snapshot;
				this.Options = options;
				this.Iteration = iteration;
			}

			public override int? ArgumentBytes => this.Begin.Key.Count + this.End.Key.Count;

			public override int? ResultBytes => this.Result.GetValueOrDefault()?.TotalBytes;

			public override string GetArguments(KeyResolver resolver)
			{
				//TODO: use resolver!
				string s = $"{this.Begin:B} <= k < {this.End:E}";
				if (this.Iteration > 1) s += $", #{this.Iteration:N0}";
				if (this.Options.Limit is > 0) s += $", limit({this.Options.Limit.Value:N0})";
				if (this.Options.IsReversed) s += ", reverse";
				if (this.Options.Streaming.HasValue) s += $", {this.Options.Streaming.Value}";
				if (this.Options.Fetch.HasValue) s += $", {this.Options.Fetch.Value}";
				return s;
			}

			public override string GetResult(KeyResolver resolver)
			{
				var result = this.Result.GetValueOrDefault();
				if (result != null)
				{
					return string.Create(CultureInfo.InvariantCulture, $"{result.Count:N0} result(s){(result.HasMore ? ", has_more" : "")}");
				}
				return base.GetResult(resolver);
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["begin"] = EncodeKeySelectorToJson(this.Begin);
				obj["end"] = EncodeKeySelectorToJson(this.End);
				obj["iteration"] = this.Iteration;
				obj["options"] = JsonObject.FromObject(this.Options);
				if (this.Result.HasValue)
				{
					obj["result"] = JsonObject.FromObject(this.Result.Value);
				}
			}

		}

		public sealed class CheckValueCommand : Command<(FdbValueCheckResult Result, Slice Actual)>
		{
			/// <summary>Selector to a key in the database</summary>
			public Slice Key { get; }

			/// <summary>Selector to a key in the database</summary>
			public Slice Expected { get; }

			public override Operation Op => Operation.CheckValue;

			public CheckValueCommand(Slice key, Slice expected, bool snapshot)
			{
				this.Key = key;
				this.Expected = expected;
				this.Snapshot = snapshot;
			}

			public override int? ArgumentBytes => this.Key.Count + this.Expected.Count;

			public override int? ResultBytes => !this.Result.HasValue ? null : this.Result.Value.Actual.Count;

			public override string GetArguments(KeyResolver resolver)
			{
				if (this.Expected.IsNull)
				{
					return $"{resolver.Resolve(this.Key)} =? not_found";
				}
				else
				{
					return $"{resolver.Resolve(this.Key)} =? `{this.Expected:V}`";
				}
			}

			protected override string Dump((FdbValueCheckResult Result, Slice Actual) value, KeyResolver resolver)
			{
				return value.Result == FdbValueCheckResult.Success ? "OK"
					: value.Actual.IsNull ? $"not_found [{value.Result}]"
					: $"`{value.Actual:V}` [{value.Result}]";
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["key"] = EncodeKeyToJson(this.Key);
				obj["expected"] = EncodeValueToJson(this.Expected);
				if (this.Result.HasValue)
				{
					obj["result"] = this.Result.Value.Result.ToString();
					obj["actual"] = EncodeValueToJson(this.Result.Value.Actual);
				}
			}

		}

		public sealed class GetVersionStampCommand : Command<VersionStamp>
		{
			public override Operation Op => Operation.GetVersionStamp;

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				if (this.Result.HasValue)
				{
					obj["result"] = JsonValue.FromValue(this.Result.Value);
				}
			}

		}

		public sealed class GetReadVersionCommand : Command<long>
		{
			public override Operation Op => Operation.GetReadVersion;

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				if (this.Result.HasValue)
				{
					obj["result"] = this.Result.Value;
				}
			}

		}

		public sealed class GetMetadataVersionCommand : Command<VersionStamp?>
		{
			/// <summary>Key read from the database</summary>
			public Slice Key { get; }

			public override Operation Op => Operation.Get;

			public GetMetadataVersionCommand(Slice key)
			{
				this.Key = key;
			}

			public override int? ArgumentBytes => this.Key.Count;

			public override int? ResultBytes => !this.Result.HasValue ? null : 10;

			public override string GetArguments(KeyResolver resolver)
			{
				return resolver.Resolve(this.Key);
			}

			public override string GetResult(KeyResolver resolver)
			{
				return this.Result.HasValue && this.Result.Value == null ? "<null>" : base.GetResult(resolver);
			}

			protected override string Dump(VersionStamp? value, KeyResolver resolver)
			{
				return value?.ToString() ?? "<null>";
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["key"] = EncodeKeyToJson(this.Key);
				if (this.Result.HasValue)
				{
					obj["result"] = JsonValue.FromValue(this.Result.Value);
				}
			}

		}

		public sealed class GetApproximateSizeCommand : Command<long>
		{
			public override Operation Op => Operation.GetApproximateSize;

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				if (this.Result.HasValue)
				{
					obj["result"] = this.Result.Value;
				}
			}

		}

		public sealed class GetAddressesForKeyCommand : Command<string[]>
		{
			/// <summary>Selector to a key in the database</summary>
			public Slice Key { get; }

			public override Operation Op => Operation.GetAddressesForKey;

			public GetAddressesForKeyCommand(Slice key)
			{
				this.Key = key;
			}

			public override int? ArgumentBytes => this.Key.Count;

			public override string GetArguments(KeyResolver resolver) => resolver.Resolve(this.Key);

			protected override string Dump(string[] value, KeyResolver resolver)
			{
				switch (value.Length)
				{
					case 0: return "<empty>";
					case 1: return $"[1] {{ {value[0]} }}";
					case 2: return $"[2] {{ {value[0]}, {value[1]} }}";
					case 3: return $"[3] {{ {value[0]}, {value[1]}, {value[2]} }}";
					default: return $"[{value.Length}] {{ {value[0]}, {value[1]}, ..., {value[^1]} }}";
				}
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["key"] = EncodeKeyToJson(this.Key);
				if (this.Result.HasValue)
				{
					obj["result"] = this.Result.Value;
				}
			}

		}

		public sealed class GetRangeSplitPointsCommand : Command<Slice[]>
		{
			/// <summary>Begin key of the range</summary>
			public Slice Begin { get; }

			/// <summary>End key of the range</summary>
			public Slice End { get; }

			/// <summary>Size of the chunks</summary>
			public long ChunkSize { get; }

			public override Operation Op => Operation.GetRangeSplitPoints;

			public GetRangeSplitPointsCommand(Slice beginKey, Slice endKey, long chunkSize)
			{
				this.Begin = beginKey;
				this.End = endKey;
				this.ChunkSize = chunkSize;
			}

			public override int? ArgumentBytes => this.Begin.Count + this.End.Count;

			public override string GetArguments(KeyResolver resolver) => string.Format(CultureInfo.InvariantCulture, "({0}...{1}) / {2}", resolver.ResolveBegin(this.Begin), resolver.ResolveEnd(this.End), this.ChunkSize);

			protected override string Dump(Slice[] res, KeyResolver resolver)
			{
				return res.Length switch
				{
					0 => "[0] { }",
					1 => $"[1] {{ {resolver.Resolve(res[0])} }}",
					2 => $"[2] {{ {resolver.Resolve(res[0])}, {resolver.Resolve(res[1])} }}",
					3 => $"[3] {{ {resolver.Resolve(res[0])}, {resolver.Resolve(res[1])}, {resolver.Resolve(res[2])} }}",
					_ => $"[{res.Length:N0}] {{ {resolver.Resolve(res[0])}, {resolver.Resolve(res[1])}, ..., {resolver.Resolve(res[^1])} }}"
				};
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["begin"] = EncodeKeyToJson(this.Begin);
				obj["end"] = EncodeKeyToJson(this.End);
				if (this.Result.HasValue)
				{
					obj["result"] = JsonArray.FromValues(this.Result.Value, v => v.ToString());
				}
			}

		}

		public sealed class GetEstimatedRangeSizeBytesCommand : Command<long>
		{
			/// <summary>Begin key of the range</summary>
			public Slice Begin { get; }

			/// <summary>End key of the range</summary>
			public Slice End { get; }

			public override Operation Op => Operation.GetEstimatedRangeSizeBytes;

			public GetEstimatedRangeSizeBytesCommand(Slice beginKey, Slice endKey)
			{
				this.Begin = beginKey;
				this.End = endKey;
			}

			public override int? ArgumentBytes => this.Begin.Count + this.End.Count;

			public override string GetArguments(KeyResolver resolver) => string.Format(CultureInfo.InvariantCulture, "{0}...{1}", resolver.ResolveBegin(this.Begin), resolver.ResolveEnd(this.End));

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["begin"] = EncodeKeyToJson(this.Begin);
				obj["end"] = EncodeKeyToJson(this.End);
				if (this.Result.HasValue)
				{
					obj["result"] = this.Result.Value;
				}
			}

		}

		public sealed class TouchMetadataVersionKeyCommand : AtomicCommand
		{
			public TouchMetadataVersionKeyCommand(Slice key)
				: base(key, Fdb.System.MetadataVersionValue, FdbMutationType.VersionStampedValue)
			{ }

		}

		public sealed class CancelCommand : Command
		{
			public override Operation Op => Operation.Cancel;

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj) { }

		}

		public sealed class ResetCommand : Command
		{
			public override Operation Op => Operation.Reset;

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj) { }

		}

		public sealed class CommitCommand : Command
		{
			public override Operation Op => Operation.Commit;

			/// <summary>Receives the commit version, if it succeeds</summary>
			public long? CommitVersion { get; internal set; }

			public override string GetResult(KeyResolver resolver)
			{
				if (this.CommitVersion != null) return "@" + this.CommitVersion;
				return base.GetResult(resolver);
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["commitVersion"] = this.CommitVersion;
			}

		}

		public sealed class OnErrorCommand : Command
		{
			public FdbError Code { get; }

			public override Operation Op => Operation.OnError;

			public OnErrorCommand(FdbError code)
			{
				this.Code = code;
			}

			public override string GetArguments(KeyResolver resolver)
			{
				return string.Format(CultureInfo.InvariantCulture, "{0} ({1})", this.Code, (int)this.Code);
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["code"] = this.Code.ToString();
			}

		}

		public sealed class WatchCommand : Command
		{
			public Slice Key { get; }

			public override Operation Op => Operation.Watch;

			public WatchCommand(Slice key, CancellationToken lifetime)
			{
				this.Key = key;
			}

			public override int? ArgumentBytes => this.Key.Count;

			public override string GetArguments(KeyResolver resolver)
			{
				return resolver.Resolve(this.Key);
			}

			/// <inheritdoc />
			protected override void OnJsonSerialize(JsonObject obj)
			{
				obj["key"] = EncodeKeyToJson(this.Key);
			}

		}

	}

}
