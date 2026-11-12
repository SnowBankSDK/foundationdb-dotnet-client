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

// The closed hierarchies of CrystalJson.ClosedTypes.cs, enrolled in a generated converter container. They compile with
// the .NET 11 SDK compiler, which runs the generator, so this fixture shares the net11.0 guard of the hierarchies.
#if NET11_0_OR_GREATER

namespace SnowBank.Data.Json.Tests
{

	[CrystalJsonConverter]
	[CrystalSerializable(typeof(ClosedCartEvent))]
	[CrystalSerializable(typeof(ClosedCartHistory))]
	[CrystalSerializable(typeof(ClosedShape))]
	[CrystalSerializable(typeof(ClosedCommand))]
	[CrystalSerializable(typeof(ClosedResult<int>))]
	public static partial class ClosedGenConverters { }

	public partial class CrystalJsonTest
	{

		[Test]
		public void Test_Generated_ClosedType_Matches_Runtime_Output()
		{
			// the generated converter of the root writes the same document as the runtime path, with the discriminator first
			// NOTE: the generated code writes the inherited members first, and the runtime path writes them last, so the documents are compared as JSON values
			void Check<TBase>(TBase value, Func<TBase, CrystalJsonSettings, string> write, Func<string, TBase> read, string discriminator) where TBase : class
			{
				var generated = write(value, CrystalJsonSettings.JsonCompact);
				Log($"{typeof(TBase).Name}: {generated}");
				Assert.That(JsonValue.Parse(generated), IsJson.EqualTo(JsonValue.Parse(CrystalJson.Serialize<TBase>(value, CrystalJsonSettings.JsonCompact))));
				Assert.That(generated, Does.StartWith($"{{\"$type\":\"{discriminator}\",").Or.StartWith($"{{\"kind\":\"{discriminator}\","));

				// the generated reader picks the derived type from the discriminator
				var decoded = read(generated);
				Assert.That(decoded, Is.InstanceOf(value.GetType()));
				Assert.That(write(decoded, CrystalJsonSettings.JsonCompact), Is.EqualTo(generated));
			}

			using (Assert.EnterMultipleScope())
			{
				Check<ClosedCartEvent>(new CartCreated { CartId = "c1", Owner = "alice" }, (v, o) => ClosedGenConverters.ClosedCartEvent.ToJsonText(v, o), j => ClosedGenConverters.ClosedCartEvent.Deserialize(j), "CartCreated");
				Check<ClosedCartEvent>(new ItemAdded { CartId = "c1", Sku = "SKU-42", Quantity = 2 }, (v, o) => ClosedGenConverters.ClosedCartEvent.ToJsonText(v, o), j => ClosedGenConverters.ClosedCartEvent.Deserialize(j), "ItemAdded");
				Check<ClosedCartEvent>(new CartCheckedOut { CartId = "c1", Total = 12.5m }, (v, o) => ClosedGenConverters.ClosedCartEvent.ToJsonText(v, o), j => ClosedGenConverters.ClosedCartEvent.Deserialize(j), "CartCheckedOut");
				Check<ClosedShape>(new ClosedTriangle { Label = "t", Sides = 3, Base = 2.5 }, (v, o) => ClosedGenConverters.ClosedShape.ToJsonText(v, o), j => ClosedGenConverters.ClosedShape.Deserialize(j), "ClosedTriangle");
				Check<ClosedShape>(new ClosedSquare { Label = "s", Sides = 4, Side = 1 }, (v, o) => ClosedGenConverters.ClosedShape.ToJsonText(v, o), j => ClosedGenConverters.ClosedShape.Deserialize(j), "ClosedSquare");
				Check<ClosedShape>(new ClosedCircle { Label = "c", Radius = 1.5 }, (v, o) => ClosedGenConverters.ClosedShape.ToJsonText(v, o), j => ClosedGenConverters.ClosedShape.Deserialize(j), "ClosedCircle");
				Check<ClosedCommand>(new StopCommand { Force = true }, (v, o) => ClosedGenConverters.ClosedCommand.ToJsonText(v, o), j => ClosedGenConverters.ClosedCommand.Deserialize(j), "StopCommand");
				Check<ClosedResult<int>>(new ClosedSuccess<int> { Value = 42 }, (v, o) => ClosedGenConverters.ClosedResult_Int32.ToJsonText(v, o), j => ClosedGenConverters.ClosedResult_Int32.Deserialize(j), "ClosedSuccess");
				Check<ClosedResult<int>>(new ClosedIntFailure { Error = "boom" }, (v, o) => ClosedGenConverters.ClosedResult_Int32.ToJsonText(v, o), j => ClosedGenConverters.ClosedResult_Int32.Deserialize(j), "ClosedIntFailure");
			}
		}

		[Test]
		public void Test_Generated_ClosedType_In_Container()
		{
			var history = new ClosedCartHistory
			{
				Last = new CartCheckedOut { CartId = "c1", Total = 12.5m },
				Events = [ new CartCreated { CartId = "c1", Owner = "alice" }, new ItemAdded { CartId = "c1", Sku = "SKU-42", Quantity = 2 } ],
			};

			var json = ClosedGenConverters.ClosedCartHistory.ToJsonText(history, CrystalJsonSettings.JsonCompact);
			Log(json);
			Assert.That(JsonValue.Parse(json), IsJson.EqualTo(JsonValue.Parse(CrystalJson.Serialize(history, CrystalJsonSettings.JsonCompact))));

			var copy = ClosedGenConverters.ClosedCartHistory.Deserialize(json);
			Assert.That(copy.Last, Is.InstanceOf<CartCheckedOut>());
			Assert.That(copy.Events, Has.Count.EqualTo(2));
			Assert.That(copy.Events![0], Is.InstanceOf<CartCreated>());
			Assert.That(copy.Events![1], Is.InstanceOf<ItemAdded>());
		}

	}

}

#endif
