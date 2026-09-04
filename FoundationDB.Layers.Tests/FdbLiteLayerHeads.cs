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

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Layers.Blobs.Tests;
	using FoundationDB.Layers.Collections.Tests;
	using FoundationDB.Layers.Counters.Tests;
	using FoundationDB.Layers.Documents.Tests;
	using FoundationDB.Layers.Experimental.Indexing.Tests;
	using FoundationDB.Layers.FullText.Tests;
	using FoundationDB.Layers.Interning.Tests;
	using FoundationDB.Layers.Tables.Tests;

	/// <summary>Runs the suite against the persistent FdbLite provider (heap engine, no Docker, no native client).</summary>
	[TestFixture]
	public sealed class BlobFactsFdbLiteFacts : BlobFacts
	{

		private FdbLiteTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against the persistent FdbLite provider (heap engine, no Docker, no native client).</summary>
	[TestFixture]
	public sealed class CounterFactsFdbLiteFacts : CounterFacts
	{

		private FdbLiteTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against the persistent FdbLite provider (heap engine, no Docker, no native client).</summary>
	[TestFixture]
	public sealed class IndexingFactsFdbLiteFacts : IndexingFacts
	{

		private FdbLiteTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against the persistent FdbLite provider (heap engine, no Docker, no native client).</summary>
	[TestFixture]
	public sealed class MapFactsFdbLiteFacts : MapFacts
	{

		private FdbLiteTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against the persistent FdbLite provider (heap engine, no Docker, no native client).</summary>
	[TestFixture]
	public sealed class MultiMapFactsFdbLiteFacts : MultiMapFacts
	{

		private FdbLiteTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against the persistent FdbLite provider (heap engine, no Docker, no native client).</summary>
	[TestFixture]
	public sealed class QueuesFactsFdbLiteFacts : QueuesFacts
	{

		private FdbLiteTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against the persistent FdbLite provider (heap engine, no Docker, no native client).</summary>
	[TestFixture]
	public sealed class RankedTestFactsFdbLiteFacts : RankedTestFacts
	{

		private FdbLiteTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against the persistent FdbLite provider (heap engine, no Docker, no native client).</summary>
	[TestFixture]
	public sealed class StringInternFactsFdbLiteFacts : StringInternFacts
	{

		private FdbLiteTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against the persistent FdbLite provider (heap engine, no Docker, no native client).</summary>
	[TestFixture]
	public sealed class VectorFactsFdbLiteFacts : VectorFacts
	{

		private FdbLiteTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against the persistent FdbLite provider (heap engine, no Docker, no native client).</summary>
	[TestFixture]
	public sealed class DocumentCollectionFactsFdbLiteFacts : DocumentCollectionFacts
	{

		private FdbLiteTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against the persistent FdbLite provider (heap engine, no Docker, no native client).</summary>
	[TestFixture]
	public sealed class FdbTextIndexFactsFdbLiteFacts : FdbTextIndexFacts
	{

		private FdbLiteTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

}
