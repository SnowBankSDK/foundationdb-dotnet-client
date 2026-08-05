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

namespace SnowBank.Data.Json
{
	/// <summary>Collection that aggregates multiple smaller collections into a single chain</summary>
	[PublicAPI]
	public sealed class CrystalJsonTypeResolverChain : ICrystalJsonTypeResolver
	{

		/// <summary>Creates a new empty chain of type collections</summary>
		[Pure]
		public static CrystalJsonTypeResolverChain Create() => new();

		/// <summary>Creates a new chain with the specified resolver as the root</summary>
		/// <param name="resolver">First resolver in the chain</param>
		public static CrystalJsonTypeResolverChain Create(ICrystalJsonTypeResolver resolver)
		{
			Contract.NotNull(resolver);

			var chain = new CrystalJsonTypeResolverChain();
			chain.Append(resolver);
			return chain;
		}

		/// <summary>Creates a new chain with the specified type collections</summary>
		/// <param name="resolvers">List of resolvers, in the same order as they would be appended to the chain using <see cref="Append"/></param>
		[Pure]
		public static CrystalJsonTypeResolverChain Create(ReadOnlySpan<ICrystalJsonTypeResolver> resolvers)
		{
			var chain = new CrystalJsonTypeResolverChain();
			foreach (var collection in resolvers)
			{
				chain = chain.Append(collection);
			}
			return chain;
		}

		/// <summary>Link in the chain</summary>
		[DebuggerDisplay("Collection={Resolver.GetType().FullName}")]
		private sealed class ChainLink
		{

			/// <summary>Collection of this link</summary>
			public required ICrystalJsonTypeResolver Resolver { get; init; }

			/// <summary>Next collection in the list, or <c>null</c> if this is the end of the list</summary>
			public required ChainLink? Next { get; init; }
		
		}

		/// <summary>Head of the chain</summary>
		private ChainLink? Head { get; set; }

#if NET9_0_OR_GREATER
		private System.Threading.Lock Lock { get; } = new();
#else
		private object Lock { get; } = new();
#endif

		/// <summary>Remove all resolvers in this chain</summary>
		public void Clear()
		{
			lock (this.Lock)
			{
				this.Head = null;
			}
		}

		/// <summary>Adds a new type collection to this chain</summary>
		/// <param name="resolver">Collection of types</param>
		/// <returns>The same chain instance</returns>
		/// <remarks>The order of appends is important for conflict resolution in case the same type is present in multiple collections. Collections are evaluated to last to first, <paramref name="resolver"/> will have priority against any previously added collection.</remarks>
		public CrystalJsonTypeResolverChain Append(ICrystalJsonTypeResolver resolver)
		{
			Contract.NotNull(resolver);

			lock (this.Lock)
			{
				// check that the chain does not already contain this resolver
				var current = this.Head;
				while (current != null)
				{
					if (current.Resolver == resolver) throw new InvalidOperationException("The given resolver is already present in this chain");
					current = current.Next;
				}

				this.Head = new() { Resolver = resolver, Next = this.Head };
			}
			return this;
		}

		/// <summary>Tests if this chain already contains the given resolver</summary>
		public bool Contains(ICrystalJsonTypeResolver resolver)
		{
			lock (this.Lock)
			{
				var current = this.Head;
				while (current != null)
				{
					if (current.Resolver == resolver) return true;
					current = current.Next;
				}
				return false;
			}
		}

		/// <inheritdoc />
		public bool TryGetConverterFor(Type type, [MaybeNullWhen(false)] out IJsonConverter converter)
		{
			var current = this.Head;
			while (current != null)
			{
				if (current.Resolver.TryGetConverterFor(type, out converter))
				{
					return true;
				}

				current = current.Next;
			}

			converter = null;
			return false;
		}

