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

//#define DEBUG_JSON_RESOLVER
//#define DEBUG_JSON_BINDER

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL2060 // Call to 'System.Reflection.MethodInfo.MakeGenericMethod' can not be statically analyzed. It's not possible to guarantee the availability of requirements of the generic method.
#pragma warning disable IL2062 // The parameter of method has a DynamicallyAccessedMembersAttribute, but the value passed to it can not be statically analyzed.
#pragma warning disable IL2067 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.
#pragma warning disable IL2070
#pragma warning disable IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
#pragma warning disable IL2112 // 'DynamicallyAccessedMembersAttribute' on a type or one of its base types references a member which requires unreferenced code.
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.

namespace SnowBank.Data.Json
{
	using System.Collections;
	using System.Collections.Immutable;
	using System.Collections.ObjectModel;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Runtime.Serialization;
	using SnowBank.Data.Tuples;
	using SnowBank.Collections.Caching;
	using SnowBank.Runtime;
	using SnowBank.Runtime.Converters;

	/// <summary>Default JSON resolver that uses reflection to serialize and deserialize managed types</summary>
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
	public sealed class CrystalJsonTypeResolver : ICrystalJsonTypeResolver
	{

		#region Private Members...

		private readonly QuasiImmutableCache<Type, CrystalJsonTypeDefinition?> TypeDefinitionCache = new (TypeEqualityComparer.Default);

		#endregion

		/// <inheritdoc />
		public bool TryGetConverterFor<T>(out IJsonConverter<T> converter)
		{
			converter = RuntimeJsonConverter<T>.Default;
			return true;
		}

		private static Dictionary<Type, IJsonConverter> ConverterCache { get; } = new();

		/// <inheritdoc />
		public bool TryGetConverterFor(Type type, out IJsonConverter converter)
		{
			converter = GetConverterFor(type);
			return true;
		}

		/// <inheritdoc />
		public bool TryResolveTypeDefinition<T>([MaybeNullWhen(false)] out CrystalJsonTypeDefinition definition)
		{
			definition = ResolveJsonType(typeof(T));
			return definition != null;
		}

		/// <inheritdoc />
		public bool TryResolveTypeDefinition(Type type, [MaybeNullWhen(false)] out CrystalJsonTypeDefinition definition)
		{
			definition = ResolveJsonType(type);
			return definition != null;
		}

		/// <summary>Returns the definition for the specified member of a type</summary>
		public bool TryResolveMember(Type type, string memberName, [MaybeNullWhen(false)] out CrystalJsonMemberDefinition definition)
		{
			definition = ResolveMemberOfType(type, memberName);
			return definition != null;
		}

		/// <summary>Returns the definition for the specified member of a type</summary>
		public bool TryResolveMember<T>(string memberName, [MaybeNullWhen(false)] out CrystalJsonMemberDefinition definition)
		{
			definition = ResolveMemberOfType(typeof(T), memberName);
			return definition != null;
		}

		[Pure, MethodImpl]
		public static IJsonConverter<T> GetConverterFor<T>() => RuntimeJsonConverter<T>.Default;

