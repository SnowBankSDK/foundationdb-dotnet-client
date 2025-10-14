#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace SnowBank.Data.Json
{
	using System.Collections;
	using System.Linq.Expressions;
	using System.Reflection;
	using SnowBank.Collections.Caching;
	using SnowBank.Data.Tuples;
	using System.Diagnostics.CodeAnalysis;
	using SnowBank.Runtime;

	public sealed class CrystalJsonDomWriter
	{
		//SAME AS FROM JSONWRITER
		private readonly CrystalJsonSettings m_settings;
		private readonly ICrystalJsonTypeResolver m_resolver;

		//REVIEW: => convert these to bit flags?
		private readonly bool m_readOnly;
		private readonly bool m_discardDefaults; //REVIEW: implement!
		private readonly bool m_discardNulls;
		private readonly bool m_discardClass;
		private readonly bool m_markVisited;
		private readonly bool m_camelCase;
		private readonly bool m_enumAsString; //REVIEW: implement!
		private readonly bool m_enumCamelCased; //REVIEW: implement!
		//END
		private readonly IEqualityComparer<string> m_keyComparer;

		internal struct VisitingContext
		{
			public int ObjectGraphDepth;
			public object[]? VisitedObjectsBuffer;
			public int VisitedObjectsCursor;

			public bool Enter(object value)
			{
				var buf = this.VisitedObjectsBuffer;
				int pos = this.VisitedObjectsCursor;
				if (pos >= MaximumObjectGraphDepth) return false;

				// check object is not already contained
				if (pos != 0)
				{
					foreach(var obj in buf.AsSpan(0, pos))
					{
						if (ReferenceEquals(obj, value)) return false;
					}
				}

				// resize if needed
				if ((buf?.Length ?? 0) <= pos)
				{
					Array.Resize(ref buf, Math.Max(pos * 2, 4));
					this.VisitedObjectsBuffer = buf;
				}

				buf![pos] = value;
				this.VisitedObjectsCursor = pos + 1;
				return true;
			}

			public bool Leave(object value)
			{
				int pos = this.VisitedObjectsCursor - 1;
				var buf = this.VisitedObjectsBuffer;
				if (pos < 0) throw new InvalidOperationException("Visited object stack is empty!");

				// check last item matches the expected value
				if (!ReferenceEquals(buf![pos], value)) return false;

				buf[pos] = null!;
				this.VisitedObjectsCursor = pos;
				return true;
			}

		}

		#region Constructors...

		public CrystalJsonDomWriter()
			: this(null, null)
		{ }

		public CrystalJsonDomWriter(CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			m_settings = settings ?? CrystalJsonSettings.Json;
			m_resolver = resolver ?? CrystalJson.DefaultResolver;

			m_readOnly = m_settings.ReadOnly;
			m_discardDefaults = m_settings.HideDefaultValues;
			m_discardNulls = m_discardDefaults || !m_settings.ShowNullMembers;
			m_discardClass = m_settings.HideClassId;
			m_camelCase = m_settings.UseCamelCasingForNames;
			m_enumAsString = m_settings.EnumsAsString;
			m_enumCamelCased = m_settings.UseCamelCasingForEnums;
			m_markVisited = !m_settings.DoNotTrackVisitedObjects;

			m_keyComparer = m_settings.IgnoreCaseForNames ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		}

		/// <summary>JSON DOM writer, with default settings</summary>
		public static readonly CrystalJsonDomWriter Default = new(CrystalJsonSettings.Json, CrystalJson.DefaultResolver);

		/// <summary>JSON DOM writer, with default settings, that produces read-only values</summary>
		public static readonly CrystalJsonDomWriter DefaultReadOnly = new(CrystalJsonSettings.JsonReadOnly, CrystalJson.DefaultResolver);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CrystalJsonDomWriter Create(CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return AreDefaultSettings(settings, resolver) ? Default : new CrystalJsonDomWriter(settings, resolver);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CrystalJsonDomWriter CreateReadOnly(CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return AreDefaultSettings(settings, resolver) ? DefaultReadOnly : new CrystalJsonDomWriter(settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly, resolver);
		}

		public static bool AreDefaultSettings(CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			// Pas s'il y a un résolver custom...
			if (resolver != null && resolver != CrystalJson.DefaultResolver) return false;

			// Les options de layout (compact, indenté, ...) n'ont aucun impact en mode DOM!
			return (settings?.Flags ?? 0 & ~CrystalJsonSettings.OptionFlags.Layout_Mask) == 0;
		}

		#endregion

		#region Public Properties...

		public CrystalJsonSettings Settings => m_settings;

		public ICrystalJsonTypeResolver Resolver => m_resolver;

		#endregion

		#region Public Methods...

		public JsonValue ParseObject<T>(T value)
		{
			return ParseObject(value, typeof(T));
		}

		/// <summary>Converts an CLR instance into the corresponding <see cref="JsonValue"/></summary>
		/// <param name="value">Instance to convert (primitive, value type or reference type) </param>
		/// <param name="declaredType">Type of the instance, as declared in its parent (ex: Property/Field in a parent class, record or struct. Element type in a collection or array, ...). It can be different from <paramref name="runtimeType"/> (interface, abstract class  ...). If unknown, use <c>typeof(object)</c>, or specify the same value as <paramref name="runtimeType"/> if the value could not be of any other type.</param>
		/// <param name="runtimeType">If specified, the actual runtime type of the instance. If <c>null</c>, <see cref="object.GetType"/> will be called on <paramref name="value"/>.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonValue ParseObject(object? value, Type declaredType, Type? runtimeType = null)
		{
			var context = default(VisitingContext);
			return ParseObjectInternal(ref context, value, declaredType, runtimeType);
		}

		internal JsonValue ParseObjectInternal(ref VisitingContext context, object? value, Type declaredType, Type? runtimeType)
		{
			Contract.Debug.Requires(declaredType != null);

			if (value == null)
			{
				return JsonNull.Null;
			}

			if (value is string s)
			{
				return JsonString.Return(s);
			}

			// already a JSON DOM element ?
			JsonValue? result;
			if ((result = value as JsonValue) != null)
			{
				return result;
			}

			runtimeType ??= value.GetType();

			if (runtimeType.IsPrimitive)
			{ // int, bool, char, float, ...
				if (TryConvertPrimitiveObject(value, runtimeType, out result))
				{
					return result;
				}
			}
			else if (runtimeType.IsValueType)
			{ // struct, datetime, guids, ...
				if (TryConvertValueTypeObject(ref context, value, runtimeType, out result))
				{
					return result;
				}
			}
			else if (runtimeType.IsArray)
			{ // T[]
				if (TryConvertListObject(ref context, (IList) value, runtimeType, out result))
				{
					return result;
				}
			}

			if (value is IJsonPackable packable)
			{ // the object knows how to do it by itself...
				return packable.JsonPack(m_settings, m_resolver);
			}

			if (value is IDictionary<string, object?> dict)
			{ // matches Dictionary<string, object> as well as ExpandoObject
				if (TryConvertDictionaryObject(ref context, dict, runtimeType, out result))
				{
					return result;
				}
			}

			if (value is IEnumerable enmr)
			{
				if (value is IDictionary idict)
				{ // => { K: V }
					if (TryConvertDictionaryObject(ref context, idict, runtimeType, out result))
					{
						return result;
					}
				}

				if (value is IList ilist)
				{ // => [ x, y, ... ]
					if (TryConvertListObject(ref context, ilist, runtimeType, out result))
					{
						return result;
					}
				}

				if (value is IVarTuple tuple)
				{
					return CrystalJsonVisitor.ConvertTupleToJson(tuple);
				}

				if (TryConvertEnumerableObject(ref context, enmr, runtimeType, out result))
				{ // => [ x, y, ...]
					return result;
				}
			}

			if (runtimeType.IsClass)
			{
				switch (value)
				{
					case System.Text.StringBuilder sb:
					{
						return JsonString.Return(sb);
					}
					case System.Net.IPAddress ip:
					{
						return JsonString.Return(ip);
					}
					case DBNull:
					{
						return JsonNull.Null;
					}
					case NodaTime.DateTimeZone dtz:
					{
						return JsonString.Return(dtz);
					}
					case System.Version v:
					{
						return JsonString.Return(v);
					}
					case System.Uri uri:
					{
						return JsonString.Return(uri);
					}
					case System.Type tp:
					{
						return JsonString.Return(tp);
					}
					case Task:
					{
						throw JsonSerializationException.CannotPackTaskLikeType(runtimeType);
					}
				}
			}
			else
			{
				if (value is System.Runtime.CompilerServices.ITuple tuple)
				{
					return CrystalJsonVisitor.ConvertTupleToJson(tuple);
				}
				//TODO: detect ValueTask and ValueTask<T> ?
			}


			if (TryConvertFromTypeDefinition(ref context, value, declaredType, runtimeType, out result))
			{
				return result;
			}

#if DEPRECATED
			if (TryPackViaDuckTyping(value, declaredType, runtimeType, out result))
			{
				return result;
			}
#endif

#if DEBUG
			// if you end up here, it means that the type dos not implement IJsonPackable
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif

			throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeType(runtimeType ?? declaredType);
		}

		#endregion

		#region DOM Helpers...

		public JsonObject BeginObject(int capacity = 0)
		{
			return new JsonObject(capacity, m_keyComparer);
		}

		public JsonArray BeginArray(int capacity = 0)
		{
			return new JsonArray(capacity);
		}

		#endregion

		#region Type Visitors...

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertPrimitiveObject(object value, Type type, [MaybeNullWhen(false)] out JsonValue result)
		{
			Contract.Debug.Requires(value != null && type != null);

			//REVIEW:TODO: m_discardDefaults si on est dans un JsonObject !

			// Cannot be an Enum because we checked for primitive types only
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Boolean:
				{
					result = (bool) value ? JsonBoolean.True : JsonBoolean.False;
					return true;
				}
				case TypeCode.Char:
				{
					result = JsonString.Create((char) value);
					return true;
				}
				case TypeCode.SByte:
				{
					result = JsonNumber.Create((sbyte) value);
					return true;
				}
				case TypeCode.Byte:
				{
					result = JsonNumber.Create((byte) value);
					return true;
				}
				case TypeCode.Int16:
				{
					result = JsonNumber.Create((short) value);
					return true;
				}
				case TypeCode.UInt16:
				{
					result = JsonNumber.Create((ushort) value);
					return true;
				}
				case TypeCode.Int32:
				{
					result = JsonNumber.Create((int) value);
					return true;
				}
				case TypeCode.UInt32:
				{
					result = JsonNumber.Create((uint) value);
					return true;
				}
				case TypeCode.Int64:
				{
					result = JsonNumber.Create((long) value);
					return true;
				}
				case TypeCode.UInt64:
				{
					result = JsonNumber.Create((ulong) value);
					return true;
				}
				case TypeCode.Single:
				{
					result = JsonNumber.Create((float) value);
					return true;
				}
				case TypeCode.Double:
				{
					result = JsonNumber.Create((double) value);
					return true;
				}
				case TypeCode.Object:
				{
					// Unless mistaken, only IntPtr/UIntPtr types are both Primitive and TypeCode.Object
					if (typeof(IntPtr) == type)
					{
						result = JsonNumber.Create(((IntPtr) value).ToInt64());
						return true;
					}
					if (typeof(UIntPtr) == type)
					{
						result = JsonNumber.Create(((UIntPtr) value).ToUInt64());
						return true;
					}
					break;
				}
				// note: Decimal is *not* Primitive!
			}
			result = null!;
			return false;
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertValueTypeObject(ref VisitingContext context, object value, Type type, [MaybeNullWhen(false)] out JsonValue result)
		{
			Contract.Debug.Requires(value != null && type != null);

			if (typeof(DateTime) == type)
			{
				result = JsonDateTime.Return((DateTime)value);
				return true;
			}
			if (typeof(DateTimeOffset) == type)
			{
				result = JsonDateTime.Return((DateTimeOffset)value);
				return true;
			}
			if (typeof(TimeSpan) == type)
			{
				result = JsonNumber.Return(((TimeSpan)value).TotalSeconds);
				return true;
			}
			if (typeof(Guid) == type)
			{
				result = JsonString.Return((Guid) value);
				return true;
			}
			if (typeof(Decimal) == type)
			{
				result = JsonNumber.Return((decimal) value);
				return true;
			}
			if (typeof(Uuid128) == type)
			{
				result = JsonString.Return((Uuid128) value);
				return true;
			}
			if (typeof(Uuid64) == type)
			{
				result = JsonString.Return((Uuid64) value);
				return true;
			}
			if (typeof(Uuid96) == type)
			{
				result = JsonString.Return((Uuid96) value);
				return true;
			}
			if (typeof(Uuid80) == type)
			{
				result = JsonString.Return((Uuid80) value);
				return true;
			}
			if (typeof(DateOnly) == type)
			{
				result = JsonDateTime.Return((DateOnly) value);
				return true;
			}
			if (typeof(TimeOnly) == type)
			{
				result = JsonNumber.Return((TimeOnly) value);
				return true;
			}

			if (type.IsEnum)
			{ // enums are converted into their string literal representation
				result = CrystalJsonEnumCache.GetName(type, (Enum) value);
				//BUGBUG: check m_enumAsString / m_enumCamelCased ?
				return true;
			}

			#region NodaTime types...

			if (typeof(NodaTime.Instant) == type)
			{
				result = JsonString.Return((NodaTime.Instant)value);
				return true;
			}
			if (typeof(NodaTime.Duration) == type)
			{
				result = JsonNumber.Return((NodaTime.Duration)value);
				return true;
			}
			if (typeof(NodaTime.LocalDateTime) == type)
			{
				result = JsonString.Return((NodaTime.LocalDateTime)value);
				return true;
			}
			if (typeof(NodaTime.ZonedDateTime) == type)
			{
				result = JsonString.Return((NodaTime.ZonedDateTime)value);
				return true;
			}
			if (typeof(NodaTime.OffsetDateTime) == type)
			{
				result = JsonString.Return((NodaTime.OffsetDateTime)value);
				return true;
			}
			if (typeof(NodaTime.Offset) == type)
			{
				result = JsonString.Return((NodaTime.Offset)value);
				return true;
			}
			if (typeof(NodaTime.LocalDate) == type)
			{
				result = JsonString.Return((NodaTime.LocalDate)value);
				return true;
			}
			if (typeof(NodaTime.LocalTime) == type)
			{
				result = JsonString.Return((NodaTime.LocalTime)value);
				return true;
			}
			//note: DateTimeZone is a class!

			#endregion

			// Nullable<T>
			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
			{
				if (nullableType.IsPrimitive)
				{
					return TryConvertPrimitiveObject(value, nullableType, out result);
				}
				// T can only be a Value Type
				// => recursive call, but safe since T cannot be Nullable<...> again (Nullable<Nullable<T>> is not allowed, at least for now!)
				return TryConvertValueTypeObject(ref context, value, nullableType, out result);
			}

			if (type == typeof(KeyValuePair<string, string>))
			{
				result = VisitKeyValuePair((KeyValuePair<string, string>) value);
				return true;
			}
			if (type == typeof(KeyValuePair<long, long>))
			{
				result = VisitKeyValuePair((KeyValuePair<long, long>) value);
				return true;
			}
			if (type.Name == "KeyValuePair`2")
			{
				result = VisitKeyValuePair(ref context, value, type);
				return true;
			}

			if (typeof(Slice) == type)
			{
				result = JsonString.Return((Slice) value);
				return true;
			}
			if (typeof(ArraySegment<byte>) == type)
			{
				result = JsonString.Return((ArraySegment<byte>)value);
				return true;
			}
			//TODO: Memory<T>/ReadOnlyMemory<T>?

#if DISABLED // too slow!
			if (type.IsGenericInstanceOf(typeof(KeyValuePair<,>)))
			{ // KeyValuePair<K, V> => [ K, V ]
				var genTypes = type.GetGenericArguments();
				var method = typeof(CrystalJsonDomWriter).GetMethod("VisitKeyValuePair", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
				method = method.MakeGenericMethod(genTypes[0], genTypes[1]);
				result = (JsonValue) method.Invoke(null, new object[] { value });
				return result != null;
			}
#endif

			// custom structs will be handled via reflection at runtime
			result = null!;
			return false;
		}

		internal JsonValue VisitKeyValuePair(ref VisitingContext context, object value, Type type)
		{
			return s_cachedKeyValuePairVisitors.GetOrAdd(type, CompileKeyValuePairVisitor)(this, ref context, value);
		}

		private delegate JsonValue KeyValuePairVisitor(CrystalJsonDomWriter writer, ref VisitingContext context, object value);

		private static readonly QuasiImmutableCache<Type, KeyValuePairVisitor> s_cachedKeyValuePairVisitors = new(TypeEqualityComparer.Default);

		private static KeyValuePairVisitor CompileKeyValuePairVisitor(Type kvType)
		{
			// (writer, ref context, obj) =>  VisitKeyValuePair<TKey, TValue>(writer, (KeyValuePair<TKey, TValue) obj)

			var prmWriter = Expression.Parameter(typeof(CrystalJsonDomWriter), "writer");
			var prmContext = Expression.Parameter(typeof(VisitingContext).MakeByRefType(), "context");
			var prmObj = Expression.Parameter(typeof(object), "obj");
			var m = typeof(CrystalJsonDomWriter).GetMethod(nameof(VisitKeyValuePairGeneric), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(kvType.GetGenericArguments());
			var body = Expression.Call(m, prmWriter, prmContext, Expression.Convert(prmObj, kvType));

			return Expression.Lambda<KeyValuePairVisitor>(body, true, [ prmWriter, prmContext, prmObj ]).Compile();
		}

		private static JsonValue VisitKeyValuePairGeneric<TKey, TValue>(CrystalJsonDomWriter writer, ref VisitingContext context, KeyValuePair<TKey, TValue> value)
		{
			return new JsonArray([
				JsonValue.FromValue<TKey>(value.Key), //REVIEW: we assume the key as a "simple" type (string, int, ...)
				JsonValue.FromValue<TValue>(writer, ref context, value.Value)
			], 2, readOnly: false);
		}

		private static JsonValue VisitKeyValuePair(KeyValuePair<string, string> kv)
		{
			return new JsonArray([
				JsonString.Return(kv.Key),
				JsonString.Return(kv.Value)
			], 2, readOnly: false);
		}

		private static JsonValue VisitKeyValuePair(KeyValuePair<long, long> kv)
		{
			return new JsonArray([
				JsonNumber.Return(kv.Key),
				JsonNumber.Return(kv.Value)
			], 2, readOnly: false);
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertDictionaryObject(ref VisitingContext context, IDictionary<string, object?> value, Type type, out JsonValue result)
		{
			Contract.Debug.Requires(value != null && type != null);

			if (value.Count == 0)
			{
				result = m_readOnly ? JsonObject.ReadOnly.Empty : new JsonObject();
				return true;
			}

			MarkVisited(ref context, value, type);
			var obj = BeginObject(value.Count);
			foreach (var kvp in value)
			{
				JsonValue val;
				if (kvp.Value == null)
				{
					if (m_discardNulls) continue;
					val = JsonNull.Null;
				}
				else
				{
					//TODO: test discard defaults !
					val = ParseObjectInternal(ref context, kvp.Value, typeof(object), null);
				}

				obj.Add(
					FormatName(kvp.Key),
					val
				);
			}
			Leave(ref context, value);

			result = obj;
			return true;
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertDictionaryObject(ref VisitingContext context, IDictionary value, Type type, out JsonValue result)
		{
			Contract.Debug.Requires(value != null && type != null);

			if (value.Count == 0)
			{
				result = m_readOnly ? JsonObject.ReadOnly.Empty : new JsonObject();
				return true;
			}

			bool stringKeys;
			Type elementType;

			var genType = type.FindGenericType(typeof(IDictionary<,>));
			if (genType != null)
			{
				var argTypes = genType.GetGenericArguments();
				stringKeys = argTypes[0] == typeof(string);
				elementType = argTypes[1];
			}
			else
			{ // we don't know the type of the keys and values
				stringKeys = false;
				elementType = typeof(object);
			}

			MarkVisited(ref context, value, type);
			var obj = BeginObject(value.Count);
			foreach (DictionaryEntry kvp in value)
			{
				JsonValue val;
				if (kvp.Value == null)
				{
					if (m_discardNulls) continue;
					val = JsonNull.Null;
				}
				else
				{
					//TODO: test discard defaults !
					val = ParseObjectInternal(ref context, kvp.Value, elementType, null);
				}

				obj.Add(
					FormatName(stringKeys ? (string)kvp.Key : TypeHelper.ConvertKeyToString(kvp.Key)),
					val
				);
			}
			Leave(ref context, value);

			result = obj;
			return true;
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertListObject(ref VisitingContext context, IList values, Type listType, out JsonValue result)
		{
			Contract.Debug.Requires(values != null && listType != null);

			Type elemType = typeof(object);
			var genType = listType.FindGenericType(typeof(IList<>));
			if (genType != null)
			{
				elemType = genType.GetGenericArguments()[0];
			}

			if (values.Count == 0)
			{
				result = m_readOnly ? JsonArray.ReadOnly.Empty : new JsonArray();
				return true;
			}

			MarkVisited(ref context, values);
			var array = BeginArray(values.Count);
			foreach (var item in values)
			{
				array.Add(item != null ? ParseObjectInternal(ref context, item, elemType, null) : JsonNull.Null);
			}
			Leave(ref context, values);
			if (m_readOnly)
			{
				array.Freeze();
			}
			result = array;
			return true;
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertEnumerableObject(ref VisitingContext context, IEnumerable values, Type sequenceType, out JsonValue result)
		{
			Contract.Debug.Requires(values != null && sequenceType != null);

			// try to pre-allocate the array if we know the number of elements in advance
			int? l = values is ICollection coll ? coll.Count : null;
			if (l == 0)
			{ // this is empty!
				result = m_readOnly ? JsonArray.ReadOnly.Empty : new JsonArray();
				return true;
			}

			MarkVisited(ref context, values);

			var array = BeginArray(l ?? 0);


			foreach (var item in values)
			{
				array.Add(item != null ? ParseObjectInternal(ref context, item, typeof(object), null) : JsonNull.Null);
			}

			Leave(ref context, values);

			if (m_readOnly)
			{
				array.Freeze();
			}

			result = array;
			return true;
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertFromTypeDefinition(ref VisitingContext context, object? value, Type declaredType, Type? runtimeType, [MaybeNullWhen(false)] out JsonValue result)
		{
			Contract.Debug.Requires(value != null && declaredType != null && runtimeType != null);

			if (!m_resolver.TryResolveTypeDefinition(runtimeType, out var typeDef) || (typeDef.CustomBinder != null && !typeDef.IsAnonymousType))
			{
				result = null!;
				return false;
			}

			MarkVisited(ref context, value, runtimeType);
			var obj = BeginObject(typeDef.Members.Length);

			//COPY/PASTA (should be very similar to CrystalJsonVisitor.VisitCustomClassOrStruct)
			if (!m_discardClass)
			{
				// we may need to add a "$type" property if the type is a derived type that belongs in a "polymorphic chain", in order to be able to deserialize it back to that specific type
				if (typeDef.TypeDiscriminatorProperty != null && typeDef.TypeDiscriminatorValue != null)
				{ // add the "$type" property
					obj[typeDef.TypeDiscriminatorProperty.Value] = typeDef.TypeDiscriminatorValue;
				}
			}
			//END

			foreach (var member in typeDef.Members)
			{
				var v = member.Getter(value);
				if (v == null)
				{
					if (!m_discardNulls)
					{
						obj[FormatName(member.Name)] = JsonNull.Null;
					}
				}
				else
				{
					obj[FormatName(member.Name)] = ParseObjectInternal(ref context, v, member.Type, null);
				}
			}
			Leave(ref context, value);

			if (m_readOnly)
			{ //PERF: find a more efficient way?
				obj.Freeze();
			}

			result = obj;
			return true;
		}

		#endregion

		#region Copy/Pasta from CrystalJsonWriter

		private const int MaximumObjectGraphDepth = 16;

		/// <summary>Test if an instance as already been visited before, to protect against cycles in the object graph</summary>
		/// <exception cref="JsonSerializationException">If this instance has already been visited before, or if the object graph is too deep</exception>
		/// <remarks>If the same instance is visited against before <see cref="Leave"/> is called, then an exception will be thrown.</remarks>
		internal void MarkVisited(ref VisitingContext context, object? value, Type? type = null)
		{
			if (context.ObjectGraphDepth >= MaximumObjectGraphDepth)
			{ // protect against object graphs that are too deep
				throw CrystalJson.Errors.Serialization_FailTooDeep(context.ObjectGraphDepth, value);
			}
			if (m_markVisited && value != null)
			{ // protect against cycles that would lead to a stack overflow
				if (!context.Enter(value))
				{
					if (!CrystalJsonWriter.TypeSafeForRecursion(type ?? value.GetType()))
					{
						throw CrystalJson.Errors.Serialization_ObjectRecursionIsNotAllowed(context.VisitedObjectsBuffer.AsSpan(0, context.VisitedObjectsCursor).ToArray(), value, context.ObjectGraphDepth);
					}
				}
			}
			++context.ObjectGraphDepth;
		}

		/// <summary>Mark this instance as visited.</summary>
		/// <remarks>The call must match a previous call to <see cref="MarkVisited"/></remarks>
		/// <exception cref="JsonSerializationException">If this instance is not on the stack, which would indicate a mismatch between <see cref="MarkVisited"/> and <see cref="Leave"/> calls</exception>
		internal void Leave(ref VisitingContext context, object? value)
		{
			if (context.ObjectGraphDepth == 0) throw CrystalJson.Errors.Serialization_InternalDepthInconsistent();
			if (m_markVisited && value != null && !context.Leave(value))
			{
				throw CrystalJson.Errors.Serialization_LeaveNotSameThanMark(context.ObjectGraphDepth, value);
			}
			--context.ObjectGraphDepth;
		}

		/// <summary>Format the name of a field, according to the current settings (camelCase, ...)</summary>
		/// <param name="name">Name of the field (ex: <c>"FooBar"</c>)</param>
		/// <returns>Formatted name (ex: <c>"fooBar"</c> if Camel Casing is enabled)</returns>
		internal string FormatName(string name)
		{
			return m_camelCase ? CrystalJsonWriter.CamelCase(name) : name;
		}

		#endregion

	}

}
