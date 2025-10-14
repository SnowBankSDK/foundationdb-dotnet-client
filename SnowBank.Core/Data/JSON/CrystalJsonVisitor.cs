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

// ReSharper disable UnusedParameter.Local
#pragma warning disable IDE0060
#pragma warning disable IL2067 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.
#pragma warning disable IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.

namespace SnowBank.Data.Json
{
	using System.Collections;
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Runtime.InteropServices;
	using System.Xml;
	using SnowBank.Data.Tuples;
	using SnowBank.Collections.Caching;
	using SnowBank.Runtime;

	/// <summary>Internal helper to convert values into JSON strings</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class CrystalJsonVisitor
	{

		public static class CustomVisitor<T>
		{
			/// <summary>Get or create a dedicated serializer for type <typeparamref name="T"/></summary>
			public static CrystalJsonTypeVisitor? Handler
			{
				get => s_typeVisitorCache[typeof(T)];
				set
				{
					if (value != null)
					{
						s_typeVisitorCache.SetItem(typeof(T), value);
					}
					else
					{
						s_typeVisitorCache.Remove(typeof(T));
					}
				}
			}
		}

		#region Type Writers ...

		private static readonly QuasiImmutableCache<Type, CrystalJsonTypeVisitor> s_typeVisitorCache = new(TypeEqualityComparer.Default);

		private static readonly Func<Type, bool, CrystalJsonTypeVisitor> CreateVisitorForTypeCallback = CreateVisitorForType;

		/// <summary>Return a visitor that is able to serialize instances of the specified type</summary>
		/// <param name="type">Type as declared in the parent (compile time) or actual instance type (at runtime)</param>
		/// <param name="atRuntime"><see langword="false"/> when performing the initial mapping (compile time), <see langword="true"/> when calling at runtime with the actual instance type</param>
		public static CrystalJsonTypeVisitor GetVisitorForType(Type type, bool atRuntime = false)
		{
			bool cacheable = !atRuntime || type.IsConcrete();

			if (cacheable)
			{ // we can cache the visitor for this type
			  //TODO: handle a different cache between atRuntime == true/false ?
			  //note: currently, the only ones that call with atRuntime==true are VisitObjectAtRuntime and VisitInterfaceAtRuntime
				return s_typeVisitorCache.GetOrAdd(type, CreateVisitorForTypeCallback, atRuntime);
			}

			// Cannot be cached, will be created everytime... :/
			return CreateVisitorForType(type, true);
		}

		/// <summary>Create a new visitor for the specified type</summary>
		/// <param name="type">Type as declared in the parent (compile time) or actual instance type (at runtime)</param>
		/// <param name="atRuntime"><see langword="false"/> when performing the initial mapping (compile time), <see langword="true"/> when calling at runtime with the actual instance type</param>
		/// <returns>Delegate that can convert instances of this type into JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.Interfaces)] Type type, bool atRuntime)
		{
			//	Type		Primitive	ValueType	Class	Enum	Interface
			// string		   _           _          X       _
			// int32, bool     X           X          _       _
			// decimal         _           X          _       _
			// enums           _           X          _       X
			// DateTime        _           X          _       _
			// TimeSpan        _           X          _       _
			// structs         _           X          _       _
			// classes         _           _          X       _
			// T[]             _           _          X       _		IEnumerable<T>
			// Dictionary<K,V> _           _          X       _		ICollection<KeyValuePair<K,V>>
			// dynamic         _           _          X       _		IDictionary<string, object>
			// interface	   _           _          ?       _

			if (typeof(string) == type)
			{
				return VisitStringInternal;
			}

			if (type.IsPrimitive)
			{ // Only for base types of the CLR (bool, int, double, char, ...)
				return CreateVisitorForPrimitiveType(type);
			}

			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
			{ // This is a Nullable<T> types, we will use a dedicated wrapper
				return CreateVisitorForNullableType(type, nullableType);
			}

			if (typeof(object) == type || type.IsAbstract)
			{
				// If a class/struct declares a member with type "object" or an interface, we will need to perform a lookup AT RUNTIME with the actuel instance type !
				// ie: the first visitor will call value.GetType() everytime, and will have to look up the specialized visitor that will be used for this specific instance
				if (atRuntime)
				{ // type == typeof(object), this is an empty singleton object
					return static (_, _, _, writer) => { writer.WriteEmptyObject(); };
				}

				return VisitObjectAtRuntime;
			}

			// If the type has a static "JsonSerialize(...)", we defer to it (aka "duck typing")
			var visitor = TryGetSerializeMethodVisitor(type);
			if (visitor != null)
			{
				return visitor;
			}

			// If the type implements IJsonSerializable, we defer to it
			if (type.IsAssignableTo<IJsonSerializable>())
			{
				return VisitJsonSerializable;
			}

			// If the type has a static "JsonPack(...)", we can also use it (though it is less performant because it will first create a JsonValue that then will be serialized)
			visitor = TryGetBindableMethodVisitor(type);
			if (visitor != null)
			{
				return visitor;
			}

			// If the type implements IJsonPackable, we can also use it (though it is less performant because it will first create a JsonValue that then will be serialized)
			if (type.IsAssignableTo<IJsonPackable>())
			{
				return VisitJsonPackable;
			}

			if (type.IsValueType)
			{ // Structs, DateTime, Enums, decimal, ...
				return CreateVisitorForValueType(type);
			}

			// Reference Types (string, classes, ...)

			if (type.IsAssignableTo<System.Xml.XmlNode>())
			{ // XML node
				return static (v, _, _, writer) => writer.WriteValue(((System.Xml.XmlNode?) v)?.OuterXml);
			}

			if (type.IsAssignableTo<System.Collections.IEnumerable>())
			{ // non-generic collection
				return CreateVisitorForEnumerableType(type);
			}

			if (type.IsInterface)
			{ // for interfaces, we need to perform a lookup at runtime for each instance
				if (atRuntime)
				{ //BUGBUG: this should not be possible for concrete instances!
					return static (_, _, _, writer) => writer.WriteEmptyObject();
				}

				return VisitInterfaceAtRuntime;
			}

			#region Common Class Types...

			if (typeof(System.Text.StringBuilder) == type)
			{
				return static (v, _, _, writer) => writer.WriteValue(v as System.Text.StringBuilder);
			}

			if (typeof(System.Net.IPAddress) == type)
			{
				return static (v, _, _, writer) => writer.WriteValue(v as System.Net.IPAddress);
			}

			if (typeof(System.Version) == type)
			{
				return static (v, _, _, writer) => writer.WriteValue(v as System.Version);
			}

			if (typeof(System.Uri) == type)
			{
				return static (v, _, _, writer) => writer.WriteValue(v as System.Uri);
			}

			// There are multiple derived types for DateTimeZone, we have to call IsAssignableFrom(...) instead of comparing the type
			if (typeof(NodaTime.DateTimeZone).IsAssignableFrom(type))
			{
				return static (v, _, _, writer) => writer.WriteValue(v as NodaTime.DateTimeZone);
			}

			if (typeof(Type).IsAssignableFrom(type))
			{
				return static (v, _, _, writer) => writer.WriteValue(((JsonString) JsonString.Return((Type) v!)).Value);
			}

			if (typeof(Task).IsAssignableFrom(type))
			{ // we don't allow serializing Task and Task<T>
				return CreateVisitorForTaskLikeType(type);
			}

			#endregion

			// class ?
			return VisitCustomClassOrStruct;
		}

		private static void VisitStringInternal(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			writer.WriteValue(value as string);
		}

		private static CrystalJsonTypeVisitor? TryGetSerializeMethodVisitor([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] Type type)
		{
			//Duck Typing: we recognize the following patterns
			// - static JsonSerialize(T, CrystalJsonWriter) => JsonValue (or derived type)
			// - instance.JsonSerialize(CrystalJsonWriter) => JsonValue (or derived type)

			// First look for a static method
			var staticMethod = type.GetMethod("JsonSerialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
			if (staticMethod != null)
			{ // create a writer that will call Type.JsonSerialize(instance, ...)
				return CreateVisitorForStaticSerializableMethod(type, staticMethod);
			}

			// Then look for an instance method
			var instanceMethod = type.GetMethod("JsonSerialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
			if (instanceMethod != null)
			{ // create a writer that will call instance.JsonSerialize(...)
				return CreateVisitorForInstanceSerializableMethod(type, instanceMethod);
			}

			return null;
		}

		private static CrystalJsonTypeVisitor? TryGetBindableMethodVisitor([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] Type type)
		{
			//Duck Typing: we recognize the following patterns
			// - static JsonPack(T instance, ...) => JsonValue (ou derived type)
			// - instance.JsonPack(...) => JsonValue (ou derived type)

			// First look for a static method
			var staticMethod = type.GetMethod("JsonPack", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
			if (staticMethod != null)
			{ // create a writer that will call Type.JsonPack(instance, ...)
				return CreateVisitorForStaticBindableMethod(type, staticMethod);
			}
			// Then look for an instance method
			var instanceMethod = type.GetMethod("JsonPack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
			if (instanceMethod != null)
			{ // create a writer that will call instance.JsonPack(...)
				return CreateVisitorForInstanceBindableMethod(type, instanceMethod);
			}

			return null;
		}

		/// <summary>Create a visitor for a BCL primitive type (int, bool, char, ...)</summary>
		/// <param name="type">Primitive type (Int32, Boolean, Single, ...)</param>
		/// <returns>Delegate that can serialize values of this type into JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForPrimitiveType(Type type)
		{
			if (typeof (bool) == type)
			{ // Boolean
				return (v, _, _, writer) => writer.WriteValue((bool) v!);
			}
			if (typeof(byte) == type)
			{ // UInt8
				return (v, _, _, writer) => writer.WriteValue((int) (byte) v!);
			}
			if (typeof(sbyte) == type)
			{ // Int8
				return (v, _, _, writer) => writer.WriteValue((sbyte) v!);
			}
			if (typeof (short) == type)
			{ // Int16
				return (v, _, _, writer) => writer.WriteValue((short) v!);
			}
			if (typeof (ushort) == type)
			{ // UInt16
				return (v, _, _, writer) => writer.WriteValue((uint) (ushort) v!);
			}
			if (typeof (int) == type)
			{ // Int32
				return (v, _, _, writer) => writer.WriteValue((int) v!);
			}
			if (typeof (uint) == type)
			{ // Int32
				return (v, _, _, writer) => writer.WriteValue((uint) v!);
			}
			if (typeof (long) == type)
			{ // Int64
				return (v, _, _, writer) => writer.WriteValue((long) v!);
			}
			if (typeof (ulong) == type)
			{ // Int64
				return (v, _, _, writer) => writer.WriteValue((ulong) v!);
			}
			if (typeof (float) == type)
			{ // Single
				return (v, _, _, writer) => writer.WriteValue((float) v!);
			}
			if (typeof (double) == type)
			{ // Double
				return (v, _, _, writer) => writer.WriteValue((double) v!);
			}
			if (typeof (char) == type)
			{ // char
				return (v, _, _, writer) => writer.WriteValue((char) v!);
			}

			if (type.IsAssignableTo<IConvertible>())
			{ // the type can convert itself into a string
				return (v, _, _, writer) => writer.WriteRaw(((IConvertible) v!).ToString(CultureInfo.InvariantCulture));
			}

			// fallback for less common types that don't have a dedicated writer
			return (v, _, _, writer) => writer.WriteRaw(v!.ToString()!);
		}

		/// <summary>Create a visitor for <c>Nullable&lt;T&gt;</c> (int?, bool?, TimeSpan?, Enum?, ...)</summary>
		/// <returns>Delegate that can serialize values of this type into JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForNullableType(Type type, Type? realType)
		{
			realType ??= Nullable.GetUnderlyingType(type) ?? throw new InvalidOperationException("Could not get underlying type for " + type.GetFriendlyName());
			var visitor = GetVisitorForType(realType, atRuntime: false);
			if (visitor == null) throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeNullableType(type);

			return (v, _, r, writer) =>
			{
				if (v == null)
					writer.WriteNull(); // "null"
				else
					visitor(v, realType, r, writer);
			};
		}

		/// <summary>Create a visitor for <c>KeyValuePair&lt;TKey, TValue&gt;</c></summary>
		private static CrystalJsonTypeVisitor CreateVisitorForKeyValuePairType(Type type)
		{
			var args = type.GetGenericArguments();
			if (args.Length != 2) throw new InvalidOperationException("Type must be a KeyValuePair<K,V>");

			// depending on the type of the key, we have specialized implementation that are more optimized
			MethodInfo method;
			if (args[0] == typeof(string))
			{
				if (args[1] == typeof(string))
				{ // string keys and values
					method = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitStringKeyStringValuePair), BindingFlags.Static | BindingFlags.NonPublic)!;
					if (method == null) throw new InvalidOperationException("Missing method CrystalJsonVisitor.VisitStringKeyStringValuePair()");
				}
				else
				{ // optimized implementation for KV<string, T>
					var gen = typeof (CrystalJsonVisitor).GetMethod(nameof(VisitStringKeyValuePair), BindingFlags.Static | BindingFlags.NonPublic);
					if (gen== null) throw new InvalidOperationException("Missing method CrystalJsonVisitor.VisitStringKeyValuePair<V>()");
					method  = gen.MakeGenericMethod(args[1]);
				}
			}
			else if (args[0] == typeof(int))
			{ // optimized implementation for KV<int, T>
				var gen = typeof (CrystalJsonVisitor).GetMethod(nameof(VisitInt32KeyValuePair), BindingFlags.Static | BindingFlags.NonPublic);
				if (gen== null) throw new InvalidOperationException("Missing method CrystalJsonVisitor.VisitInt32KeyValuePair<K,V>()");
				method  = gen.MakeGenericMethod(args[1]);
			}
			else
			{ // generic implementation for KV<TK, TV>
				var gen = typeof (CrystalJsonVisitor).GetMethod(nameof(VisitKeyValuePair), BindingFlags.Static | BindingFlags.NonPublic);
				if (gen== null) throw new InvalidOperationException("Missing method CrystalJsonVisitor.VisitKeyValuePair<K,V>()");
				method  = gen.MakeGenericMethod(args);
			}
			return (CrystalJsonTypeVisitor) method.CreateDelegate(typeof (CrystalJsonTypeVisitor));
		}

		[UsedImplicitly]
		private static void VisitInt32KeyValuePair<TValue>(object? value, Type _, Type? __, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			// '[ KEY, VALUE ]'
			var kvp = (KeyValuePair<int, TValue>) value;
			writer.WriteInlinePair<TValue>(kvp.Key, kvp.Value);
		}

		[UsedImplicitly]
		private static void VisitStringKeyStringValuePair(object? value, Type _, Type? __, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			// '[ KEY, VALUE ]'
			var kvp = (KeyValuePair<string, string>) value;
			writer.WriteInlinePair(kvp.Key, kvp.Value);
		}

		[UsedImplicitly]
		private static void VisitStringKeyValuePair<TValue>(object? value, Type _, Type? __, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			// '[ KEY, VALUE ]'
			var kvp = (KeyValuePair<string, TValue>) value;
			writer.WriteInlinePair<TValue>(kvp.Key, kvp.Value);
		}

		[UsedImplicitly]
		private static void VisitKeyValuePair<TKey, TValue>(object? value, Type _, Type? __, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			// '[ KEY, VALUE ]'
			var kvp = (KeyValuePair<TKey, TValue>)value;
			var state = writer.BeginArray();
			{
				writer.WriteHeadSeparator();
				VisitValue<TKey>(kvp.Key, writer);
				writer.WriteTailSeparator();
				VisitValue<TValue>(kvp.Value, writer);
			}
			writer.EndArray(state);
		}

		private static CrystalJsonTypeVisitor CreateVisitorForArraySegmentType(Type type)
		{
			var args = type.GetGenericArguments();
			if (args.Length != 1) throw new InvalidOperationException("Type must be an ArraySegment<T>");

			if (args[0] == typeof(byte))
			{ // byte => Base64
				return (v, _, _, w) => w.WriteBuffer(((ArraySegment<byte>) v!).AsSlice());
			}
			else
			{ // T => array
				var method = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitArraySegment), BindingFlags.Static | BindingFlags.NonPublic);
				if (method == null) throw new InvalidOperationException("Missing method CrystalJsonVisitor.VisitArraySegment<K,V>()");

				// create the concrete version of this method, and convert it into a delegate
				var gen = method.MakeGenericMethod(args);
				return (CrystalJsonTypeVisitor) gen.CreateDelegate(typeof(CrystalJsonTypeVisitor));
			}
		}

		[UsedImplicitly]
		private static void VisitArraySegment<T>(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			var array = value != null ? (ArraySegment<T>) value : default;
			if (array.Count == 0)
			{
				if (array.Array == null)
				{
					writer.WriteNull();
				}
				else
				{
					writer.WriteEmptyArray();
				}
				return;
			}

			// '[ KEY, VALUE ]'
			var state = writer.BeginArray();
			{
				for (int i = 0; i < array.Count; i++)
				{
					writer.WriteFieldSeparator();
					VisitValue<T>(array.Array![array.Offset + i], writer);
				}
			}
			writer.EndArray(state);
		}

		/// <summary>Create a visitor for common value types (Datetime, TimeSpan, Guid, struct, enum, ...)</summary>
		private static CrystalJsonTypeVisitor CreateVisitorForValueType(Type type)
		{
			if (type == typeof (DateTime))
			{ // DateTime
				return static (v, _, _, writer) => writer.WriteValue((DateTime) v!);
			}

			if (type == typeof (TimeSpan))
			{ // TimeSpan
				return static (v, _, _, writer) => writer.WriteValue((TimeSpan) v!);
			}

			if (type == typeof (Guid))
			{ // Guid
				return static (v, _, _, writer) => writer.WriteValue((Guid) v!);
			}

			if (type == typeof (DateTimeOffset))
			{ // DateTime
				return static (v, _, _, writer) => writer.WriteValue((DateTimeOffset) v!);
			}

			if (type == typeof (decimal))
			{ // decimal
				return static (v, _, _, writer) => writer.WriteValue((decimal) v!);
			}

			if (type == typeof(Uuid128))
			{ // 128-bit UUID
				return static (v, _, _, writer) => writer.WriteValue((Uuid128) v!);
			}

			if (type == typeof(Uuid96))
			{ // 96-bit UUID
				return static (v, _, _, writer) => writer.WriteValue((Uuid96) v!);
			}

			if (type == typeof(Uuid80))
			{ // 80-bit UUID
				return static (v, _, _, writer) => writer.WriteValue((Uuid80) v!);
			}

			if (type == typeof(Uuid64))
			{ // 64-bit UUID
				return static (v, _, _, writer) => writer.WriteValue((Uuid64) v!);
			}

			if (type == typeof (DateOnly))
			{ // DateOnly
				return static (v, _, _, writer) => writer.WriteValue((DateOnly) v!);
			}

			if (type == typeof (TimeOnly))
			{ // TimeOnly
				return static (v, _, _, writer) => writer.WriteValue((TimeOnly) v!);
			}

			if (type.IsAssignableTo<IVarTuple>())
			{ // Variable-Length Tuple (struct)
				return CreateVisitorForSTupleType(type);
			}

			if (type.IsAssignableTo<ITuple>())
			{ // ValueTuple, Tuple, ...
				return CreateVisitorForITupleType(type);
			}

			if (type.IsEnum)
			{ // Enum
				return static (v, _, _, writer) => writer.WriteEnum((Enum) v!);
			}

			if (type == typeof(ValueTask))
			{
				return CreateVisitorForTaskLikeType(type);
			}

			if (type.IsGenericType)
			{
				if (type.IsGenericInstanceOf(typeof (KeyValuePair<,>)))
				{
					return CreateVisitorForKeyValuePairType(type);
				}
				if (type.IsGenericInstanceOf(typeof(ArraySegment<>)))
				{
					return CreateVisitorForArraySegmentType(type);
				}
				if (type.IsGenericInstanceOf(typeof(ValueTask<>)))
				{
					return CreateVisitorForTaskLikeType(type);
				}
			}

			if (type == typeof(Slice))
			{
				return static (v, _, _, writer) => writer.WriteBuffer((Slice) v!);
			}

			if (type == typeof (System.Drawing.Color))
			{ // Color => we will output the "Name" property of the color
#if !NET461 && !NET472
				//TODO: HACKHACK: how to convert color into HTML name in .NET Standard 2.0 ?
				return static (v, _, _, writer) => writer.WriteValue(((System.Drawing.Color) v!).ToString());
#else
				return static (v, _, _, writer) => writer.WriteValue(System.Drawing.ColorTranslator.ToHtml((System.Drawing.Color) v));
#endif
			}

			#region NodaTime...

			if (type == typeof (NodaTime.Instant))
			{
				return static (v, _, _, writer) => writer.WriteValue((NodaTime.Instant) v!);
			}
			if (type == typeof (NodaTime.Duration))
			{
				return static (v, _, _, writer) => writer.WriteValue((NodaTime.Duration) v!);
			}
			if (type == typeof (NodaTime.LocalDateTime))
			{
				return static (v, _, _, writer) => writer.WriteValue((NodaTime.LocalDateTime) v!);
			}
			if (type == typeof (NodaTime.ZonedDateTime))
			{
				return static (v, _, _, writer) => writer.WriteValue((NodaTime.ZonedDateTime) v!);
			}
			if (type == typeof (NodaTime.Offset))
			{
				return static (v, _, _, writer) => writer.WriteValue((NodaTime.Offset) v!);
			}
			if (type == typeof (NodaTime.OffsetDateTime))
			{
				return static (v, _, _, writer) => writer.WriteValue((NodaTime.OffsetDateTime) v!);
			}
			if (type == typeof (NodaTime.LocalDate))
			{
				return static (v, _, _, writer) => writer.WriteValue((NodaTime.LocalDate) v!);
			}
			if (type == typeof (NodaTime.LocalTime))
			{
				return static (v, _, _, writer) => writer.WriteValue((NodaTime.LocalTime) v!);
			}

			#endregion

			// struct ?
			return VisitCustomClassOrStruct;
		}

		private static CrystalJsonTypeVisitor CreateVisitorForTaskLikeType(Type type)
		{
			return (_, dt, rt, _) => throw JsonSerializationException.CannotPackTaskLikeType(rt ?? type);
		}

		/// <summary>Create a visitor for enumerable types (arrays, lists, dictionaries, sets, ...)</summary>
		/// <param name="type">Type that implements IEnumerable</param>
		private static CrystalJsonTypeVisitor CreateVisitorForEnumerableType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
		{
			if (type.IsAssignableTo<ICollection<KeyValuePair<string, string?>>>())
			{ // Key/Value store
				return (v, _, _, writer) => VisitStringDictionary(v as ICollection<KeyValuePair<string, string?>>, writer);
			}

			if (type.IsAssignableTo<ICollection<KeyValuePair<string, object?>>>())
			{ // Key/Value store / ExpandoObject
				return (v, _, _, writer) => VisitGenericObjectDictionary(v as ICollection<KeyValuePair<string, object?>>, writer);
			}

			if (type.IsAssignableTo<IVarTuple>())
			{ // Tuple (non struct)
				return CreateVisitorForSTupleType(type);
			}

			if (type.IsAssignableTo<IDictionary>())
			{ // Key/Value store

				if (type.IsGenericType)
				{ // Generic dictionary, we can optimize Dictionary<string, T> with a specialized visitor
					var args = type.GetGenericArguments();
					if (args.Length == 2)
					{  // Dictionary<string, T>
						var arg0 = args[0];
						var arg1 = args[1];
						if (arg0 == typeof(string))
						{
							return arg1 == typeof(string) ? CreateIDictionaryVisitor_StringKeyAndValue() : CreateIDictionaryVisitor_StringKey(arg1);
						}
						if (arg0 == typeof(int))
						{
							return arg1 == typeof(string) ? CreateIDictionaryVisitor_Int32Key_StringValue() : CreateIDictionaryVisitor_Int32Key(arg1);
						}
						return CreateIDictionaryVisitor(arg0, arg1);
					}
				}
				return CreateIDictionaryVisitor(typeof(object), typeof(object));
			}

			if (type == typeof(byte[]))
			{ // Array of bytes
				return CreateByteBufferVisitor();
			}

			if (type.IsArray)
			{ // generic array: T[]

				if (type == typeof(string[]))
				{
					return VisitStringArrayInternal;
				}
				if (type == typeof(int[]))
				{
					return VisitInt32ArrayInternal;
				}

				var elemType = type.GetElementType() ?? throw new InvalidOperationException("Failed to get array element type for " + type.Name);
				return CompileGenericVisitorMethod(nameof(VisitArrayInternal), nameof(VisitArray), elemType);
			}

			if (type.IsGenericInstanceOf(typeof(List<>), out var listType))
			{
				if (listType == typeof(List<string>))
				{
					return VisitStringListInternal;
				}
				if (listType == typeof(List<int>))
				{
					return VisitInt32ListInternal;
				}

				var elemType = type.GetGenericArguments()[0];
				return CompileGenericVisitorMethod(nameof(VisitListInternal), nameof(VisitList), elemType);
			}

			if (type.IsAssignableTo<IEnumerable<string>>())
			{ // Sequence of strings
				return (v, _, _, writer) => VisitEnumerableOfString((IEnumerable<string>?) v, writer);
			}

			// is it a generic IEnumerable<T> ?
			if (type.IsGenericType)
			{ // We have a specialized visitor for simple collections with a single generic type argument
				var args = type.GetGenericArguments();
				if (args.Length == 1)
				{
					return CreateIEnumerableVisitor(args[0]);
				}
			}

			// Is it a type that implements IEnumerable<T> as part of its interfaces ?
			var interfaces = type.GetInterfaces();
			if (interfaces.Length > 0)
			{
				// the type may implement multiple interfaces, look for one that is IEnumerable<T
				foreach (var candidate in interfaces)
				{
					if (candidate.IsGenericInstanceOf(typeof(IEnumerable<>)))
					{
						var args = candidate.GetGenericArguments();
						if (args.Length == 1)
						{
							return CreateIEnumerableVisitor(args[0]);
						}
					}
				}
			}

			// non generic collection
			return CreateIEnumerableVisitor(typeof(object));
		}

		private static CrystalJsonTypeVisitor CreateIEnumerableVisitor(Type itemType)
		{
			return (v, _, _, writer) => VisitEnumerable(v as IEnumerable, itemType, writer);
		}

		private static CrystalJsonTypeVisitor CreateIDictionaryVisitor(Type keyType, Type valueType)
		{
			return (v, _, _, writer) => VisitDictionary((IDictionary?) v, keyType, valueType, writer);
		}

		private static CrystalJsonTypeVisitor CreateIDictionaryVisitor_StringKeyAndValue()
		{
			return (v, _, _, writer) => VisitDictionary_StringKeyAndValue((IDictionary<string, string?>?) v , writer);
		}

		private static CrystalJsonTypeVisitor CreateIDictionaryVisitor_StringKey(Type valueType)
		{
			var visitor = GetVisitorForType(valueType, atRuntime: false);
			return (v, _, _, writer) => VisitDictionary_StringKey(v as IDictionary, valueType, visitor, writer);
		}
		
		private static CrystalJsonTypeVisitor CreateIDictionaryVisitor_Int32Key_StringValue()
		{
			return (v, _, _, writer) => VisitDictionary_Int32Key_StringValue((IDictionary<int, string?>?) v, writer);
		}
		
		private static CrystalJsonTypeVisitor CreateIDictionaryVisitor_Int32Key(Type valueType)
		{
			var visitor = GetVisitorForType(valueType, atRuntime: false);
			return (v, _, _, writer) => VisitDictionary_Int32Key(v as IDictionary, valueType, visitor, writer);
		}
		
		private static CrystalJsonTypeVisitor CreateByteBufferVisitor()
		{
			return (v, _, _, writer) => writer.WriteBuffer(v as byte[]);
		}

		/// <summary>Create a visitor for a type that follows the JsonSerializable static pattern</summary>
		private static CrystalJsonTypeVisitor CreateVisitorForStaticSerializableMethod(Type type, MethodInfo method)
		{
			Contract.Debug.Requires(type != null && method != null);
			var parameters = method.GetParameters();
			// We accept:
			// - void TValue.JsonSerialize(TValue value, CrystalJsonWriter writer)
			if (parameters.Length != 2) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidSignature(type, method);
			var prmType = parameters[0].ParameterType;
			if (!type.IsAssignableFrom(prmType)) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidFirstParam(type, method, prmType);
			if (parameters[1].ParameterType != typeof(CrystalJsonWriter)) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidSecondParam(type, method, prmType);

			var prmValue = Expression.Parameter(typeof(object), "v");
			var prmDeclaringType = Expression.Parameter(typeof(Type), "dt");
			var prmRuntimeType = Expression.Parameter(typeof(Type), "rt");
			var prmWriter = Expression.Parameter(typeof(CrystalJsonWriter), "w");
			var castedValue = prmValue.CastFromObject(prmType);
			var body = Expression.Call(method, castedValue, prmWriter);
			return Expression.Lambda<CrystalJsonTypeVisitor>(body, true, prmValue, prmDeclaringType, prmRuntimeType, prmWriter).Compile();
		}

		/// <summary>Create a visitor for a type that follows the JsonSerializable instance pattern</summary>
		private static CrystalJsonTypeVisitor CreateVisitorForInstanceSerializableMethod(Type type, MethodInfo method)
		{
#if DEBUG_JSON_CONVERTER
			Debug.WriteLine("CrystalJsonConverter.CreateConverterForStaticSerializable(" + type + ", " + method + ")");
#endif
			var parameters = method.GetParameters();
			// we accept:
			// - (TValue value).JsonSerialize(CrystalJsonWriter writer)
			if (parameters.Length != 1 || parameters[0].ParameterType != typeof (CrystalJsonWriter)) throw new InvalidOperationException($"Instance serialization method must take a single parameter of type '{nameof(CrystalJsonWriter)}'."); //TODO

			var prmValue = Expression.Parameter(typeof(object), "value");
			var prmDeclaringType = Expression.Parameter(typeof(Type), "declaringType");
			var prmRuntimeType = Expression.Parameter(typeof(Type), "runtimeType");
			var prmWriter = Expression.Parameter(typeof(CrystalJsonWriter), "writer");
			var castedValue = prmValue.CastFromObject(type);
			var body = Expression.Call(castedValue, method, prmWriter);
			return Expression.Lambda<CrystalJsonTypeVisitor>(body, [ prmValue, prmDeclaringType, prmRuntimeType, prmWriter ]).Compile();
		}


		/// <summary>Create a visitor for a types that follows the JsonPack static pattern</summary>
		private static CrystalJsonTypeVisitor CreateVisitorForStaticBindableMethod(Type type, MethodInfo method)
		{
			Contract.Debug.Requires(type != null && method != null);
			var parameters = method.GetParameters();
			// we accept:
			// - TValue.JsonPack(TValue value, CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
			if (parameters.Length != 3) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidSignature(type, method);
			var prmType = parameters[0].ParameterType;
			if (!type.IsAssignableFrom(prmType)) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidFirstParam(type, method, prmType);
			if (parameters[1].ParameterType != typeof(CrystalJsonSettings)) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidSecondParam(type, method, prmType);

			var prmWriter = Expression.Parameter(typeof(CrystalJsonWriter), "w");
			var prmValue = Expression.Parameter(typeof(object), "v");
			var prmDeclaringType = Expression.Parameter(typeof(Type), "dt"); //NOT USED?
			var prmRuntimeType = Expression.Parameter(typeof(Type), "rt"); //NOT USED?
			var varSettings = Expression.Property(prmWriter, nameof(CrystalJsonWriter.Settings));
			var varResolver = Expression.Property(prmWriter, nameof(CrystalJsonWriter.Resolver));
			var castedValue = prmValue.CastFromObject(prmType);
			// body = (Type).JsonPack(instance, settings, resolver)
			var body = Expression.Call(method, castedValue, varSettings, varResolver);

			// body = (Type).JsonPack(instance, settings, resolver).JsonSerialize(writer)
			body = Expression.Call(body, nameof(JsonValue.JsonSerialize), null, prmWriter);

			var lambda = Expression.Lambda<CrystalJsonTypeVisitor>(body, true, [ prmValue, prmDeclaringType, prmRuntimeType, prmWriter ]);
			return lambda.Compile();
		}

		/// <summary>Create a visitor for a types that follows the JsonPack instance pattern</summary>
		private static CrystalJsonTypeVisitor CreateVisitorForInstanceBindableMethod(Type type, MethodInfo method)
		{
#if DEBUG_JSON_CONVERTER

			Debug.WriteLine("CrystalJsonConverter.CreateVisitorForInstanceBindableMethod(" + type + ", " + method + ")");
#endif
			Contract.Debug.Requires(type != null && method != null && !method.IsStatic);

			var parameters = method.GetParameters();
			// we accept: (TValue value).JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
			if (parameters.Length != 2) throw CrystalJson.Errors.Serialization_InstanceJsonPackMethodInvalidSignature(type, method);
			if (parameters[0].ParameterType != typeof(CrystalJsonSettings)) throw new InvalidOperationException($"First parameter must be a {nameof(CrystalJsonSettings)}"); //TODO
			if (parameters[1].ParameterType != typeof(ICrystalJsonTypeResolver)) throw new InvalidOperationException($"Second parameter must be a {nameof(ICrystalJsonTypeResolver)}"); //TODO

			var prmWriter = Expression.Parameter(typeof(CrystalJsonWriter), "writer");
			var prmValue = Expression.Parameter(typeof(object), "value");
			var prmDeclaringType = Expression.Parameter(typeof(Type), "declaringType"); //NOT USED?
			var prmRuntimeType = Expression.Parameter(typeof(Type), "runtimeType"); //NOT USED?
			var varSettings = Expression.Property(prmWriter, nameof(CrystalJsonWriter.Settings));
			var varResolver = Expression.Property(prmWriter, nameof(CrystalJsonWriter.Resolver));

			var castedValue = prmValue.CastFromObject(type);
			// body = (instance)?.JsonPack(settings, resolver) ?? JsonNull.Null
			Expression body = Expression.Call(castedValue, method, varSettings, varResolver);

			// body = ((instance)?.JsonPack(settings, resolver) ?? JsonNull.Null).JsonSerialize(writer)

			body = Expression.Condition(
				Expression.Equal(prmValue, Expression.Default(type)),
				Expression.Constant(JsonNull.Missing, typeof(JsonValue)),
				body
			);

			body = Expression.Call(body, nameof(JsonValue.JsonSerialize), null, prmWriter);

			var lambda = Expression.Lambda<CrystalJsonTypeVisitor>(body, [ prmValue, prmDeclaringType, prmRuntimeType, prmWriter ]);
			return lambda.Compile();
		}

		#endregion

		#region Custom Serializers...

		/// <summary>Visit a generic value</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void VisitValue<T>(T? value, CrystalJsonWriter writer)
		{
			#region <JIT_HACK>
#if !DEBUG
			if (default(T) is not null)
			{
				if (typeof(T) == typeof(bool)) { writer.WriteValue((bool) (object) value!); return; }
				if (typeof(T) == typeof(char)) { writer.WriteValue((char) (object) value!); return; }
				if (typeof(T) == typeof(int)) { writer.WriteValue((int) (object) value!); return; }
				if (typeof(T) == typeof(long)) { writer.WriteValue((long) (object) value!); return; }
				if (typeof(T) == typeof(uint)) { writer.WriteValue((uint) (object) value!); return; }
				if (typeof(T) == typeof(ulong)) { writer.WriteValue((ulong) (object) value!); return; }
				if (typeof(T) == typeof(double)) { writer.WriteValue((double) (object) value!); return; }
				if (typeof(T) == typeof(float)) { writer.WriteValue((float) (object) value!); return; }
				if (typeof(T) == typeof(Guid)) { writer.WriteValue((Guid) (object) value!); return; }
				if (typeof(T) == typeof(Uuid128)) { writer.WriteValue((Uuid128) (object) value!); return; }
				if (typeof(T) == typeof(Uuid96)) { writer.WriteValue((Uuid96) (object) value!); return; }
				if (typeof(T) == typeof(Uuid80)) { writer.WriteValue((Uuid80) (object) value!); return; }
				if (typeof(T) == typeof(Uuid64)) { writer.WriteValue((Uuid64) (object) value!); return; }
				if (typeof(T) == typeof(TimeSpan)) { writer.WriteValue((TimeSpan) (object) value!); return; }
				if (typeof(T) == typeof(DateTime)) { writer.WriteValue((DateTime) (object) value!); return; }
				if (typeof(T) == typeof(DateTimeOffset)) { writer.WriteValue((DateTimeOffset) (object) value!); return; }
				if (typeof(T) == typeof(DateOnly)) { writer.WriteValue((DateOnly) (object) value!); return; }
				if (typeof(T) == typeof(TimeOnly)) { writer.WriteValue((TimeOnly) (object) value!); return; }
				if (typeof(T) == typeof(NodaTime.Duration)) { writer.WriteValue((NodaTime.Duration) (object) value!); return; }
				if (typeof(T) == typeof(NodaTime.Instant)) { writer.WriteValue((NodaTime.Instant) (object) value!); return; }
				if (typeof(T) == typeof(Half)) { writer.WriteValue((Half) (object) value!); return; }
#if NET8_0_OR_GREATER
				if (typeof(T) == typeof(Int128)) { writer.WriteValue((Int128) (object) value!); return; }
				if (typeof(T) == typeof(UInt128)) { writer.WriteValue((UInt128) (object) value!); return; }
#endif
			}
			else
			{
				if (typeof(T) == typeof(bool?)) { writer.WriteValue((bool?) (object?) value); return; }
				if (typeof(T) == typeof(char?)) { writer.WriteValue((char?) (object?) value); return; }
				if (typeof(T) == typeof(int?)) { writer.WriteValue((int?) (object?) value); return; }
				if (typeof(T) == typeof(long?)) { writer.WriteValue((long?) (object?) value); return; }
				if (typeof(T) == typeof(uint?)) { writer.WriteValue((uint?) (object?) value); return; }
				if (typeof(T) == typeof(ulong?)) { writer.WriteValue((ulong?) (object?) value); return; }
				if (typeof(T) == typeof(double?)) { writer.WriteValue((double?) (object?) value); return; }
				if (typeof(T) == typeof(float?)) { writer.WriteValue((float?) (object?) value); return; }
				if (typeof(T) == typeof(Guid?)) { writer.WriteValue((Guid?) (object?) value); return; }
				if (typeof(T) == typeof(Uuid128?)) { writer.WriteValue((Uuid128?) (object?) value); return; }
				if (typeof(T) == typeof(Uuid96?)) { writer.WriteValue((Uuid96?) (object?) value); return; }
				if (typeof(T) == typeof(Uuid80?)) { writer.WriteValue((Uuid80?) (object?) value); return; }
				if (typeof(T) == typeof(Uuid64?)) { writer.WriteValue((Uuid64?) (object?) value); return; }
				if (typeof(T) == typeof(TimeSpan?)) { writer.WriteValue((TimeSpan?) (object?) value); return; }
				if (typeof(T) == typeof(DateTime?)) { writer.WriteValue((DateTime?) (object?) value); return; }
				if (typeof(T) == typeof(DateTimeOffset?)) { writer.WriteValue((DateTimeOffset?) (object?) value); return; }
				if (typeof(T) == typeof(DateOnly?)) { writer.WriteValue((DateOnly?) (object?) value); return; }
				if (typeof(T) == typeof(TimeOnly?)) { writer.WriteValue((TimeOnly?) (object?) value); return; }
				if (typeof(T) == typeof(NodaTime.Duration?)) { writer.WriteValue((NodaTime.Duration?) (object?) value); return; }
				if (typeof(T) == typeof(NodaTime.Instant?)) { writer.WriteValue((NodaTime.Instant?) (object?) value); return; }
				if (typeof(T) == typeof(Half?)) { writer.WriteValue((Half?) (object?) value); return; }
#if NET8_0_OR_GREATER
				if (typeof(T) == typeof(Int128?)) { writer.WriteValue((Int128?) (object?) value!); return; }
				if (typeof(T) == typeof(UInt128?)) { writer.WriteValue((UInt128?) (object?) value!); return; }
#endif
			}

#endif
			#endregion </JIT_HACK>

			if (value == null)
			{ // "null"
				writer.WriteNull(); // "null"
				return;
			}

			if (value is string s)
			{
				writer.WriteValue(s);
				return;
			}

			if (value is JsonValue j)
			{
				j.JsonSerialize(writer);
				return;
			}

			Type type = value.GetType();
			if (writer.Resolver.TryGetConverterFor<T>(out var converter))
			{
				converter.Serialize(writer, value);
				return;
			}

			var visitor = GetVisitorForType(type);
			if (visitor == null) throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeType(type);
			visitor(value, typeof(T), type, writer);
		}

		/// <summary>Visit a boxed value (Primitive, ValueType, Class, Struct, ...)</summary>
		/// <param name="value">Value that needs to be serialized (could be of any type)</param>
		/// <param name="declaredType">Type of this value as declared in the containing type, or null if unknown or top-level</param>
		/// <param name="writer">Serialization context</param>
		public static void VisitValue(object? value, Type declaredType, CrystalJsonWriter writer)
		{
			if (value == null)
			{ // "null"
				writer.WriteNull(); // "null"
				return;
			}

			if (value is string s)
			{
				writer.WriteValue(s);
				return;
			}

			if (value is JsonValue j)
			{
				j.JsonSerialize(writer);
				return;
			}

			Type type = value.GetType();
			if (writer.Resolver.TryGetConverterFor(type, out var converter))
			{
				converter.Serialize(value, declaredType, null, writer);
				return;
			}

			var visitor = GetVisitorForType(type);
			if (visitor == null) throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeType(type);
			visitor(value, declaredType, type, writer);
		}

		#region Arrays, Lists, Sequences, ...

		private static void VisitStringArrayInternal(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var array = (string[])value;
			if (array.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var state = writer.BeginArray();

			// first item
			writer.WriteHeadSeparator();
			writer.WriteValue(array[0]);
			// rest of the array
			for (int i = 1; i < array.Length; i++)
			{
				if ((i & 0x3F) == 0) writer.MaybeFlush();

				writer.WriteTailSeparator();
				writer.WriteValue(array[i]);
			}

			writer.EndArray(state);
		}

		private static void VisitStringListInternal(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var list = (List<string>) value;
			var items = CollectionsMarshal.AsSpan(list);

			if (items.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var state = writer.BeginArray();

			// first item
			writer.WriteHeadSeparator();
			writer.WriteValue(items[0]);
			// rest of the list
			for (int i = 1; i < items.Length; i++)
			{
				if ((i & 0x3F) == 0) writer.MaybeFlush();

				writer.WriteTailSeparator();
				writer.WriteValue(items[i]);
			}

			writer.EndArray(state);
		}

		private static void VisitInt32ArrayInternal(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var array = (int[]) value;
			if (array.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var state = writer.BeginArray();

			// first item
			writer.WriteHeadSeparator();
			writer.WriteValue(array[0]);
			// rest of the array
			for (int i = 1; i < array.Length; i++)
			{
				if ((i & 0x3F) == 0) writer.MaybeFlush();

				writer.WriteTailSeparator();
				writer.WriteValue(array[i]);
			}

			writer.EndArray(state);
		}

		private static void VisitInt32ListInternal(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var list = (List<int>) value;
			var items = CollectionsMarshal.AsSpan(list);

			if (items.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var state = writer.BeginArray();

			// first item
			writer.WriteHeadSeparator();
			writer.WriteValue(items[0]);
			// rest of the list
			for (int i = 1; i < items.Length; i++)
			{
				if ((i & 0x3F) == 0) writer.MaybeFlush();

				writer.WriteTailSeparator();
				writer.WriteValue(items[i]);
			}

			writer.EndArray(state);
		}

		/// <summary>Visit a collection of strings</summary>
		public static void VisitEnumerableOfString(IEnumerable<string>? enumerable, CrystalJsonWriter writer)
		{
			if (enumerable == null)
			{
				writer.WriteNull();
				return;
			}

			writer.MarkVisited(enumerable);

			// les valeurs sont déjà des string
			using (var it = enumerable.GetEnumerator())
			{
				int n = 10;
				if (it.MoveNext())
				{
					var state = writer.BeginArray();
					writer.WriteHeadSeparator();
					writer.WriteValue(it.Current);

					while (it.MoveNext())
					{
						if (n == 0) { writer.MaybeFlush(); n = 10; }

						writer.WriteFieldSeparator();
						writer.WriteValue(it.Current);

						--n;
					}

					writer.EndArray(state);
				}
				else
				{
					writer.WriteEmptyArray();
				}
			}

			writer.Leave(enumerable);
		}

		private static CrystalJsonTypeVisitor CompileGenericVisitorMethod(string methodName, string visitorName, Type type)
		{
			// (v, t, r, w) => method(v, t, r, w)

			var method = typeof(CrystalJsonVisitor).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
			Contract.Debug.Assert(method != null);
			method = method.MakeGenericMethod(type);

			return method.CreateDelegate<CrystalJsonTypeVisitor>();
		}

		private static void VisitArrayInternal<T>(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var array = (T[]) value;

			if (array.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var elemType = typeof(T);
			var visitor = GetVisitorForType(elemType, atRuntime: false);

			var state = writer.BeginArray();

			// head
			writer.WriteHeadSeparator();
			visitor(array[0], elemType, null, writer);

			// tail
			for (int i = 1; i < array.Length; i++)
			{
				if (i % 10 == 0) writer.MaybeFlush();

				writer.WriteTailSeparator();
				visitor(array[i], elemType, null, writer);
			}

			writer.EndArray(state);
		}

		private static void VisitListInternal<T>(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var list = (List<T>) value;
			var items = CollectionsMarshal.AsSpan(list);

			if (items.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var elemType = typeof(T);
			var visitor = GetVisitorForType(elemType, atRuntime: false);

			var state = writer.BeginArray();

			// head
			writer.WriteHeadSeparator();
			visitor(items[0], elemType, null, writer);

			// tail
			for (int i = 1; i < items.Length; i++)
			{
				if (i % 10 == 0) writer.MaybeFlush();

				writer.WriteTailSeparator();
				visitor(items[i], elemType, null, writer);
			}

			writer.EndArray(state);
		}

		/// <summary>Visit an array of generic values</summary>
		public static void VisitSpan<T>(ReadOnlySpan<T?> items, CrystalJsonWriter writer)
		{
			if (items.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var state = writer.BeginArray();

			// head
			writer.WriteHeadSeparator();
			VisitValue<T>(items[0], writer);

			// tail
			for (int i = 1; i < items.Length; i++)
			{
				if (i % 10 == 0) writer.MaybeFlush();

				writer.WriteTailSeparator();
				VisitValue<T>(items[i], writer);
			}

			writer.EndArray(state);
		}

		/// <summary>Visit an array of generic values</summary>
		public static void VisitArray<T>(T[]? array, CrystalJsonWriter writer)
		{
			if (array == null)
			{
				writer.WriteNull();
				return;
			}

			if (array.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var state = writer.BeginArray();

			// head
			writer.WriteHeadSeparator();
			VisitValue<T>(array[0], writer);

			// tail
			for (int i = 1; i < array.Length; i++)
			{
				if (i % 10 == 0) writer.MaybeFlush();

				writer.WriteTailSeparator();
				VisitValue<T>(array[i], writer);
			}

			writer.EndArray(state);
		}

		/// <summary>Visit an array of generic values</summary>
		public static void VisitList<T>(List<T>? list, CrystalJsonWriter writer)
		{
			if (list == null)
			{
				writer.WriteNull();
				return;
			}

			var items = CollectionsMarshal.AsSpan(list);

			if (items.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var state = writer.BeginArray();

			// head
			writer.WriteHeadSeparator();
			VisitValue<T>(items[0], writer);

			// tail
			for (int i = 1; i < items.Length; i++)
			{
				if (i % 10 == 0) writer.MaybeFlush();

				writer.WriteTailSeparator();
				VisitValue<T>(items[i], writer);
			}

			writer.EndArray(state);
		}

		public static void VisitEnumerable(IEnumerable? enumerable, Type elementType, CrystalJsonWriter writer)
		{
			if (enumerable == null)
			{
				writer.WriteNull();
				return;
			}

			writer.MarkVisited(enumerable);

			// we must convert each item into strings...
			var it = enumerable.GetEnumerator();

			try
			{
				int n = 0;
				if (it.MoveNext())
				{
					var state = writer.BeginArray();
					// head
					writer.WriteHeadSeparator();
					VisitValue(it.Current, elementType, writer);
					// tail
					while (it.MoveNext())
					{
						if (n == 10)
						{
							writer.MaybeFlush();
							n = 0;
						}

						writer.WriteTailSeparator();
						VisitValue(it.Current, elementType, writer);

						++n;
					}
					writer.EndArray(state);
				}
				else
				{
					writer.WriteEmptyArray();
				}
			}
			finally
			{
				(it as IDisposable)?.Dispose();
			}

			writer.Leave(enumerable);
		}

		#endregion

		#region Dictionaries...

		private const int DICTIONARY_AUTO_FLUSH_INTERVAL = 10;

		/// <summary>Visit a <c>Dictionary&lt;string, string&gt;</c></summary>
		public static void VisitStringDictionary(ICollection<KeyValuePair<string, string?>>? items, CrystalJsonWriter writer)
		{
			if (items == null)
			{ // "null" or "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (items.Count == 0)
			{ // empty => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(items);
			var state = writer.BeginObject();

			if (items is Dictionary<string, string?> dict)
			{
				WriteItemsFast(writer, dict);
			}
			else
			{
				WriteItemsSlow(writer, items);
			}

			writer.EndObject(state); // "}"
			writer.Leave(items);
		}

		/// <summary>Visit a <c>Dictionary&lt;string, object&gt;</c></summary>
		public static void VisitGenericDictionary<TValue>(ICollection<KeyValuePair<string, TValue>>? items, CrystalJsonWriter writer)
		{
			if (items == null)
			{ // "null" or "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (items.Count == 0)
			{ // empty => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(items);
			var state = writer.BeginObject();

			switch (items)
			{
				case Dictionary<string, string?> dict:
				{
					WriteItemsFast(writer, dict);
					break;
				}
				case Dictionary<string, TValue> dict:
				{
					WriteItemsFast(writer, dict);
					break;
				}
				case ICollection<KeyValuePair<string, string?>> dict:
				{
					WriteItemsSlow(writer, dict);
					break;
				}
				default:
				{
					WriteItemsSlow(writer, items);
					break;
				}
			}

			writer.EndObject(state); // "}"
			writer.Leave(items);
		}

		private static void WriteItemsFast(CrystalJsonWriter writer, Dictionary<string, string?> items)
		{
			int n = DICTIONARY_AUTO_FLUSH_INTERVAL;
			foreach (var kvp in items)
			{
				if (n == 0)
				{
					writer.MaybeFlush();
					n = DICTIONARY_AUTO_FLUSH_INTERVAL;
				}

				writer.WriteUnsafeName(kvp.Key);
				writer.WriteValue(kvp.Value);

				--n;
			}
		}

		private static void WriteItemsSlow(CrystalJsonWriter writer, ICollection<KeyValuePair<string, string?>> items)
		{
			int n = DICTIONARY_AUTO_FLUSH_INTERVAL;
			foreach (var kvp in items)
			{
				if (n == 0)
				{
					writer.MaybeFlush();
					n = DICTIONARY_AUTO_FLUSH_INTERVAL;
				}

				writer.WriteUnsafeName(kvp.Key);
				writer.WriteValue(kvp.Value);

				--n;
			}
		}

		private static void WriteItemsFast<TValue>(CrystalJsonWriter writer, Dictionary<string, TValue> items)
		{
			int n = DICTIONARY_AUTO_FLUSH_INTERVAL;
			foreach (var kvp in items)
			{
				if (n == 0)
				{
					writer.MaybeFlush();
					n = DICTIONARY_AUTO_FLUSH_INTERVAL;
				}

				writer.WriteUnsafeName(kvp.Key);
				VisitValue<TValue>(kvp.Value, writer);

				--n;
			}
		}

		private static void WriteItemsSlow<TValue>(CrystalJsonWriter writer, ICollection<KeyValuePair<string, TValue>> items)
		{
			int n = DICTIONARY_AUTO_FLUSH_INTERVAL;
			foreach (var kvp in items)
			{
				if (n == 0)
				{
					writer.MaybeFlush();
					n = DICTIONARY_AUTO_FLUSH_INTERVAL;
				}

				writer.WriteUnsafeName(kvp.Key);
				VisitValue<TValue>(kvp.Value, writer);

				--n;
			}
		}

		/// <summary>Visit a collection of <c>KeyValuePair&lt;string, object&gt;</c></summary>
		public static void VisitGenericObjectDictionary(ICollection<KeyValuePair<string, object?>>? items, CrystalJsonWriter writer)
		{
			if (items == null)
			{ // "null" or "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (items.Count == 0)
			{ // empty => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(items);
			var state = writer.BeginObject();

			if (items is Dictionary<string, object?> dict)
			{
				WriteItemsFast(writer, dict);
			}
			else
			{
				WriteItemsSlow(writer, items);
			}

			writer.EndObject(state); // "}"
			writer.Leave(items);
		}

		private static void WriteItemsFast(CrystalJsonWriter writer, Dictionary<string, object?> items)
		{
			var objType = typeof(object);
			int n = DICTIONARY_AUTO_FLUSH_INTERVAL;
			foreach (var kvp in items)
			{
				if (n == 0)
				{
					writer.MaybeFlush();
					n = DICTIONARY_AUTO_FLUSH_INTERVAL;
				}
				writer.WriteUnsafeName(kvp.Key);
				VisitValue(kvp.Value, objType, writer);

				--n;
			}
		}

		private static void WriteItemsSlow(CrystalJsonWriter writer, ICollection<KeyValuePair<string, object?>> items)
		{
			var objType = typeof(object);
			int n = DICTIONARY_AUTO_FLUSH_INTERVAL;
			foreach (var kvp in items)
			{
				if (n == 0)
				{
					writer.MaybeFlush();
					n = DICTIONARY_AUTO_FLUSH_INTERVAL;
				}
				writer.WriteUnsafeName(kvp.Key);
				VisitValue(kvp.Value, objType, writer);

				--n;
			}
		}

		/// <summary>Visit a non-generic <c>IDictionary</c></summary>
		public static void VisitDictionary(IDictionary? dictionary, Type keyType, Type valueType, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" ou "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // vide => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			bool keyIsString = keyType == typeof(string);
			bool valueIsString = valueType == typeof(string);

			// we will need to convert key and/or values :(
			int n = DICTIONARY_AUTO_FLUSH_INTERVAL;
			foreach (DictionaryEntry entry in dictionary)
			{
				var name = keyIsString ? (entry.Key as string) : TypeHelper.ConvertKeyToString(entry.Key);
				if (name == null) continue; // not supported!

				if (n == 0)
				{
					writer.MaybeFlush();
					n = DICTIONARY_AUTO_FLUSH_INTERVAL;
				}

				// key
				writer.WriteUnsafeName(name);
				// value
				if (valueIsString)
				{
					writer.WriteValue(entry.Value as string);
				}
				else
				{
					VisitValue(entry.Value, valueType, writer);
				}

				--n;
			}

			// done!
			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		/// <summary>Visit a non-generic <c>IDictionary</c> with keys known to be strings</summary>
		public static void VisitDictionary_StringKey(IDictionary? dictionary, Type valueType, CrystalJsonTypeVisitor visitor, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" or "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // empty => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			int n = DICTIONARY_AUTO_FLUSH_INTERVAL;
			foreach (DictionaryEntry entry in dictionary)
			{
				if (n == 0)
				{
					writer.MaybeFlush();
					n = DICTIONARY_AUTO_FLUSH_INTERVAL;
				}

				// key
				writer.WriteUnsafeName((string) entry.Key);
				// value
				visitor(entry.Value, valueType, entry.Value?.GetType() ?? valueType, writer);

				--n;
			}

			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		/// <summary>Visit a <c>Dictionary&lt;string, string&gt;</c></summary>
		public static void VisitDictionary_StringKeyAndValue(IDictionary<string, string?>? dictionary, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" or "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // empty => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			int n = DICTIONARY_AUTO_FLUSH_INTERVAL;
			foreach (var kv in dictionary)
			{
				if (n == 0)
				{
					writer.MaybeFlush();
					n = DICTIONARY_AUTO_FLUSH_INTERVAL;
				}

				// key
				writer.WriteUnsafeName(kv.Key);
				// value
				writer.WriteValue(kv.Value);

				--n;
			}

			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		/// <summary>Visit a <c>Dictionary&lt;int, T&gt;</c></summary>
		public static void VisitDictionary_Int32Key(IDictionary? dictionary, Type valueType, CrystalJsonTypeVisitor visitor, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" or "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // empty => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			int n = DICTIONARY_AUTO_FLUSH_INTERVAL;
			foreach (DictionaryEntry entry in dictionary)
			{
				if (n == 0)
				{
					writer.MaybeFlush();
					n = DICTIONARY_AUTO_FLUSH_INTERVAL;
				}

				// key
				writer.WriteName((int) entry.Key);
				// value
				visitor(entry.Value, valueType, entry.Value?.GetType() ?? valueType, writer);

				--n;
			}

			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		/// <summary>Visit a <c>Dictionary&lt;int, string&gt;</c></summary>
		public static void VisitDictionary_Int32Key_StringValue(IDictionary<int, string?>? dictionary, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" or "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // empty => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			int n = DICTIONARY_AUTO_FLUSH_INTERVAL;
			foreach (var entry in dictionary)
			{
				if (n == 0)
				{
					writer.MaybeFlush();
					n = DICTIONARY_AUTO_FLUSH_INTERVAL;
				}

				// key
				writer.WriteName(entry.Key);
				// value
				writer.WriteValue(entry.Value);

				--n;
			}

			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		#endregion

		#region VarTuples...

		[Pure]
		public static CrystalJsonTypeVisitor CreateVisitorForSTupleType(Type type)
		{
			Contract.Debug.Requires(type != null && typeof(IVarTuple).IsAssignableFrom(type));

			// for STuple<...> we have a more specialized version
			if (type.IsValueType && type.IsGenericType && type.Name.StartsWith(nameof(STuple) + "`", StringComparison.Ordinal))
			{
				var args = type.GetGenericArguments();
				if (args.Length <= 7)
				{
					// lookup the corresponding VisitSTuple#N# method
					var m = args.Length switch
					{
						1 => typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple1))!,
						2 => typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple2))!,
						3 => typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple3))!,
						4 => typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple4))!,
						5 => typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple5))!,
						6 => typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple6))!,
						7 => typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple7))!,
						8 => typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple8))!,
						_ => throw new InvalidOperationException()
					};
					Contract.Debug.Assert(m != null, "Missing method to serialize generic tuple");
					// convert to a delegate
					return (CrystalJsonTypeVisitor) m.MakeGenericMethod(args).CreateDelegate(typeof(CrystalJsonTypeVisitor));
				}
			}

			if (type == typeof(STuple))
			{ // the empty tuple is serialized as an empty array
				return (_, _, _, writer) => writer.WriteEmptyArray();
			}
			
			// if not, use a slower version that will inspect each item, one by one
			return VisitSTupleGeneric;
		}

		/// <summary>Visit a boxed tuple</summary>
		public static void VisitSTupleGeneric(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var tuple = (IVarTuple) value;
			int n = tuple.Count;
			if (n == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var state = writer.BeginArray();
			// Head
			writer.WriteHeadSeparator();
			VisitValue(tuple[0], writer);
			// Tail
			for (int i = 1; i < n; i++)
			{
				if (i % 10 == 0) { writer.MaybeFlush(); }
				
				writer.WriteFieldSeparator();
				VisitValue(tuple[i], writer);
			}
			writer.EndArray(state);
		}

		/// <summary>Visit a tuple of length 1</summary>
		public static void VisitSTuple1<T1>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }

			var t = (STuple<T1>) tuple;
			var state = writer.BeginInlineArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.EndInlineArray(state);
		}

		/// <summary>Visit a tuple of length 2</summary>
		public static void VisitSTuple2<T1, T2>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }

			var t = (STuple<T1, T2>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.EndArray(state);
		}

		/// <summary>Visit a tuple of length 3</summary>
		public static void VisitSTuple3<T1, T2, T3>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }

			var t = (STuple<T1, T2, T3>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.EndArray(state);
		}

		/// <summary>Visit a tuple of length 4</summary>
		public static void VisitSTuple4<T1, T2, T3, T4>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }

			var t = (STuple<T1, T2, T3, T4>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.EndArray(state);
		}

		/// <summary>Visit a tuple of length 5</summary>
		public static void VisitSTuple5<T1, T2, T3, T4, T5>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }

