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
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THE
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace SnowBank.Benchmarks
{
	using System.Buffers;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Text.Encodings.Web;
	using System.Text.Json;
	using System.Text.Json.Nodes;
	using JsonValue = SnowBank.Data.Json.JsonValue;
	using BenchmarkDotNet.Attributes;
	using BenchmarkDotNet.Configs;
	using SnowBank.Data.Json;

	/// <summary>Compares CrystalJson with System.Text.Json, layer by layer, on the same document and the same values.</summary>
	/// <remarks>
	/// <para>Each category isolates one layer, so that a difference can be attributed:</para>
	/// <list type="bullet">
	/// <item><b>ScalarWrite</b>: one value written per operation into a reusable writer. CrystalJson writes UTF-16 characters into its
	/// buffer and flushes to <see cref="TextWriter.Null"/>; System.Text.Json writes UTF-8 bytes into an <see cref="ArrayBufferWriter{T}"/>.
	/// Both are their native in-memory form, so this measures the value formatting, not the transcoding.</item>
	/// <item><b>SerializeObject</b>: a whole document from a CLR object, to a string and to UTF-8 bytes. The UTF-8 rows include the
	/// transcoding for CrystalJson, which formats characters first.</item>
	/// <item><b>ParseDom</b>: the text to a document object model. CrystalJson always builds its DOM; the closest System.Text.Json
	/// equivalents are the mutable <see cref="JsonNode"/> and the read-only, pooled <see cref="JsonDocument"/>.</item>
	/// <item><b>Deserialize</b>: the text to a CLR object. CrystalJson goes through its DOM; System.Text.Json goes through its reader.</item>
	/// <item><b>TokenScan</b>: the floor of System.Text.Json, a <see cref="Utf8JsonReader"/> walking every token without building anything.
	/// CrystalJson has no public forward-only reader, so this row has no counterpart.</item>
	/// </list>
	/// <para>System.Text.Json uses <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> so that non-ASCII text is written as is, like CrystalJson does.</para>
	/// </remarks>
	[MemoryDiagnoser]
	[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
	[CategoriesColumn]
	public class JsonCompareBenchmarks
	{

		#region Model...

		public sealed class Order
		{
			public Guid Id { get; set; }
			public string Number { get; set; } = "";
			public DateTime Created { get; set; }
			public DateTimeOffset Updated { get; set; }
			public string Customer { get; set; } = "";
			public double Total { get; set; }
			public bool Paid { get; set; }
			public List<OrderLine> Lines { get; set; } = [ ];
			public Dictionary<string, string> Tags { get; set; } = [ ];
		}

		public sealed class OrderLine
		{
			public string Sku { get; set; } = "";
			public int Quantity { get; set; }
			public double Price { get; set; }
			public string? Note { get; set; }
		}

		private static Order CreateOrder() => new()
		{
			Id = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"),
			Number = "ORD-2025-000123",
			Created = new DateTime(2025, 6, 16, 17, 46, 17, 934, DateTimeKind.Utc),
			Updated = new DateTimeOffset(2025, 6, 17, 9, 12, 0, TimeSpan.FromHours(2)),
			Customer = "Société Générale de Façades",
			Total = 1234.56,
			Paid = true,
			Lines =
			[
				new() { Sku = "SKU-0001", Quantity = 2, Price = 19.99, Note = null },
				new() { Sku = "SKU-0002", Quantity = 1, Price = 199.00, Note = "gift wrap" },
				new() { Sku = "SKU-0003", Quantity = 12, Price = 3.50, Note = "carton of \"twelve\"" },
				new() { Sku = "SKU-0004", Quantity = 1, Price = 899.90, Note = null },
				new() { Sku = "SKU-0005", Quantity = 3, Price = 24.75, Note = "délai 48h" },
			],
			Tags = new() { ["channel"] = "web", ["region"] = "fr-idf", ["priority"] = "normal" },
		};

		#endregion

		private const int BatchSize = 1000;

		private static readonly JsonSerializerOptions StjOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
		private static readonly JsonWriterOptions StjWriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, SkipValidation = true };

		private Order Model = null!;
		private string Text = null!;
		private byte[] Utf8 = null!;

		private ArrayBufferWriter<byte> StjBuffer = null!;
		private Utf8JsonWriter StjWriter = null!;

		private static readonly string SampleString = "Société Générale de \"Façades\"";
		private static readonly Guid SampleGuid = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
		private static readonly DateTime SampleDate = new(2025, 6, 16, 17, 46, 17, 934, DateTimeKind.Utc);

		[GlobalSetup]
		public void Setup()
		{
			this.Model = CreateOrder();
			// the same compact text for every parser, produced once
			this.Text = JsonSerializer.Serialize(this.Model, StjOptions);
			this.Utf8 = Encoding.UTF8.GetBytes(this.Text);
			this.StjBuffer = new ArrayBufferWriter<byte>(64 * 1024);
			this.StjWriter = new Utf8JsonWriter(this.StjBuffer, StjWriterOptions);
			this.CrystalWriter = new CrystalJsonWriter(TextWriter.Null, 0, CrystalJsonSettings.JsonCompact, CrystalJson.DefaultResolver);

			// both sides must agree on the document before anything is compared
			var crystal = CrystalJson.Parse(this.Text);
			if (crystal["Number"].ToString() != this.Model.Number || crystal["Lines"].AsArray().Count != 5) throw new InvalidOperationException("CrystalJson did not parse the sample");
		}

		// one writer for the whole run, flushed after each batch: a fresh writer per batch would grow its buffer from 1 KB to 64 KB every time
		private CrystalJsonWriter CrystalWriter = null!;

		private Utf8JsonWriter ResetStjWriter()
		{
			this.StjBuffer.ResetWrittenCount();
			this.StjWriter.Reset(this.StjBuffer);
			return this.StjWriter;
		}

		#region ScalarWrite...

		[Benchmark(Baseline = true, OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "Int32")]
		public void Crystal_Write_Int32()
		{
			var writer = this.CrystalWriter;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(1234567 + i);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "Int32")]
		public void Stj_Write_Int32()
		{
			var writer = ResetStjWriter();
			for (int i = 0; i < BatchSize; i++) writer.WriteNumberValue(1234567 + i);
			writer.Flush();
		}

		[Benchmark(Baseline = true, OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "Int64")]
		public void Crystal_Write_Int64()
		{
			var writer = this.CrystalWriter;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(1234567890123456L + i);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "Int64")]
		public void Stj_Write_Int64()
		{
			var writer = ResetStjWriter();
			for (int i = 0; i < BatchSize; i++) writer.WriteNumberValue(1234567890123456L + i);
			writer.Flush();
		}

		[Benchmark(Baseline = true, OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "Double")]
		public void Crystal_Write_Double()
		{
			var writer = this.CrystalWriter;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(1234.5678 + i);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "Double")]
		public void Stj_Write_Double()
		{
			var writer = ResetStjWriter();
			for (int i = 0; i < BatchSize; i++) writer.WriteNumberValue(1234.5678 + i);
			writer.Flush();
		}

		[Benchmark(Baseline = true, OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "String")]
		public void Crystal_Write_String()
		{
			var writer = this.CrystalWriter;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(SampleString);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "String")]
		public void Stj_Write_String()
		{
			var writer = ResetStjWriter();
			for (int i = 0; i < BatchSize; i++) writer.WriteStringValue(SampleString);
			writer.Flush();
		}

		[Benchmark(Baseline = true, OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "Guid")]
		public void Crystal_Write_Guid()
		{
			var writer = this.CrystalWriter;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(SampleGuid);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "Guid")]
		public void Stj_Write_Guid()
		{
			var writer = ResetStjWriter();
			for (int i = 0; i < BatchSize; i++) writer.WriteStringValue(SampleGuid);
			writer.Flush();
		}

		[Benchmark(Baseline = true, OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "DateTime")]
		public void Crystal_Write_DateTime()
		{
			var writer = this.CrystalWriter;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(SampleDate);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize), BenchmarkCategory("ScalarWrite", "DateTime")]
		public void Stj_Write_DateTime()
		{
			var writer = ResetStjWriter();
			for (int i = 0; i < BatchSize; i++) writer.WriteStringValue(SampleDate);
			writer.Flush();
		}

		#endregion

		#region SerializeObject...

		[Benchmark(Baseline = true), BenchmarkCategory("SerializeObject", "ToString")]
		public string Crystal_Serialize_ToString() => CrystalJson.Serialize(this.Model, CrystalJsonSettings.JsonCompact);

		[Benchmark, BenchmarkCategory("SerializeObject", "ToString")]
		public string Stj_Serialize_ToString() => JsonSerializer.Serialize(this.Model, StjOptions);

		[Benchmark(Baseline = true), BenchmarkCategory("SerializeObject", "ToUtf8")]
		public int Crystal_Serialize_ToUtf8() => CrystalJson.ToSlice(this.Model, CrystalJsonSettings.JsonCompact).Count;

		[Benchmark, BenchmarkCategory("SerializeObject", "ToUtf8")]
		public int Stj_Serialize_ToUtf8() => JsonSerializer.SerializeToUtf8Bytes(this.Model, StjOptions).Length;

		#endregion

		#region ParseDom...

		[Benchmark(Baseline = true), BenchmarkCategory("ParseDom", "FromString")]
		public JsonValue Crystal_Parse_FromString() => CrystalJson.Parse(this.Text);

		[Benchmark, BenchmarkCategory("ParseDom", "FromString")]
		public JsonNode? Stj_JsonNode_FromString() => JsonNode.Parse(this.Text);

		[Benchmark, BenchmarkCategory("ParseDom", "FromString")]
		public int Stj_JsonDocument_FromString()
		{
			using var doc = JsonDocument.Parse(this.Text);
			return doc.RootElement.GetProperty("Lines").GetArrayLength();
		}

		[Benchmark(Baseline = true), BenchmarkCategory("ParseDom", "FromUtf8")]
		public JsonValue Crystal_Parse_FromUtf8() => CrystalJson.Parse(this.Utf8.AsSpan());

		[Benchmark, BenchmarkCategory("ParseDom", "FromUtf8")]
		public JsonNode? Stj_JsonNode_FromUtf8() => JsonNode.Parse(this.Utf8.AsSpan());

		[Benchmark, BenchmarkCategory("ParseDom", "FromUtf8")]
		public int Stj_JsonDocument_FromUtf8()
		{
			using var doc = JsonDocument.Parse(this.Utf8.AsMemory());
			return doc.RootElement.GetProperty("Lines").GetArrayLength();
		}

		#endregion

		#region Deserialize...

		[Benchmark(Baseline = true), BenchmarkCategory("Deserialize", "FromString")]
		public Order Crystal_Deserialize_FromString() => CrystalJson.Deserialize<Order>(this.Text);

		[Benchmark, BenchmarkCategory("Deserialize", "FromString")]
		public Order? Stj_Deserialize_FromString() => JsonSerializer.Deserialize<Order>(this.Text, StjOptions);

		[Benchmark(Baseline = true), BenchmarkCategory("Deserialize", "FromUtf8")]
		public Order Crystal_Deserialize_FromUtf8() => CrystalJson.Parse(this.Utf8.AsSpan()).Required<Order>();

		[Benchmark, BenchmarkCategory("Deserialize", "FromUtf8")]
		public Order? Stj_Deserialize_FromUtf8() => JsonSerializer.Deserialize<Order>(this.Utf8.AsSpan(), StjOptions);

		#endregion

		#region TokenScan...

		[Benchmark, BenchmarkCategory("TokenScan")]
		public int Stj_Utf8JsonReader_Scan()
		{
			var reader = new Utf8JsonReader(this.Utf8);
			int tokens = 0;
			while (reader.Read()) tokens++;
			return tokens;
		}

		#endregion

	}

}
