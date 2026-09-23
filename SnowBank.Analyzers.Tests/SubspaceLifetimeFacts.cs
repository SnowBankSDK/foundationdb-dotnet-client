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

namespace SnowBank.Analyzers.Tests
{

	[TestFixture]
	public class SubspaceLifetimeFacts
	{

		private const string Usings = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using FoundationDB.Client;

			""";

		/// <summary>A layer whose State class holds a subspace by design.</summary>
		private const string Layer = """
			class MyLayer : IFdbLayer<MyLayer.State>
			{
				public string Name => "MyLayer";
				public ValueTask<State> Resolve(IFdbReadOnlyTransaction tr) => throw new NotSupportedException();
				public sealed class State
				{
					public IKeySubspace? Subspace { get; set; }
					public IKeySubspace? Field;
				}
			}

			""";

		private static DiagnosticResult Stored(int location, string name) => Verify.Diagnostic(FdbDescriptors.SubspaceStoredAcrossTransactions).WithLocation(location).WithArguments(name);

		private static DiagnosticResult Returned(int location) => Verify.Diagnostic(FdbDescriptors.SubspaceReturnedFromHandler).WithLocation(location);

		#region FDB1001...

		[Test]
		public Task Reports_A_Subspace_Field() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + """
			class Store
			{
				private IKeySubspace? {|#0:subspace|};
			}
			""", Stored(0, "subspace"));

		[Test]
		public Task Reports_A_Directory_Subspace_Auto_Property_And_A_Static() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + """
			class Store
			{
				public FdbDirectorySubspace? {|#0:Dir|} { get; set; }
				private static IKeySubspace? {|#1:s_root|};
			}
			""", Stored(0, "Dir"), Stored(1, "s_root"));

		[Test]
		public Task Reports_A_Singleton_Registration() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + """
			using Microsoft.Extensions.DependencyInjection;
			class Startup
			{
				void Configure(IServiceCollection services, IKeySubspace subspace) => services.{|#0:AddSingleton<IKeySubspace>|}(subspace);
			}
			""", TestReferences.WithAspNetCore, OutputKind.DynamicallyLinkedLibrary, Stored(0, "AddSingleton<IKeySubspace>"));

		[Test]
		public Task Reports_A_Layer_State_Kept_Outside_The_Layer() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + Layer + """
			class Cache
			{
				public MyLayer.State? {|#0:Cached|} { get; set; }
			}
			""", Stored(0, "Cached"));

		[Test]
		public Task Ignores_The_State_Of_A_Layer() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + Layer);

		[Test]
		public Task Ignores_A_Key_Struct() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + """
			readonly struct MyKey : IFdbKey
			{
				public readonly IKeySubspace? Subspace;
				public IKeySubspace? GetSubspace() => this.Subspace;
				public bool FastEqualTo<TOtherKey>(in TOtherKey key) where TOtherKey : struct, IFdbKey => throw new NotSupportedException();
				public int FastCompareTo<TOtherKey>(in TOtherKey key) where TOtherKey : struct, IFdbKey => throw new NotSupportedException();
				public bool TryGetSpan(out ReadOnlySpan<byte> span) => throw new NotSupportedException();
				public bool TryGetSizeHint(out int sizeHint) => throw new NotSupportedException();
				public bool TryEncode(Span<byte> destination, out int bytesWritten) => throw new NotSupportedException();
				public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => throw new NotSupportedException();
				public string ToString(string? format, IFormatProvider? formatProvider) => throw new NotSupportedException();
				public bool Equals(FdbRawKey other) => throw new NotSupportedException();
				public int CompareTo(FdbRawKey other) => throw new NotSupportedException();
				public bool Equals(Slice other) => throw new NotSupportedException();
				public int CompareTo(Slice other) => throw new NotSupportedException();
				public bool Equals(IFdbKey? other) => throw new NotSupportedException();
				public int CompareTo(object? obj) => throw new NotSupportedException();
				public bool Equals(ReadOnlySpan<byte> other) => throw new NotSupportedException();
				public int CompareTo(ReadOnlySpan<byte> other) => throw new NotSupportedException();
				public bool Contains(ReadOnlySpan<byte> key) => throw new NotSupportedException();
				public bool Contains<TOtherKey>(in TOtherKey key) where TOtherKey : struct, IFdbKey => throw new NotSupportedException();
			}
			""");

		[Test]
		public Task Ignores_A_Manual_Prefix() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + """
			class Store
			{
				private readonly IKeySubspace fixedArea = KeySubspace.FromKey(Slice.FromByteString("\x01"));
				public IKeySubspace Metadata { get; } = KeySubspace.FromKey(Slice.FromByteString("\xFF/metadata"));
			}
			""");

		[Test]
		public Task Ignores_A_Computed_Property() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + """
			class Store
			{
				public IKeySubspace Subspace => throw new NotSupportedException();
			}
			""");

		#endregion

		#region FDB1002...

		[Test]
		public Task Reports_A_Subspace_Returned_From_ReadWriteAsync() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + """
			class C
			{
				async Task M(IFdbDatabase db, CancellationToken ct)
				{
					var s = await db.ReadWriteAsync(tr => {|#0:db.Root["a"].CreateOrOpenAsync(tr)|}, ct);
				}
			}
			""", Returned(0));

		[Test]
		public Task Reports_A_Subspace_Returned_From_A_Block_Body() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + """
			class C
			{
				async Task M(IFdbDatabase db, CancellationToken ct)
				{
					var s = await db.ReadWriteAsync(async tr =>
					{
						var sub = await db.Root["a"].CreateOrOpenAsync(tr);
						return {|#0:sub|};
					}, ct);
				}
			}
			""", Returned(0));

		[Test]
		public Task Reports_A_Layer_State_Returned_From_ReadAsync() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + Layer + """
			class C
			{
				async Task M(IFdbDatabase db, MyLayer layer, CancellationToken ct)
				{
					var s = await db.ReadAsync(tr => {|#0:layer.Resolve(tr).AsTask()|}, ct);
				}
			}
			""", Returned(0));

		[Test]
		public Task Ignores_WriteAsync() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + """
			class C
			{
				Task M(IFdbDatabase db, CancellationToken ct) => db.WriteAsync(tr => db.Root["a"].CreateOrOpenAsync(tr), ct);
			}
			""");

		[Test]
		public Task Ignores_A_Value_Result() => Verify.Analyzer<SubspaceLifetimeAnalyzer>(Usings + """
			class C
			{
				Task<Slice> M(IFdbDatabase db, Slice key, CancellationToken ct) => db.ReadAsync(tr => tr.GetAsync(key), ct);
			}
			""");

		#endregion

	}
}
