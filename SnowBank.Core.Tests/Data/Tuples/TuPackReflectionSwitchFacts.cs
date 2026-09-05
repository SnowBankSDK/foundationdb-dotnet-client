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

namespace SnowBank.Data.Tuples.Tests
{
	using SnowBank.Data.Tuples.Binary;

	/// <summary>Tests for the <c>TuPack.IsReflectionSupported</c> feature switch: with reflection disabled, an exotic
	/// tuple element type (one with no compile-time fast path) must fail with a clear <see cref="NotSupportedException"/>
	/// instead of silently building a reflection-based encoder that trimming may have removed.</summary>
	/// <remarks>
	/// Each test targets a distinct runtime-reachable guard and uses a fixture-private element type so its reflective
	/// encoder is never cached by another test (the per-type caches and the switch are process-global). The decode-side
	/// guards, and the encode guards for framework tuple types, are shadowed at runtime by the encoder guard that runs
	/// first, so they cannot be pinned here; the trim probe (Sandbox/AotTupleProbe, scd with the switch off) proves the
	/// trimmer folds and removes those reflective builders.
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[NonParallelizable] // toggles the process-wide reflection switch
	public class TuPackReflectionSwitchFacts : SimpleTest
	{

		private const string ReflectionSwitch = "SnowBank.Data.Tuples.TuPack.IsReflectionSupported";

		/// <summary>A plain struct used ONLY by this fixture. It has no compile-time fast path and does not implement
		/// <see cref="ITuplePackable"/>, so it isolates the boxed and nullable reflective builders: with reflection off
		/// the guard throws <see cref="NotSupportedException"/>, and if the guard is removed the type falls through to the
		/// "unsupported" path (a different exception), so each test genuinely discriminates its guard. Fixture-private so
		/// its reflective encoder is never cached by another test (the per-type caches and the switch are process-global).</summary>
		private readonly struct ProbeStruct
		{
			public ProbeStruct(int value) => this.Value = value;

			public int Value { get; }
		}

		private static void WithReflectionDisabled(Action body)
		{
			bool had = AppContext.TryGetSwitch(ReflectionSwitch, out var previous);
			AppContext.SetSwitch(ReflectionSwitch, false);
			try
			{
				body();
			}
			finally
			{
				AppContext.SetSwitch(ReflectionSwitch, had ? previous : true);
			}
		}

		/// <summary>Asserts the delegate throws <see cref="NotSupportedException"/>, unwrapping the <see cref="TypeInitializationException"/>
		/// that the runtime raises when the failing builder runs inside a static field initializer.</summary>
		private static void AssertThrowsReflectionDisabled(Action action)
		{
			var ex = Assert.Catch(action);
			while (ex is TypeInitializationException && ex.InnerException is not null)
			{
				ex = ex.InnerException;
			}
			Assert.That(ex, Is.InstanceOf<NotSupportedException>());
		}

		[Test]
		public void Test_Boxed_Exotic_Encode_Throws_When_Reflection_Disabled()
		{
			// boxing an exotic element routes through the runtime-type reflective builder (SerializeObjectTo ->
			// CreateBoxedEncoder). With reflection off that builder must throw NotSupportedException, not run.
			WithReflectionDisabled(() =>
				AssertThrowsReflectionDisabled(() => TuPack.EncodeKey<object>(new ProbeStruct(42))));
		}

		[Test]
		public void Test_Boxed_Exotic_Span_Encode_Throws_When_Reflection_Disabled()
		{
			// the span (Try) encode path uses a separate reflective builder (TrySerializeObjectTo -> CreateBoxedSpanEncoder),
			// which is the path an AoT/trimmed consumer hits. It must also throw with reflection off.
			WithReflectionDisabled(() =>
				AssertThrowsReflectionDisabled(() => TuPack.TryEncodeKey<object>(new byte[64], out _, new ProbeStruct(42))));
		}

		[Test]
		public void Test_Nullable_Of_Exotic_Encode_Throws_When_Reflection_Disabled()
		{
			// Nullable<T> of a type with no fast path takes the reflective nullable builder in GetSerializerFor.
			// With reflection off it must throw NotSupportedException instead of building it.
			WithReflectionDisabled(() =>
				AssertThrowsReflectionDisabled(() => TuPack.EncodeKey((ProbeStruct?) new ProbeStruct(7))));
		}

		[Test]
		public void Test_Common_Keys_Are_Byte_Identical_Regardless_Of_Switch()
		{
			// well-known element types take the compile-time fast path and never consult the switch, so their bytes
			// (what is written to the database) must not depend on the switch state.
			Slice[] Encode() =>
			[
				TuPack.EncodeKey(123),
				TuPack.EncodeKey(-1L),
				TuPack.EncodeKey("hello"),
				TuPack.EncodeKey(true),
				TuPack.EncodeKey(Guid.Parse("11111111-2222-3333-4444-555555555555")),
				TuPack.EncodeKey(123, "x", true),
				TuPack.EncodeKey("items", "SKU", 42),
				TuPack.EncodeKey(int.MinValue, long.MaxValue, "unicode: é中", (bool?) null),
				// composite keys with an embedded tuple element (route through ITuplePackable, not reflection)
				TuPack.EncodeKey("prefix", STuple.Create(1, 2)),
				TuPack.Pack(STuple.Create("a", 1).Append("b", 2L)),
			];

			var reflectionOn = Encode();

			Slice[] reflectionOff = [];
			WithReflectionDisabled(() => { reflectionOff = Encode(); });

			Assert.That(reflectionOff.Length, Is.EqualTo(reflectionOn.Length));
			for (int i = 0; i < reflectionOn.Length; i++)
			{
				Assert.That(reflectionOff[i], Is.EqualTo(reflectionOn[i]), $"key #{i} differs between reflection on and off");
			}

			// common keys must also still decode with reflection off
			WithReflectionDisabled(() =>
			{
				Assert.That(TuPack.DecodeKey<int>(reflectionOn[0]), Is.EqualTo(123));
				Assert.That(TuPack.DecodeKey<string>(reflectionOn[2]), Is.EqualTo("hello"));
			});
		}

	}

}
