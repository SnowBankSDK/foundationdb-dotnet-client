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

//#define PROTOTYPE_REFLECTION_EMIT

namespace SnowBank.Runtime
{
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Reflection;
	using SnowBank.Runtime;

	/// <summary>Extensions methods that add advanced features on <see cref="Type"/></summary>
	[PublicAPI]
	public static class TypeHelper
	{

		/// <summary>Converts an arbitrary dictionary key to a string literal, that can be used as a key in a string-only map (such as the field name in a JSON Object)</summary>
		/// <param name="key">Instance that represents a key</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, ContractAnnotation("key:notnull => notnull")]
		[return: NotNullIfNotNull(nameof(key))]
		public static string? ConvertKeyToString(object? key)
		{
			return key switch
			{
				null => null,
				string name => name, // already a string
				double d => d.ToString("R", CultureInfo.InvariantCulture), // we need to use "R" (roundtrip) so as not to lose the last digit
				float f => f.ToString("R", CultureInfo.InvariantCulture), // we need to use "R" (roundtrip) so as not to lose the last digit
				DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture), // use ISO format
				DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture), // use ISO format
				NodaTime.Instant t => NodaTime.Text.InstantPattern.ExtendedIso.Format(t), // use Extended ISO format
				Type type => GetFriendlyName(type), // use a readable version of the type (ex: "List<int>" instead of "List`1")
				IConvertible convertible => convertible.ToString(CultureInfo.InvariantCulture), // hope that the type returns something sensible
				_ => key.ToString()
			};
		}

		[Pure]
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		private static Type? FindReplacementType(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
			Type type
		)
		{
			Contract.Debug.Requires(type != null);
			if (type.IsGenericType)
			{
				if (type.IsGenericInstanceOf(typeof(IDictionary<,>)))
				{
					return typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments());
				}
				if (type.IsGenericInstanceOf(typeof(ICollection<>)))
				{
					return typeof(List<>).MakeGenericType(type.GetGenericArguments());
				}
				if (type.IsGenericInstanceOf(typeof(IEnumerable<>)))
				{
					return typeof(ReadOnlyCollection<>).MakeGenericType(type.GetGenericArguments());
				}
			}

			return null;
		}

		/// <summary>Generates a factory method that will instantiate an object of the specified type, or equivalent, if possible</summary>
		/// <param name="type">Type of the object to instantiate (can be an interface or abstract class, for a limited set of "well known" types)</param>
		/// <returns>Function that calls the parameterless constructor for the type, or <see langword="null"/> if it is impossible to create such a type (unsupported interface or abstract class, type without a parameterless ctor, ...)</returns>
		[Pure]
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		public static Func<object>? CompileGenerator(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
			this Type type
		)
		{
			Contract.Debug.Requires(type != null);
			if (type.IsInterface || type.IsAbstract)
			{ // must be a well known type
				var replacementType = FindReplacementType(type);
				if (replacementType == null)
				{ // no luck !
					return null;
				}
				// we have an alternative type that'll do the job
				type = replacementType;
			}

			if (type.IsClass)
			{ // "object func() => new T();"
				var defaultConstructor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);

				if (defaultConstructor != null)
				{
					return Expression.Lambda<Func<object>>(Expression.TypeAs(Expression.New(defaultConstructor), typeof(object))).Compile();
				}
				else
				{
					return () => Activator.CreateInstance(type)!;
				}
				//NOTE: we could also try FormatterServices.GetUninitializedObject(Type) that can instantiate without calling a ctor
				// ( http://msdn.microsoft.com/en-us/library/system.runtime.serialization.formatterservices.getuninitializedobject.aspx )
				// Problem: some types may not work properly if the ctor is not called (ex: private fields may not be initialized correctly!)
			}

			// we can always instantiate structs with the default ctor
			return Expression.Lambda<Func<object>>(Expression.Convert(Expression.Default(type), typeof(object))).Compile();
		}

		#region Getters / Setters...

		/// <summary>Generates a getter Lambda <c>(object instance) => (object) ((TInstance) instance).FIELD</c></summary>
		[Pure]
		public static Func<object, object> CompileGetter(this FieldInfo field)
		{
			Contract.NotNull(field);
			// "object func(object instance) { return (object) ((instance.TYPE)instance).Field; }"
			var prmInstance = Expression.Parameter(typeof(object), "instance");
			var body = Expression.Field(prmInstance.CastFromObject(field.DeclaringType!), field).BoxToObject();
			return Expression.Lambda<Func<object, object>>(body, prmInstance).Compile();
		}

		/// <summary>Generates a setter Lambda <c>(object instance, object value) => ((TInstance) instance).FIELD = (TValue) value</c></summary>
		/// <returns>Returns <c>null</c> if the field is read-only</returns>
		[Pure]
		public static Action<object, object?>? CompileSetter(this FieldInfo field)
		{
			Contract.NotNull(field);
			if (field.IsInitOnly) return null; // readonly !

			// "void func(object instance, object value) { ((instance.TYPE)instance).Field = (value.TYPE)value); }"
			var prmInstance = Expression.Parameter(typeof(object), "instance");
			var prmValue = Expression.Parameter(typeof(object), "value");

			var body = Expression.Assign(Expression.Field(prmInstance.CastFromObject(field.DeclaringType!), field), prmValue.CastFromObject(field.FieldType));
			return Expression.Lambda<Action<object, object?>>(body, prmInstance, prmValue).Compile();
		}

		/// <summary>Generates a getter Lambda <c>(object instance) => (object) ((TInstance) instance).PROPERTY</c></summary>
		/// <remarks>Set <paramref name="nonPublic"/> to also use a non-public get accessor (compiled expression trees can invoke them)</remarks>
		[Pure]
		public static Func<object, object> CompileGetter(this PropertyInfo property, bool nonPublic = false)
		{
			Contract.NotNull(property);
			// "object func(object target) { return (object) ((instance.TYPE)instance).get_Property(); }"
			var prmInstance = Expression.Parameter(typeof(object), "instance");
			var body = Expression.Call(prmInstance.CastFromObject(property.DeclaringType!), property.GetGetMethod(nonPublic)!).BoxToObject();
			return Expression.Lambda<Func<object, object>>(body, prmInstance).Compile();
		}

		/// <summary>Generates a setter Lambda <c>(object instance, object value) => ((TInstance) instance).PROPERTY = (TValue) value</c></summary>
		/// <returns>Returns <c>null</c> if the property is read-only</returns>
		/// <remarks>Set <paramref name="nonPublic"/> to also use a non-public set accessor (compiled expression trees can invoke them)</remarks>
		[Pure]
		public static Action<object, object?>? CompileSetter(this PropertyInfo property, bool nonPublic = false)
		{
			Contract.NotNull(property);
			var setMethod = property.GetSetMethod(nonPublic);
			if (setMethod == null) return null; // non modifiable !?
			// "void func(object target, object value) { ((instance.TYPE)instance).set_Property((value.TYPE)value)); }"
			var prmInstance = Expression.Parameter(typeof(object), "instance");
			var prmValue = Expression.Parameter(typeof(object), "value");
			var body = Expression.Call(prmInstance.CastFromObject(property.DeclaringType!), setMethod, prmValue.CastFromObject(property.PropertyType));
			return Expression.Lambda<Action<object, object?>>(body, prmInstance, prmValue).Compile();
		}

		#endregion

		/// <summary>Returns an <see cref="System.Attribute"/> of a member of a type, given its name</summary>
		/// <param name="member">Member (field or property)</param>
		/// <param name="attributeName">Name (without the namespace) of the attribute to fetch (ex: "DataMemberAttribute")</param>
		/// <param name="inherit"><see langword="true"/> to also look into the attributes up the derived chain</param>
		/// <param name="attribute">receives the corresponding <see cref="System.Attribute"/> if found; otherwise, <see langword="null"/></param>
		/// <returns><see langword="true"/> if the attribute was found; otherwise, <see langword="false"/>.</returns>
		[ContractAnnotation("=> true, attribute:notnull; => false, attribute:null")]
		public static bool TryGetCustomAttribute(this MemberInfo member, string attributeName, bool inherit, out Attribute? attribute)
		{
			Contract.Debug.Requires(member != null && attributeName != null);
			attribute = member.GetCustomAttributes(inherit).FirstOrDefault(attr => attr.GetType().Name == attributeName) as Attribute;
			return attribute != null;
		}

		#region Type Extension Methods..

		/// <summary>Returns the default value for a type</summary>
		/// <returns><see langword="null"/>, <see langword="0"/>, <see langword="false"/>, DateTime.MinValue, ...</returns>
		[Pure]
		public static object? GetDefaultValue(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			this Type type)
		{
			// notes:
			// - Activator.CreateInstance() will invoke the default ctor() on the struct, if there is one
			// - RuntimeHelpers.GetUninitializedObject() is supposed to revert to the old "default(T)" behavior.

			// BUT: When called with nullable types, GetUninitializedObject(typeof(Nullable<T>) returns the default(T), not null !?!
			// => Activator.CreateInstance(typeof(int?)) == null
			// => RuntimeHelpers.GetUninitializedObject(typeof(int?)) == 0
			
			return type.IsValueType
				? Activator.CreateInstance(type)
				: null; // Reference Type / Interface
		}

		/// <summary>Returns the full name of a type, that can be later passed to Type.GetType(...) to retrieve the original type</summary>
		/// <param name="type">Type</param>
		/// <returns>Name of the type, with the form <c>"Namespace.ClassName, AssemblyName"</c></returns>
		[Pure]
		public static string GetAssemblyName(this Type type)
		{
			Contract.NotNull(type);

			// we want to generate "Namespace.ClassName, AssemblyName"

			var assemblyName = type.Assembly.GetName();
			if (assemblyName.Name == "mscorlib")
			{ // for mscorlib types, we don't need to specify the assembly name
				return type.FullName!;
			}

			// note: type.AssemblyQualifiedName and type.Assembly.FullName add the suffix ", Version=xxx, Culture=xxx, PublicKey=xxx" that we need to remove
			string displayName = assemblyName.FullName;
			int p = displayName.IndexOf(',');
			if (p > 0) displayName = displayName[..p];
			return type.FullName + ", " + displayName;
		}

		/// <summary>Returns a "human-readable" version of a type's name, including the generic arguments and nested types.</summary>
		/// <returns>"string", "int", "FooBar", "FooBar.NestedBaz", "List&lt;int>", "Dictionary&lt;string, Something&lt;Foo, Bar>>"</returns>
		/// <remarks>This name will NOT be in a format that easily allows retrieve it later, and should mostly be used in error message, logs or debug messages.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string GetFriendlyName(this Type type) => GetCompilableTypeName(type, omitNamespace: true, global: false);

		/// <summary>Returns a "human-readable" version of a type's name, including the generic arguments and nested types.</summary>
		/// <returns>"string", "int", "FooBar", "FooBar.NestedBaz", "List&lt;int>", "Dictionary&lt;string, Something&lt;Foo, Bar>>"</returns>
		/// <remarks>This name will NOT be in a format that easily allows retrieve it later, and should mostly be used in error message, logs or debug messages.</remarks>
		[Pure]
		public static string GetCompilableTypeName(Type type, bool omitNamespace, bool global)
		{
			Contract.NotNull(type);

			if (type.IsByRef)
			{
				//TODO: how can we detect "readonly" to use "in" instead?
				return "ref " + GetCompilableTypeName(type.GetElementType()!, omitNamespace, global);
			}

			if (type.IsArray)
			{ // => "Acme[]"

				int rank = type.GetArrayRank();
				var suffix = rank switch
				{
					1 => "[]",
					2 => "[,]",
					_ => type.Name[type.Name.IndexOf('[')..]
				};
				return GetCompilableTypeName(type.GetElementType()!, omitNamespace, global) + suffix;
			}

			if (type == typeof(string))
			{
				return "string";
			}

			if (type.IsPrimitive)
			{
				switch (Type.GetTypeCode(type))
				{
					case TypeCode.Boolean:  return "bool";
					case TypeCode.Char:     return "char";
					case TypeCode.SByte:    return "sbyte";
					case TypeCode.Byte:     return "byte";
					case TypeCode.Int16:    return "short";
					case TypeCode.UInt16:   return "ushort";
					case TypeCode.Int32:    return "int";
					case TypeCode.UInt32:   return "uint";
					case TypeCode.Int64:    return "long";
					case TypeCode.UInt64:   return "ulong";
					case TypeCode.Single:   return "float";
					case TypeCode.Double:   return "double";
					case TypeCode.Decimal:  return "decimal";
					case TypeCode.DateTime: return "DateTime";
				}
			}

			if (type.IsGenericParameter && type.DeclaringMethod == null)
			{
				return type.Name;
			}

			string? globalPrefix = global ? "global::" : null;

			if (type.IsNullableType())
			{
				return GetCompilableTypeName(type.GetGenericArguments()[0], omitNamespace, global) + "?";
			}

			string? parentPrefix = null;
			int outerOffset = 0;
			if (type.IsNested)
			{
				var outer = type.DeclaringType!;
				if (outer.IsGenericType)
				{
					// If this is of the form Outer<T1, T2>.Inner<T3>, the DeclaringType will not have the actual concrete types,
					// we have to get them from the generic type arguments of the inner type. It *looks* like the convention is
					// that Inner.GetGenericArguments() will return the types in order, so { T1, T2, T3 }, and if we know that the
					// outer types takes only N types (2 in the above example), then it will be the first N types that can be used
					// to construct the correct Outer class.
					// Example: if we have "Type inner = typeof(Outer<string, bool>.Inner<Guid>);" then:
					// > inner.DeclaringType returns "Outer<T1, T2>" instead of expected "Outer<string, bool>."
					// > inner.GetGenericArguments() will return { string, int, Guid }
					// > inner.DeclaringType.GetGenericArguments() will return { <T1>, <T2> } of length 2
					// > inner.DeclaringType.MakeGenericType(inner.GetGenericArguments().Take(2).ToArray()) will return the correct "Outer<string, bool>" !

					var innerArgs = type.GetGenericArguments(); // these will be the concrete types
					var outerArgs = outer.GetGenericArguments(); // these will be the generic types
					var concreteArgs = new Type[outerArgs.Length];
					Array.Copy(innerArgs, 0, concreteArgs, 0, concreteArgs.Length);

					//note: we only make a generic version of the type to get its name, and will not invoke any member at runtime
					#pragma warning disable IL3050
					#pragma warning disable IL2055
					outer = outer.MakeGenericType(concreteArgs);
					#pragma warning restore IL2055
					#pragma warning restore IL3050
					
					outerOffset = outerArgs.Length;
				}

				if (type.Name.StartsWith("<>"))
				{
					return GetCompilableTypeName(outer, omitNamespace, global);
				}

				parentPrefix = GetCompilableTypeName(outer, omitNamespace, false) + ".";
			}

			if (!omitNamespace)
			{
				parentPrefix = type.Namespace + "." + parentPrefix;
			}

			if (type.IsGenericType)
			{ // => "Acme<string, bool>"


				var args = type.GetGenericArguments();
				if (outerOffset != 0)
				{
					var tmp = args.Length != outerOffset ? new Type[args.Length - outerOffset] : [ ];
					Array.Copy(args, outerOffset, tmp, 0, tmp.Length);
					args = tmp;
				}
				string baseName;
				if (type.IsAnonymousType())
				{
					// the compiler generates "<>f__AnonymousType#<....,....,....>" where # is a unique counter (in hex).
					// we will replace this with "AnonymousType<...,...,...>" which is easier to read, and is similar to what is done by ASP.NET MVC
					baseName = "AnonymousType";
				}
				else
				{
					baseName = type.GetGenericTypeDefinition().Name;
					// generic arguments are after the backtick (`)
					int p = baseName.IndexOf('`');
					if (p > 0) baseName = baseName[..p];
				}

				// we will recursively call GetFriendlyName on each generic argument types
				if (args.Length == 0)
				{
					return globalPrefix + parentPrefix + baseName;
				}
				return $"{globalPrefix}{parentPrefix}{baseName}<{string.Join(", ", args.Select(arg => GetCompilableTypeName(arg, omitNamespace, global)))}>";
			}

			if (type.IsPointer)
			{ // => "Acme*"
				string baseName = globalPrefix + parentPrefix + GetCompilableTypeName(type.GetElementType()!, omitNamespace, global);
				return baseName + type.Name[type.Name.IndexOf('*')..];
			}

			// standard case
			return globalPrefix + parentPrefix + type.Name;
		}

		/// <summary>Returns a "human-readable" version of a method's name, including the parameters.</summary>
		/// <returns>"</returns>
		public static string GetFriendlyName(this MethodBase method, bool omitNamespace = true)
		{
			var declaringType = method.DeclaringType;
			var typeName = declaringType != null ? GetCompilableTypeName(declaringType, omitNamespace, global: false) : null;
			var parameters = method.GetParameters();

			if (method.IsConstructor)
			{
				return $"{(method.IsStatic ? "static " : null)}{typeName}({string.Join(", ", parameters.Select(p => $"{p.ParameterType.GetFriendlyName()} {p.Name}"))})";
			}

			//if (parameters.Length == 1 && method.Name.StartsWith("get_"))
			//{
			//	return $"{typeName}.{method.Name}[{parameters[0].ParameterType.GetFriendlyName()} {parameters[0].Name}] {{ get; }}";
			//}
			//if (parameters.Length == 2 && method.Name.StartsWith("set_"))
			//{
			//	return $"{typeName}.{method.Name}[{parameters[0].ParameterType.GetFriendlyName()} {parameters[0].Name}] {{ set; }}";
			//}

			return $"{typeName}{(typeName != null ? "." : null)}{method.Name}({string.Join(", ", parameters.Select(p => $"{p.ParameterType.GetFriendlyName()} {p.Name}"))})";
		}

		/// <summary>Returns the generic version of a method</summary>
		/// <param name="type">Declaring type</param>
		/// <param name="name">Name of the method</param>
		/// <param name="flags">Binding flags</param>
		/// <param name="types">Types of the generic arguments of the method</param>
		/// <returns>Corresponding generic method</returns>
		[Pure]
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
		public static MethodInfo? MakeGenericMethod(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] this Type type,
			string name,
			BindingFlags flags,
			params Type[] types
		)
		{
			Contract.Debug.Requires(type != null && name != null && types != null && types.Length > 0);
			var mi = type.GetMethod(name, flags);
			if (mi == null) return null;
			Contract.Debug.Assert(mi.IsGenericMethod, "Should reference a generic method");
			Contract.Debug.Assert(mi.GetGenericArguments().Length == types.Length, "Should have the correct number of generic type arguments");
			return mi.MakeGenericMethod(types);
		}

		/// <summary>Tests if a type is in instance of another type, abstract class, or interface</summary>
		/// <typeparam name="T">Expected type</typeparam>
		/// <param name="type">Type of the instance</param>
		/// <returns><see langword="true"/> if an instance of type <paramref name="type"/> can be assigned to a variable of type <typeparamref name="T"/></returns>
		[Pure]
		public static bool IsAssignableTo<T>(this Type type)
		{
			return type.IsAssignableTo(typeof(T));
		}

		/// <summary>Tests if a type is implements a generic type or interface</summary>
		/// <param name="type">Type of the inspected instance (ex: <c>List&lt;string&gt;</c>)</param>
		/// <param name="genericType">Type of the generic interface (ex: <c>IList&lt;T&gt;</c>)</param>
		/// <returns><see langword="true"/> if the type implements the generic interface</returns>
		/// <remarks>This method is required because <c>new List&lt;string>().GetType().IsAssignableTo(IList&lt;>)</c> returns false (the types implements <c>IList&lt;string&gt;</c> which is not the same as <c>IList&lt;&gt;</c>)</remarks>
		/// <example><code>
		/// typeof(List&lt;string&gt;).IsGenericInstanceOf(typeof(IList&lt;&gt;)) == true
		/// typeof(HashSet&lt;string&gt;).IsGenericInstanceOf(typeof(IList&lt;&gt;)) == false
		/// </code></example>
		[Pure]
		public static bool IsGenericInstanceOf([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, Type genericType)
		{
			return FindGenericType(type, genericType) != null;
		}

		/// <summary>Tests if a type is implements a generic type or interface</summary>
		/// <param name="type">Type of the inspected instance (ex: <c>List&lt;string&gt;</c>)</param>
		/// <param name="genericType">Type of the generic interface (ex: <c>IList&lt;&gt;</c>)</param>
		/// <param name="closedType">Receives the corresponding closed generic type (ex: <c>IList&lt;string&gt;</c>)</param>
		/// <returns><see langword="true"/> if the type implements the generic interface</returns>
		/// <remarks>This method is required because <c>new List&lt;string>().GetType().IsAssignableTo(IList&lt;>)</c> returns false (the types implements <c>IList&lt;string&gt;</c> which is not the same as <c>IList&lt;&gt;</c>)</remarks>
		/// <example><code>
		/// typeof(List&lt;string&gt;).IsGenericInstanceOf(typeof(IList&lt;&gt;), out var closedType) == true &amp;&amp; closedType == typeof(IList&lt;string&gt;)
		/// typeof(HashSet&lt;string&gt;).IsGenericInstanceOf(typeof(IList&lt;&gt;)) == false
		/// </code></example>
		[Pure]
		public static bool IsGenericInstanceOf([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, Type genericType, [MaybeNullWhen(false)] out Type closedType)
		{
			closedType = FindGenericType(type, genericType);
			return closedType != null;
		}

		/// <summary>Tests if a type implements <see cref="IEnumerable{T}"/> and returns the type of the enumerated elements when this is the case</summary>
		/// <param name="type">Type of the inspected instance (ex: <c>List&lt;string&gt;</c>)</param>
		/// <param name="elementType">When the method returns <see langword="true"/>, receives the types of the enumerated elements</param>
		/// <returns><see langword="true"/> if the type implements <see cref="IEnumerable{T}"/>; otherwise, <see langword="false"/>.</returns>
		/// <example><code>
		/// typeof(List&lt;string&gt;).IsEnumerableType(out var elementType) == true; elementType == typeof(string)
		/// typeof(Dictionary&lt;int, string&gt;).IsEnumerableType(out var elementType) == true; elementType == typeof(KeyValuePair&lt;int, string&gt;)
		/// typeof(string).IsEnumerableType(out var elementType) == true; elementType == typeof(char)
		/// typeof(int).IsEnumerableType(out var elementType) == false;
		/// </code></example>
		public static bool IsEnumerableType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, [MaybeNullWhen(false)] out Type elementType)
		{
			if (type.IsArray)
			{
				elementType = type.GetElementType();
				return elementType != null;
			}

			var enumerableType = FindGenericType(type, typeof(IEnumerable<>));
			if (enumerableType != null)
			{
				elementType = enumerableType.GenericTypeArguments[0];
				return true;
			}

			elementType = null;
			return false;
		}

		/// <summary>Tests if a type implements <see cref="IDictionary{TKey,TValue}"/> and returns the types of the keys and values when this is the case</summary>
		/// <param name="type">Type of the inspected instance (ex: <c>List&lt;string&gt;</c>)</param>
		/// <param name="keyType">When the method returns <see langword="true"/>, receives the types of the keys</param>
		/// <param name="valueType">When the method returns <see langword="true"/>, receives the types of the values</param>
		/// <returns><see langword="true"/> if the type implements <see cref="IDictionary{TKey, TValue}"/>; otherwise, <see langword="false"/>.</returns>
		/// <example><code>
		/// typeof(Dictionary&lt;int, string&gt;).IsDictionaryType(out var keyType, out var valueType) == true; keyType == typeof(int); valueType == typeof(string)
		/// typeof(List&lt;KeyValuePair&lt;int, string&gt;&gt;).IsDictionaryType(...) == false
		/// </code></example>
		public static bool IsDictionaryType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, [MaybeNullWhen(false)] out Type keyType, [MaybeNullWhen(false)] out Type valueType)
		{
			var dictionaryType = FindGenericType(type, typeof(IDictionary<,>));
			if (dictionaryType != null)
			{
				keyType = dictionaryType.GenericTypeArguments[0];
				valueType = dictionaryType.GenericTypeArguments[1];
				return true;
			}

			keyType = null;
			valueType = null;
			return false;
		}

		/// <summary>Returns the closed version of the generic interface implemented by a type</summary>
		/// <param name="type">Type of the inspected instance (ex: List&lt;string&gt;)</param>
		/// <param name="genericType">Type of the generic interface (ex: IList&lt;&gt;)</param>
		/// <returns>Closed interface (ex: IList&lt;string&gt;), or <see langword="null"/> if the type does not implement this generic interface</returns>
		[Pure]
		public static Type? FindGenericType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, Type genericType)
		{
			Contract.NotNull(genericType);

			// we want to return the closed version of the generic type implemented by a class
			// ex: if type == typeof(List<string>) and genericType == typeof(IList<>), we want to return typeof(IList<string>)

			// is it already the exact type?
			if (type.IsSameGenericType(genericType)) return type;

			if (genericType.IsInterface)
			{ // we are looking for an interface...
				foreach (var t in type.GetInterfaces())
				{
					// warning: GetInterfaces() will return the "closed" generic types, which are not equal to typeof(IFoo<>)
					// => we need to compare them using GetGenericTypeDefinition()
					if (t.IsSameGenericType(genericType)) return t;
				}
				return null;
			}
			else
			{ // we are looking for an abstract class
				Type? parent = type.BaseType;
				while (parent != null && typeof(object) != parent)
				{
					if (parent.IsSameGenericType(genericType))
					{
						return parent;
					}
					parent = parent.BaseType;
				}
				return null;
			}
		}

		/// <summary>Tests if a generic type has the same generic definition as another type (ie: <c>Dictionary&lt;string, int> ~= Dictionary&lt;,></c></summary>
		/// <param name="type">Type to inspect (ex: IDictionary&lt;string, string&gt;)</param>
		/// <param name="genericType">Generic type (ex: IDictionary&lt;,&gt;)</param>
		/// <returns><see langword="true"/> if the types are equivalent; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public static bool IsSameGenericType(this Type type, Type? genericType)
		{
			return type == genericType || (genericType != null && type.IsGenericType && genericType == type.GetGenericTypeDefinition());
		}

		/// <summary>Tests if a type is "concrete", meaning it is not a class or struct that is not abstract</summary>
		/// <param name="type">Type to inspect</param>
		/// <returns><see langword="false"/> if the type is an interface an abstract class, or <see cref="System.Object"/>; otherwise, <see langword="true"/></returns>
		/// <remarks>This includes both sealed class, and non-sealed but non-abstract class.</remarks>
		[Pure]
		public static bool IsConcrete(this Type type)
		{
			return typeof(object) != type && !type.IsInterface && !type.IsAbstract;
		}

		/// <summary>Tests if a type is a concrete implementation of a specific interface</summary>
		/// <returns><see langword="false"/> if the type does not implement this interface, is an itself an interface, an abstract class, or <see cref="System.Object"/>; otherwise, <see langword="true"/></returns>
		[Pure]
		public static bool IsConcreteImplementationOfInterface(this Type type, Type interfaceType)
		{
			return type.IsConcrete() && interfaceType.IsAssignableFrom(type);
		}

		/// <summary>Tests if a type is nullable (ie: 'int?' or 'DateTime?')</summary>
		[Pure]
		public static bool IsNullableType(this Type type)
		{
			//note: we should check if it is generic, and if its generic definition is 'Nullable<>' but this is too slow at runtime
			// CORRECT but SLOW: return type.IsGenericType && type.Name == "Nullable`1" && typeof(Nullable<>) == type.GetGenericTypeDefinition();
			// => we exploit the fact that the 'Name' of typeof(T?) or typeof(Nullable<T>) is always "Nullable`1", and that it is not possible to derive from a struct...
			return type.Name == "Nullable`1";
		}

		/// <summary>Tests if an instance of type can be null (Reference Type, Nullable Type)</summary>
		[Pure]
		public static bool CanAssignNull(this Type type)
		{
			return !type.IsValueType || IsNullableType(type);
		}

		/// <summary>Tests if a type is an anonymous type generated by the compiler (ie: <c>new { foo = ..., bar = .... }</c>)</summary>
		[Pure]
		public static bool IsAnonymousType(this Type type)
		{
			// There is no direct test to detect anonymous types (they are just like any other type)
			// The only distinctive signs are: (cf http://stackoverflow.com/questions/315146/anonymous-types-are-there-any-distingushing-characteristics )
			// * Must be a class
			// * must have the attribute [CompilerGeneratedAttribute]
			// * Must be sealed Elle est sealed
			// * Must derive from System.Object
			// * Must be generic, with has many type arguments than properties
			// * Must have a single ctor, with the same number of parameters
			// * Must override Equals, GetHashcode et ToString(), and nothing else
			// * Name will be "<>f_AnonymousType...." in C#, and "VB$AnonymousType..." in VB.NET

			return type.IsClass
				&& type.IsSealed
				&& type.IsGenericType
				&& typeof(object) == type.BaseType
				&& (type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal) || type.Name.StartsWith("VB$AnonymousType", StringComparison.Ordinal));
				//&& type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false);
		}

		/// <summary>Returns the type returned by a 'Task-like' type (<c>Task</c>, <c>Task&gt;T&lt;</c>, <c>ValueTask&lt;T&gt;</c>, ...), or <see langword="null"/> if it is not a task.</summary>
		[Pure]
		public static Type? GetTaskLikeType(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
			this Type taskLike
		)
		{
			if (taskLike == typeof(System.Threading.Tasks.Task)) return typeof(void);
			if (taskLike == typeof(System.Threading.Tasks.ValueTask)) return typeof(void);

			var t = taskLike.FindGenericType(typeof(System.Threading.Tasks.Task<>));
			if (t != null) return t.GenericTypeArguments[0];

			t = taskLike.FindGenericType(typeof(System.Threading.Tasks.ValueTask<>));
			if (t != null) return t.GenericTypeArguments[0];

			return null;
		}

		/// <summary>Tests if a property is a custom indexer (ex: "get_Item[string key]")</summary>
		[Pure]
		public static bool IsCustomIndexer(this PropertyInfo prop)
		{
			// indexers have at least one argument
			return prop.GetIndexParameters().Length != 0;
		}

		#endregion

	}

}
