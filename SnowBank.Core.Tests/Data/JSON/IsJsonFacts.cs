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

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable StringLiteralTypo

namespace SnowBank.Data.Json.Binary.Tests
{
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public class IsJsonFacts : SimpleTest
	{

		[Test]
		public void Test_IsJson_Assertions()
		{
			// To simplify unit tests, we want to be able to write "Assert.That(<JsonValue>, IsJson.EqualTo(<any type>)" to bypass the need to call ".Get<any_type>(...)"
			// The "IsJson" type exists to work around a few issues, like Is.True/Is.Null not working as intended.

			DateTime now = DateTime.Now;
			Guid id = Guid.NewGuid();
			var obj = new JsonObject
			{
				["str"] = "world",
				["int"] = 42,
				["zero"] = 0,
				["true"] = true,
				["false"] = false,
				["id"] = id,
				["date"] = now,
				["null"] = null, // explicit null
			};

			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["str"], IsJson.EqualTo("world"));
				Assert.That(obj["str"], IsJson.Not.EqualTo("something else"));
				Assert.That(obj["int"], IsJson.EqualTo(42));
				Assert.That(obj["int"], IsJson.Not.EqualTo(123));
				Assert.That(obj["true"], IsJson.True);
				Assert.That(obj["false"], IsJson.False);
				Assert.That(obj["zero"], IsJson.Zero);
				Assert.That(obj["id"], IsJson.EqualTo(id));
				Assert.That(obj["id"], IsJson.Not.EqualTo(Guid.NewGuid()));
				Assert.That(obj["id"], IsJson.Not.EqualTo(Guid.Empty));
				Assert.That(obj["id"], IsJson.EqualTo((Uuid128) id));
				Assert.That(obj["id"], IsJson.Not.EqualTo((Uuid128) Guid.NewGuid()));
				Assert.That(obj["id"], IsJson.Not.EqualTo(Uuid128.Empty));
				Assert.That(obj["date"], IsJson.EqualTo(now));
				Assert.That(obj["null"], IsJson.ExplicitNull);

				Assert.That(obj["str"], IsJson.String);
				Assert.That(obj["false"], IsJson.Boolean);
				Assert.That(obj["int"], IsJson.Number);
			}

			var top = new JsonObject
			{
				["foo"] = obj.Copy(),
				["bar"] = JsonArray.Create(obj.Copy()),
				["empty"] = JsonObject.ReadOnly.Empty,
				["null"] = null, // explicit null
			};