			var t = (STuple<T1, T2, T3, T4, T5>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.EndArray(state);
		}

		/// <summary>Visit a tuple of length 6</summary>
		public static void VisitSTuple6<T1, T2, T3, T4, T5, T6>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }

			var t = (STuple<T1, T2, T3, T4, T5, T6>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item6);
			writer.EndArray(state);
		}

		/// <summary>Visit a tuple of length 7</summary>
		public static void VisitSTuple7<T1, T2, T3, T4, T5, T6, T7>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }

			var t = (STuple<T1, T2, T3, T4, T5, T6, T7>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item6);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item7);
			writer.EndArray(state);
		}

		/// <summary>Visit a tuple of length 8</summary>
		public static void VisitSTuple8<T1, T2, T3, T4, T5, T6, T7, T8>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }

			var t = (STuple<T1, T2, T3, T4, T5, T6, T7, T8>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item6);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item7);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item8);
			writer.EndArray(state);
		}

		#endregion

		#region ValueTuples...

		[Pure]
		public static CrystalJsonTypeVisitor CreateVisitorForITupleType(Type type)
		{
			Contract.Debug.Requires(type != null);
			Contract.Debug.Requires(typeof(System.Runtime.CompilerServices.ITuple).IsAssignableFrom(type));

			// for ValueTuple<...> we have a specialized version that is faster
			if (type.IsValueType && type.IsGenericType && type.Name.StartsWith(nameof(ValueTuple) + "`", StringComparison.Ordinal))
			{
				var args = type.GetGenericArguments();
				if (args.Length <= 8)
				{
					// lookup the corresponding VisitValueTuple#N# method
					MethodInfo m;
					switch (args.Length)
					{
						case 1: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple1))!; break;
						case 2: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple2))!; break;
						case 3: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple3))!; break;
						case 4: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple4))!; break;
						case 5: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple5))!; break;
						case 6: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple6))!; break;
						case 7: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple7))!; break;
						case 8: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple8))!; break;
						default: throw new InvalidOperationException();
					}
					Contract.Debug.Assert(m != null, "Missing method to serialize generic tuple");
					// convert into a delegate
					return (CrystalJsonTypeVisitor) m.MakeGenericMethod(args).CreateDelegate(typeof(CrystalJsonTypeVisitor));
				}
			}
			// otherwise, use a slow version that will inspect each item, one by one
			return VisitITupleGeneric;
		}

		public static void VisitITupleGeneric(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var tuple = (System.Runtime.CompilerServices.ITuple) value;
			int n = tuple.Length;
			if (n == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var state = writer.BeginArray();
			// Head
			writer.WriteHeadSeparator();
			VisitValue(tuple[0], writer);
			// Tail
			for (int i = 1; i < n; i++)
			{
				writer.WriteFieldSeparator();
				VisitValue(tuple[i], writer);
			}
			writer.EndArray(state);
		}

		/// <summary>Visit a boxed ValueTuple of length 1</summary>
		public static void VisitValueTuple1<T1>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1>) tuple;
			var state = writer.BeginInlineArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.EndInlineArray(state);
		}

		/// <summary>Visit a boxed ValueTuple of length 2</summary>
		public static void VisitValueTuple2<T1, T2>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.EndArray(state);
		}

		/// <summary>Visit a boxed ValueTuple of length 3</summary>
		public static void VisitValueTuple3<T1, T2, T3>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2, T3>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.EndArray(state);
		}

		/// <summary>Visit a boxed ValueTuple of length 4</summary>
		public static void VisitValueTuple4<T1, T2, T3, T4>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2, T3, T4>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.EndArray(state);
		}

		/// <summary>Visit a boxed ValueTuple of length 5</summary>
		public static void VisitValueTuple5<T1, T2, T3, T4, T5>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2, T3, T4, T5>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.EndArray(state);
		}

		/// <summary>Visit a boxed ValueTuple of length 6</summary>
		public static void VisitValueTuple6<T1, T2, T3, T4, T5, T6>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2, T3, T4, T5, T6>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item6);
			writer.EndArray(state);
		}

		/// <summary>Visit a boxed ValueTuple of length 7</summary>
		public static void VisitValueTuple7<T1, T2, T3, T4, T5, T6, T7>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2, T3, T4, T5, T6, T7>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item6);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item7);
			writer.EndArray(state);
		}

		/// <summary>Visit a boxed ValueTuple of length 7</summary>
		public static void VisitValueTuple8<T1, T2, T3, T4, T5, T6, T7, T8>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item6);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item7);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item8);
			writer.EndArray(state);
		}

		#endregion

		/// <summary>Visit a type that implements <see cref="IJsonSerializable"/></summary>
		public static void VisitJsonSerializable(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
				writer.WriteNull(); // "null"
			else
				((IJsonSerializable)value).JsonSerialize(writer);
		}

		/// <summary>Visit a type that implements <see cref="IJsonPackable"/></summary>
		public static void VisitJsonPackable(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
				writer.WriteNull(); // "null"
			else
			{
				var json = ((IJsonPackable) value).JsonPack(writer.Settings, writer.Resolver);
				json.JsonSerialize(writer);
			}
		}

		/// <summary>Visit a custom reference or value type</summary>
		public static void VisitCustomClassOrStruct(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull(); // "null"
				return;
			}

			writer.MarkVisited(value);
			var state = writer.BeginObject();

			// what type is it?
			runtimeType ??= value.GetType();
			if (!writer.Resolver.TryResolveTypeDefinition(runtimeType, out var typeDef))
			{ // uhoh, this should not happen!
				throw CrystalJson.Errors.Serialization_CouldNotResolveTypeDefinition(runtimeType);
			}

			// process the members (properties and fields)
			bool discardDefaults = writer.DiscardDefaults;
			bool discardNulls = writer.DiscardNulls;

			if (!writer.DiscardClass)
			{
				// we may output a "$type" property if the type is a derived type that belongs in a "polymorphic chain", in order to be able to deserialize it back to that specific type
				if (typeDef.TypeDiscriminatorProperty != null && typeDef.TypeDiscriminatorValue != null)
				{
					writer.WriteField(typeDef.TypeDiscriminatorProperty, typeDef.TypeDiscriminatorValue);
				}
			}

			foreach (var member in typeDef.Members)
			{
				var child = member.Getter(value);
				if (discardDefaults)
				{ // ignore 0, false, DateTime.MinValue, ...
					if (member.IsDefaultValue(child)) continue;
				}
				else if (discardNulls)
				{ // ignore null (classes, Nullable<T>, ...)
					if (child == null) continue;
				}

				writer.WriteFieldSeparator();
				writer.WritePropertyName(member.EncodedName);
				if (member.Attributes != null)
				{
					var tmp = writer.PushAttributes(member.Attributes);
					member.Visitor(child, member.Type, null, writer);
					writer.PopAttributes(tmp);
				}
				else
				{
					member.Visitor(child, member.Type, null, writer);
				}
			}

			writer.EndObject(state); // "}"
			writer.Leave(value);
		}

		/// <summary>Helper called at runtime to visit a member declared as <see cref="System.Object"/> in the parent type.</summary>
		/// <param name="value">Instance to visit</param>
		/// <param name="declaringType">Type as declared in the parent object, must be 'object'</param>
		/// <param name="runtimeType">Actual type, found at runtime</param>
		/// <param name="writer">Serialization context</param>
		private static void VisitObjectAtRuntime(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			// If we end up here, this is a member is declared as "object" in the containing struct or class, ie: (declaringType == typeof(object)
			// We have to inspect the actual type of the instance at runtime:
			// - if value.GetType() is also 'object', it is an empty object (singleton? lock?) that will be serialized as "{ }".
			// - otherwise, we pass through to the writer that correspond to the runtime type of this instance.

			if (value == null)
			{
				writer.WriteNull(); // "null"
				return;
			}

			runtimeType ??= value.GetType();
			if (runtimeType == typeof(object))
			{ // this is an empty object
				writer.WriteEmptyObject();
				return;
			}

			// find the visitor for this type
			var visitor = GetVisitorForType(runtimeType, atRuntime: true); // atRuntime = true, to prevent an infinite loop (if false, would return a callback that would call the same method again!)
			if (visitor == null) throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeType(runtimeType);

			// pass-through
			visitor(value, declaringType, runtimeType, writer);
		}

		/// <summary>Helper called at runtime to visit a member declared as an interface in the parent type.</summary>
		/// <param name="value">Instance to visit</param>
		/// <param name="declaringType">Type as declared in the parent object, must be an interface.</param>
		/// <param name="runtimeType">Actual type, found at runtime</param>
		/// <param name="writer">Serialization context</param>
		private static void VisitInterfaceAtRuntime(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			// if we end up here, the member is declared as an interface, and we need to look up its actual runtime type, to select the correct writer.
			if (value == null)
			{
				writer.WriteNull(); // "null"
				return;
			}

			runtimeType ??= value.GetType();
			if (runtimeType == typeof(object))
			{ //BUGBUG: should not be possible, because top level type 'object' does not implement any interface?
				writer.WriteEmptyObject();
				return;
			}

			// Find the visitor for this type
			// note: atRuntime = true, to prevent infinite loops (if false, it will return a callback that re-invokes the same method !)
			var visitor = GetVisitorForType(runtimeType, atRuntime: true);
			if (visitor == null) throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeType(runtimeType);

			// Pass through
			visitor(value, declaringType, runtimeType, writer);
		}

		/// <summary>Visit an <see cref="System.Xml.XmlNode"/></summary>
		public static void VisitXmlNode(XmlNode? node, CrystalJsonWriter writer)
		{
			if (node == null)
			{
				writer.WriteNull(); // "null"
			}

			var converted = ConvertXmlNodeToJson(node);
			if (converted.IsNull)
			{
				writer.WriteNull(); // "null"
			}
			else
			{
				converted.JsonSerialize(writer);
			}
		}

		public static void ConvertDictionaryToJson(JsonObject root, IEnumerable<KeyValuePair<string, string>> wmlDictionary, char separator = '.')
		{
			foreach (var kvp in wmlDictionary)
			{
				string path = kvp.Key;
				int k = path.LastIndexOf(separator);
				if (k >= 0)
				{// remove the key from the xpath, if it contains a separator
					var key = path.AsMemory(k + 1);
					var parentPath = path.AsMemory(0, k);
					root.GetOrCreateObject(parentPath).Set(key, kvp.Value);
				}
				else
				{
					root.Set(kvp.Key, kvp.Value);
				}
			}
		}

		/// <summary>Convert an <see cref="System.Xml.XmlNode"/> into a JSON object or value</summary>
		/// <param name="node">XML node to convert</param>
		/// <param name="flatten">If <see langword="false"/>, all XML attributes will be prefixed by '<c>@</c>'. If <see langword="true"/>, they will be mixed with the children of this node and could create conflicts</param>
		/// <remarks>Child with the same name will be merged into an array.</remarks>
		/// <example><code>ConvertXmlNodeToJson(&lt;foo id="123">&lt;bar>hello&lt;/bar>&lt;baz>world&lt;/baz>&lt;/foo>) => { "foo": { "@id": "123", "bar": "hello", "baz": "world" } }</code></example>
		[Pure]
		public static JsonValue ConvertXmlNodeToJson(XmlNode? node, bool flatten = false)
		{
			if (node == null) return JsonNull.Null;

			switch (node.NodeType)
			{
				case XmlNodeType.Element:
				{
					var elem = (XmlElement)node;

					var obj = new JsonObject();
					if (elem.HasAttributes)
					{
						foreach (XmlAttribute attr in elem.Attributes)
						{
							obj[flatten ? attr.Name : ("@" + attr.Name)] = attr.Value;
						}
					}
					if (elem.HasChildNodes)
					{
						XmlNodeList children = elem.ChildNodes;
						if (children.Count == 1)
						{
							var first = children[0]!;
							if (first.NodeType == XmlNodeType.Text || first.NodeType == XmlNodeType.CDATA)
							{
								return first.Value;
							}
						}

						foreach (XmlNode child in children)
						{
							var converted = ConvertXmlNodeToJson(child, flatten);
							if (JsonNull.Missing.Equals(converted))
							{ // by default, ignore "missing" nulls (but keep explicit nulls)
								continue;
							}

							string name = child.Name;

							// If there is already a child with the same name, it will become an array
							if (obj.TryGetValue(name, out var previous))
							{
								if (previous is JsonArray prevArray)
								{
									prevArray.Add(converted);
								}
								else
								{
									var arr = JsonArray.Create(previous, converted);
									obj[name] = arr;
								}
							}
							else
							{
								obj.Add(name, converted);
							}
						}
					}
					return obj;
				}
				case XmlNodeType.Text:
				case XmlNodeType.Attribute:
				case XmlNodeType.CDATA:
				{
					return node.Value;
				}
				default:
				{
					return JsonNull.Missing;
				}
			}
		}

		[Pure]
		public static JsonValue ConvertTupleToJson(IVarTuple? tuple)
		{
			if (tuple == null) return JsonNull.Null;
			int n = tuple.Count;
			if (n == 0) return JsonArray.Create(); //BUGBUG: TODO: readonly?
			var arr = new JsonArray(n);
			for (int i = 0; i < n; i++)
			{
				arr.Add(JsonValue.FromValue(tuple[i]));
			}
			return arr;
		}

		[Pure]
		public static JsonValue ConvertTupleToJson(System.Runtime.CompilerServices.ITuple? tuple)
		{
			if (tuple == null) return JsonNull.Null;
			int n = tuple.Length;
			if (n == 0) return JsonArray.Create(); //BUGBUG: TODO: readonly?
			var arr = new JsonArray(n);
			for (int i = 0; i < n; i++)
			{
				arr.Add(JsonValue.FromValue(tuple[i]));
			}
			return arr;
		}

		#endregion

	}

}