		/// <inheritdoc />
		public bool TryGetConverterFor<T>([MaybeNullWhen(false)] out IJsonConverter<T> converter)
		{
			var current = this.Head;
			while(current != null)
			{
				if (current.Resolver.TryGetConverterFor<T>(out converter))
				{
					return true;
				}
				current = current.Next;
			}
			converter = null;
			return false;
		}

		public bool TryResolveTypeDefinition(Type type, [MaybeNullWhen(false)] out CrystalJsonTypeDefinition definition)
		{
			var current = this.Head;
			while (current != null)
			{
				if (current.Resolver.TryResolveTypeDefinition(type, out definition))
				{
					return true;
				}

				current = current.Next;
			}

			definition = null;
			return false;
		}

		public bool TryResolveTypeDefinition<T>([MaybeNullWhen(false)] out CrystalJsonTypeDefinition definition)
		{
			var current = this.Head;
			while (current != null)
			{
				if (current.Resolver.TryResolveTypeDefinition<T>(out definition))
				{
					return true;
				}

				current = current.Next;
			}

			definition = null;
			return false;
		}

	}

	internal sealed class RuntimeJsonConverter<T> : IJsonConverter<T>
	{

		/// <summary>Default converter for instances of type <typeparamref name="T"/></summary>
		public static readonly IJsonConverter<T> Default = new RuntimeJsonConverter<T>();

		public static IJsonConverter<T> GetInstance() => RuntimeJsonConverter<T>.Default;

		public CrystalJsonTypeDefinition? GetDefinition() => CrystalJson.DefaultResolver.ResolveJsonType(typeof(T));

		/// <inheritdoc />
		public Type GetTargetType() => typeof(T);

		private CrystalJsonTypeVisitor? CachedVisitor;

		void IJsonConverter.Serialize(object? instance, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (instance is null)
			{
				writer.WriteNull();
				return;
			}

			var visitor = this.CachedVisitor ??= CrystalJsonVisitor.GetVisitorForType(typeof(T));
			if (visitor == null) throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeType(typeof(T));
			visitor(instance, typeof(T), instance.GetType(), writer);
		}

		/// <inheritdoc />
		public void Serialize(CrystalJsonWriter writer, T? instance)
		{
			if (instance is null)
			{
				writer.WriteNull();
				return;
			}

			var visitor = this.CachedVisitor ??= CrystalJsonVisitor.GetVisitorForType(typeof(T));
			if (visitor == null) throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeType(typeof(T));
			visitor(instance, typeof(T), instance.GetType(), writer);
		}

		/// <inheritdoc />
		object? IJsonConverter.BindJsonValue(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			return Unpack(value, resolver);
		}

		/// <inheritdoc />
		public T Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			if (value.IsNullOrMissing()) return default(T)!;
			return (T?) CrystalJson.DefaultResolver.BindJsonValue(typeof(T), value)!;
		}

		/// <inheritdoc />
		public JsonValue Pack(ref CrystalJsonPackContext context, T instance)
		{
			// the DOM writer carries its own depth and visited-object guards for the reflection walk it performs
			return CrystalJsonDomWriter.Create(context.Settings, context.Resolver).ParseObject<T>(instance);
		}

		/// <inheritdoc />
		public bool TryMapMemberToPropertyName(string memberName, [MaybeNullWhen(false)] out string propertyName)
		{
			if (!CrystalJson.DefaultResolver.TryResolveMember<T>(memberName, out var def))
			{
				propertyName = null;
				return false;
			}
			propertyName = def.Name;
			return true;
		}

		/// <inheritdoc />
		public bool TryMapPropertyToMemberName(string propertyName, [MaybeNullWhen(false)] out string memberName)
		{
			if (CrystalJson.DefaultResolver.TryResolveTypeDefinition<T>(out var def))
			{
				foreach (var member in def.Members)
				{
					if (member.Name == propertyName)
					{
						memberName = member.OriginalName;
						return true;
					}
				}
			}

			memberName = null;
			return false;
		}

	}

}