			using (Assert.EnterMultipleScope())
			{
				Assert.That(top["null"], IsJson.ExplicitNull);
				Assert.That(top["null"], IsJson.Not.Missing);
				Assert.That(top["null"], IsJson.Null);
				Assert.That(top["not_found"], IsJson.Missing);
				Assert.That(top["not_found"], IsJson.Not.ExplicitNull);
				Assert.That(top["not_found"], IsJson.Null);

				Assert.That(top["empty"], IsJson.Empty);
				Assert.That(top["null"], IsJson.Not.Empty);
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(top["foo"], IsJson.Not.Null);
				Assert.That(top["foo"], IsJson.Object.And.Not.Empty);
				Assert.That(top["foo"]["str"], IsJson.EqualTo("world"));
				Assert.That(top["foo"]["int"], IsJson.EqualTo(42));
				Assert.That(top["foo"]["true"], IsJson.True);
				Assert.That(top["foo"]["false"], IsJson.False);
				Assert.That(top["foo"]["zero"], IsJson.Zero);
				Assert.That(top["foo"]["id"], IsJson.EqualTo(id));
				Assert.That(top["foo"]["date"], IsJson.EqualTo(now));
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(top["bar"], IsJson.Not.Null);
				Assert.That(top["bar"], IsJson.Array.And.Not.Empty);
				Assert.That(top["bar"][0], IsJson.Not.Null);
				Assert.That(top["bar"][0], IsJson.Object);
				Assert.That(top["bar"][0]["str"], IsJson.EqualTo("world"));
				Assert.That(top["bar"][0]["int"], IsJson.EqualTo(42));
				Assert.That(top["bar"][0]["true"], IsJson.True);
				Assert.That(top["bar"][0]["false"], IsJson.False);
				Assert.That(top["bar"][0]["zero"], IsJson.Zero);
				Assert.That(top["bar"][0]["id"], IsJson.EqualTo(id));
				Assert.That(top["bar"][0]["date"], IsJson.EqualTo(now));
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(top["not_found"]["str"], IsJson.Missing);
				Assert.That(top["not_found"]["str"], IsJson.Not.EqualTo("world"));
				Assert.That(top["a"]["b"]["c"]["d"], IsJson.Null);
				Assert.That(top["bar"][123], IsJson.Error);
				Assert.That(top["bar"][123]["str"], IsJson.Error);
				Assert.That(top["bar"][123]["str"], IsJson.Not.EqualTo("hello"));
				Assert.That(top["bar"][123]["int"], IsJson.Error);
				Assert.That(top["bar"][123]["int"], IsJson.Not.EqualTo(42));
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(top["false"], IsJson.Not.True);
				Assert.That(top["true"], IsJson.Not.False);
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["str"], IsJson.Not.EqualTo(123));
				Assert.That(obj["str"], IsJson.Not.Boolean.Or.Number);
				Assert.That(obj["str"], IsJson.GreaterThan("worlc"));
				Assert.That(obj["str"], IsJson.GreaterThanOrEqualTo("world"));
				Assert.That(obj["str"], IsJson.LessThan("worle"));
				Assert.That(obj["str"], IsJson.LessThanOrEqualTo("world"));
				Assert.That(() => Assert.That(obj["str"], IsJson.EqualTo("something_else")), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["str"], IsJson.GreaterThan("world")), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["str"], IsJson.LessThan("world")), Throws.InstanceOf<AssertionException>());
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["int"], IsJson.GreaterThan(41));
				Assert.That(obj["int"], IsJson.GreaterThanOrEqualTo(42));
				Assert.That(obj["int"], IsJson.LessThan(43));
				Assert.That(obj["int"], IsJson.LessThanOrEqualTo(42));
				Assert.That(() => Assert.That(obj["int"], IsJson.EqualTo(123)), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["int"], IsJson.GreaterThan(42)), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["int"], IsJson.LessThan(42)), Throws.InstanceOf<AssertionException>());
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(() => Assert.That(obj["true"], IsJson.False), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["false"], IsJson.True), Throws.InstanceOf<AssertionException>());
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(() => Assert.That(obj["str"], IsJson.Not.String), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["str"], IsJson.String.And.Number), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["str"], IsJson.Boolean.Or.Number), Throws.InstanceOf<AssertionException>());
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonString.Return("hello world"), IsJson.ReadOnly);
				Assert.That(JsonNumber.Return(42), IsJson.ReadOnly);
				Assert.That(JsonBoolean.True, IsJson.ReadOnly);
				Assert.That(JsonNull.Null, IsJson.ReadOnly);
				Assert.That(JsonObject.ReadOnly.Empty, IsJson.ReadOnly);
				Assert.That(new JsonObject(), IsJson.Not.ReadOnly);
				Assert.That(new JsonObject().ToReadOnly(), IsJson.ReadOnly);
				Assert.That(JsonArray.ReadOnly.Empty, IsJson.ReadOnly);
				Assert.That(new JsonArray(), IsJson.Not.ReadOnly);
				Assert.That(new JsonArray().ToReadOnly(), IsJson.ReadOnly);
				Assert.That(() => Assert.That(new JsonArray(), IsJson.ReadOnly), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(JsonArray.ReadOnly.Empty, IsJson.Not.ReadOnly), Throws.InstanceOf<AssertionException>());
			}
		}

		[Test]
		public void Test_Is_Json_Assertions()
		{
			// Basically the same as Test_IsJson_Assertions(), but using the "Is.Json" static extension property

			DateTime now = DateTime.Now;
			Guid id = Guid.NewGuid();
			var obj = new JsonObject
			{
				["str"] = "world",
				["int"] = 42,
				["zero"] = 0,
				["true"] = true,
				["false"] = false,
				["id"] = id,
				["date"] = now,
				["null"] = null, // explicit null
			};

			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["str"], Is.Json.EqualTo("world"));
				Assert.That(obj["str"], Is.Json.Not.EqualTo("something else"));
				Assert.That(obj["int"], Is.Json.EqualTo(42));
				Assert.That(obj["int"], Is.Json.Not.EqualTo(123));
				Assert.That(obj["true"], Is.Json.True);
				Assert.That(obj["false"], Is.Json.False);
				Assert.That(obj["zero"], Is.Json.Zero);
				Assert.That(obj["id"], Is.Json.EqualTo(id));
				Assert.That(obj["id"], Is.Json.Not.EqualTo(Guid.NewGuid()));
				Assert.That(obj["id"], Is.Json.Not.EqualTo(Guid.Empty));
				Assert.That(obj["id"], Is.Json.EqualTo((Uuid128) id));
				Assert.That(obj["id"], Is.Json.Not.EqualTo((Uuid128) Guid.NewGuid()));
				Assert.That(obj["id"], Is.Json.Not.EqualTo(Uuid128.Empty));
				Assert.That(obj["date"], Is.Json.EqualTo(now));
				Assert.That(obj["null"], Is.Json.ExplicitNull);

				Assert.That(obj["str"], Is.Json.String);
				Assert.That(obj["false"], Is.Json.Boolean);
				Assert.That(obj["int"], Is.Json.Number);
			}

			var top = new JsonObject
			{
				["foo"] = obj.Copy(),
				["bar"] = JsonArray.Create(obj.Copy()),
				["empty"] = JsonObject.ReadOnly.Empty,
				["null"] = null, // explicit null
			};

			using (Assert.EnterMultipleScope())
			{
				Assert.That(top["null"], Is.Json.ExplicitNull);
				Assert.That(top["null"], Is.Json.Not.Missing);
				Assert.That(top["null"], Is.Json.Null);
				Assert.That(top["not_found"], Is.Json.Missing);
				Assert.That(top["not_found"], Is.Json.Not.ExplicitNull);
				Assert.That(top["not_found"], Is.Json.Null);

				Assert.That(top["empty"], Is.Json.Empty);
				Assert.That(top["null"], Is.Json.Not.Empty);
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(top["foo"], Is.Json.Not.Null);
				Assert.That(top["foo"], Is.Json.Object.And.Not.Empty);
				Assert.That(top["foo"]["str"], Is.Json.EqualTo("world"));
				Assert.That(top["foo"]["int"], Is.Json.EqualTo(42));
				Assert.That(top["foo"]["true"], Is.Json.True);
				Assert.That(top["foo"]["false"], Is.Json.False);
				Assert.That(top["foo"]["zero"], Is.Json.Zero);
				Assert.That(top["foo"]["id"], Is.Json.EqualTo(id));
				Assert.That(top["foo"]["date"], Is.Json.EqualTo(now));
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(top["bar"], Is.Json.Not.Null);
				Assert.That(top["bar"], Is.Json.Array.And.Not.Empty);
				Assert.That(top["bar"][0], Is.Json.Not.Null);
				Assert.That(top["bar"][0], Is.Json.Object);
				Assert.That(top["bar"][0]["str"], Is.Json.EqualTo("world"));
				Assert.That(top["bar"][0]["int"], Is.Json.EqualTo(42));
				Assert.That(top["bar"][0]["true"], Is.Json.True);
				Assert.That(top["bar"][0]["false"], Is.Json.False);
				Assert.That(top["bar"][0]["zero"], Is.Json.Zero);
				Assert.That(top["bar"][0]["id"], Is.Json.EqualTo(id));
				Assert.That(top["bar"][0]["date"], Is.Json.EqualTo(now));
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(top["not_found"]["str"], Is.Json.Missing);
				Assert.That(top["not_found"]["str"], Is.Json.Not.EqualTo("world"));
				Assert.That(top["a"]["b"]["c"]["d"], Is.Json.Null);
				Assert.That(top["bar"][123], Is.Json.Error);
				Assert.That(top["bar"][123]["str"], Is.Json.Error);
				Assert.That(top["bar"][123]["str"], Is.Json.Not.EqualTo("hello"));
				Assert.That(top["bar"][123]["int"], Is.Json.Error);
				Assert.That(top["bar"][123]["int"], Is.Json.Not.EqualTo(42));
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(top["false"], Is.Json.Not.True);
				Assert.That(top["true"], Is.Json.Not.False);
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["str"], Is.Json.Not.EqualTo(123));
				Assert.That(obj["str"], Is.Json.Not.Boolean.Or.Number);
				Assert.That(obj["str"], Is.Json.GreaterThan("worlc"));
				Assert.That(obj["str"], Is.Json.GreaterThanOrEqualTo("world"));
				Assert.That(obj["str"], Is.Json.LessThan("worle"));
				Assert.That(obj["str"], Is.Json.LessThanOrEqualTo("world"));
				Assert.That(() => Assert.That(obj["str"], Is.Json.EqualTo("something_else")), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["str"], Is.Json.GreaterThan("world")), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["str"], Is.Json.LessThan("world")), Throws.InstanceOf<AssertionException>());
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["int"], Is.Json.GreaterThan(41));
				Assert.That(obj["int"], Is.Json.GreaterThanOrEqualTo(42));
				Assert.That(obj["int"], Is.Json.LessThan(43));
				Assert.That(obj["int"], Is.Json.LessThanOrEqualTo(42));
				Assert.That(() => Assert.That(obj["int"], Is.Json.EqualTo(123)), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["int"], Is.Json.GreaterThan(42)), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["int"], Is.Json.LessThan(42)), Throws.InstanceOf<AssertionException>());
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(() => Assert.That(obj["true"], Is.Json.False), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["false"], Is.Json.True), Throws.InstanceOf<AssertionException>());
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(() => Assert.That(obj["str"], Is.Json.Not.String), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["str"], Is.Json.String.And.Number), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(obj["str"], Is.Json.Boolean.Or.Number), Throws.InstanceOf<AssertionException>());
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonString.Return("hello world"), Is.Json.ReadOnly);
				Assert.That(JsonNumber.Return(42), Is.Json.ReadOnly);
				Assert.That(JsonBoolean.True, Is.Json.ReadOnly);
				Assert.That(JsonNull.Null, Is.Json.ReadOnly);
				Assert.That(JsonObject.ReadOnly.Empty, Is.Json.ReadOnly);
				Assert.That(new JsonObject(), Is.Json.Not.ReadOnly);
				Assert.That(new JsonObject().ToReadOnly(), Is.Json.ReadOnly);
				Assert.That(JsonArray.ReadOnly.Empty, Is.Json.ReadOnly);
				Assert.That(new JsonArray(), Is.Json.Not.ReadOnly);
				Assert.That(new JsonArray().ToReadOnly(), Is.Json.ReadOnly);
				Assert.That(() => Assert.That(new JsonArray(), Is.Json.ReadOnly), Throws.InstanceOf<AssertionException>());
				Assert.That(() => Assert.That(JsonArray.ReadOnly.Empty, Is.Json.Not.ReadOnly), Throws.InstanceOf<AssertionException>());
			}
		}

		[Test]
		public void Test_IsJson_Within_Tolerance()
		{

			[MethodImpl(MethodImplOptions.NoInlining)]
			static double Add(double x, double y) => x + y;

			// Due to IEEE being IEEE, 1.23d + 4.56d is ~5.789999999999999d which is NOT 5.79d
			Assume.That(Add(1.23d, 4.56d) == 5.79d, Is.False);
			// NUnit does not consider them equal either...
			Assume.That(Add(1.23d, 4.56d), Is.Not.EqualTo(5.79d));
			// ...unless we use a tolerance of 1 ULPS
			Assume.That(Add(1.23d, 4.56d), Is.EqualTo(5.79d).Within(1).Ulps);

			// if we add the same values via JsonNumbers
			var a = JsonNumber.Create(1.23d);
			var b = JsonNumber.Create(4.56d);
			var c = a.Plus(b);
			// we should get the same behavior without custom constraints
			Assert.That(c.ToDouble(), Is.EqualTo(Add(1.23d, 4.56d)));
			Assert.That(c.ToDouble(), Is.Not.EqualTo(5.79d));

			// but by default, IsJson should compare within floating point numbers within 1 ULPS
			Assert.That(c, IsJson.EqualTo(5.79d));

			// unless we use a custom tolerance or comparer
			Assert.That(c, IsJson.Not.EqualTo(5.79d, 0));
			Assert.That(c, IsJson.Not.EqualTo(5.79d, double.Epsilon));
			Assert.That(c, IsJson.Not.EqualTo(5.79d, null));

			// of course, this should not affect exact equality!
			Assert.That(a, IsJson.EqualTo(1.23d));
			Assert.That(a, IsJson.EqualTo(1.23d, 0));
			Assert.That(a, IsJson.EqualTo(1.23d, null));
		}

	}

}