		public static IJsonConverter<T> GetConverterFor<T>(ICrystalJsonTypeResolver? resolver)
		{
			return (resolver is null || ReferenceEquals(resolver, CrystalJson.DefaultResolver)) ? RuntimeJsonConverter<T>.Default
				: resolver.TryGetConverterFor<T>(out var converter) ? converter
				: throw new InvalidOperationException($"Does not know how to handle type {typeof(T).GetFriendlyName()}");
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		public static IJsonConverter GetConverterFor(Type type)
		{
			lock (ConverterCache)
			{
				if (!ConverterCache.TryGetValue(type, out var converter))
				{
					converter = GetGenericConverter(type);
					ConverterCache[type] = converter;
				}
				return converter;
			}

			[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
			static IJsonConverter GetGenericConverter(Type type)
			{
				var m = typeof(RuntimeJsonConverter<>).MakeGenericType(type).GetMethod(nameof(RuntimeJsonConverter<>.GetInstance), BindingFlags.Static | BindingFlags.Public);
				Contract.Debug.Assert(m != null);
				return (IJsonConverter) m!.Invoke(null, null)!;
			}
		}

		public static IJsonConverter GetConverterFor(Type type, ICrystalJsonTypeResolver? resolver)
		{
			if (!(resolver ?? CrystalJson.DefaultResolver).TryGetConverterFor(type, out var converter))
			{
				throw new InvalidOperationException($"Does not know how to handle type {type.GetFriendlyName()}");
			}
			return converter;
		}

		/// <summary>Inspects a type to retrieve the definitions of its members</summary>
		/// <param name="type">Type to inspect</param>
		/// <returns>List of compiled member definitions, or <see langword="null"/> if the type is not compatible</returns>
		/// <remarks>The list is cached for later calls</remarks>
		public CrystalJsonTypeDefinition? ResolveJsonType(Type type)
		{
#if DEBUG_JSON_RESOLVER
			Debug.WriteLine(this.GetType().Name + ".ResolveType(" + type + ")");
#endif
			Contract.NotNull(type);

			// ReSharper disable once ConvertClosureToMethodGroup
			return this.TypeDefinitionCache.GetOrAdd(type, ResolveNewTypeHandler, this);
		}

		public CrystalJsonMemberDefinition? ResolveMemberOfType(Type type, string memberName)
		{
			var typeDef = ResolveJsonType(type);
			if (typeDef != null)
			{
				//HACKHACK: PERF: OPTIMIZE: use a dictionary?
				foreach (var def in typeDef.Members)
				{
					if (def.OriginalName == memberName || def.Name == memberName)
					{
						return def;
					}
				}
			}

			return null;
		}

		private static readonly Func<Type, CrystalJsonTypeResolver, CrystalJsonTypeDefinition?> ResolveNewTypeHandler =
			static (t, self) => self.ResolveJsonTypeNoCache(t);

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		private CrystalJsonTypeDefinition? ResolveJsonTypeNoCache(Type type)
		{
			Contract.Debug.Requires(type != null);
			return GetTypeDefinition(type);
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		private CrystalJsonTypeDefinition? GetTypeDefinition(Type type)
		{
#if DEBUG_JSON_RESOLVER
			Debug.WriteLine(this.GetType().Name + ".ResolveNewTypeWriter(" + type + ") => from reflection");
#endif

			if (type.IsPrimitive)
			{ // int, long, bool, ...
				return null;
			}
			if (typeof(Delegate).IsAssignableFrom(type))
			{ // delegate, Func<..>, Action<..>, WaitCallback, ...
				return null;
			}

			// Special case for Nullable<T> types
			var nullable = Nullable.GetUnderlyingType(type);
			if (nullable != null)
			{
				var def = ResolveJsonType(nullable);
				if (def == null) return null; // we don't know how to handle this type!
				return CreateNullableTypeWrapper(type, def);
			}

			// quick test for KeyValuePair<TKey, TValue>
			if (type.Name == "KeyValuePair`2")
			{
				return CreateFromKeyValuePair(type);
			}

			return CreateFromReflection(type);
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		private CrystalJsonTypeDefinition CreateFromKeyValuePair(Type type)
		{
			// We have an input that looks like "{ Key: ..., Value: ... }", and we want to convert it into the corresponding KeyValuePair<TKey, TValue>

			// (value, _, resolver) =>
			// {
			//    var obj = value.AsObjectOrDefault();
			//    return obj == null
			//      ? default(KeyValuePair<TKey, TValue>)
			//      : new KeyValuePair<TKey, TValue>(
			//          obj.Get<TKey>("Key", default(TKey), resolver),
			//          obj.Get<TValue>("Value", default(TValue), resolver)
			//    );
			// }

			var args = type.GetGenericArguments();
			var keyType = args[0];
			var valueType = args[1];

			var prmValue = Expression.Parameter(typeof(JsonValue), "value");
			var prmType = Expression.Parameter(typeof(Type), "_"); // ignored
			var prmResolver = Expression.Parameter(typeof(ICrystalJsonTypeResolver), "resolver");

			var varObj = Expression.Variable(typeof(JsonObject), "obj");

			var body = Expression.Block(
				[ varObj ],
				[
					Expression.Assign(varObj, Expression.Call(typeof(JsonValueExtensions), nameof(JsonValueExtensions.AsObjectOrDefault), null, prmValue)),
					Expression.Convert(
						Expression.Condition(
							Expression.ReferenceEqual(varObj, Expression.Default(typeof(JsonObject))),
							Expression.Default(type),
							Expression.New(
								type.GetConstructors().Single(),
								Expression.Call(varObj, nameof(JsonObject.Get), [ keyType ], Expression.Constant("Key"), Expression.Default(keyType), prmResolver), // Get<TKey>("Key", default, resolver)
								Expression.Call(varObj, nameof(JsonObject.Get), [ valueType ], Expression.Constant("Value"), Expression.Default(valueType), prmResolver) // Get<TValue>("Value", default, resolver)
							)
						),
						typeof(object)
					)
				]
			);
			var binder = Expression.Lambda<CrystalJsonTypeBinder>(body, $"<>_KV_{keyType.Name}_{valueType.Name}_Unpack", true, [ prmValue, prmType, prmResolver ]).Compile();

			var keyProperty = type.GetProperty("Key")!;
			var valueProperty = type.GetProperty("Value")!;

			//REVIEW: not sure if this is necessary, since we already have a custom binder?
			var members = new[]
			{
				new CrystalJsonMemberDefinition()
				{
					Type = keyType,
					Name = "Key",
					OriginalName = "Key",
					EncodedName = new("Key"),
					Flags = CrystalJsonMemberFlags.ReadOnly | CrystalJsonMemberFlags.Key,
					NullableOfType = GetNullableType(keyType),
					DefaultValue = keyType.GetDefaultValue(),
					Getter = keyProperty.CompileGetter(),
					Setter = null, //TODO: private setter?
					//TODO: visitor? binder?
					Visitor = (_, _, _, _) => throw new NotImplementedException(),
					Binder = (_, _, _) => throw new NotImplementedException(),
				},
				new CrystalJsonMemberDefinition()
				{
					Type = valueType,
					Name = "Value",
					OriginalName = "Value",
					EncodedName = new("Value"),
					NullableOfType = GetNullableType(valueType),
					DefaultValue = valueType.GetDefaultValue(),
					Flags = CrystalJsonMemberFlags.ReadOnly,
					Getter = valueProperty.CompileGetter(),
					Setter = null, //TODO: private setter?
					//TODO: visitor? binder?
					Visitor = (_, _, _, _) => throw new NotImplementedException(),
					Binder = (_, _, _) => throw new NotImplementedException(),
				}
			};

			return new(type, CrystalJsonTypeFlags.None, binder, null, members, null, null, null, null, null);
		}

		public object? BindJsonValue(Type? type, JsonValue? value)
		{
#if DEBUG_JSON_BINDER
			Debug.WriteLine(this.GetType().Name + ".BindValue(" + type + ", " + value + ")");
#endif

			if (value == null) return null;

			switch (value.Type)
			{
				case JsonType.Null:
				{
					return null;
				}
				case JsonType.Object:
				{
					return BindJsonObject(type, value as JsonObject);
				}
				case JsonType.Array:
				{
					return BindJsonArray(type, value as JsonArray);
				}
				default:
				{
					return value.Bind(type, this);
				}
			}
		}

		public object? BindJsonObject(Type? type, JsonObject? value)
		{
#if DEBUG_JSON_BINDER
			Debug.WriteLine(this.GetType().Name + ".BindObject(" + type + ", " + value + ")");
#endif
			if (value == null) return null;
			type ??= typeof(object);


			if (type.IsAssignableTo(typeof(JsonValue)))
			{
				if (typeof(JsonObject) == type || typeof(JsonValue) == type)
				{ // This is already an Object!
					return value;
				}
				throw JsonBindingException.CannotBindJsonObjectToThisType(value, type);
			}

			// some types are not object
			if (type.IsPrimitive || typeof(string) == type || typeof(DateTime) == type || typeof(decimal) == type)
			{
				throw JsonBindingException.CannotBindJsonObjectToThisType(value, type);
			}

			return CrystalJsonParser.DeserializeCustomClassOrStruct(value, type, this);
		}

		private static readonly QuasiImmutableCache<Type, Func<CrystalJsonTypeResolver, JsonArray?, object?>> DefaultArrayBinders = new(TypeEqualityComparer.Default);

		private static readonly Func<Type, Func<CrystalJsonTypeResolver, JsonArray?, object?>> JsonArrayBinderCallback = CreateDefaultJsonArrayBinder;

		public object? BindJsonArray(Type? type, JsonArray? array)
			=> DefaultArrayBinders.GetOrAdd(type ?? typeof(object), JsonArrayBinderCallback)(this, array);

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder(Type? type)
		{
#if DEBUG_JSON_BINDER
			Debug.WriteLine(this.GetType().Name + ".BindArray(" + type + ", " + array + ")");
#endif

			if (type == null || typeof(object) == type)
			{ // Auto-detect?
				return (_, array) => array?.ToObject();
			}

			if (type.IsAssignableTo(typeof(JsonValue)))
			{
				if (typeof(JsonArray) == type || typeof(JsonValue) == type)
				{ // This is already an Array!
					return (_, array) => array;
				}
				return CreateDefaultJsonArrayBinder_Invalid(type);
			}

			if (typeof(string) == type)
			{ // We cannot convert an array into a string
				return CreateDefaultJsonArrayBinder_Invalid(type); //note: prevents from falling through to the IEnumerable check below
			}

			if (type.IsArray)
			{ // return type is an array (T[])

				// ex:
				// if we are called with type == typeof(int), we must output an int[]
				// if we are called with type == typeof(int[]), we must output a 2-dimensional array of type 'int[][]'

				Type elementType = type.GetElementType()!;
				if (elementType == typeof(bool)) return static (_, array) => array?.ToBoolArray();
				if (elementType == typeof(string)) return static (_, array) => array?.ToStringArray();
				if (elementType == typeof(int)) return static (_, array) => array?.ToInt32Array();
				if (elementType == typeof(long)) return static (_, array) => array?.ToInt64Array();
				if (elementType == typeof(double)) return static (_, array) => array?.ToDoubleArray();
				if (elementType == typeof(float)) return static (_, array) => array?.ToSingleArray();
				if (elementType == typeof(Guid)) return static (_, array) => array?.ToGuidArray();
				if (elementType == typeof(Uuid128)) return static (_, array) => array?.ToUuid128Array();
				if (elementType == typeof(DateTime)) return static (_, array) => array?.ToDateTimeArray();
				if (elementType == typeof(DateTimeOffset)) return static (_, array) => array?.ToDateTimeOffsetArray();
				if (elementType == typeof(NodaTime.Instant)) return static (_, array) => array?.ToInstantArray();
				if (elementType == typeof(decimal)) return static (_, array) => array?.ToDecimalArray();
#if NET5_0_OR_GREATER
				// System.Half does not exist on netstandard2.0
				if (elementType == typeof(Half)) return static (_, array) => array?.ToHalfArray();
#endif
				return CreateDefaultJsonArrayBinder_Filler(nameof(FillArray), elementType);
			}

			// is this a List<T> or other generic collection type?
			if (type.IsGenericType)
			{
				// If the type derives from ICollection<> or IList<>, we will create a mutable List<T>
				// If the type only implements IEnumerable<>, we will create a ReadOnlyCollection<T>

				// If the caller requests an interface, we will choose the most appropriate type
				// If the caller specifies a concrete type:
				// - If this is a known type (List<T>, ReadOnlyCollection<T>, ImmutableList<T>), we can use the dedicated filler method
				// - If not, we have to construct the type manually, and call Add() or AddRange() (like the compiler does with collection initializers)
				string? filler = null;

				if (type.IsGenericInstanceOf(typeof(IEnumerable<>)))
				{
					if (type.IsInterface)
					{ // find the best fallback implementation for this interface

						if (type.IsGenericInstanceOf(typeof(IReadOnlyCollection<>)))
						{ // IReadOnlyCollection<T>, IImmutableXYZ<T>, ...
							if (type.IsGenericInstanceOf(typeof(IImmutableList<>)))
							{ // => ImmutableList<T>
								filler = nameof(FillImmutableList);
							}
							else if (type.IsGenericInstanceOf(typeof(IImmutableSet<>)))
							{ // => ImmutableHashSet<T>
								filler = nameof(FillImmutableHashSet);
							}
							else
							{ // => ReadOnlyCollection<T>
								filler = nameof(FillReadOnlyCollection);
							}
						}
						else if (type.IsGenericInstanceOf(typeof(ICollection<>)))
						{  // List<T>, HashSet<T>, ...
							if (type.IsGenericInstanceOf(typeof(IList<>)))
							{ // => List<T>
								filler = nameof(FillList);
							}
							else if (type.IsGenericInstanceOf(typeof(ISet<>)))
							{ // => HashSet<T>
								filler = nameof(FillHashSet);
							}
							else
							{ // => List<T> also ?
								filler = nameof(FillList);
							}
						}
						else
						{ // => caller does not care
							filler = nameof(FillEnumerable);
						}
					}
					else
					{ // is this something we know about ?

						if (type.IsGenericInstanceOf(typeof(List<>)))
						{
							filler = nameof(FillList);
						}
						else if (type.IsGenericInstanceOf(typeof(HashSet<>)))
						{
							filler = nameof(FillHashSet);
						}
						else if (type.IsGenericInstanceOf(typeof(ReadOnlyCollection<>)))
						{
							filler = nameof(FillReadOnlyCollection);
						}
						else if (type.IsGenericInstanceOf(typeof(ImmutableList<>)))
						{
							filler = nameof(FillImmutableList);
						}
						else if (type.IsGenericInstanceOf(typeof(ImmutableArray<>)))
						{
							filler = nameof(FillImmutableArray);
						}
						else if (type.IsGenericInstanceOf(typeof(IImmutableSet<>)))
						{ // => ImmutableHashSet<T>
							filler = nameof(FillImmutableHashSet);
						}
						else if (type.IsGenericInstanceOf(typeof(Dictionary<,>)) || type.IsGenericInstanceOf(typeof(ImmutableDictionary<,>)))
						{
							// dictionaries normally bind from a JSON object, but the dictionary binder also accepts the
							// legacy DCJS wire shape [ { "Key": ..., "Value": ... }, ... ]: defer to it when it exists
							return (resolver, array) =>
							{
								if (array is not null
								 && resolver.TryResolveTypeDefinition(type, out var typeDef)
								 && typeDef.CustomBinder is not null)
								{
									return typeDef.CustomBinder(array, type, resolver);
								}
								throw new JsonBindingException($"Cannot create a binder from a JSON Array to dictionary type '{type.GetFriendlyName()}'.");
							};
						}
					}
				}

				if (filler != null)
				{
#if FULL_DEBUG
					System.Diagnostics.Debug.WriteLine("JsonArray: Using filler '" + filler + "' for type " + type.GetFriendlyName());
#endif
					var elementType = type.GetGenericArguments()[0];
					if (filler == nameof(FillList))
					{ // special cases!
						if (elementType == typeof(bool)) return static (_, array) => array?.ToBoolList();
						if (elementType == typeof(string)) return static (_, array) => array?.ToStringList();
						if (elementType == typeof(int)) return static (_, array) => array?.ToInt32List();
						if (elementType == typeof(long)) return static (_, array) => array?.ToInt64List();
						if (elementType == typeof(double)) return static (_, array) => array?.ToDoubleList();
						if (elementType == typeof(float)) return static (_, array) => array?.ToSingleList();
						if (elementType == typeof(Guid)) return static (_, array) => array?.ToGuidList();
						if (elementType == typeof(Uuid128)) return static (_, array) => array?.ToUuid128List();
						if (elementType == typeof(DateTime)) return static (_, array) => array?.ToDateTimeList();
						if (elementType == typeof(DateTimeOffset)) return static (_, array) => array?.ToDateTimeOffsetList();
						if (elementType == typeof(NodaTime.Instant)) return static (_, array) => array?.ToInstantList();
						if (elementType == typeof(decimal)) return static (_, array) => array?.ToDecimalList();
#if NET5_0_OR_GREATER
						// System.Half does not exist on netstandard2.0
						if (elementType == typeof(Half)) return static (_, array) => array?.ToHalfList();
#endif
					}
					return CreateDefaultJsonArrayBinder_Filler(filler, elementType);
				}

				if (type.IsGenericInstanceOf(typeof(KeyValuePair<,>)))
				{
					var binder = CreateBinderForKeyValuePair(type);
					if (binder != null) return CreateDefaultJsonArrayBinder_Binder(type, binder);
				}
			}

			if (type == typeof(IVarTuple))
			{
				return CreateDefaultJsonArrayBinder_STuple(type);
			}

#if !NETSTANDARD2_0
			// System.Runtime.CompilerServices.ITuple is not visible to netstandard2.0
			if (typeof(System.Runtime.CompilerServices.ITuple).IsAssignableFrom(type))
			{
				return CreateDefaultJsonArrayBinder_ITuple(type);
			}
#else
			// ITuple is not visible to the netstandard2.0 compiler, but the runtime usually carries it: probe dynamically
			if (RuntimeITuple.IsTuple(type))
			{
				return CreateDefaultJsonArrayBinder_ITuple(type);
			}
#endif

			var staticMethod = type.GetMethod("JsonUnpack", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (staticMethod != null)
			{
				var binder = CreateBinderStaticJsonBindable(type, staticMethod);
				Contract.Debug.Assert(binder != null);
				return CreateDefaultJsonArrayBinder_Binder(type, binder);
			}

			// look for a method with a same name (before the introduction of static methods in interfaces)
			if (type.IsGenericInstanceOf(typeof(IJsonDeserializable<>)))
			{ // use the method defined in the interface
				var binder = CreateStaticJsonDeserializerBinder(type);
				return CreateDefaultJsonArrayBinder_Binder(type, binder);
			}

			staticMethod = type.GetMethod("JsonDeserialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (staticMethod != null)
			{
				var binder = CreateBinderStaticJsonSerializable(type, staticMethod);
				Contract.Debug.Assert(binder != null);
				return CreateDefaultJsonArrayBinder_Binder(type, binder);
			}

			if (type.IsAssignableTo<IEnumerable>())
			{
				return CreateDefaultJsonArrayBinder_Boxed(type);
			}

			// we don't know how to bind this type of collection
			return CreateDefaultJsonArrayBinder_Invalid(type);
		}

		private static CrystalJsonTypeBinder CreateStaticJsonDeserializerBinder(Type type)
		{
			var m = typeof(CrystalJsonTypeResolver)
				.GetMethod(nameof(BindJsonDeserializer), BindingFlags.Static | BindingFlags.NonPublic)!
				.MakeGenericMethod(type);

#if NET5_0_OR_GREATER
			return m.CreateDelegate<CrystalJsonTypeBinder>();
#else
			return (CrystalJsonTypeBinder) m.CreateDelegate(typeof(CrystalJsonTypeBinder)); // the generic CreateDelegate<T>() overload is .NET 5+
#endif
		}

		private static object? BindJsonDeserializer<TValue>(JsonValue? value, Type type, ICrystalJsonTypeResolver resolver) where TValue : IJsonDeserializable<TValue>
		{
			if (value is null or JsonNull) return null;
#if NET7_0_OR_GREATER
			return TValue.JsonDeserialize(value, resolver);
#else
			// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
			return JsonDeserializableDispatcher<TValue>.Invoke(value, resolver);
#endif
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_Boxed(Type type)
		{
			return (resolver, array) => ConvertToBoxedEnumerable(type, array, resolver.BindJsonValue);
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_STuple(Type type)
		{
			return (resolver, array) => ConvertToSTuple(type, array, resolver.BindJsonValue);
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_ITuple(Type type)
		{
			var filler = CreateBinderForValueTuple(type);
			return (resolver, array) => filler(array, type, resolver);
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_Filler(string filler, Type elementType)
		{
			return (resolver, array) => resolver.InvokeFiller(filler, elementType, array);
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_Binder(Type type, CrystalJsonTypeBinder binder)
		{
			return (resolver, array) => binder(array, type, resolver);
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_Invalid(Type type)
		{
			return (_, array) => throw JsonBindingException.CannotBindJsonArrayToThisType(array, type);
		}

		private static object? ConvertToBoxedEnumerable(Type type, JsonArray? array, Func<Type, JsonValue?, object?> convert)
		{
			if (array == null) return null;

			var res = new List<object?>(array.Count);
			foreach (var item in array)
			{
				res.Add(convert(typeof(object), item));
			}

			if (type.IsAssignableTo<ICollection>())
			{ // Modifiable ?
				return res;
			}
			else
			{ // Non modifiable
				return new ReadOnlyCollection<object?>(res);
			}
		}

		private static object? ConvertToSTuple(Type _, JsonArray? array, Func<Type, JsonValue?, object?> convert)
		{
			if (array == null) return null;
			var res = array.Count != 0 ? new object?[array.Count] : [ ];
			int p = 0;
			foreach (var item in array)
			{
				res[p++] = convert(typeof(object), item);
			}
			return new ListTuple<object?>(res, 0, res.Length);
		}

		private Func<Type, JsonValue?, object?>? m_cachedValueBinder;

		private object? InvokeFiller(
			string name,
			Type type,
			JsonArray? array
		)
		{
			return GetFillerMethod(this.GetType(), name, type).Invoke(this, [ array, m_cachedValueBinder ??= this.BindJsonValue ]);
		}

		#region Filler Methods...

		private static readonly QuasiImmutableCache<string, MethodInfo> s_fillers = new(null, StringComparer.Ordinal, null);

		[Pure]
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		private static MethodInfo GetFillerMethod(
			Type resolverType,
			string name,
			Type resultType
		)
		{
			string key = resultType.FullName + ":" + name;
			return s_fillers.TryGetValue(key, out MethodInfo? mi) ? mi : MakeFillerMethod(resolverType, name, resultType, key);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		private static MethodInfo MakeFillerMethod(
			Type resolverType,
			string name,
			Type resultType,
			string key
		)
		{
			var mi = resolverType.MakeGenericMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy, [ typeof(JsonValue), resultType ]);
			if (mi == null) throw new ArgumentException($"No matching filler method {name} found on {resolverType.GetFriendlyName()}", nameof(name));

			s_fillers.SetItem(key, mi);
			return mi;
		}

		internal static bool TryMapException(Exception ex, int index, Type elementType, JsonValue? child, [MaybeNullWhen(false)] out JsonBindingException mapped)
		{
			if (ex is TargetInvocationException tiEx)
			{
				ex = tiEx.InnerException ?? ex;
			}
			if (ex is JsonBindingException jbEx)
			{
				mapped = Wrap(jbEx, index, elementType, child);
				return true;
			}

			mapped = null;
			return false;

			static JsonBindingException Wrap(JsonBindingException jbEx, int index, Type elementType, JsonValue? child)
			{
				var path = JsonPath.Create(index);
				var reason = jbEx.Reason ?? jbEx.Message;
				if (jbEx.Path != null)
				{
					path = JsonPath.Combine(path, jbEx.Path.Value);
				}
				var targetType = jbEx.TargetType ?? elementType;
				var jsonValue = jbEx.Value ?? child;
				return new($"Cannot bind{(jsonValue != null ? $" JSON {jsonValue.Type} to":"")} field '({elementType.GetFriendlyName()}[]){path}' of type '{targetType.GetFriendlyName()}': {reason}", reason, path, jbEx.Value, targetType, jbEx.InnerException);
			}
		}

		[UsedImplicitly]
		public static TOutput[] FillArray<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			var type = typeof(TOutput);
			var list = new TOutput[array.Count];
			for (int i = 0; i < list.Length; i++)
			{
				try
				{
					list[i] = (TOutput) convert(type, array[i]);
				}
				catch (Exception ex)
				{
					if (TryMapException(ex, i, typeof(TOutput), array[i] as JsonValue, out var mapped))
					{
						throw mapped;
					}
				}
			}
			return list;
		}

		[UsedImplicitly]
		public static List<TOutput> FillList<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			var type = typeof(TOutput);
			var list = new List<TOutput>(array.Count);
			foreach (var item in array)
			{
				try
				{
					list.Add((TOutput) convert(type, item));
				}
				catch (Exception ex)
				{
					if (TryMapException(ex, list.Count, typeof(TOutput), item as JsonValue, out var mapped))
					{
						throw mapped;
					}
				}
			}
			return list;
		}

		[UsedImplicitly]
		public static HashSet<TOutput> FillHashSet<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			var type = typeof(TOutput);
			var list = new HashSet<TOutput>();
			foreach (var item in array)
			{
				try
				{
					list.Add((TOutput) convert(type, item));
				}
				catch (Exception ex)
				{
					if (TryMapException(ex, list.Count, typeof(TOutput), item as JsonValue, out var mapped))
					{
						throw mapped;
					}
				}
			}
			return list;
		}

		public static ImmutableList<TOutput> FillImmutableList<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			var type = typeof(TOutput);
			var list = ImmutableList.CreateBuilder<TOutput>();
			foreach (var item in array)
			{
				try
				{
					list.Add((TOutput) convert(type, item));
				}
				catch (Exception ex)
				{
					if (TryMapException(ex, list.Count, typeof(TOutput), item as JsonValue, out var mapped))
					{
						throw mapped;
					}
				}
			}
			return list.ToImmutable();
		}

		public static ImmutableArray<TOutput> FillImmutableArray<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			var type = typeof(TOutput);
			var list = ImmutableArray.CreateBuilder<TOutput>();
			foreach (var item in array)
			{
				try
				{
					list.Add((TOutput) convert(type, item));
				}
				catch (Exception ex)
				{
					if (TryMapException(ex, list.Count, typeof(TOutput), item as JsonValue, out var mapped))
					{
						throw mapped;
					}
				}
			}
			return list.ToImmutable();
		}

		[UsedImplicitly]
		public static ImmutableHashSet<TOutput> FillImmutableHashSet<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			var type = typeof(TOutput);
			var hashSet = ImmutableHashSet.CreateBuilder<TOutput>();
			foreach (var item in array)
			{
				try
				{
					hashSet.Add((TOutput) convert(type, item));
				}
				catch (Exception ex)
				{
					if (TryMapException(ex, hashSet.Count, typeof(TOutput), item as JsonValue, out var mapped))
					{
						throw mapped;
					}
				}
			}
			return hashSet.ToImmutable();
		}

		[UsedImplicitly]
		public static ReadOnlyCollection<TOutput> FillReadOnlyCollection<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			return new ReadOnlyCollection<TOutput>(FillList<TInput, TOutput>(array, convert));
		}

		[UsedImplicitly]
		public static IEnumerable<TOutput> FillEnumerable<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			// if the caller wants an IEnumerable<T>, we assume the intended purpose is LINQ/foreach, and does not require .Count or being able to change the collection
			// => List<T> is the simplest, but can be modified (Add, Remove)
			// => T[] is more efficient in memory, is also modifiable (arr[2] = xxx)
			// => ImmutableList<T> is overkill
			// => ReadOnlyCollection<T>(...) may be the best compromise ?
			//REVIEW: do we really care about returning a read-only instance?

			return new ReadOnlyCollection<TOutput>(FillArray<TInput, TOutput>(array, convert));
		}

		#endregion

		private static bool FilterMemberByAttributes(MemberInfo member, bool hasDataContract, ref string name, ref object? defaultValue, ref CrystalJsonMemberFlags flags)
		{
			// note: [JsonIgnore] is checked BEFORE the [DataMember] opt-in: an "ignore this member" signal always wins over
			// an "include this member" signal ([DataMember], [JsonProperty], [JsonInclude]), because mixing the two on one
			// member is an application bug, and both serialization paths must resolve it the same way

			{ // "[JsonIgnore]" (System.Text.Json, JSON.NET, or any attribute with that name)
				// we cannot "say the name" of these attributes without referencing their packages,
				// so we have to search using the name. This could break when using code trimming!
				if (member.TryGetCustomAttribute("JsonIgnoreAttribute", true, out var attr) && attr != null)
				{
					// the System.Text.Json attribute has a Condition property that turns the exclusion into a
					// per-member serialization rule; read it by name so any TFM (and any lookalike attribute
					// without the property, like the JSON.NET one) is handled the same way
					switch (attr.GetType().GetProperty("Condition")?.GetValue(attr)?.ToString())
					{
						case "Never":
						{ // always serialized, even when the settings discard nulls or defaults
							flags |= CrystalJsonMemberFlags.AlwaysEmit;
							break;
						}
						case "WhenWritingNull":
						{ // omitted when null, regardless of the settings
							flags |= CrystalJsonMemberFlags.OmitNullValues;
							break;
						}
						case "WhenWritingDefault":
						{ // omitted when equal to the member's default, regardless of the settings
							flags |= CrystalJsonMemberFlags.OmitDefaultValues;
							break;
						}
						default:
						{ // "Always", or an attribute without a Condition: excluded from both directions
							return false;
						}
					}
				}
			}

			if (hasDataContract)
			{ // must have an attribute "[DataMember]" to be eligible
				var attr = member.GetCustomAttribute<System.Runtime.Serialization.DataMemberAttribute>(inherit: true);
				if (attr == null)
				{ // skip!
					return false;
				}
				// check if a custom name is specified
				name = attr.Name ?? name;
				return true;
			}

			{ // must not have an attribute called "Ignore" in its name
				// we cannot "say the name" of this attribute without a ref to the Json.NET package,
				// so we have to search using the name. This could break when using code trimming!
				if (member.TryGetCustomAttribute("IgnoreAttribute", true, out var attr) && attr != null)
				{ // skip!
					return false;
				}
			}

			// look for other legacy serialization attributes
			// REVIEW: these are old attributes used during the era of SOAP and XML serialization, maybe we could drop support for them?
				
			{ // must not have "[XmlIgnore]"
				var attr = member.GetCustomAttribute<System.Xml.Serialization.XmlIgnoreAttribute>(inherit: true);
				if (attr != null)
				{ // skip!
					return false;
				}
			}

			{ // [XmlElement]
				var attr = member.GetCustomAttribute<System.Xml.Serialization.XmlElementAttribute>(inherit: true);
				if (attr != null)
				{ // [XmlElement(ElementName="xxx")]
					name = attr.ElementName;
					return true;
				}
			}

			{ // [XmlAttribute]
				var attr = member.GetCustomAttribute<System.Xml.Serialization.XmlAttributeAttribute>(inherit: true);
				if (attr != null)
				{ // [XmlAttribute(AttributeName="xxx")]
					name = attr.AttributeName;
					return true;
				}
			}

			return true;
		}

		/// <summary>Tests if a member carries the System.Text.Json <c>[JsonInclude]</c> attribute (matched by name, so no TFM needs a package reference)</summary>
		private static bool HasJsonIncludeAttribute(MemberInfo member)
			=> member.TryGetCustomAttribute("JsonIncludeAttribute", true, out var attr) && attr != null;

		private static bool FilterMemberByType(MemberInfo _, Type type)
		{
			if (typeof(Delegate).IsAssignableFrom(type))
			{
				// Delegate cannot be serialized, but are frequently found when dumping objects from unit tests (that include hooks or callbacks)
				return false;
			}

			return true;
		}

		private static JsonPropertyAttribute? FindPropertyAttribute(MemberInfo member)
		{
#if !NETSTANDARD2_0
			System.Text.Json.Serialization.JsonPropertyNameAttribute? fallbackSystemTextJson = null;
#else
			// the netstandard2.0 build does not reference System.Text.Json: recognize its attribute by name+namespace and read
			// its Name via reflection, the same way the Newtonsoft attribute is handled below (so [JsonPropertyName]
			// on consumer DTOs keeps working, just slower)
			Attribute? fallbackSystemTextJson = null;
#endif
			Attribute? fallbackNewtonsoftJson = null;
			foreach (var attr in member.GetCustomAttributes(true))
			{
				// look for our own attribute, that has priority
				if (attr is JsonPropertyAttribute jp)
				{
					return jp;
				}

				// recognize [JsonPropertyName(...)] from System.Text.Json, if present
#if !NETSTANDARD2_0
				if (attr is System.Text.Json.Serialization.JsonPropertyNameAttribute jpn)
				{
					fallbackSystemTextJson = jpn;
				}
#else
				if (attr.GetType().Name == "JsonPropertyNameAttribute" && attr.GetType().Namespace == "System.Text.Json.Serialization")
				{
					fallbackSystemTextJson = (Attribute) attr;
				}
#endif

				// likewise, recognize the attribute from JSON.Net
				//note: since we don't reference the package, we have to test the name+namespace !
				if (attr.GetType().Name == "JsonPropertyAttribute" && attr.GetType().Namespace == "Newtonsoft.Json")
				{
					fallbackNewtonsoftJson = (Attribute) attr;
				}
			}

			if (fallbackSystemTextJson is not null)
			{ // fake the original [JsonProperty("...")] by copying the name of the other attribute
#if NET5_0_OR_GREATER
				return new JsonPropertyAttribute(fallbackSystemTextJson.Name);
#else
				var stjName = (string?) fallbackSystemTextJson.GetType().GetProperty("Name")?.GetValue(fallbackSystemTextJson);
				if (stjName != null) return new JsonPropertyAttribute(stjName);
#endif
			}

			if (fallbackNewtonsoftJson is not null)
			{ // we need to access the "PropertyName" property via reflection!
				var name = (string?) fallbackNewtonsoftJson.GetType().GetProperty("PropertyName")?.GetValue(fallbackNewtonsoftJson);
				if (name != null) return new JsonPropertyAttribute(name);
			}

			// no valid candidate found
			return null;
		}

		private static CrystalJsonMemberDefinition[] GetMembersFromReflection(Type type)
		{
			Contract.NotNull(type);

			// test if there are attributes that could impact the serialization
			// note: we do not reference the types explicitly in the code, so that we don't have to reference their assemblies
			bool hasDataContract = type.GetCustomAttribute<DataContractAttribute>(inherit: true) != null;

			var members = new List<CrystalJsonMemberDefinition>();

			// non-public members are invisible by default, but [JsonInclude] (System.Text.Json) opts them in
			// explicitly (note: this is a superset of STJ, whose reflection serializer rejects non-public members)
			var fields = new List<FieldInfo>(type.GetFields(BindingFlags.Instance | BindingFlags.Public));
			foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
			{
				if (HasJsonIncludeAttribute(field))
				{
					fields.Add(field);
				}
			}

			foreach (var field in fields)
			{
				Contract.Debug.Assert(field != null);

				var fieldType = field.FieldType;
				
				if (!FilterMemberByType(field, fieldType))
				{
					continue;
				}

				var jp = FindPropertyAttribute(field);

				string name = jp?.PropertyName ?? field.Name;
				object? defaultValue;
				try
				{
					defaultValue = jp?.DefaultValue ?? fieldType.GetDefaultValue();
				}
				catch (Exception e)
				{
					throw CrystalJson.Errors.Serialization_CouldNotGetDefaultValueForMember(type, field, e);
				}

				var flags = CrystalJsonMemberFlags.None;
				if (!FilterMemberByAttributes(field, hasDataContract, ref name, ref defaultValue, ref flags))
				{
					continue; // skip
				}

				var visitor = CrystalJsonVisitor.GetVisitorForType(fieldType, atRuntime: false);
				if (visitor == null) throw new ArgumentException($"Doesn't know how to serialize field {field.Name} of type {fieldType.GetFriendlyName()}", nameof(type));

				if (IsInitOnlyMember(field)) flags |= CrystalJsonMemberFlags.InitOnly;
				if (IsRequiredMember(field)) flags |= CrystalJsonMemberFlags.Required;
				if (IsKeyMember(field)) flags |= CrystalJsonMemberFlags.Key;
				if (IsNotNullMemberType(field, fieldType)) flags |= CrystalJsonMemberFlags.NotNull;
				if (defaultValue is not null && !defaultValue.Equals(fieldType.GetDefaultValue())) flags |= CrystalJsonMemberFlags.NonZeroDefault;

				var definition = new CrystalJsonMemberDefinition()
				{
					Type = fieldType,
					Flags = flags,
					Name = name,
					OriginalName = field.Name,
					EncodedName = new(name),
					NullableOfType = GetNullableType(fieldType),
					Attributes = jp,
					DefaultValue = defaultValue,
					Getter = field.CompileGetter(),
					Setter = field.CompileSetter(),
					Visitor = visitor,
					Binder = GenericJsonValueBinder,
				};

				members.Add(definition);
			}

			var properties = new List<PropertyInfo>(type.GetProperties(BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public));
			foreach (var property in type.GetProperties(BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic))
			{
				if (HasJsonIncludeAttribute(property))
				{
					properties.Add(property);
				}
			}

			foreach (var property in properties)
			{
				Type propertyType = property.PropertyType;

				if (!FilterMemberByType(property, propertyType))
				{ // skip it!
					continue;
				}

				var jp = FindPropertyAttribute(property);

				string name = jp?.PropertyName ?? property.Name;
				object? defaultValue;
				try
				{
					defaultValue = jp?.DefaultValue ?? propertyType.GetDefaultValue();
				}
				catch (Exception e)
				{
					throw CrystalJson.Errors.Serialization_CouldNotGetDefaultValueForMember(type, property, e);
				}

				var flags = CrystalJsonMemberFlags.None;
				if (!FilterMemberByAttributes(property, hasDataContract, ref name, ref defaultValue, ref flags))
				{ // skip it !
					continue; // skip
				}

				// [JsonInclude] also unlocks the non-public accessors of the property
				bool includeNonPublic = HasJsonIncludeAttribute(property);

				// skip properties that take parameters (possible in VB.NET)
				var method = property.GetGetMethod(includeNonPublic);
				if (method == null || method.GetParameters().Length > 0) continue;

				var visitor = CrystalJsonVisitor.GetVisitorForType(propertyType);
				Contract.Debug.Assert(visitor != null);

				var getter = property.CompileGetter(includeNonPublic);

				Action<object, object?>? setter;
				method = property.GetSetMethod(includeNonPublic);
				if (method != null && method.GetParameters().Length == 1)
				{ // we have a direct setter
					setter = property.CompileSetter(includeNonPublic);
				}
				else
				{ // attempts to generate a custom setter if there isn't one
					setter = TryCompileAdderForReadOnlyCollection(property);
				}

				if (setter == null) flags |= CrystalJsonMemberFlags.ReadOnly;
				if (IsInitOnlyMember(property)) flags |= CrystalJsonMemberFlags.InitOnly;
				if (IsRequiredMember(property)) flags |= CrystalJsonMemberFlags.Required;
				if (IsKeyMember(property)) flags |= CrystalJsonMemberFlags.Key;
				if (IsNotNullMemberType(property, propertyType)) flags |= CrystalJsonMemberFlags.NotNull;
				if (defaultValue is not null && !defaultValue.Equals(propertyType.GetDefaultValue())) flags |= CrystalJsonMemberFlags.NonZeroDefault;

				var definition = new CrystalJsonMemberDefinition()
				{
					Type = propertyType,
					Flags = flags,
					Name = name,
					OriginalName = property.Name,
					EncodedName = new(name),
					NullableOfType = GetNullableType(propertyType),
					Attributes = jp,
					DefaultValue = defaultValue,
					Getter = getter,
					Setter = setter,
					Visitor = visitor,
					Binder = GenericJsonValueBinder,
				};
				members.Add(definition);
			}

			return members.ToArray();
		}

		/// <summary>Tests if a member of a type is decorated with <see cref="System.ComponentModel.DataAnnotations.KeyAttribute"/></summary>
		public static bool IsKeyMember(MemberInfo member)
		{
			return member.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() != null;
		}

		/// <summary>Tests if a member is a readonly field, or an init-only property</summary>
		public static bool IsInitOnlyMember(MemberInfo member)
		{
			switch (member)
			{
				case FieldInfo field:
				{
					return field.IsInitOnly;
				}
				case PropertyInfo property:
				{
					var setter = property.GetSetMethod();
					if (setter != null)
					{
						foreach (var mod in setter.ReturnParameter.GetRequiredCustomModifiers())
						{
							if (mod == typeof(IsExternalInit)) return true;
						}
					}
					return false;
				}
				default:
				{
					return false;
				}
			}
		}

		/// <summary>Tests if a member of a type is decorated with the <see langword="required"/> keyword</summary>
		public static bool IsRequiredMember(MemberInfo member)
		{
			return member.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>() != null;
		}

#if NET5_0_OR_GREATER
		private static readonly NullabilityInfoContext s_nullabilityContext = new();
#endif

		/// <summary>Tests if a member of a type is decorated with the <see langword="required"/> keyword</summary>
		public static bool IsNotNullMemberType(MemberInfo member, Type memberType)
		{
			// value types, except Nullable<T>, cannot be null
			if (IsNullableType(memberType)) return false;
			if (memberType.IsValueType) return true;

#if NET5_0_OR_GREATER
			switch (member)
			{
				case PropertyInfo property:
				{
					lock (s_nullabilityContext)
					{
						return s_nullabilityContext.Create(property).ReadState != NullabilityState.Nullable;
					}
				}
				case FieldInfo field:
				{
					lock (s_nullabilityContext)
					{
						return s_nullabilityContext.Create(field).ReadState != NullabilityState.Nullable;
					}
				}
				default:
				{
					return false;
				}
			}
#else
			// NullabilityInfoContext (.NET 6+) is not available: without the nullable-reference-type metadata reader,
			// conservatively treat every reference-type member as nullable (fewer non-null constraints enforced when
			// binding, never a spurious "required" failure)
			return false;
#endif
		}

		private static Action<object, object?>? TryCompileAdderForReadOnlyCollection(
			PropertyInfo? property
		)
		{
			Contract.Debug.Requires(property?.DeclaringType != null);

			// We have an object with a "get-only" collection that is initialized by the ctor:
			// ex: public List<Foo> Foos { get; } = new List<Foo>();
			// => we can still deserialize the collection by calling obj.Foos.Add(..) instead of obj.Foos = [ ... ]

			var collectionType = property.PropertyType.FindGenericType(typeof(ICollection<>));
			if (collectionType == null)
			{
				return null;
			}

			// we want to compile:
			//	AddRangeTo<TItem>((IEnumerable<TItem>) value, (PROPERTY_TYPE) instance.PROPERTY);

			var prmInstance = Expression.Parameter(typeof(object), "instance");
			var prmValue = Expression.Parameter(typeof(object), "value");

			var src = prmValue.CastFromObject(collectionType);
			var dst = Expression.Property(prmInstance.CastFromObject(property.DeclaringType), property);

			var m = typeof(CrystalJsonTypeResolver)
				.GetMethod(nameof(AddRangeTo), BindingFlags.NonPublic | BindingFlags.Static)!
				.MakeGenericMethod(collectionType.GetGenericArguments()[0]);

			var body = Expression.Call(m, src, dst);

			var lambda = Expression.Lambda<Action<object, object?>>(
				body,
				"<>_Append_" + property.DeclaringType.GetFriendlyName() + "_" + property.Name,
				tailCall: true,
				[ prmInstance, prmValue ]
			);
			return lambda.Compile();

		}

		private static void AddRangeTo<T>(IEnumerable<T>? source, ICollection<T>? destination)
		{
			if (source != null && destination != null)
			{
				foreach (var item in source)
				{
					destination.Add(item);
				}
			}
		}

		private static readonly CrystalJsonTypeBinder GenericJsonValueBinder = (v, t, r) => (v ?? JsonNull.Missing).Bind(t, r);

#if !NETSTANDARD2_0
		// System.Text.Json interop is disabled on the netstandard2.0 build: [JsonPolymorphic]/[JsonDerivedType] on consumer DTOs are not recognized there
		private static bool TryFindJsonPolymorphicAttribute(Type type, [MaybeNullWhen(false)] out System.Text.Json.Serialization.JsonPolymorphicAttribute attr, [MaybeNullWhen(false)] out Type parent)
		{
			attr = type.GetCustomAttribute<System.Text.Json.Serialization.JsonPolymorphicAttribute>(inherit: false);
			if (attr != null)
			{ // we are the "top"
				parent = type;
				return true;
			}

			// is it on our base type?
			if (type.BaseType is not null && type.BaseType != typeof(object))
			{
				if (TryFindJsonPolymorphicAttribute(type.BaseType, out attr, out parent))
				{ // we found it
					return true;
				}
			}

			// is it on one of our interfaces?
			foreach(var iface in type.GetInterfaces())
			{
				// there is zero chances that it is under the System namespace
				if (iface.Namespace is "System" || iface.Namespace!.StartsWith("System.")) continue;

				if (TryFindJsonPolymorphicAttribute(iface, out attr, out parent))
				{
					return true;
				}
			}

			parent = null;
			return false;
		}
#else
		// the netstandard2.0 build does not reference System.Text.Json: recognize [JsonPolymorphic]/[JsonDerivedType]
		// by their full name and read their properties via reflection, the same way [JsonPropertyName] is handled by
		// FindPropertyAttribute (slower, but polymorphic consumer DTOs keep working)
		private static bool TryFindJsonPolymorphicAttributeByName(Type type, out string? discriminatorPropertyName, [MaybeNullWhen(false)] out Type parent)
		{
			foreach (var attr in type.GetCustomAttributes(inherit: false))
			{
				var at = attr.GetType();
				if (at.FullName == "System.Text.Json.Serialization.JsonPolymorphicAttribute")
				{
					discriminatorPropertyName = (string?) at.GetProperty("TypeDiscriminatorPropertyName")?.GetValue(attr);
					parent = type;
					return true;
				}
			}

			// is it on our base type?
			if (type.BaseType is not null && type.BaseType != typeof(object))
			{
				if (TryFindJsonPolymorphicAttributeByName(type.BaseType, out discriminatorPropertyName, out parent))
				{
					return true;
				}
			}

			// is it on one of our interfaces?
			foreach (var iface in type.GetInterfaces())
			{
				if (iface.Namespace is "System" || iface.Namespace!.StartsWith("System.")) continue;
				if (TryFindJsonPolymorphicAttributeByName(iface, out discriminatorPropertyName, out parent))
				{
					return true;
				}
			}

			discriminatorPropertyName = null;
			parent = null;
			return false;
		}

		private static List<KeyValuePair<Type, object?>> GetJsonDerivedTypesByName(Type parent)
		{
			var res = new List<KeyValuePair<Type, object?>>();
			foreach (var attr in parent.GetCustomAttributes(inherit: false))
			{
				var at = attr.GetType();
				if (at.FullName == "System.Text.Json.Serialization.JsonDerivedTypeAttribute")
				{
					if (at.GetProperty("DerivedType")?.GetValue(attr) is Type derived)
					{
						res.Add(new(derived, at.GetProperty("TypeDiscriminator")?.GetValue(attr)));
					}
				}
			}
			return res;
		}
#endif

		/// <summary>Extracts the type definition via reflection, and generate a list of compiled binders</summary>
		/// <param name="type">Class, struct, interface. Primitive type are not supported</param>
		private CrystalJsonTypeDefinition CreateFromReflection(Type type)
		{
			Contract.Debug.Requires(type != null && !type.IsPrimitive);

			JsonEncodedPropertyName? typeDiscriminatorProperty = null;
			JsonValue? typeDiscriminatorValue = null;
			Dictionary<JsonValue, Type>? derivedTypeMap = null;
			Type? baseType = null; // if we are not a "leaf"

			// look for polymorphic types (either on the type itself or one of its parent)
#if !NETSTANDARD2_0
			if (TryFindJsonPolymorphicAttribute(type, out var polymorphic, out var parent))
			{
				typeDiscriminatorProperty = new(polymorphic.TypeDiscriminatorPropertyName ?? "$type");

				var derivedTypes = parent.GetCustomAttributes<System.Text.Json.Serialization.JsonDerivedTypeAttribute>(inherit: false);

				if (type == parent)
				{ // we are the top

					// generate the type map
					derivedTypeMap = new(derivedTypes.TryGetNonEnumeratedCount(out var count) ? count : 0, JsonValueComparer.Default);
					foreach (var t in derivedTypes)
					{
						if (t.TypeDiscriminator == null) continue;
						derivedTypeMap[JsonValue.FromValue(t.TypeDiscriminator)] = t.DerivedType;
					}
				}
				else
				{ // we are a derived type

					// find our entry to extract the discriminator value
					bool found = false;
					foreach (var t in derivedTypes)
					{
						if (t.DerivedType == type)
						{
							if (t.TypeDiscriminator is not null)
							{
								Contract.Debug.Assert(t.TypeDiscriminator is string or int);
								typeDiscriminatorValue = JsonValue.FromValue(t.TypeDiscriminator);
							}
							found = true;
							break;
						}
					}

					if (!found) throw new InvalidOperationException("Cannot resolve intermediate type {} in polymorphic chain under {} because there is not JsonDerivedType attribute for this specific type.");
					baseType = parent;
				}
			}
#else
			// same logic as above, but reading the System.Text.Json attributes by name via reflection
			if (TryFindJsonPolymorphicAttributeByName(type, out var discriminatorName, out var parent))
			{
				typeDiscriminatorProperty = new(discriminatorName ?? "$type");

				var derivedTypes = GetJsonDerivedTypesByName(parent);

				if (type == parent)
				{ // we are the top

					// generate the type map
					derivedTypeMap = new(derivedTypes.Count, JsonValueComparer.Default);
					foreach (var t in derivedTypes)
					{
						if (t.Value == null) continue;
						derivedTypeMap[JsonValue.FromValue(t.Value)] = t.Key;
					}
				}
				else
				{ // we are a derived type

					// find our entry to extract the discriminator value
					bool found = false;
					foreach (var t in derivedTypes)
					{
						if (t.Key == type)
						{
							if (t.Value is not null)
							{
								Contract.Debug.Assert(t.Value is string or int);
								typeDiscriminatorValue = JsonValue.FromValue(t.Value);
							}
							found = true;
							break;
						}
					}

					if (!found) throw new InvalidOperationException("Cannot resolve intermediate type {} in polymorphic chain under {} because there is not JsonDerivedType attribute for this specific type.");
					baseType = parent;
				}
			}
#endif

			// enumerate the members
			var members = GetMembersFromReflection(type);

			// look for any custom binders
			var binder = FindCustomBinder(type, out var generator, members);

			if (binder == null)
			{ // we need to generate one ourselves for this type

#if NET5_0_OR_GREATER
				if (polymorphic != null && type.IsInterface || type.IsAbstract)
#else
				if (type.IsAbstract) // no polymorphic support on this target: only the abstract case remains
#endif
				{ // we are part of a polymorphic chain but cannot be constructed
					//generator = null;
				}
				else
				{
					generator ??= RequireGeneratorForType(type);
				}
			}

			return new CrystalJsonTypeDefinition(type, CrystalJsonTypeFlags.None, binder, generator, members, null, baseType, typeDiscriminatorProperty, typeDiscriminatorValue, derivedTypeMap);
		}

		/// <summary>Extracts the type definition for the Nullable&lt;T&gt; version of a struct</summary>
		/// <param name="nullableType">Nullable type (ex: Nullable&lt;AcmeStruct&gt;)</param>
		/// <param name="definition">Definition of the underlying type (AcmeStruct)</param>
		/// <returns>Definition that uses the underlying definition to bind a Nullable version of this type</returns>
		private CrystalJsonTypeDefinition CreateNullableTypeWrapper(Type nullableType, CrystalJsonTypeDefinition definition)
		{
			Contract.NotNull(nullableType);
			Contract.NotNull(definition);

			_ = TryGetConverterFor(definition.Type, out var converter); // may be null, will throw in the generated binder!
			return new(definition.Type, definition.Flags, NullableBinder, null, definition.Members, definition.Visitor, definition.BaseType, definition.TypeDiscriminatorProperty, definition.TypeDiscriminatorValue, definition.DerivedTypeMap);

			object? NullableBinder(JsonValue? value, Type bindingType, ICrystalJsonTypeResolver resolver)
			{
				// it can be either null, or an instance of T
				if (value.IsNullOrMissing()) return null;
				return converter != null
					? converter.BindJsonValue(value, resolver)
					: throw new NotSupportedException($"Does not know how to unpack type {bindingType.GetFriendlyName()}");
			}
		}

		private static Func<object> RequireGeneratorForType(Type type)
		{
			return type.CompileGenerator() ?? throw new InvalidOperationException($"Could not find any parameterless constructor required to deserialize instances of type '{type.GetFriendlyName()}'");
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		private static CrystalJsonTypeBinder? FindCustomBinder(
			Type type,
			out Func<object>? generator,
			CrystalJsonMemberDefinition[] members
		)
		{
			generator = null;
			CrystalJsonTypeBinder? binder;

			// Static Bindable
			var staticMethod = type.GetMethod("JsonUnpack", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (staticMethod != null)
			{
				binder = CreateBinderStaticJsonBindable(type, staticMethod);
				Contract.Debug.Assert(binder != null);
				return binder;
			}

			// ctor that takes a JsonValue as the first parameter
			var ctor = FindJsonConstructor(type);
			if (ctor != null)
			{
				binder = CreateBinderJsonConstructor(type, ctor);
				return binder;
			}

			// Static Serializable

			if (type.IsGenericInstanceOf(typeof(IJsonDeserializable<>)))
			{ // use the method defined in the interface
				return CreateStaticJsonDeserializerBinder(type);
			}

			// look for a method with a same name (before the introduction of static methods in interfaces)
			staticMethod = type.GetMethod("JsonDeserialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (staticMethod != null)
			{
				binder = CreateBinderStaticJsonSerializable(type, staticMethod);
				Contract.Debug.Assert(binder != null);
				return binder;
			}

			// Dictionary?
			if (type.IsGenericInstanceOf(typeof(IDictionary<,>)))
			{
				if (type.Name == "ImmutableDictionary`2" && type.IsGenericInstanceOf(typeof(ImmutableDictionary<,>)))
				{
					return CreateBinderForImmutableDictionary(type);
				}

				generator = RequireGeneratorForType(type);
				binder = CreateBinderForDictionary(type, generator);
				if (binder != null) return binder;
			}

			if (type.IsAnonymousType())
			{ // anonymous types have a private ctor that takes all the members (in order)...
				binder = CreateBinderForAnonymousType(type, members);
				if (binder != null) return binder;
			}

			return null;
		}

		private static ConstructorInfo? FindJsonConstructor(Type type)
		{
			foreach (var ctor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (IsJsonConstructor(ctor))
				{
					return ctor;
				}
			}
			return null;
		}

		private static bool IsJsonConstructor(ConstructorInfo ctor)
		{
			// Recognize the following ctors as able to deserialize JSON
			// - ctor(JsonValue value)
			// - ctor(JsonValue value, ICrystalJsonTypeResolver resolver)

			var args = ctor.GetParameters();
			if (args.Length == 0) return false;
			var arg0Type = args[0].ParameterType;
			if (arg0Type != typeof (JsonValue) && arg0Type != typeof(JsonObject) && arg0Type != typeof(JsonArray)) return false;
			if (args.Length > 1)
			{
				if (!typeof (ICrystalJsonTypeResolver).IsAssignableFrom(args[1].ParameterType)) return false;
				if (args.Length > 2)
				{
					//TODO: handle optional params(resolver, ...)
					return false;
				}
			}
			return true;
		}

		private static CrystalJsonTypeBinder? CreateBinderForAnonymousType(Type type, CrystalJsonMemberDefinition[] members)
		{
			// enumerate the properties
			var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.CanRead && !prop.CanWrite).ToArray();

			// enumerate the ctors
			var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
			if (ctors.Length != 1) return null; // either not an anonymous type, or the compiler behavior has changed??
			var ctor = ctors[0];

			var args = ctor.GetParameters();
			if (args.Length != props.Length) return null; // same thing, not anonymous or something changed

			var map = members.ToDictionary(m => m.Name, StringComparer.Ordinal);
			var fields = props.Select(p =>
			(
				Name: p.Name,
				Type: p.PropertyType,
				Binder: map[p.Name].Binder
			)).ToArray();

			return CreateBinderForAnonymousType_NoScope(ctor, fields);
		}

		private static CrystalJsonTypeBinder CreateBinderForAnonymousType_NoScope(ConstructorInfo ctor, (string Name, Type Type, CrystalJsonTypeBinder Binder)[] fields)
		{
			return (v, t, r) =>
			{
				if (v == null || v.IsNull) return null;
				if (v is not JsonObject obj) throw FailCannotDeserializeNotJsonObject(t);

				var items = new object?[fields.Length];
				for (int i = 0; i < fields.Length; i++)
				{
					var field = fields[i];
					var value = obj[field.Name];
					items[i] = field.Binder(value, field.Type, r);
				}
				//PERF: compile a lambda that invokes the ctor?
				return ctor.Invoke(items);
			};
		}

		/// <summary>Generates a binder that calls the static JsonSerialize method ("duck typing")</summary>
		private static CrystalJsonTypeBinder CreateBinderStaticJsonSerializable(Type type, MethodInfo staticMethod)
		{
			// The type must have a static "factory" that will deserialize instances of this type

			// We are called with this signature:      object binder(JsonValue value, Type bindingType, CrystalJsonTypeResolver resolver)
			// We call a method with this signature:   Type [Type].JsonDeserialize([JsonValue|JsonObject] value, CrystalJsonTypeResolver resolver)

			var prms = staticMethod.GetParameters();
			if (prms.Length != 2)
			{
				throw new InvalidOperationException($"Static serialization method '{type.GetFriendlyName()}.{staticMethod.Name}' method must take two parameters");
			}
			var prmJsonType = prms[0].ParameterType;
			if (prmJsonType != typeof(JsonValue) && prmJsonType != typeof(JsonObject) && prmJsonType != typeof(JsonArray))
			{
				throw new InvalidOperationException($"First parameter of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must either be of type JsonValue, JsonObject or JsonArray (it was {prmJsonType.GetFriendlyName()})");
			}
			if (prms[1].ParameterType != typeof(ICrystalJsonTypeResolver))
			{
				throw new InvalidOperationException($"Second parameter of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must be of type {nameof(ICrystalJsonTypeResolver)} (it was {prmJsonType.GetFriendlyName()})");
			}

			if (!type.IsAssignableFrom(staticMethod.ReturnType))
			{
				throw new InvalidOperationException($"Return type of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must be assignable to type {type.GetFriendlyName()} (it was {prmJsonType.GetFriendlyName()})");
			}

			// we want to produce: (value, bindingType, resolver) => (object) [Type].JsonDeserialize((JsonObject)value, resolver);

			var prmValue = Expression.Parameter(typeof(JsonValue), "value");
			var prmBindingType = Expression.Parameter(typeof(Type), "bindingType");
			var prmResolver = Expression.Parameter(typeof(ICrystalJsonTypeResolver), "resolver");

			// We may need to cast from JsonValue to JsonObject or JsonArray
			var castedJsonObject =
				prmJsonType == typeof(JsonObject) ? prmValue.CastFromObject(typeof(JsonObject)) :
				prmJsonType == typeof(JsonArray) ? prmValue.CastFromObject(typeof(JsonArray)) :
				prmValue;

			// body == "(object) Type.JsonDeserialize((JsonObject) value, resolver)"
			var body = Expression.Call(staticMethod, castedJsonObject, prmResolver).BoxToObject();

			// If the instance is null, we return default(Type) automatically

			// "(value == null)"
			var isNull = Expression.Equal(prmValue, Expression.Constant(null, typeof(JsonValue)));

			// "(value == null) ? null : ( ... )"
			body = Expression.Condition(isNull, Expression.Constant(null), body, typeof(object));

			// body == "(value == null) ? null : (object) Type.JsonSerialize((JsonObject) value, resolver)"

			return Expression.Lambda<CrystalJsonTypeBinder>(body, "<>_" + type.Name + "_JsonDeserialize", true, [ prmValue, prmBindingType, prmResolver ]).Compile();
		}

		/// <summary>Generates a binder that invokes the static <c>JsonUnpack</c> method on a type</summary>
		private static CrystalJsonTypeBinder CreateBinderStaticJsonBindable(Type type, MethodInfo staticMethod)
		{
			// The type has a static "factory" that can pack instance of this type into JsonValue

			// we are called with this signature:         object binder(JsonValue value, Type bindingType, ICrystalJsonTypeResolver resolver)
			// we need to invoke a method with signature: [Type].JsonUnpack([TJsonValue] value, ICrystalJsonTypeResolver resolver)

			var prms = staticMethod.GetParameters();
			if (prms.Length != 2)
			{
				throw new InvalidOperationException($"Static deserialization method '{type.GetFriendlyName()}.{staticMethod.Name}' method must take two parameters");
			}
			Type prmJsonType = prms[0].ParameterType;
			if (prmJsonType != typeof(JsonValue) && !typeof(JsonValue).IsAssignableFrom(prmJsonType))
			{
				throw new InvalidOperationException($"First parameter of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must be an JsonValue (it was {prmJsonType.GetFriendlyName()})");
			}
			if (prms[1].ParameterType != typeof(ICrystalJsonTypeResolver))
			{
				throw new InvalidOperationException($"Second parameter of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must be a {nameof(ICrystalJsonTypeResolver)} (it was {prmJsonType.GetFriendlyName()})");
			}
			if (!type.IsAssignableFrom(staticMethod.ReturnType))
			{
				throw new InvalidOperationException($"Return type of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must be assignable to type {type.GetFriendlyName()} (it was {prmJsonType.GetFriendlyName()})");
			}

			// we want to produce: (value, bindingType, resolver) => { return (object) [Type].JsonUnpack((JsonObject)value, resolver); }

			var prmValue = Expression.Parameter(typeof(JsonValue), "value");
			var prmBindingType = Expression.Parameter(typeof(Type), "bindingType");
			var prmResolver = Expression.Parameter(typeof(ICrystalJsonTypeResolver), "resolver");

			// The method may want a derived type of JsonValue
			var castedJsonObject = prmJsonType != typeof(JsonValue) ? prmValue.CastFromObject(prmJsonType) : prmValue;

			// body == "(object) Type.JsonUnpack((TJsonValue) value, resolver)"
			var body = Expression.Call(staticMethod, castedJsonObject, prmResolver).BoxToObject();

			// If the instance is null, we return default(Type)

			// "(value == null)"
			var isNull = Expression.Equal(prmValue, Expression.Constant(null, typeof(JsonValue)));

			// "(value == null) ? null : ( ... )"
			body = Expression.Condition(isNull, Expression.Constant(null), body, typeof(object));

			// body == "(value == null) ? null : (object) Type.JsonUnpack((TJsonValue) value, resolver)"

			return Expression.Lambda<CrystalJsonTypeBinder>(body, "<>_" + type.Name + "_JsonUnpack", true, [ prmValue, prmBindingType, prmResolver ]).Compile();
		}

		/// <summary>Generates a binder that invokes the ctor that accepts a JsonValue (or derived) as the first parameter</summary>
		private static CrystalJsonTypeBinder CreateBinderJsonConstructor(Type type, ConstructorInfo ctor)
		{
			// the class must have a ctor that takes a JsonValue, and optionally a type resolver

			// we are called with this signature:     object binder(JsonValue value, Type bindingType, CrystalJsonTypeResolver resolver)
			// we need to call a ctor with signature: new Type([JsonValue|JsonObject|JsonArray|....] value [, CrystalJsonTypeResolver resolver])

			var prms = ctor.GetParameters();

			if (prms.Length == 0 || prms.Length > 2) ThrowHelper.ThrowInvalidOperationException($"Private constructor for type {type.GetFriendlyName()} must take either one or two arguments");
			var prmJsonType = prms[0].ParameterType;
			if (prmJsonType != typeof(JsonValue) && prmJsonType != typeof(JsonObject) && prmJsonType != typeof(JsonArray))
			{
				ThrowHelper.ThrowInvalidOperationException($"First parameter of constructor '{type.GetFriendlyName()}' must either be of type {nameof(JsonValue)}, {nameof(JsonObject)} or {nameof(JsonArray)} (it was {prmJsonType.GetFriendlyName()})");
			}
			if (prms.Length > 1 && prms[1].ParameterType != typeof(ICrystalJsonTypeResolver))
			{
				ThrowHelper.ThrowInvalidOperationException($"Second parameter of constructor '{type.GetFriendlyName()}' must be of type {nameof(ICrystalJsonTypeResolver)} (it was {prms[1].ParameterType.GetFriendlyName()})");
			}

			// we want to produce: (value, bindingType, resolver) => (object) new [Type]((JsonObject)value, resolver);

			var prmValue = Expression.Parameter(typeof(JsonValue), "value");
			var prmBindingType = Expression.Parameter(typeof(Type), "bindingType");
			var prmResolver = Expression.Parameter(typeof(ICrystalJsonTypeResolver), "resolver");

			// We may need to cast the value to JsonObject or JsonArray
			var castedJsonObject =
				prmJsonType == typeof(JsonObject) ? prmValue.CastFromObject(typeof(JsonObject)) :
				prmJsonType == typeof(JsonArray) ? prmValue.CastFromObject(typeof(JsonArray)) :
				prmValue;

			// body == "(object) Type.JsonDeserialize((JsonObject) value, resolver)"
			Expression body = Expression.New(ctor, prms.Length == 1 ? [ castedJsonObject ] : [ castedJsonObject, prmResolver ]).BoxToObject();

			// "value.IsNullOrMissing()"
			var isNull = Expression.Call(typeof(JsonValueExtensions).GetMethod(nameof(JsonValueExtensions.IsNullOrMissing))!, prmValue);

			// "(value == null) ? null : ( ... )"
			body = Expression.Condition(isNull, Expression.Constant(null), body, typeof(object));

			// body == "(value == null) ? null : (object) Type.JsonSerialize((JsonObject) value, resolver)"
			return Expression.Lambda<CrystalJsonTypeBinder>(body, "<>_" + type.Name + "_JsonSerialize", true, [ prmValue, prmBindingType, prmResolver ]).Compile();
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		private static CrystalJsonTypeBinder? CreateBinderForImmutableDictionary(Type type)
		{
			var dicType = type.FindGenericType(typeof(ImmutableDictionary<,>));

			// we only support ImmutableDictionary<K, V> for now
			if (dicType == null) return null; 

			var typeArgs = dicType.GetGenericArguments();
			var keyType = typeArgs[0];
			var valueType = typeArgs[1];

			if (keyType == typeof(string))
			{
				return CreateBinderForImmutableDictionary_StringKey(valueType);
			}
			if (keyType == typeof(int))
			{
				return CreateBinderForImmutableDictionary_Int32Key(valueType);
			}

			// unsupported key type
			//REVIEW: throw an exception instead?
			return null;
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		private static CrystalJsonTypeBinder CreateBinderForImmutableDictionary_StringKey(Type valueType)
		{
			var m = typeof(CrystalJsonTypeResolver)
				.GetMethod(nameof(BindImmutableDictionary_StringKey), BindingFlags.Static | BindingFlags.NonPublic)!
				.MakeGenericMethod(valueType);

#if NET5_0_OR_GREATER
			return m.CreateDelegate<CrystalJsonTypeBinder>();
#else
			return (CrystalJsonTypeBinder) m.CreateDelegate(typeof(CrystalJsonTypeBinder)); // the generic CreateDelegate<T>() overload is .NET 5+
#endif
		}

		private static object? BindImmutableDictionary_StringKey<TValue>(JsonValue? value, Type type, ICrystalJsonTypeResolver resolver)
		{
			if (value.IsNullOrMissing()) return null;
			if (value is not JsonObject obj) throw FailCannotDeserializeNotJsonObject(type);

			var converter = GetConverterFor<TValue>(resolver);

			var instance = ImmutableDictionary.CreateBuilder<string, TValue?>(obj.Comparer);
			foreach (var item in obj)
			{
				instance.Add(item.Key, converter.Unpack(item.Value, resolver));
			}
			return instance.ToImmutable();
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		private static CrystalJsonTypeBinder CreateBinderForImmutableDictionary_Int32Key(Type valueType)
		{
			var m = typeof(CrystalJsonTypeResolver)
			        .GetMethod(nameof(BindImmutableDictionary_Int32Key), BindingFlags.Static | BindingFlags.NonPublic)!
			        .MakeGenericMethod(valueType);

#if NET5_0_OR_GREATER
			return m.CreateDelegate<CrystalJsonTypeBinder>();
#else
			return (CrystalJsonTypeBinder) m.CreateDelegate(typeof(CrystalJsonTypeBinder)); // the generic CreateDelegate<T>() overload is .NET 5+
#endif
		}

		private static object? BindImmutableDictionary_Int32Key<TValue>(JsonValue? value, Type type, ICrystalJsonTypeResolver resolver)
		{
			if (value.IsNullOrMissing()) return null;
			if (value is not JsonObject obj) throw FailCannotDeserializeNotJsonObject(type);

			var converter = GetConverterFor<TValue>(resolver);

			var instance = ImmutableDictionary.CreateBuilder<int, TValue?>();
			foreach (var item in obj)
			{
				instance.Add(StringConverters.ToInt32(item.Key, 0), converter.Unpack(item.Value, resolver));
			}
			return instance.ToImmutable();
		}

		private static CrystalJsonTypeBinder? CreateBinderForDictionary(Type type, Func<object> generator)
		{
			// get the type of keys and values
			var dicType = type.FindGenericType(typeof(IDictionary<,>));
			if (dicType == null) return null; // we only support IDictionary<K, V>

			var typeArgs = dicType.GetGenericArguments();
			var keyType = typeArgs[0];
			var valueType = typeArgs[1];

			if (keyType == typeof(string))
			{ // fastest path when keys are strings
				return CreateBinderForDictionary_StringKey(generator, valueType);
			}
			if (keyType == typeof(int))
			{ // fastest path when keys are ints
				return CreateBinderForDictionary_Int32Key(generator, valueType);
			}

			if (keyType == typeof(uint)
			 || keyType == typeof(long)
			 || keyType == typeof(ulong)
			 || keyType == typeof(Guid)
			 || keyType == typeof(Uuid64)
			 || keyType == typeof(Uuid96)
			 || keyType == typeof(Uuid80)
			 || keyType == typeof(Uuid128)
			//TODO: more?
			)
			{ // fast path for basic types
				var convert = TypeConverters.CreateBoxedConverter<string>(keyType);
				Contract.Debug.Assert(convert != null);
				return CreateBinderForDictionary_BoxedKey(generator, keyType, valueType, convert!);
			}

			// unsupported key type
			//REVIEW: throw an exception instead?
			return null;
		}

		private static CrystalJsonTypeBinder CreateBinderForDictionary_StringKey(Func<object> generator, Type valueType)
		{
			return (v, t, r) =>
			{
				if (v == null || v.IsNull) return null;
				if (v is not JsonObject obj)
				{
					if (v is JsonArray arr)
					{ // tolerate the legacy DCJS wire shape
						var items = (IDictionary) generator();
						FillDictionaryFromLegacyPairs(items, arr, typeof(string), GetConverterFor(valueType, r), r);
						return items;
					}
					throw FailCannotDeserializeNotJsonObject(t);
				}

				var converter = GetConverterFor(valueType, r);

				var instance = (IDictionary) generator();
				foreach (var item in obj)
				{
					instance.Add(item.Key, converter.BindJsonValue(item.Value, r));
				}

				return instance;
			};
		}

		private static CrystalJsonTypeBinder CreateBinderForDictionary_Int32Key(Func<object> generator, Type valueType)
		{
			return (v, t, r) =>
			{
				if (v == null || v.IsNull) return null;
				if (v is not JsonObject obj)
				{
					if (v is JsonArray arr)
					{ // tolerate the legacy DCJS wire shape
						var items = (IDictionary) generator();
						FillDictionaryFromLegacyPairs(items, arr, typeof(int), GetConverterFor(valueType, r), r);
						return items;
					}
					throw FailCannotDeserializeNotJsonObject(t);
				}

				var converter = GetConverterFor(valueType, r);

				var instance = (IDictionary) generator();
				foreach (var item in obj)
				{
					instance.Add(StringConverters.ToInt32(item.Key, 0), converter.BindJsonValue(item.Value, r));
				}

				return instance;
			};
		}

		private static CrystalJsonTypeBinder CreateBinderForDictionary_BoxedKey(Func<object> generator, Type keyType, Type valueType, Func<string, object> convert)
		{
			return (v, t, r) =>
			{
				if (v is not JsonObject obj)
				{
					if (v is JsonArray arr)
					{ // tolerate the legacy DCJS wire shape
						var items = (IDictionary) generator();
						FillDictionaryFromLegacyPairs(items, arr, keyType, GetConverterFor(valueType, r), r);
						return items;
					}
					return v == null || v.IsNull ? null : throw FailCannotDeserializeNotJsonObject(t);
				}

				var converter = GetConverterFor(valueType, r);

				var instance = (IDictionary) generator();
				foreach (var item in obj)
				{
					instance.Add(convert(item.Key), converter.BindJsonValue(item.Value, r));
				}

				return instance;
			};
		}

		/// <summary>Fills a dictionary from the legacy DataContractJsonSerializer wire shape (<c>[ { "Key": ..., "Value": ... }, ... ]</c>)</summary>
		/// <remarks>
		/// <para>Only reached when the input is a JSON array, which the object-map binder otherwise rejects, so the regular path pays no cost.</para>
		/// <para>Strict by design: every element must be an object with exactly the two members <c>Key</c> and <c>Value</c> (exact casing); anything else fails the whole bind.</para>
		/// </remarks>
		private static void FillDictionaryFromLegacyPairs(IDictionary instance, JsonArray array, Type keyType, IJsonConverter valueConverter, ICrystalJsonTypeResolver resolver)
		{
			for (int i = 0; i < array.Count; i++)
			{
				if (array[i] is not JsonObject kv || kv.Count != 2 || !kv.TryGetValue("Key", out var key) || !kv.TryGetValue("Value", out var value))
				{
					throw new JsonBindingException($"Cannot bind element #{i} of a legacy key/value-pair array to a dictionary entry: expected an object with exactly the two members 'Key' and 'Value'.", JsonPath.Create(i), array[i], instance.GetType());
				}
				instance.Add(key.Bind(keyType, resolver)!, valueConverter.BindJsonValue(value, resolver));
			}
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailCannotDeserializeNotJsonObject(Type t) => new($"Cannot deserialize {t.GetFriendlyName()} type because input value is not a JsonObject");

		private static CrystalJsonTypeBinder? CreateBinderForKeyValuePair(Type type)
		{
			var args = type.GetGenericArguments();
			if (args.Length != 2)
			{
				return null; // we only support KeyValuePair<K, V>
			}

			var keyType = args[0];
			var valueType = args[1];

			var prmKey = Expression.Parameter(typeof(object), "key");
			var prmValue = Expression.Parameter(typeof(object), "value");
			var empty = type.GetDefaultValue();

			var ctor = type.GetConstructor(args);
			Contract.Debug.Assert(ctor != null);

			// (object key, object value) => (object) new KeyValuePair<K, V>((K)key, (V)value)
			var expr = Expression
				.New(ctor, prmKey.CastFromObject(keyType), prmValue.CastFromObject(valueType))
				.BoxToObject();

			var lambda = Expression.Lambda<Func<object?, object?, object>>(expr, $"<>_KV_{keyType.Name}_{valueType.Name}_Unpack", true, [ prmKey, prmValue ]).Compile();
			return CreateBinderForKeyValuePair_NoScope(keyType, valueType, empty, lambda);
		}

		private static CrystalJsonTypeBinder CreateBinderForKeyValuePair_NoScope(
			Type keyType,
			Type valueType,
			object? empty,
			Func<object?, object?, object> converter
		)
		{
			var keyConverter = GetConverterFor(keyType);
			var valueConverter = GetConverterFor(valueType);

			return (v, t, r) =>
			{
				if (v is null or JsonNull) return empty;
				Contract.Debug.Assert(v is JsonArray);

				var arr = (JsonArray) v;
				if (arr.Count == 0) return empty;
				if (arr.Count != 2) throw FailCannotDeserializeNotJsonArrayPair(t);

				return converter(
					keyConverter.BindJsonValue(arr[0], r),
					valueConverter.BindJsonValue(arr[1], r)
				);
			};
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailCannotDeserializeNotJsonArrayPair(Type t) => new($"Cannot deserialize {t.GetFriendlyName()} type because input value is not a JsonArray with 2 elements");

		private static CrystalJsonTypeBinder CreateBinderForValueTuple(Type type)
		{
			// we want to generate: (value, ..., resolver) => (object) ValueTuple.Create(..., array[i].As<Ti>(resolver), ...)

			var prmValue = Expression.Parameter(typeof(JsonValue), "value");
			var prmType = Expression.Parameter(typeof(Type), "type");
			var prmResolver = Expression.Parameter(typeof(ICrystalJsonTypeResolver), "resolver");

			Expression expr;

			var args = type.GetGenericArguments();
			if (args.Length > 0)
			{
				// JsonValue JsonValue[int index]
				var arrayIndexer = typeof(JsonValue).GetProperty("Item", typeof(JsonValue), [ typeof(int) ]);

				// JsonValueExtensions.As<T>(JsonValue, TValue?, ICrystalJsonTypeResolver)
#if NET5_0_OR_GREATER
				var asMethod = typeof(JsonValueExtensions).GetMethod(
					nameof(JsonValueExtensions.As),
					BindingFlags.Static | BindingFlags.Public,
					null,
					[ typeof(JsonValue), Type.MakeGenericMethodParameter(0), typeof(ICrystalJsonTypeResolver) ],
					null);
#else
				// Type.MakeGenericMethodParameter is .NET 5+: scan the overloads manually (one-time cost, result is cached by the caller)
				MethodInfo? asMethod = null;
				foreach (var candidate in typeof(JsonValueExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public))
				{
					if (candidate.Name != nameof(JsonValueExtensions.As) || !candidate.IsGenericMethodDefinition) continue;
					var prms = candidate.GetParameters();
					if (prms.Length == 3 && prms[0].ParameterType == typeof(JsonValue) && prms[1].ParameterType.IsGenericParameter && prms[2].ParameterType == typeof(ICrystalJsonTypeResolver))
					{
						asMethod = candidate;
						break;
					}
				}
#endif
				Contract.Debug.Assert(asMethod != null, $"Could not find the {nameof(JsonValueExtensions)}.{nameof(JsonValueExtensions.As)}As<...>(...) extension method!");

				var items = new Expression[args.Length];
				for (int i = 0; i < items.Length; i++)
				{
					// value[i].As<Ti>(resolver)
					items[i] = Expression.Call(
						asMethod.MakeGenericMethod(args[i]),
						Expression.MakeIndex(prmValue, arrayIndexer, [ Expression.Constant(i) ]),
						Expression.Default(args[i]), prmResolver
					);
				}

				// ValueTuple<T...> ValueTuple.Create<T...>(..., ..., ...)
				var tupleMethod = typeof(ValueTuple)
					.GetMethods(BindingFlags.Static | BindingFlags.Public)
					.SingleOrDefault(m => m.Name == nameof(ValueTuple.Create) && m.GetGenericArguments().Length == args.Length);
				Contract.Debug.Assert(tupleMethod != null, "Could not find a compatible ValueTuple.Create(...) method for this type!");

				expr = Expression
					.Call(tupleMethod.MakeGenericMethod(args), items)
					.BoxToObject();
			}
			else
			{
				expr = Expression.Constant(ValueTuple.Create()).BoxToObject();
			}

			return Expression.Lambda<CrystalJsonTypeBinder>(expr, "<>_VT_" + args.Length + "_" + type.Name + "_Unpack", true, new[] { prmValue, prmType, prmResolver }).Compile();
		}

		public static bool IsSealedType(Type type)
		{
			// We considered a type to be "sealed" if it is impossible for any instance assigned to a parameter of this type to be of a different type.

			// Types considered "sealed":
			// - primitive types:
			//   - int, bool, DateTime, ...
			// - structs:
			//   - public struct Foo { }
			//   - public record struct Foo { }
			// - sealed classes or records:
			//   - public sealed class Foo { }
			//   - public sealed record Foo { }

			// Types considered "not sealed":
			// - interfaces:
			//   - IEnumerable<string>, ...
			//   - public interface IFoo { }
			// - abstract classes or records:
			//   - public abstract class FooBase { }
			//   - public abstract record FooBase { }
			// - non-sealed clases or records:
			//   - public class Foo { }
			//   - public record Foo { }

			if (type.IsValueType)
			{
				return true;
			}

			if (type.IsInterface || type.IsAbstract)
			{
				return false;
			}

			return type.IsSealed;
		}

		public static bool IsNullableType(Type type)
		{
			if (!type.IsValueType || !type.IsNullableType())
			{
				return false;
			}
			return true;
		}

		public static Type? GetNullableType(Type type)
		{
			if (!type.IsValueType || !type.IsGenericType || !type.IsNullableType())
			{
				return null;
			}

			return type.GenericTypeArguments[0];
		}

	}

}
