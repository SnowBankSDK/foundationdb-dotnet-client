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

// ReSharper disable AccessToDisposedClosure
// ReSharper disable AccessToModifiedClosure
// ReSharper disable CollectionNeverUpdated.Local
// ReSharper disable JoinDeclarationAndInitializer
// ReSharper disable PreferConcreteValueOverDefault
// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable RedundantCast
// ReSharper disable RedundantExplicitArrayCreation
// ReSharper disable RedundantExplicitParamsArrayCreation
// ReSharper disable StringLiteralTypo
// ReSharper disable UseObjectOrCollectionInitializer

// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable UseCollectionExpression
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable IDE0004 // Remove Unnecessary Cast
#pragma warning disable IDE0017 // Simplify object initialization
#pragma warning disable NUnit2009 // The same value has been provided as both the actual and the expected argument
#pragma warning disable NUnit2010 // Use EqualConstraint for better assertion messages in case of failure
#pragma warning disable NUnit2021 // Incompatible types for EqualTo constraint
#pragma warning disable NUnit2045 // Use Assert.Multiple
#pragma warning disable NUnit2050 // NUnit 4 no longer supports string.Format specification

namespace SnowBank.Data.Json.Tests
{
	using System.Collections.ObjectModel;
	using System.Linq.Expressions;
	using System.Net;
	using SnowBank.Data.Tuples;
	using NUnit.Framework.Constraints;

	public partial class CrystalJsonTest
	{

		#region JsonNull...

		[Test]
		public void Test_JsonNull_Explicit()
		{
			var json = JsonNull.Null;
			Assert.That(json, Is.Not.Null);
			Assert.That(json, Is.InstanceOf<JsonNull>());
			Assert.That(JsonNull.Null, Is.SameAs(json), "JsonNull.Null should be a singleton");

			var value = (JsonNull) json;
			using (Assert.EnterMultipleScope())
			{
				Assert.That(value.Type, Is.EqualTo(JsonType.Null), "value.Type");
				Assert.That(value.IsNull, Is.True, "value.IsNull");
				Assert.That(value.IsMissing, Is.False, "value.IsMissing");
				Assert.That(value.IsError, Is.False, "value.IsError");
				Assert.That(value.IsDefault, Is.True, "value.IsDefault");
				Assert.That(value.ToObject(), Is.Null, "value.ToObject()");
				Assert.That(value.ToString(), Is.EqualTo(""), "value.ToString()");
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(value.Equals(JsonNull.Null), Is.True, "EQ null");
				Assert.That(value.Equals(JsonNull.Missing), Is.False, "NEQ missing");
				Assert.That(value.Equals(JsonNull.Error), Is.False, "NEQ error");
				Assert.That(value.Equals(default(JsonValue)), Is.True);
				Assert.That(value.Equals(default(object)), Is.True);

				Assert.That(value.Equals(0), Is.False);
				Assert.That(value.Equals(123), Is.False);
				Assert.That(value.Equals(false), Is.False);
				Assert.That(value.Equals(""), Is.False);
				Assert.That(value.Equals("hello"), Is.False);
				Assert.That(value.Equals(JsonObject.Create()), Is.False);
				Assert.That(value.Equals(JsonArray.Create()), Is.False);

				Assert.That(value.StrictEquals(JsonNull.Null), Is.True, "EQ null");
				Assert.That(value.StrictEquals(JsonNull.Missing), Is.False, "NEQ missing");
				Assert.That(value.StrictEquals(JsonNull.Error), Is.False, "NEQ error");
				Assert.That(value.StrictEquals(false), Is.False);
				Assert.That(value.StrictEquals(0), Is.False);
				Assert.That(value.StrictEquals(""), Is.False);
				Assert.That(value.StrictEquals("null"), Is.False);

				Assert.That(value.CompareTo(default(JsonValue)), Is.EqualTo(0));
				Assert.That(value.CompareTo(JsonNull.Null), Is.EqualTo(0));
				Assert.That(value.CompareTo(JsonNull.Missing), Is.EqualTo(-1));
				Assert.That(value.CompareTo(JsonNull.Error), Is.EqualTo(-1));
				Assert.That(value.CompareTo(0), Is.EqualTo(-1));
				Assert.That(value.CompareTo(123), Is.EqualTo(-1));
				Assert.That(value.CompareTo(""), Is.EqualTo(-1));
				Assert.That(value.CompareTo("hello"), Is.EqualTo(-1));
			}

			// we must check a few corner cases when binding Null:
			// - for Value Types, null must bind into default(T) (ex: JsonNull.Null.As<int>() => 0)
			// - for JsonValue or JsonNull types, it must bind into the JsonNull.Null singleton (and not return a null reference!)
			// - for all other types, it should bind into a null reference

			using (Assert.EnterMultipleScope())
			{ // Bind(typeof(T), ...)
				Assert.That(json.Bind<string>(), Is.Null);
				Assert.That(json.Bind<int>(), Is.Zero);
				Assert.That(json.Bind<bool>(), Is.False);
				Assert.That(json.Bind<Guid>(), Is.EqualTo(Guid.Empty));
				Assert.That(json.Bind<int?>(), Is.Null);
				Assert.That(json.Bind<string[]>(), Is.Null);
				Assert.That(json.Bind<List<string>>(), Is.Null);
				Assert.That(json.Bind<IList<string>>(), Is.Null);

				// special case
				Assert.That(json.Bind<JsonNull>(), Is.SameAs(JsonNull.Null), "JsonNull.Bind<JsonNull>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(json.Bind<JsonValue>(), Is.SameAs(JsonNull.Null), "JsonNull.Bind<JsonValue>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(json.Bind<JsonString>(), Is.Null, "JsonNull.Bind<JsonString>() should return null, because a JsonString instance cannot represent null itself!");
				Assert.That(json.Bind<JsonNumber>(), Is.Null, "JsonNull.Bind<JsonNumber>() should return null, because a JsonNumber instance cannot represent null itself!");
				Assert.That(json.Bind<JsonBoolean>(), Is.Null, "JsonNull.Bind<JsonBoolean>() should return null, because a JsonBoolean instance cannot represent null itself!");
				Assert.That(json.Bind<JsonObject>(), Is.Null, "JsonNull.Bind<JsonObject>() should return null, because a JsonObject instance cannot represent null itself!");
				Assert.That(json.Bind<JsonArray>(), Is.Null, "JsonNull.Bind<JsonArray>() should return null, because a JsonArray instance cannot represent null itself!");
			}

			using (Assert.EnterMultipleScope())
			{ // Required<T>()
				Assert.That(() => json.Required<string>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => json.Required<int>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => json.Required<bool>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => json.Required<Guid>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => json.Required<int?>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => json.Required<string[]>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => json.Required<List<string>>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => json.Required<IList<string>>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => json.Required<JsonNull>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => json.Required<JsonValue>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => json.Required<JsonString>(), Throws.InstanceOf<JsonBindingException>());
			}

			using (Assert.EnterMultipleScope())
			{ // OrDefault<T>()
				Assert.That(json.As<string>(), Is.Null);
				Assert.That(json.As<int>(), Is.Zero);
				Assert.That(json.As<bool>(), Is.False);
				Assert.That(json.As<Guid>(), Is.EqualTo(Guid.Empty));
				Assert.That(json.As<int?>(), Is.Null);
				Assert.That(json.As<string[]>(), Is.Null);
				Assert.That(json.As<List<string>>(), Is.Null);
				Assert.That(json.As<IList<string>>(), Is.Null);

				// special case
				Assert.That(json.As<JsonNull>(), Is.SameAs(JsonNull.Null), "JsonNull.OrDefault<JsonNull>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(json.As<JsonValue>(), Is.SameAs(JsonNull.Null), "JsonNull.OrDefault<JsonValue>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(json.As<JsonString>(), Is.Null, "JsonNull.OrDefault<JsonString>() should return null, because a JsonString instance cannot represent null itself!");
				Assert.That(json.As<JsonNumber>(), Is.Null, "JsonNull.OrDefault<JsonNumber>() should return null, because a JsonNumber instance cannot represent null itself!");
				Assert.That(json.As<JsonBoolean>(), Is.Null, "JsonNull.OrDefault<JsonBoolean>() should return null, because a JsonBoolean instance cannot represent null itself!");
				Assert.That(json.As<JsonObject>(), Is.Null, "JsonNull.OrDefault<JsonObject>() should return null, because a JsonObject instance cannot represent null itself!");
				Assert.That(json.As<JsonArray>(), Is.Null, "JsonNull.OrDefault<JsonArray>() should return null, because a JsonArray instance cannot represent null itself!");
			}

			using (Assert.EnterMultipleScope())
			{ // Embedded Fields with explicit null

				//note: anonymous class that is used as a template to create an inline class
				var template = new
				{
					/*int*/ Int32 = 666,
					/*bool*/ Bool = true,
					/*string*/ String = "FAILED",
					/*Guid*/ Guid = Guid.Parse("66666666-6666-6666-6666-666666666666"),
					/*int?*/ NullInt32 = (int?) 666,
					/*bool?*/ NullBool = (bool?) true,
					/*Guid?*/ NullGuid = (Guid?) Guid.Parse("66666666-6666-6666-6666-666666666666"),
					/*JsonValue*/ JsonValue = (JsonValue) "FAILED",
					/*JsonNull*/ JsonString = (JsonString) "FAILED",
					/*JsonNull*/ JsonArray = JsonArray.Create([ "FAILED" ]),
					/*JsonNull*/ JsonObject = JsonObject.Create(("FAILED", "EPIC")),
				};

				// when deserializing an object with all members explicitly set to null, we should return the default of this type
				var j = JsonObject
					.Parse("""{ "Int32": null, "Bool": null, "String": null, "Guid": null, "NullInt32": null, "NullBool": null, "NullGuid": null, "JsonValue": null, "JsonNull": null, "JsonArray": null, "JsonObject": null }""")
					.As(template);

				Assert.That(j.Int32, Is.Zero);
				Assert.That(j.Bool, Is.False);
				Assert.That(j.String, Is.Null);
				Assert.That(j.Guid, Is.EqualTo(Guid.Empty));
				Assert.That(j.NullInt32, Is.Null);
				Assert.That(j.NullBool, Is.Null);
				Assert.That(j.NullGuid, Is.Null);
				Assert.That(j.JsonValue, Is.SameAs(JsonNull.Null), "Properties with type JsonValue should bind null into JsonNull.Null!");
				Assert.That(j.JsonString, Is.Null);
				Assert.That(j.JsonArray, Is.Null);
				Assert.That(j.JsonObject, Is.Null);
			}

			Assert.That(SerializeToSlice(json), Is.EqualTo(Slice.FromString("null")));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(value, Is.Not.EqualTo((object) 0));
				Assert.That(value, Is.Not.EqualTo((object) false));
				Assert.That(value, Is.Not.EqualTo((object?) null));
				Assert.That(value, Is.Not.EqualTo((object) ""));

				Assert.That(value.ValueEquals<int>(0), Is.False);
				Assert.That(value.ValueEquals<bool>(false), Is.False);
				Assert.That(value.ValueEquals<string>(null), Is.True);
				Assert.That(value.ValueEquals<string>(""), Is.False);

				Assert.That(value.ValueEquals<int?>(0), Is.False);
				Assert.That(value.ValueEquals<int?>(null), Is.True);
				Assert.That(value.ValueEquals<bool?>(false), Is.False);
				Assert.That(value.ValueEquals<bool?>(null), Is.True);
			}
		}

		[Test]
		public void Test_JsonNull_Missing()
		{
			var missing = JsonNull.Missing;
			Assert.That(missing, Is.Not.Null);
			Assert.That(missing, Is.InstanceOf<JsonNull>());

			var value = (JsonNull) missing;
			using (Assert.EnterMultipleScope())
			{
				Assert.That(value.Type, Is.EqualTo(JsonType.Null), "value.Type");
				Assert.That(value.IsNull, Is.True, "value.IsNull");
				Assert.That(value.IsMissing, Is.True, "value.IsMissing");
				Assert.That(value.IsError, Is.False, "value.IsError");
				Assert.That(value.IsDefault, Is.True, "value.IsDefault");
				Assert.That(value.ToObject(), Is.Null, "value.ToObject()");
				Assert.That(value.ToString(), Is.EqualTo(""), "value.ToString()");
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(value.Equals(JsonNull.Missing), Is.True, "EQ missing");
				Assert.That(value.Equals(JsonNull.Null), Is.False, "NEQ null");
				Assert.That(value.Equals(JsonNull.Error), Is.False, "NEQ error");
				Assert.That(value.Equals(default(JsonValue)), Is.True);
				Assert.That(value!.Equals(default(object)), Is.True);

				Assert.That(value.Equals(0), Is.False);
				Assert.That(value.Equals(123), Is.False);
				Assert.That(value.Equals(false), Is.False);
				Assert.That(value.Equals(""), Is.False);
				Assert.That(value.Equals("hello"), Is.False);
				Assert.That(value.Equals(JsonObject.Create()), Is.False);
				Assert.That(value.Equals(JsonArray.Create()), Is.False);

				Assert.That(value.StrictEquals(JsonNull.Missing), Is.True, "EQ missing");
				Assert.That(value.StrictEquals(JsonNull.Null), Is.False, "NEQ null");
				Assert.That(value.StrictEquals(JsonNull.Error), Is.False, "NEQ error");
				Assert.That(value.StrictEquals(false), Is.False);
				Assert.That(value.StrictEquals(0), Is.False);
				Assert.That(value.StrictEquals(""), Is.False);
				Assert.That(value.StrictEquals("null"), Is.False);

				Assert.That(value.CompareTo(default(JsonValue)), Is.EqualTo(0));
				Assert.That(value.CompareTo(JsonNull.Null), Is.EqualTo(1));
				Assert.That(value.CompareTo(JsonNull.Missing), Is.EqualTo(0));
				Assert.That(value.CompareTo(JsonNull.Error), Is.EqualTo(-1));
				Assert.That(value.CompareTo(0), Is.EqualTo(-1));
				Assert.That(value.CompareTo(123), Is.EqualTo(-1));
				Assert.That(value.CompareTo(""), Is.EqualTo(-1));
				Assert.That(value.CompareTo("hello"), Is.EqualTo(-1));
			}

			//note: JsonNull.Missing sould bind the same way as JsonNull.Null (=> default(T))
			// except for T == JsonValue or JsonNull, in which case it should return itself as the JsonNull.Missing singleton

			using (Assert.EnterMultipleScope())
			{
				Assert.That(missing.Bind(typeof(JsonValue)), Is.SameAs(JsonNull.Missing));
				Assert.That(missing.Bind<JsonValue>(), Is.SameAs(JsonNull.Missing));
				Assert.That(() => missing.Required<JsonValue>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(missing.As<JsonValue>(), Is.SameAs(JsonNull.Missing));
				Assert.That(missing.As<JsonValue>(null, resolver: CrystalJson.DefaultResolver), Is.SameAs(JsonNull.Missing));
				Assert.That(missing.As<JsonValue>(123), Is.EqualTo(JsonNumber.Create(123)));

				Assert.That(missing.Bind(typeof(JsonNull)), Is.SameAs(JsonNull.Missing));
				Assert.That(missing.Bind<JsonNull>(), Is.SameAs(JsonNull.Missing));
				Assert.That(() => missing.Required<JsonNull>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(missing.As<JsonNull>(), Is.SameAs(JsonNull.Missing));
				Assert.That(missing.As<JsonNull>(null, resolver: CrystalJson.DefaultResolver), Is.SameAs(JsonNull.Missing));
			}

			Assert.That(SerializeToSlice(missing), Is.EqualTo(Slice.FromString("null")));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(value, Is.Not.EqualTo((object) 0));
				Assert.That(value, Is.Not.EqualTo((object) false));
				Assert.That(value, Is.Not.EqualTo((object?) null));
				Assert.That(value, Is.Not.EqualTo((object) ""));

				Assert.That(value.ValueEquals<int>(0), Is.False);
				Assert.That(value.ValueEquals<bool>(false), Is.False);
				Assert.That(value.ValueEquals<string>(null), Is.True);
				Assert.That(value.ValueEquals<string>(""), Is.False);

				Assert.That(value.ValueEquals<int?>(0), Is.False);
				Assert.That(value.ValueEquals<int?>(null), Is.True);
				Assert.That(value.ValueEquals<bool?>(false), Is.False);
				Assert.That(value.ValueEquals<bool?>(null), Is.True);
			}
		}

		[Test]
		public void Test_JsonNull_Error()
		{
			var error = JsonNull.Error;
			Assert.That(error, Is.Not.Null);
			Assert.That(error, Is.InstanceOf<JsonNull>());

			var value = (JsonNull) error;
			using (Assert.EnterMultipleScope())
			{
				Assert.That(value.Type, Is.EqualTo(JsonType.Null), "value.Type");
				Assert.That(value.IsNull, Is.True, "value.IsNull");
				Assert.That(value.IsMissing, Is.False, "value.IsMissing");
				Assert.That(value.IsError, Is.True, "value.IsError");
				Assert.That(value.IsDefault, Is.True, "value.IsDefault");
				Assert.That(value.ToObject(), Is.Null, "value.ToObject()");
				Assert.That(value.ToString(), Is.EqualTo(""), "value.ToString()");
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(value.Equals(JsonNull.Error), Is.True, "EQ error");
				Assert.That(value.Equals(JsonNull.Null), Is.False, "NEQ null");
				Assert.That(value.Equals(JsonNull.Missing), Is.False, "NEQ missing");
				Assert.That(value.Equals(default(JsonValue)), Is.True);
				Assert.That(value!.Equals(default(object)), Is.True);

				Assert.That(value.Equals(0), Is.False);
				Assert.That(value.Equals(123), Is.False);
				Assert.That(value.Equals(false), Is.False);
				Assert.That(value.Equals(""), Is.False);
				Assert.That(value.Equals("hello"), Is.False);
				Assert.That(value.Equals(JsonObject.Create()), Is.False);
				Assert.That(value.Equals(JsonArray.Create()), Is.False);

				Assert.That(value.StrictEquals(JsonNull.Error), Is.True, "EQ error");
				Assert.That(value.StrictEquals(JsonNull.Null), Is.False, "NEQ null");
				Assert.That(value.StrictEquals(JsonNull.Missing), Is.False, "NEQ missing");
				Assert.That(value.StrictEquals(false), Is.False);
				Assert.That(value.StrictEquals(0), Is.False);
				Assert.That(value.StrictEquals(""), Is.False);
				Assert.That(value.StrictEquals("null"), Is.False);

				Assert.That(value.CompareTo(default(JsonValue)), Is.EqualTo(0));
				Assert.That(value.CompareTo(JsonNull.Null), Is.EqualTo(1));
				Assert.That(value.CompareTo(JsonNull.Missing), Is.EqualTo(1));
				Assert.That(value.CompareTo(JsonNull.Error), Is.EqualTo(0));
				Assert.That(value.CompareTo(0), Is.EqualTo(-1));
				Assert.That(value.CompareTo(123), Is.EqualTo(-1));
				Assert.That(value.CompareTo(""), Is.EqualTo(-1));
				Assert.That(value.CompareTo("hello"), Is.EqualTo(-1));
			}

			Assert.That(SerializeToSlice(error), Is.EqualTo(Slice.FromString("null")));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(value, Is.Not.EqualTo((object) 0));
				Assert.That(value, Is.Not.EqualTo((object) false));
				Assert.That(value, Is.Not.EqualTo((object?) null));
				Assert.That(value, Is.Not.EqualTo((object) ""));

				Assert.That(value.ValueEquals<int>(0), Is.False);
				Assert.That(value.ValueEquals<bool>(false), Is.False);
				Assert.That(value.ValueEquals<string>(null), Is.True);
				Assert.That(value.ValueEquals<string>(""), Is.False);

				Assert.That(value.ValueEquals<int?>(0), Is.False);
				Assert.That(value.ValueEquals<int?>(null), Is.True);
				Assert.That(value.ValueEquals<bool?>(false), Is.False);
				Assert.That(value.ValueEquals<bool?>(null), Is.True);
			}
		}

		#endregion

		#region JsonBoolean...

		[Test]
		public void Test_JsonBoolean()
		{
			// JsonBoolean.True

			Assert.That(JsonBoolean.True, Is.Not.Null);
			Assert.That(JsonBoolean.True.Type, Is.EqualTo(JsonType.Boolean));
			Assert.That(JsonBoolean.True.IsNull, Is.False);
			Assert.That(JsonBoolean.True.IsDefault, Is.False);
			Assert.That(JsonBoolean.True.IsReadOnly, Is.True);
			Assert.That(JsonBoolean.True.ToObject(), Is.True);
			Assert.That(JsonBoolean.True.ToString(), Is.EqualTo("true"));
			Assert.That(JsonBoolean.True.Equals(JsonBoolean.True), Is.True);
			Assert.That(JsonBoolean.True.Equals(JsonBoolean.False), Is.False);
			Assert.That(JsonBoolean.True.Equals(JsonNull.Null), Is.False);
			Assert.That(JsonBoolean.True.Equals(true), Is.True);
			Assert.That(JsonBoolean.True.Equals(false), Is.False);
			Assert.That(JsonBoolean.True, Is.SameAs(JsonBoolean.True));

			// JsonBoolean.False

			Assert.That(JsonBoolean.False, Is.Not.Null);
			Assert.That(JsonBoolean.False.Type, Is.EqualTo(JsonType.Boolean));
			Assert.That(JsonBoolean.False.IsNull, Is.False);
			Assert.That(JsonBoolean.False.IsDefault, Is.True);
			Assert.That(JsonBoolean.False.IsReadOnly, Is.True);
			Assert.That(JsonBoolean.False.ToObject(), Is.False);
			Assert.That(JsonBoolean.False.ToString(), Is.EqualTo("false"));
			Assert.That(JsonBoolean.False.Equals((JsonBoolean) JsonBoolean.True), Is.False);
			Assert.That(JsonBoolean.False.Equals((JsonBoolean) JsonBoolean.False), Is.True);
			Assert.That(JsonBoolean.False.Equals((JsonValue) JsonBoolean.True), Is.False);
			Assert.That(JsonBoolean.False.Equals((JsonValue) JsonBoolean.False), Is.True);
			Assert.That(JsonBoolean.False.Equals(JsonNull.Null), Is.False);
			Assert.That(JsonBoolean.False.Equals(true), Is.False);
			Assert.That(JsonBoolean.False.Equals(false), Is.True);

			// JsonBoolean.Return

			Assert.That(JsonBoolean.Return(false), Is.SameAs(JsonBoolean.False), "JsonBoolean.Return(false) should return the False singleton");
			Assert.That(JsonBoolean.Return(true), Is.SameAs(JsonBoolean.True), "JsonBoolean.Return(true) should return the True singleton");
			Assert.That(JsonBoolean.Return((bool?) null), Is.SameAs(JsonNull.Null), "JsonBoolean.Return(null) should return the Null singleton");
			Assert.That(JsonBoolean.Return((bool?) false), Is.SameAs(JsonBoolean.False), "JsonBoolean.Return(false) should return the False singleton");
			Assert.That(JsonBoolean.Return((bool?) true), Is.SameAs(JsonBoolean.True), "JsonBoolean.Return(true) should return the True singleton");

			// Conversions

			Assert.That(JsonBoolean.False.ToString(), Is.EqualTo("false"));
			Assert.That(JsonBoolean.False.Bind<string>(), Is.EqualTo("false"));
			Assert.That(JsonBoolean.False.Bind(typeof(string)), Is.EqualTo("false"));
			Assert.That(JsonBoolean.True.ToString(), Is.EqualTo("true"));
			Assert.That(JsonBoolean.True.Bind<string>(), Is.EqualTo("true"));
			Assert.That(JsonBoolean.True.Bind(typeof(string)), Is.InstanceOf<string>());

			Assert.That(JsonBoolean.False.ToInt32(), Is.EqualTo(0));
			Assert.That(JsonBoolean.False.Bind<int>(), Is.EqualTo(0));
			Assert.That(JsonBoolean.False.Bind(typeof(int)), Is.InstanceOf<int>());
			Assert.That(JsonBoolean.False.Bind(typeof(int)), Is.EqualTo(0));
			Assert.That(JsonBoolean.True.ToInt32(), Is.EqualTo(1));
			Assert.That(JsonBoolean.True.Bind<int>(), Is.EqualTo(1));
			Assert.That(JsonBoolean.True.Bind(typeof(int)), Is.InstanceOf<int>());
			Assert.That(JsonBoolean.True.Bind(typeof(int)), Is.EqualTo(1));

			Assert.That(JsonBoolean.False.ToInt64(), Is.EqualTo(0L));
			Assert.That(JsonBoolean.False.Bind<long>(), Is.EqualTo(0L));
			Assert.That(JsonBoolean.False.Bind(typeof(long)), Is.EqualTo(0L));
			Assert.That(JsonBoolean.True.ToInt64(), Is.EqualTo(1L));
			Assert.That(JsonBoolean.True.Bind<long>(), Is.EqualTo(1L));
			Assert.That(JsonBoolean.True.Bind(typeof(long)), Is.InstanceOf<long>());
			Assert.That(JsonBoolean.True.Bind(typeof(long)), Is.EqualTo(1L));

			Assert.That(JsonBoolean.False.ToSingle(), Is.EqualTo(0f));
			Assert.That(JsonBoolean.False.Bind<float>(), Is.EqualTo(0f));
			Assert.That(JsonBoolean.False.Bind(typeof(float)), Is.EqualTo(0f));
			Assert.That(JsonBoolean.True.ToSingle(), Is.EqualTo(1f));
			Assert.That(JsonBoolean.True.Bind<float>(), Is.EqualTo(1f));
			Assert.That(JsonBoolean.True.Bind(typeof(float)), Is.InstanceOf<float>());
			Assert.That(JsonBoolean.True.Bind(typeof(float)), Is.EqualTo(1f));

			Assert.That(JsonBoolean.False.ToDouble(), Is.EqualTo(0d));
			Assert.That(JsonBoolean.False.Bind<double>(), Is.EqualTo(0d));
			Assert.That(JsonBoolean.False.Bind(typeof(double)), Is.EqualTo(0d));
			Assert.That(JsonBoolean.True.ToDouble(), Is.EqualTo(1d));
			Assert.That(JsonBoolean.True.Bind<double>(), Is.EqualTo(1d));
			Assert.That(JsonBoolean.True.Bind(typeof(double)), Is.InstanceOf<double>());
			Assert.That(JsonBoolean.True.Bind(typeof(double)), Is.EqualTo(1d));

			Assert.That(JsonBoolean.False.ToDecimal(), Is.EqualTo(0m));
			Assert.That(JsonBoolean.False.Bind<decimal>(), Is.EqualTo(0m));
			Assert.That(JsonBoolean.False.Bind(typeof(decimal)), Is.EqualTo(0m));
			Assert.That(JsonBoolean.True.ToDecimal(), Is.EqualTo(1m));
			Assert.That(JsonBoolean.True.Bind<decimal>(), Is.EqualTo(1m));
			Assert.That(JsonBoolean.True.Bind(typeof(decimal)), Is.EqualTo(1m));
			Assert.That(JsonBoolean.True.Bind(typeof(decimal)), Is.InstanceOf<decimal>());

			Assert.That(JsonBoolean.False.ToGuid(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonBoolean.False.Bind<Guid>(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonBoolean.False.Bind(typeof(Guid)), Is.EqualTo(Guid.Empty));
			Assert.That(JsonBoolean.True.ToGuid(), Is.EqualTo(Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")));
			Assert.That(JsonBoolean.True.Bind<Guid>(), Is.EqualTo(Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")));
			Assert.That(JsonBoolean.True.Bind(typeof(Guid)), Is.EqualTo(Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")));
			Assert.That(JsonBoolean.True.Bind(typeof(Guid)), Is.InstanceOf<Guid>());

			Assert.That(SerializeToSlice(JsonBoolean.False), Is.EqualTo(Slice.FromBytes("false"u8)));
			Assert.That(SerializeToSlice(JsonBoolean.True), Is.EqualTo(Slice.FromBytes("true"u8)));
		}

		[Test]
		public void Test_JsonBoolean_ValueEquals()
		{
			Assert.That(JsonBoolean.False.ValueEquals<bool>(false), Is.True);
			Assert.That(JsonBoolean.False.ValueEquals<bool>(true), Is.False);
			Assert.That(JsonBoolean.False.ValueEquals(0), Is.False);
			Assert.That(JsonBoolean.False.ValueEquals(1), Is.False);
			Assert.That(JsonBoolean.False.ValueEquals(""), Is.False);

			Assert.That(JsonBoolean.True.ValueEquals<bool>(false), Is.False);
			Assert.That(JsonBoolean.True.ValueEquals<bool>(true), Is.True);
			Assert.That(JsonBoolean.True.ValueEquals(0), Is.False);
			Assert.That(JsonBoolean.True.ValueEquals(1), Is.False);
			Assert.That(JsonBoolean.True.ValueEquals("true"), Is.False);
		}

		[Test]
		public void Test_JsonBoolean_StrictEquals()
		{
			Assert.That(JsonBoolean.False.StrictEquals(JsonBoolean.False), Is.True);
			Assert.That(JsonBoolean.True.StrictEquals(JsonBoolean.True), Is.True);
			Assert.That(JsonBoolean.False.StrictEquals(false), Is.True);
			Assert.That(JsonBoolean.True.StrictEquals(true), Is.True);

			Assert.That(JsonBoolean.False.StrictEquals(JsonNull.Missing), Is.False);
			Assert.That(JsonBoolean.False.StrictEquals(0), Is.False);
			Assert.That(JsonBoolean.False.StrictEquals(""), Is.False);
			Assert.That(JsonBoolean.False.StrictEquals("false"), Is.False);
			Assert.That(JsonBoolean.True.StrictEquals(JsonArray.ReadOnly.Empty), Is.False);
			Assert.That(JsonBoolean.True.StrictEquals(1), Is.False);
			Assert.That(JsonBoolean.True.StrictEquals(""), Is.False);
			Assert.That(JsonBoolean.True.StrictEquals("true"), Is.False);
		}

		#endregion

		#region JsonString...

		[Test]
		public void Test_JsonString()
		{
			{ // empty singleton
				JsonValue value = JsonString.Empty;
				Assert.That(value, Is.Not.Null);
				Assert.That(value.Type, Is.EqualTo(JsonType.String));
				Assert.That(value, Is.InstanceOf<JsonString>());
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(""));
				Assert.That(value.ToString(), Is.EqualTo(""));
				Assert.That(value.Equals(JsonString.Empty), Is.True);
				Assert.That(value.Equals(String.Empty), Is.True);
				Assert.That(value.Equals(default(string)), Is.False);
				var str = (JsonString) value!;
				Assert.That(str.IsNullOrEmpty, Is.True);
				Assert.That(str.Length, Is.EqualTo(0));
				Assert.That(SerializeToSlice(str), Is.EqualTo(Slice.FromString("\"\"")));
			}

			{ // return null
				var value = JsonString.Return((string) null!);
				Assert.That(value, Is.Not.Null);
				Assert.That(value, Is.SameAs(JsonNull.Null));
			}

			{ // return empty string
				var value = JsonString.Return("");
				Assert.That(value, Is.Not.Null);
				Assert.That(value, Is.SameAs(JsonString.Empty));
			}

			{ // return string
				var value = JsonString.Return("Hello, World!");
				Assert.That(value, Is.Not.Null);
				Assert.That(value.Type, Is.EqualTo(JsonType.String));
				Assert.That(value, Is.InstanceOf<JsonString>());
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo("Hello, World!"));
				Assert.That(value.ToString(), Is.EqualTo("Hello, World!"));
				Assert.That(value.Equals("Hello, World!"), Is.True);
				Assert.That(value.Equals(JsonString.Return("Hello, World!")), Is.True);
				var str = (JsonString) value;
				Assert.That(str.IsNullOrEmpty, Is.False);
				Assert.That(str.Length, Is.EqualTo(13));
				Assert.That(SerializeToSlice(str), Is.EqualTo(Slice.FromString("\"Hello, World!\"")));
			}

			{ // create null
				Assert.That(() => JsonString.Create((string) null!), Throws.InstanceOf<ArgumentNullException>());
			}

			{ // create empty string
				JsonString value = JsonString.Create("");
				Assert.That(value, Is.Not.Null);
				Assert.That(value, Is.SameAs(JsonString.Empty));
			}

			{ // create string
				JsonString str = JsonString.Create("Hello, World!");
				Assert.That(str, Is.Not.Null);
				Assert.That(str.Type, Is.EqualTo(JsonType.String));
				Assert.That(str.IsNull, Is.False);
				Assert.That(str.IsDefault, Is.False);
				Assert.That(str.ToObject(), Is.EqualTo("Hello, World!"));
				Assert.That(str.ToString(), Is.EqualTo("Hello, World!"));
				Assert.That(str.Equals("Hello, World!"), Is.True);
				Assert.That(str.Equals(JsonString.Return("Hello, World!")), Is.True);
				Assert.That(str.IsNullOrEmpty, Is.False);
				Assert.That(str.Length, Is.EqualTo(13));
				Assert.That(SerializeToSlice(str), Is.EqualTo(Slice.FromString("\"Hello, World!\"")));
			}

			{ // return RoS<char>
				var value = JsonString.Return("***Hello, World!***".AsSpan(3, 13));
				Assert.That(value, Is.Not.Null);
				Assert.That(value.Type, Is.EqualTo(JsonType.String));
				Assert.That(value, Is.InstanceOf<JsonString>());
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo("Hello, World!"));
				Assert.That(value.ToString(), Is.EqualTo("Hello, World!"));
				Assert.That(value.Equals("Hello, World!"), Is.True);
				Assert.That(value.Equals(JsonString.Return("Hello, World!")), Is.True);
				var str = (JsonString) value;
				Assert.That(str.IsNullOrEmpty, Is.False);
				Assert.That(str.Length, Is.EqualTo(13));
				Assert.That(SerializeToSlice(str), Is.EqualTo(Slice.FromString("\"Hello, World!\"")));
			}

			{ // create RoS<char>
				JsonString str = JsonString.Create("***Hello, World!***".AsSpan(3, 13));
				Assert.That(str, Is.Not.Null);
				Assert.That(str.Type, Is.EqualTo(JsonType.String));
				Assert.That(str.IsNull, Is.False);
				Assert.That(str.IsDefault, Is.False);
				Assert.That(str.ToObject(), Is.EqualTo("Hello, World!"));
				Assert.That(str.ToString(), Is.EqualTo("Hello, World!"));
				Assert.That(str.Equals("Hello, World!"), Is.True);
				Assert.That(str.Equals(JsonString.Return("Hello, World!")), Is.True);
				Assert.That(str.IsNullOrEmpty, Is.False);
				Assert.That(str.Length, Is.EqualTo(13));
				Assert.That(SerializeToSlice(str), Is.EqualTo(Slice.FromString("\"Hello, World!\"")));
			}

			{ // from StringBuilder
				var sb = new StringBuilder("Hello").Append(", World!");
				var value = JsonString.Return(sb);
				Assert.That(value, Is.InstanceOf<JsonString>());
				Assert.That(value.ToStringOrDefault(), Is.EqualTo("Hello, World!"));
				Assert.That(value.ToObject(), Is.EqualTo("Hello, World!"));
				Assert.That(value.ToString(), Is.EqualTo("Hello, World!"));
				sb.Append('?');
				Assert.That(value.ToStringOrDefault(), Is.EqualTo("Hello, World!"), "Mutating the original StringBuilder should not have any impact on the string");
			}

			{ // return Guid
				var value = JsonString.Return(Guid.Parse("016f3491-9416-47e2-b627-f84c507056d8"));
				Assert.That(value, Is.Not.Null);
				Assert.That(value.Type, Is.EqualTo(JsonType.String));
				Assert.That(value, Is.InstanceOf<JsonString>());
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo("016f3491-9416-47e2-b627-f84c507056d8"));
				Assert.That(value.ToString(), Is.EqualTo("016f3491-9416-47e2-b627-f84c507056d8"));
				Assert.That(value.ToGuid(), Is.EqualTo(Guid.Parse("016f3491-9416-47e2-b627-f84c507056d8")));
				Assert.That(value.Equals("016f3491-9416-47e2-b627-f84c507056d8"), Is.True);
				Assert.That(value.Equals(Guid.Parse("016f3491-9416-47e2-b627-f84c507056d8")), Is.True);
				Assert.That(value.Equals(JsonString.Return("016f3491-9416-47e2-b627-f84c507056d8")), Is.True);
				var str = (JsonString) value;
				Assert.That(str.IsNullOrEmpty, Is.False);
				Assert.That(str.Length, Is.EqualTo(36));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("\"016f3491-9416-47e2-b627-f84c507056d8\"")));
			}

			{ // create Guid
				JsonString str = JsonString.Create(Guid.Parse("016f3491-9416-47e2-b627-f84c507056d8"));
				Assert.That(str, Is.Not.Null);
				Assert.That(str.Type, Is.EqualTo(JsonType.String));
				Assert.That(str.IsNull, Is.False);
				Assert.That(str.IsDefault, Is.False);
				Assert.That(str.ToObject(), Is.EqualTo("016f3491-9416-47e2-b627-f84c507056d8"));
				Assert.That(str.ToString(), Is.EqualTo("016f3491-9416-47e2-b627-f84c507056d8"));
				Assert.That(str.ToGuid(), Is.EqualTo(Guid.Parse("016f3491-9416-47e2-b627-f84c507056d8")));
				Assert.That(str.Equals("016f3491-9416-47e2-b627-f84c507056d8"), Is.True);
				Assert.That(str.Equals(Guid.Parse("016f3491-9416-47e2-b627-f84c507056d8")), Is.True);
				Assert.That(str.Equals(JsonString.Return("016f3491-9416-47e2-b627-f84c507056d8")), Is.True);
				Assert.That(str.IsNullOrEmpty, Is.False);
				Assert.That(str.Length, Is.EqualTo(36));
				Assert.That(SerializeToSlice(str), Is.EqualTo(Slice.FromString("\"016f3491-9416-47e2-b627-f84c507056d8\"")));
			}

			{ // from IP Address
				var value = JsonString.Return(IPAddress.Parse("192.168.1.2"));
				Assert.That(value, Is.Not.Null);
				Assert.That(value.Type, Is.EqualTo(JsonType.String));
				Assert.That(value, Is.InstanceOf<JsonString>());
				Assert.That(value.ToObject(), Is.EqualTo("192.168.1.2"));
				Assert.That(value.ToString(), Is.EqualTo("192.168.1.2"));
				Assert.That(value.Equals("192.168.1.2"), Is.True);
				Assert.That(value.Equals(JsonString.Return("192.168.1.2")), Is.True);
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("\"192.168.1.2\"")));

				Assert.That(JsonString.Return(default(IPAddress)), Is.EqualTo(JsonNull.Null));
				Assert.That(JsonString.Return(IPAddress.Loopback), Is.EqualTo((JsonString) "127.0.0.1"));
				Assert.That(JsonString.Return(IPAddress.Any), Is.EqualTo((JsonString) "0.0.0.0"));
				Assert.That(JsonString.Return(IPAddress.None), Is.EqualTo((JsonString) "255.255.255.255")); //note: None == Broadcast
				Assert.That(JsonString.Return(IPAddress.IPv6Loopback), Is.EqualTo((JsonString) "::1"));
				Assert.That(JsonString.Return(IPAddress.IPv6Any), Is.EqualTo((JsonString) "::")); //note: IPv6None == IPv6Any
			}

			{ // from Type

				// basic types: alias
				Assert.That(JsonString.Return(typeof(string)), Is.EqualTo((JsonValue) "string"));
				Assert.That(JsonString.Return(typeof(bool)), Is.EqualTo((JsonValue) "bool"));
				Assert.That(JsonString.Return(typeof(char)), Is.EqualTo((JsonValue) "char"));
				Assert.That(JsonString.Return(typeof(int)), Is.EqualTo((JsonValue) "int"));
				Assert.That(JsonString.Return(typeof(long)), Is.EqualTo((JsonValue) "long"));
				Assert.That(JsonString.Return(typeof(uint)), Is.EqualTo((JsonValue) "uint"));
				Assert.That(JsonString.Return(typeof(ulong)), Is.EqualTo((JsonValue) "ulong"));
				Assert.That(JsonString.Return(typeof(float)), Is.EqualTo((JsonValue) "float"));
				Assert.That(JsonString.Return(typeof(double)), Is.EqualTo((JsonValue) "double"));
				Assert.That(JsonString.Return(typeof(decimal)), Is.EqualTo((JsonValue) "decimal"));
				Assert.That(JsonString.Return(typeof(DateTime)), Is.EqualTo((JsonValue) "DateTime"));
				Assert.That(JsonString.Return(typeof(TimeSpan)), Is.EqualTo((JsonValue) "TimeSpan"));
				Assert.That(JsonString.Return(typeof(Guid)), Is.EqualTo((JsonValue) "Guid"));

				// basic nullable types: alias?
				Assert.That(JsonString.Return(typeof(bool?)), Is.EqualTo((JsonValue) "bool?"));
				Assert.That(JsonString.Return(typeof(char?)), Is.EqualTo((JsonValue) "char?"));
				Assert.That(JsonString.Return(typeof(int?)), Is.EqualTo((JsonValue) "int?"));
				Assert.That(JsonString.Return(typeof(long?)), Is.EqualTo((JsonValue) "long?"));
				Assert.That(JsonString.Return(typeof(uint?)), Is.EqualTo((JsonValue) "uint?"));
				Assert.That(JsonString.Return(typeof(ulong?)), Is.EqualTo((JsonValue) "ulong?"));
				Assert.That(JsonString.Return(typeof(float?)), Is.EqualTo((JsonValue) "float?"));
				Assert.That(JsonString.Return(typeof(double?)), Is.EqualTo((JsonValue) "double?"));
				Assert.That(JsonString.Return(typeof(decimal?)), Is.EqualTo((JsonValue) "decimal?"));
				Assert.That(JsonString.Return(typeof(DateTime?)), Is.EqualTo((JsonValue) "DateTime?"));
				Assert.That(JsonString.Return(typeof(TimeSpan?)), Is.EqualTo((JsonValue) "TimeSpan?"));
				Assert.That(JsonString.Return(typeof(Guid?)), Is.EqualTo((JsonValue) "Guid?"));

				// system types: Full Name
				Assert.That(
					JsonString.Return(typeof(List<string>)),
					Is.EqualTo((JsonValue) typeof(List<string>).FullName),
					"Core system types should only have NAMESPACE.NAME");

				// third party types: FullName + AssemblyName
				Assert.That(
					JsonString.Return(typeof(JsonValue)),
					Is.EqualTo((JsonValue) typeof(JsonValue).AssemblyQualifiedName),
					"Non-system types should have NEMESPACE.NAME, ASSEMBLY");

				Assert.That(JsonString.Return(default(Type)), Is.EqualTo(JsonNull.Null));
			}
		}

		[Test]
		public void Test_JsonString_Convert_ToBoolean()
		{
			Assert.That(JsonString.Return("false").ToBoolean(), Is.False);
			Assert.That(JsonString.Return("false").ToBooleanOrDefault(), Is.False);
			Assert.That(JsonString.Return("false").Bind<bool>(), Is.False);
			Assert.That(JsonString.Return("false").Bind(typeof(bool)), Is.False);
			Assert.That(JsonString.Return("true").ToBoolean(), Is.True);
			Assert.That(JsonString.Return("true").ToBooleanOrDefault(), Is.True);
			Assert.That(JsonString.Return("true").Bind<bool>(), Is.True);
			Assert.That(JsonString.Return("true").Bind(typeof(bool)), Is.True);
		}

		[Test]
		public void Test_JsonString_Convert_ToInt32()
		{
			Assert.That(JsonString.Return("0").ToInt32(), Is.EqualTo(0));
			Assert.That(JsonString.Return("0").ToInt32OrDefault(), Is.EqualTo(0));
			Assert.That(JsonString.Return("0").Bind<int>(), Is.EqualTo(0));
			Assert.That(JsonString.Return("0").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(0));
			Assert.That(JsonString.Return("1").ToInt32(), Is.EqualTo(1));
			Assert.That(JsonString.Return("1").ToInt32OrDefault(), Is.EqualTo(1));
			Assert.That(JsonString.Return("1").Bind<int>(), Is.EqualTo(1));
			Assert.That(JsonString.Return("1").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(1));
			Assert.That(JsonString.Return("123").ToInt32(), Is.EqualTo(123));
			Assert.That(JsonString.Return("123").ToInt32OrDefault(), Is.EqualTo(123));
			Assert.That(JsonString.Return("123").Bind<int>(), Is.EqualTo(123));
			Assert.That(JsonString.Return("123").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonString.Return("666666666").Bind<int>(), Is.EqualTo(666666666));
			Assert.That(JsonString.Return("666666666").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(666666666));
			Assert.That(JsonString.Return("2147483647").Bind<int>(), Is.EqualTo(int.MaxValue));
			Assert.That(JsonString.Return("2147483647").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(int.MaxValue));
			Assert.That(JsonString.Return("-2147483648").Bind<int>(), Is.EqualTo(int.MinValue));
			Assert.That(JsonString.Return("-2147483648").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(int.MinValue));

			Assert.That(((JsonString) JsonString.Return("123")).TryConvertToInt32(out var result), Is.True);
			Assert.That(result, Is.EqualTo(123));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToInt32(out result), Is.False);
			Assert.That(result, Is.Zero);

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToInt32(out result), Is.False);
			Assert.That(result, Is.Zero);
		}

		[Test]
		public void Test_JsonString_Convert_ToInt64()
		{
			Assert.That(JsonString.Return("0").ToInt64(), Is.EqualTo(0));
			Assert.That(JsonString.Return("1").Bind<long>(), Is.EqualTo(1));
			Assert.That(JsonString.Return("1").Bind(typeof(long)), Is.InstanceOf<long>().And.EqualTo(1));
			Assert.That(JsonString.Return("123").ToInt64(), Is.EqualTo(123));
			Assert.That(JsonString.Return("666666666").Bind<long>(), Is.EqualTo(666666666));
			Assert.That(JsonString.Return("666666666").Bind(typeof(long)), Is.InstanceOf<long>().And.EqualTo(666666666));
			Assert.That(JsonString.Return("9223372036854775807").Bind<long>(), Is.EqualTo(long.MaxValue));
			Assert.That(JsonString.Return("9223372036854775807").Bind(typeof(long)), Is.InstanceOf<long>().And.EqualTo(long.MaxValue));
			Assert.That(JsonString.Return("-9223372036854775808").Bind<long>(), Is.EqualTo(long.MinValue));
			Assert.That(JsonString.Return("-9223372036854775808").Bind(typeof(long)), Is.InstanceOf<long>().And.EqualTo(long.MinValue));
			Assert.That(JsonString.Return("123").Bind(typeof(long)), Is.InstanceOf<long>());

			Assert.That(((JsonString) JsonString.Return("123")).TryConvertToInt64(out var result), Is.True);
			Assert.That(result, Is.EqualTo(123L));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToInt64(out result), Is.False);
			Assert.That(result, Is.Zero);

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToInt64(out result), Is.False);
			Assert.That(result, Is.Zero);

		}

		[Test]
		public void Test_JsonString_Convert_ToSingle()
		{
			Assert.That(JsonString.Return("0").ToSingle(), Is.EqualTo(0f));
			Assert.That(JsonString.Return("1").Bind<float>(), Is.EqualTo(1f));
			Assert.That(JsonString.Return("1").Bind(typeof(float)), Is.EqualTo(1f));
			Assert.That(JsonString.Return("1.23").ToSingle(), Is.EqualTo(1.23f));
			Assert.That(JsonString.Return("1.23").Bind<float>(), Is.EqualTo(1.23f));
			Assert.That(JsonString.Return("1.23").Bind(typeof(float)), Is.InstanceOf<float>().And.EqualTo(1.23f));
			Assert.That(JsonString.Return("3.14159274").Bind<float>(), Is.EqualTo((float) Math.PI));
			Assert.That(JsonString.Return("3.14159274").Bind(typeof(float)), Is.InstanceOf<float>().And.EqualTo((float) Math.PI));
			Assert.That(JsonString.Return("NaN").Bind<float>(), Is.EqualTo(float.NaN));
			Assert.That(JsonString.Return("NaN").Bind(typeof(float)), Is.InstanceOf<float>().And.EqualTo(float.NaN));
			Assert.That(JsonString.Return("Infinity").Bind<float>(), Is.EqualTo(float.PositiveInfinity));
			Assert.That(JsonString.Return("Infinity").Bind(typeof(float)), Is.InstanceOf<float>().And.EqualTo(float.PositiveInfinity));
			Assert.That(JsonString.Return("-Infinity").Bind<float>(), Is.EqualTo(float.NegativeInfinity));
			Assert.That(JsonString.Return("-Infinity").Bind(typeof(float)), Is.InstanceOf<float>().And.EqualTo(float.NegativeInfinity));

			Assert.That(((JsonString) JsonString.Return("1.23")).TryConvertToSingle(out var result), Is.True);
			Assert.That(result, Is.EqualTo(1.23f));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToSingle(out result), Is.False);
			Assert.That(result, Is.Zero);

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToSingle(out result), Is.False);
			Assert.That(result, Is.Zero);
		}

		[Test]
		public void Test_JsonString_Convert_ToDouble()
		{
			Assert.That(JsonString.Return("0").ToDouble(), Is.EqualTo(0d));
			Assert.That(JsonString.Return("1").Bind(typeof(double)), Is.EqualTo(1d));
			Assert.That(JsonString.Return("1.23").ToDouble(), Is.EqualTo(1.23d));
			Assert.That(JsonString.Return("3.1415926535897931").Bind(typeof(double)), Is.EqualTo(Math.PI));
			Assert.That(JsonString.Return("NaN").Bind(typeof(double)), Is.EqualTo(double.NaN));
			Assert.That(JsonString.Return("Infinity").Bind(typeof(double)), Is.EqualTo(double.PositiveInfinity));
			Assert.That(JsonString.Return("-Infinity").Bind(typeof(double)), Is.EqualTo(double.NegativeInfinity));
			Assert.That(JsonString.Return("1.23").Bind(typeof(double)), Is.InstanceOf<double>());

			Assert.That(((JsonString) JsonString.Return("1.23")).TryConvertToDouble(out var result), Is.True);
			Assert.That(result, Is.EqualTo(1.23d));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToDouble(out result), Is.False);
			Assert.That(result, Is.Zero);

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToDouble(out result), Is.False);
			Assert.That(result, Is.Zero);
		}

		[Test]
		public void Test_JsonString_Convert_ToDecimal()
		{
			Assert.That(JsonString.Return("0").ToDecimal(), Is.EqualTo(decimal.Zero));
			Assert.That(JsonString.Return("1").Bind(typeof(decimal)), Is.EqualTo(decimal.One));
			Assert.That(JsonString.Return("-1").Bind(typeof(decimal)), Is.EqualTo(decimal.MinusOne));
			Assert.That(JsonString.Return("1.23").ToDecimal(), Is.EqualTo(1.23m));
			Assert.That(JsonString.Return("3.1415926535897931").Bind(typeof(decimal)), Is.EqualTo(Math.PI));
			Assert.That(JsonString.Return("79228162514264337593543950335").Bind(typeof(decimal)), Is.EqualTo(decimal.MaxValue));
			Assert.That(JsonString.Return("-79228162514264337593543950335").Bind(typeof(decimal)), Is.EqualTo(decimal.MinValue));
			Assert.That(JsonString.Return("1.23").Bind(typeof(decimal)), Is.InstanceOf<decimal>());

			Assert.That(((JsonString) JsonString.Return("1.23")).TryConvertToDecimal(out var result), Is.True);
			Assert.That(result, Is.EqualTo(1.23m));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToDecimal(out result), Is.False);
			Assert.That(result, Is.Zero);

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToDecimal(out result), Is.False);
			Assert.That(result, Is.Zero);
		}

		[Test]
		public void Test_JsonString_Convert_ToGuid()
		{
			Assert.That(JsonString.Empty.ToGuid(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonString.Empty.ToGuidOrDefault(), Is.Null);
			Assert.That(JsonString.Return("00000000-0000-0000-0000-000000000000").ToGuid(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonString.Return("00000000-0000-0000-0000-000000000000").ToGuidOrDefault(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonString.Return("b771bab0-7ad2-4945-a501-1dd939ca9bac").ToGuid(), Is.EqualTo(new Guid("b771bab0-7ad2-4945-a501-1dd939ca9bac")));
			Assert.That(JsonString.Return("591d8e31-1b79-4532-b7b9-4f8a9c0d0010").Bind(typeof(Guid)), Is.EqualTo(new Guid("591d8e31-1b79-4532-b7b9-4f8a9c0d0010")));

			Assert.That(((JsonString) JsonString.Return("133a3e6c-9ce5-4e9f-afe4-fa8c59945704")).TryConvertToGuid(out var result), Is.True);
			Assert.That(result, Is.EqualTo(new Guid("133a3e6c-9ce5-4e9f-afe4-fa8c59945704")));

			Assert.That(((JsonString) JsonString.Return("133a3e6c-9ce5-4e9f-afe4-fa8c5994570")).TryConvertToGuid(out result), Is.False);
			Assert.That(result, Is.EqualTo(Guid.Empty));

			Assert.That(((JsonString) JsonString.Return("133a3e6c-9ce5-4e9f-afe4-fa8c599457045")).TryConvertToGuid(out result), Is.False);
			Assert.That(result, Is.EqualTo(Guid.Empty));

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToGuid(out result), Is.False);
			Assert.That(result, Is.EqualTo(Guid.Empty));
		}

		[Test]
		public void Test_JsonString_Convert_ToDateTime()
		{
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Unspecified)));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768Z").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Utc)));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+01:00").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+01").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-01").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local).AddHours(2)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+11:30").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local).AddHours(-10).AddMinutes(-30)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-11:30").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local).AddHours(12).AddMinutes(30)), "Ne marche que si la local TZ est Paris !");

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:34:56.768")).TryConvertToDateTime(out var result), Is.True);
			Assert.That(result, Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Unspecified)));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:34:56.768Z")).TryConvertToDateTime(out result), Is.True);
			Assert.That(result, Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Utc)));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:34:56.768+01:00")).TryConvertToDateTime(out result), Is.True);
			Assert.That(result, Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local)));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("yyyy-mm-ddThh:mm:ss.fff")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-13-01T12:34:56.768")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-03-32T12:34:56.768")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T25:34:56.768")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:60:56.768")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:34:60.768")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:34:56.00a")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));
		}

		[Test]
		public void Test_JsonString_Convert_ToDateTimeOffset()
		{
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768Z").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.Zero)));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+01:00").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(1))));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+04").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(4))));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-07").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(-7))));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-11").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(-11))));
		}

		[Test]
		public void Test_JsonString_Convert_Custom_Enum()
		{
			Assert.That(JsonString.Return("Foo").Required<DummyJsonEnum>(), Is.EqualTo(DummyJsonEnum.Foo));
			Assert.That(JsonString.Return("Bar").Required<DummyJsonEnum>(), Is.EqualTo(DummyJsonEnum.Bar));
			Assert.That(JsonString.Return("Bar").Required<DummyJsonEnumTypo>(), Is.EqualTo(DummyJsonEnumTypo.Bar));
			Assert.That(JsonString.Return("Barrh").Required<DummyJsonEnumTypo>(), Is.EqualTo(DummyJsonEnumTypo.Bar));
			Assert.That(() => JsonString.Return("Barrh").Required<DummyJsonEnum>(), Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_JsonString_Convert_IpAddress()
		{
			Assert.That(JsonNull.Null.As<IPAddress>(), Is.Null);
			Assert.That(JsonString.Empty.As<IPAddress>(), Is.Null);
			Assert.That(JsonString.Return("127.0.0.1").Required<IPAddress>(), Is.EqualTo(IPAddress.Loopback));
			Assert.That(JsonString.Return("0.0.0.0").Required<IPAddress>(), Is.EqualTo(IPAddress.Any));
			Assert.That(JsonString.Return("255.255.255.255").Required<IPAddress>(), Is.EqualTo(IPAddress.None));
			Assert.That(JsonString.Return("192.168.1.2").Required<IPAddress>(), Is.EqualTo(IPAddress.Parse("192.168.1.2")));
			Assert.That(JsonString.Return("::1").Required<IPAddress>(), Is.EqualTo(IPAddress.IPv6Loopback));
			Assert.That(JsonString.Return("::").Required<IPAddress>(), Is.EqualTo(IPAddress.IPv6Any));
			Assert.That(JsonString.Return("fe80::b8bc:1664:15a0:3a79%11").Required<IPAddress>(), Is.EqualTo(IPAddress.Parse("fe80::b8bc:1664:15a0:3a79%11")));
			Assert.That(JsonString.Return("[::1]").Required<IPAddress>(), Is.EqualTo(IPAddress.IPv6Loopback));
			Assert.That(JsonString.Return("[::]").Required<IPAddress>(), Is.EqualTo(IPAddress.IPv6Any));
			Assert.That(JsonString.Return("[fe80::b8bc:1664:15a0:3a79%11]").Required<IPAddress>(), Is.EqualTo(IPAddress.Parse("fe80::b8bc:1664:15a0:3a79%11")));
			Assert.That(() => JsonString.Return("127.0.0.").Required<IPAddress>(), Throws.InstanceOf<FormatException>());
			Assert.That(() => JsonString.Return("127.0.0.1.2").Required<IPAddress>(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_JsonString_Convert_Empty_String()
		{

			{ // empty => T : must return default(T) so 0/false/...
				Assert.That(JsonString.Empty.Required<bool>(), Is.False, "'' -> bool");
				Assert.That(JsonString.Empty.Required<int>(), Is.Zero, "'' -> int");
				Assert.That(JsonString.Empty.Required<long>(), Is.Zero, "'' -> long");
				Assert.That(JsonString.Empty.Required<float>(), Is.Zero, "'' -> float");
				Assert.That(JsonString.Empty.Required<double>(), Is.Zero, "'' -> double");
				Assert.That(JsonString.Empty.Required<DateTime>(), Is.EqualTo(DateTime.MinValue), "'' -> DateTime");
				Assert.That(JsonString.Empty.Required<DateTimeOffset>(), Is.EqualTo(DateTimeOffset.MinValue), "'' -> DateTimeOffset");
			}

			{ // empty => T?: must return default(T?) so null
				Assert.That(JsonString.Empty.As<bool?>(), Is.Null, "'' -> bool?");
				Assert.That(JsonString.Empty.As<int?>(), Is.Null, "'' -> int?");
				Assert.That(JsonString.Empty.As<long?>(), Is.Null, "'' -> long?");
				Assert.That(JsonString.Empty.As<float?>(), Is.Null, "'' -> float?");
				Assert.That(JsonString.Empty.As<double?>(), Is.Null, "'' -> double?");
				Assert.That(JsonString.Empty.As<DateTime?>(), Is.Null, "'' -> DateTime?");
				Assert.That(JsonString.Empty.As<DateTimeOffset?>(), Is.Null, "'' -> DateTimeOffset?");
			}
		}

		[Test]
		public void Test_JsonString_Convert_AutoCast()
		{
			JsonValue value = "hello"; // implicit cast
			Assert.That(value, Is.Not.Null);
			Assert.That(value, Is.InstanceOf<JsonString>());
			JsonString str = (JsonString) value;
			Assert.That(str.Value, Is.EqualTo("hello"));

			var s = (string?) value; // explicit cast
			Assert.That(s, Is.Not.Null);
			Assert.That(s, Is.EqualTo("hello"));
		}

		[Test]
		public void Test_JsonString_Comparisons()
		{

			// comparisons
			void Compare(string a, string b)
			{
				JsonValue ja = JsonString.Return(a);
				JsonValue jb = JsonString.Return(b);

				Assert.That(Math.Sign(ja.CompareTo(jb)), Is.EqualTo(Math.Sign(string.CompareOrdinal(a, b))), $"'{a}' cmp '{b}'");
				Assert.That(Math.Sign(jb.CompareTo(ja)), Is.EqualTo(Math.Sign(string.CompareOrdinal(b, a))), $"'{b}' cmp '{a}'");
			}

			Compare("", "");
			Compare("abc", "");
			Compare("abc", "abc");
			Compare("aaa", "bbb");
			Compare("aa", "a");
			Compare("aa", "aaa");
			Compare("ABC", "abc");
			Compare("bat", "batman");

			void SortStrings(string message, string[] ss, string[] expected)
			{
				var arr = ss.Select(x => JsonString.Return(x)).ToArray();
				Log(string.Join<JsonValue>(", ", arr));
				Array.Sort(arr);
				Log(string.Join<JsonValue>(", ", arr));
				Assert.That(arr.Select(x => x.ToString()).ToArray(), Is.EqualTo(expected), message);
			}

			SortStrings(
				"sorting should use ordinal, case sensitive",
				["a", "b", "c", "aa", "ab", "aC", "aaa", "abc"],
				["a", "aC", "aa", "aaa", "ab", "abc", "b", "c"]
			);
			SortStrings(
				"sorting should use lexicographical order",
				["cat", "bat", "catamaran", "catZ", "batman"],
				["bat", "batman", "cat", "catZ", "catamaran"]
			);
			SortStrings(
				"numbers < UPPERs << lowers",
				["a", "1", "A"],
				["1", "A", "a"]
			);
			SortStrings(
				"numbers should be sorted lexicographically if comparing strings (1 < 10 < 2)",
				["0", "1", "2", "7", "10", "42", "100", "1000"],
				["0", "1", "10", "100", "1000", "2", "42", "7"]
			);

			// cmp with numbers
			Assert.That(JsonString.Return("ABC").CompareTo(JsonNumber.Return(123)),  Is.GreaterThan(0), "'ABC' cmp 123");
			Assert.That(JsonNumber.Return(123).CompareTo(JsonString.Return("ABC")),  Is.LessThan(0),    "123 cmp 'ABC'");

			Assert.That(JsonString.Return("123").CompareTo(JsonNumber.Return(123)),  Is.EqualTo(0),     "'123' cmp 123");
			Assert.That(JsonString.Return("100").CompareTo(JsonNumber.Return(123)),  Is.LessThan(0),    "'100' cmp 123");
			Assert.That(JsonString.Return("1000").CompareTo(JsonNumber.Return(123)), Is.GreaterThan(0), "'1000' cmp 123");
			Assert.That(JsonNumber.Return(123).CompareTo(JsonString.Return("123")),  Is.EqualTo(0),     "123 cmp '123'");
			Assert.That(JsonNumber.Return(123).CompareTo(JsonString.Return("100")),  Is.GreaterThan(0), "123 cmp '100'");
			Assert.That(JsonNumber.Return(123).CompareTo(JsonString.Return("1000")), Is.LessThan(0),    "123 cmp '1000'");
		}

		[Test]
		public void Test_JsonString_ValueEquals()
		{
			Assert.That(JsonString.Return("hello").ValueEquals<string>("hello"), Is.True);
			Assert.That(JsonString.Return("hello").ValueEquals<string>(""), Is.False);
			Assert.That(JsonString.Return("hello").ValueEquals<string>("hello"), Is.True);
			Assert.That(JsonString.Return("").ValueEquals<string>(""), Is.True);
			Assert.That(JsonString.Return("").ValueEquals<string>("hello"), Is.False);
			Assert.That(JsonString.Return("").ValueEquals<string>(null), Is.False);

			Assert.That(JsonString.Return("hello").ValueEquals<int>(123), Is.False);
			Assert.That(JsonString.Return("123").ValueEquals<int>(123), Is.False);
			Assert.That(JsonString.Return("").ValueEquals<bool>(false), Is.False);
		}

		[Test]
		public void Test_JsonString_StrictEquals()
		{
			Assert.That(JsonString.Return("").StrictEquals(""), Is.True);
			Assert.That(JsonString.Return("hello").StrictEquals("hello"), Is.True);

			Assert.That(JsonString.Return("hello").StrictEquals("world"), Is.False);
			Assert.That(JsonString.Return("hello").StrictEquals("Hello"), Is.False);
			Assert.That(JsonString.Return("123").StrictEquals(123), Is.False);
			Assert.That(JsonString.Return("").StrictEquals(false), Is.False);
			Assert.That(JsonString.Return("false").StrictEquals(false), Is.False);
			Assert.That(JsonString.Return("true").StrictEquals(true), Is.False);
		}

		[Test]
		public void Test_JsonDateTime()
		{
			{
				var value = JsonDateTime.MinValue;
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.True);
				Assert.That(value.ToDateTime(), Is.EqualTo(DateTime.MinValue));
				Assert.That(value.ToDateOnly(), Is.EqualTo(DateOnly.MinValue));
				Assert.That(value.IsLocalTime, Is.False, "MinValue should be unspecifed");
				Assert.That(value.IsUtc, Is.False, "MinValue should be unspecified");
			}

			{
				var value = JsonDateTime.MaxValue;
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(DateTime.MaxValue));
				Assert.That(value.ToDateOnly(), Is.EqualTo(DateOnly.MaxValue));
				Assert.That(value.IsLocalTime, Is.False, "MaxValue should be unspecified");
				Assert.That(value.IsUtc, Is.False, "MaxValue should be unspecified");
			}

			{
				var value = JsonDateTime.DateOnlyMaxValue;
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(DateTime.MaxValue.Date));
				Assert.That(value.ToDateOnly(), Is.EqualTo(DateOnly.MaxValue));
				Assert.That(value.IsLocalTime, Is.False, "MaxValue should be unspecified");
				Assert.That(value.IsUtc, Is.False, "MaxValue should be unspecified");
			}

			{
				var value = new JsonDateTime(1974, 3, 24);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(new DateTime(1974, 3, 24)));
				Assert.That(value.ToDateOnly(), Is.EqualTo(new DateOnly(1974, 3, 24)));
				Assert.That(value.IsLocalTime, Is.False, "TZ is unspecified");
				Assert.That(value.IsUtc, Is.False, "TZ is unspecified");
			}

			{
				var value = new JsonDateTime(1974, 3, 24, 12, 34, 56, DateTimeKind.Utc);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(new DateTime(1974, 3, 24, 12, 34, 56, DateTimeKind.Utc)));
				Assert.That(value.ToDateOnly(), Is.EqualTo(new DateOnly(1974, 3, 24)));
				Assert.That(value.IsLocalTime, Is.False);
				Assert.That(value.IsUtc, Is.True);
			}

			{
				var value = new JsonDateTime(1974, 3, 24, 12, 34, 56, 789, DateTimeKind.Local);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(new DateTime(1974, 3, 24, 12, 34, 56, 789, DateTimeKind.Local)));
				Assert.That(value.ToDateOnly(), Is.EqualTo(new DateOnly(1974, 3, 24)));
				Assert.That(value.IsLocalTime, Is.True);
				Assert.That(value.IsUtc, Is.False);
			}

			{
				var now = DateTime.UtcNow;
				var value = new JsonDateTime(now.Ticks, DateTimeKind.Utc);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(now));
				Assert.That(value.ToDateOnly(), Is.EqualTo(DateOnly.FromDateTime(now)));
				Assert.That(value.IsLocalTime, Is.False);
				Assert.That(value.IsUtc, Is.True);
			}

			{
				var now = DateTimeOffset.Now;
				var today = DateOnly.FromDateTime(now.LocalDateTime);
				var value = new JsonDateTime(today);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(today.ToDateTime(default)));
				Assert.That(value.ToDateOnly(), Is.EqualTo(today));
				Assert.That(value.ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(today.ToDateTime(default), now.Offset)));
				Assert.That(value.IsLocalTime, Is.False); // DateOnly are "unspecified"
				Assert.That(value.IsUtc, Is.False);
			}

			// Nullables

			Assert.That(JsonDateTime.Return((DateTime?) null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonDateTime.Return((DateTimeOffset?) null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonDateTime.Return((DateOnly?) null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonDateTime.Return((DateTime?) DateTime.Now).Type, Is.EqualTo(JsonType.DateTime));
			Assert.That(JsonDateTime.Return((DateTimeOffset?) DateTimeOffset.Now).Type, Is.EqualTo(JsonType.DateTime));
			Assert.That(JsonDateTime.Return((DateOnly?) DateOnly.FromDateTime(DateTime.Now)).Type, Is.EqualTo(JsonType.DateTime));
		}

		[Test]
		public void Test_JsonGuid()
		{
			{
				var guid = Guid.NewGuid();
				var value = JsonString.Return(guid);
				Assert.That(value, Is.Not.Null);
				Assert.That(value.Type, Is.EqualTo(JsonType.String)); //note: for now, GUIDs are represented as strings with format "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.ToString(), Is.EqualTo(guid.ToString("D"))); // we expected something like "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
				Assert.That(value.ToStringOrDefault(), Is.EqualTo(guid.ToString("D"))); // we expected something like "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
				Assert.That(value.ToGuid(), Is.EqualTo(guid));
				Assert.That(value.ToGuidOrDefault(), Is.EqualTo(guid));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("\"" + guid.ToString("D") + "\"")));
			}

			{
				var value = JsonString.Return(Guid.Empty);
				Assert.That(value, Is.Not.Null);
				Assert.That(value, Is.SameAs(JsonNull.Null)); //REVIEW: for now, Guid.Empty => JsonNull.Null. Maybe change this to return JsonString.Empty?
				Assert.That(value.ToString(), Is.EqualTo(string.Empty));
				Assert.That(value.ToStringOrDefault(), Is.Null);
				Assert.That(value.ToGuid(), Is.EqualTo(Guid.Empty));
				Assert.That(value.ToGuidOrDefault(), Is.Null);
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("null")), "SerializeToSlice");
			}

			// Nullables
			{
				Assert.That(JsonString.Return((Guid?) null), Is.SameAs(JsonNull.Null));
				Assert.That(JsonString.Return((Guid?) Guid.Empty), Is.SameAs(JsonNull.Null)); //REVIEW: for now, Guid.Empty => JsonNull.Null. Maybe change this to return JsonString.Empty?
				Assert.That(JsonString.Return((Guid?) Guid.NewGuid()).Type, Is.EqualTo(JsonType.String));
			}
		}

		#endregion

		#region JsonNumber...

		[Test]
		public void Test_JsonNumber()
		{
			{ // 0 (singleton)
				var value = JsonNumber.Zero;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.True);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(0));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("0"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("0")));
			}

			{ // 1 (singleton)
				var value = JsonNumber.One;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(1));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("1"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("1")));
			}

			{ // -1 (singleton)
				var value = JsonNumber.MinusOne;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(-1));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.True);
				Assert.That(value.ToString(), Is.EqualTo("-1"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("-1")));
			}

			{ // 123 (cached)
				var value = JsonNumber.Create(123);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToInt32(), Is.EqualTo(123));
				Assert.That(value.ToObject(), Is.EqualTo(123));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.Literal, Is.EqualTo("123"));
				Assert.That(value.ToString(), Is.EqualTo("123"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("123")));
			}

			{ // -123 (cached)
				var value = JsonNumber.Create(-123);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToInt32(), Is.EqualTo(-123));
				Assert.That(value.ToObject(), Is.EqualTo(-123));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.True);
				Assert.That(value.Literal, Is.EqualTo("-123"));
				Assert.That(value.ToString(), Is.EqualTo("-123"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("-123")));
			}

			{ // 123456 (not cached)
				var value = JsonNumber.Create(123456);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToInt32(), Is.EqualTo(123456));
				Assert.That(value.ToObject(), Is.EqualTo(123456));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.Literal, Is.EqualTo("123456"));
				Assert.That(value.ToString(), Is.EqualTo("123456"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("123456")));
			}

			{ // -123456 (not cached)
				var value = JsonNumber.Create(-123456);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToInt32(), Is.EqualTo(-123456));
				Assert.That(value.ToObject(), Is.EqualTo(-123456));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.True);
				Assert.That(value.Literal, Is.EqualTo("-123456"));
				Assert.That(value.ToString(), Is.EqualTo("-123456"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("-123456")));
			}

			{ // int.MaxValue + 1 (long)
				var value = JsonNumber.Create(1L + int.MaxValue); // outside the range of Int32, so should be stored as an unsigned long
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(2147483648));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("2147483648"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("2147483648")));
			}

			{ // 123UL (cached)
				var value = JsonNumber.Create(123UL);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(123));
				Assert.That(value.ToObject(), Is.InstanceOf<long>(), "small integers should be converted to 'long'");
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("123"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("123")));

				Assert.That(value, Is.SameAs(JsonNumber.Return(123)), "should return the same cached instance");
			}

			{
				var value = JsonNumber.Create(1.23f);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(1.23f));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("1.23"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("1.23")));
			}

			{
				var value = JsonNumber.Create(-1.23f);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(-1.23f));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.IsNegative, Is.True);
				Assert.That(value.ToString(), Is.EqualTo("-1.23"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("-1.23")));
			}

			{
				var value = JsonNumber.Create(Math.PI);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(Math.PI));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo(Math.PI.ToString("R")));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString(Math.PI.ToString("R"))));
			}

			{
				var value = JsonNumber.Create(double.NaN);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(double.NaN));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.ToString(), Is.EqualTo("NaN"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("NaN")));
			}

			{
				var value = JsonNumber.DecimalZero;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.True);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(0d));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("0"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("0")));
			}

			{
				var value = JsonNumber.DecimalOne;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(1d));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("1"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("1")));
			}

			// Nullables

			Assert.That(JsonNumber.Return((int?) null), Is.SameAs(JsonNull.Null));
			Assert.That(JsonNumber.Return((uint?) null), Is.SameAs(JsonNull.Null));
			Assert.That(JsonNumber.Return((long?) null), Is.SameAs(JsonNull.Null));
			Assert.That(JsonNumber.Return((ulong?) null), Is.SameAs(JsonNull.Null));
			Assert.That(JsonNumber.Return((float?) null), Is.SameAs(JsonNull.Null));
			Assert.That(JsonNumber.Return((double?) null), Is.SameAs(JsonNull.Null));

			Assert.That(JsonNumber.Return((int?) 42), Is.InstanceOf<JsonNumber>().And.EqualTo(42));
			Assert.That(JsonNumber.Return((uint?) 42), Is.InstanceOf<JsonNumber>().And.EqualTo(42U));
			Assert.That(JsonNumber.Return((long?) 42), Is.InstanceOf<JsonNumber>().And.EqualTo(42L));
			Assert.That(JsonNumber.Return((ulong?) 42), Is.InstanceOf<JsonNumber>().And.EqualTo(42UL));
			Assert.That(JsonNumber.Return((float?) 3.14f), Is.InstanceOf<JsonNumber>().And.EqualTo(3.14f));
			Assert.That(JsonNumber.Return((double?) 3.14d), Is.InstanceOf<JsonNumber>().And.EqualTo(3.14d));

			// Conversions

			// Primitive
			Assert.That(JsonNumber.Create(123).ToInt32(), Is.EqualTo(123));
			Assert.That(JsonNumber.Create(-123).ToInt32(), Is.EqualTo(-123));
			Assert.That(JsonNumber.Create(123L).ToInt64(), Is.EqualTo(123L));
			Assert.That(JsonNumber.Create(-123L).ToInt64(), Is.EqualTo(-123L));
			Assert.That(JsonNumber.Create(123f).ToSingle(), Is.EqualTo(123f));
			Assert.That(JsonNumber.Create(123d).ToDouble(), Is.EqualTo(123d));
			Assert.That(JsonNumber.Create(Math.PI).ToDouble(), Is.EqualTo(Math.PI));

			Assert.That(JsonNumber.Create(123).Required<int>(), Is.EqualTo(123));
			Assert.That(JsonNumber.Create(-123).Required<int>(), Is.EqualTo(-123));
			Assert.That(JsonNumber.Create(123L).Required<long>(), Is.EqualTo(123L));
			Assert.That(JsonNumber.Create(-123L).Required<long>(), Is.EqualTo(-123L));
			Assert.That(JsonNumber.Create(123f).Required<float>(), Is.EqualTo(123f));
			Assert.That(JsonNumber.Create(123d).Required<double>(), Is.EqualTo(123d));
			Assert.That(JsonNumber.Create(Math.PI).Required<double>(), Is.EqualTo(Math.PI));

			// Enum
			// ... that derives from Int32
			Assert.That(JsonNumber.Zero.Bind(typeof (DummyJsonEnum), null), Is.EqualTo(DummyJsonEnum.None), "{0}.Bind(DummyJsonEnum)");
			Assert.That(JsonNumber.One.Bind(typeof (DummyJsonEnum), null), Is.EqualTo(DummyJsonEnum.Foo), "{1}.Bind(DummyJsonEnum)");
			Assert.That(JsonNumber.Create(42).Bind(typeof (DummyJsonEnum), null), Is.EqualTo(DummyJsonEnum.Bar), "{42}.Bind(DummyJsonEnum)");
			Assert.That(JsonNumber.Create(66).Bind(typeof (DummyJsonEnum), null), Is.EqualTo((DummyJsonEnum) 66), "{66}.Bind(DummyJsonEnum)");
			// ... that does not derive from Int32
			Assert.That(JsonNumber.Zero.Bind(typeof (DummyJsonEnumShort), null), Is.EqualTo(DummyJsonEnumShort.None), "{0}.Bind(DummyJsonEnumShort)");
			Assert.That(JsonNumber.One.Bind(typeof (DummyJsonEnumShort), null), Is.EqualTo(DummyJsonEnumShort.One), "{1}.Bind(DummyJsonEnumShort)");
			Assert.That(JsonNumber.Create(65535).Bind(typeof (DummyJsonEnumShort), null), Is.EqualTo(DummyJsonEnumShort.MaxValue), "{65535}.Bind(DummyJsonEnumShort)");

			// TimeSpan
			Assert.That(JsonNumber.Zero.ToTimeSpan(), Is.EqualTo(TimeSpan.Zero), "{0}.ToTimeSpan()");
			Assert.That(JsonNumber.Create(3600).ToTimeSpan(), Is.EqualTo(TimeSpan.FromHours(1)), "{3600}.ToTimeSpan()");
			Assert.That(
				JsonNumber.Return(TimeSpan.MaxValue.TotalSeconds + 1).ToTimeSpan(),
				Is.EqualTo(TimeSpan.MaxValue),
				"{TimeSpan.MaxValue.TotalSeconds + 1}.ToTimeSpan()");
			Assert.That(
				JsonNumber.Return(TimeSpan.MinValue.TotalSeconds - 1).ToTimeSpan(),
				Is.EqualTo(TimeSpan.MinValue),
				"{TimeSpan.MinValue.TotalSeconds - 1}.ToTimeSpan()");

			// DateTime
			//note: dates are converted into the number of days (floating point) since Unix Epoch, using UTC as the reference timezone
			Assert.That(JsonNumber.Zero.ToDateTime(), Is.EqualTo(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "0.ToDateTime()");
			Assert.That(JsonNumber.One.ToDateTime(), Is.EqualTo(new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc)), "1.ToDateTime()");
			Assert.That(JsonNumber.Create(86400).ToDateTime(), Is.EqualTo(new DateTime(1970, 1, 2, 0, 0, 0, DateTimeKind.Utc)), "86400.ToDateTime()");
			Assert.That(JsonNumber.Create(1484830412.854).ToDateTime(), Is.EqualTo(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc)).Within(TimeSpan.FromMilliseconds(1)), "(DAYS).ToDateTime()");
			Assert.That(JsonNumber.Return(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc)).ToDouble(), Is.EqualTo(1484830412.854), "(UTC).Value");
			Assert.That(JsonNumber.Return(new DateTime(2017, 1, 19, 13, 53, 32, 854, DateTimeKind.Local)).ToDouble(), Is.EqualTo(1484830412.854), "(LOCAL).Value");
			Assert.That(JsonNumber.Return(DateTime.MinValue).ToDouble(), Is.EqualTo(0), "MinValue"); // by convention, MinValue == 0 == epoch
			Assert.That(JsonNumber.Return(DateTime.MaxValue).ToDouble(), Is.EqualTo(double.NaN), "MaxValue"); // by convention, MaxValue == NaN
			Assert.That(JsonNumber.NaN.ToDateTime(), Is.EqualTo(DateTime.MaxValue), "MaxValue"); // by convention, NaN == MaxValue

			// DateTimeOffset
			//note: dates are converted into the number of days (floating point) since Unix Epoch, using UTC as the reference timezone
			Assert.That(JsonNumber.Zero.ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)), "0.ToDateTimeOffset()");
			Assert.That(JsonNumber.One.ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(1970, 1, 1, 0, 0, 1, TimeSpan.Zero)), "1.ToDateTimeOffset()");
			Assert.That(JsonNumber.Create(86400).ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(1970, 1, 2, 0, 0, 0, TimeSpan.Zero)), "86400.ToDateTimeOffset()");
			Assert.That(JsonNumber.Create(1484830412.854).ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2017, 1, 19, 12, 53, 32, 854, TimeSpan.Zero)).Within(TimeSpan.FromMilliseconds(1)), "(DAYS).ToDateTimeOffset()");
			Assert.That(JsonNumber.Create(new DateTimeOffset(2017, 1, 19, 12, 53, 32, 854, offset: TimeSpan.Zero)).ToDouble(), Is.EqualTo(1484830412.854), "(UTC).Value");
			Assert.That(JsonNumber.Create(new DateTimeOffset(2017, 1, 19, 13, 53, 32, 854, offset: TimeSpan.FromHours(1))).ToDouble(), Is.EqualTo(1484830412.854), "(LOCAL).Value");
			Assert.That(JsonNumber.Create(DateTimeOffset.MinValue).ToDouble(), Is.EqualTo(0), "MinValue"); // by convention, MinValue == 0 == epoch
			Assert.That(JsonNumber.Create(DateTimeOffset.MaxValue).ToDouble(), Is.EqualTo(double.NaN), "MaxValue"); // by convention, MaxValue == NaN
			Assert.That(JsonNumber.NaN.ToDateTimeOffset(), Is.EqualTo(DateTimeOffset.MaxValue), "MaxValue"); // by convention, NaN == MaxValue

			// DateOnly
			//note: dates are converted into the number of days (floating point) since Unix Epoch, using UTC as the reference timezone
			Assert.That(JsonNumber.Zero.ToDateOnly(), Is.EqualTo(new DateOnly(1970, 1, 1)), "0.ToDateOnly()");
			Assert.That(JsonNumber.One.ToDateOnly(), Is.EqualTo(new DateOnly(1970, 1, 2)), "1.ToDateOnly()");
			Assert.That(JsonNumber.Create(31).ToDateOnly(), Is.EqualTo(new DateOnly(1970, 2, 1)), "31.ToDateOnly()");
			Assert.That(JsonNumber.Create(new DateOnly(2017, 1, 19)).ToDouble(), Is.EqualTo(17185), "(DATE).Value");
			Assert.That(JsonNumber.Create(17185).ToDateOnly(), Is.EqualTo(new DateOnly(2017, 1, 19)), "(DAYS).ToDateOnly()");
			Assert.That(JsonNumber.Create(DateOnly.MinValue).ToDouble(), Is.EqualTo(0), "MinValue"); // by convention, MinValue == 0 == epoch
			Assert.That(JsonNumber.Create(DateOnly.MaxValue).ToDouble(), Is.EqualTo(double.NaN), "MaxValue"); // by convention, MaxValue == NaN
			Assert.That(JsonNumber.NaN.ToDateOnly(), Is.EqualTo(DateOnly.MaxValue), "MaxValue"); // by convention, NaN == MaxValue

			// TimeOnly
			//note: times are encoded as the number of seconds since midnight
			Assert.That(JsonNumber.Zero.ToTimeOnly(), Is.EqualTo(new TimeOnly(0, 0, 0)), "0.ToTimeOnly()");
			Assert.That(JsonNumber.One.ToTimeOnly(), Is.EqualTo(new TimeOnly(0, 0, 1)), "1.ToTimeOnly()");
			Assert.That(JsonNumber.Create(60).ToTimeOnly(), Is.EqualTo(new TimeOnly(0, 1, 0)), "31.ToTimeOnly()");
			Assert.That(JsonNumber.Create(3600).ToTimeOnly(), Is.EqualTo(new TimeOnly(1, 0, 0)), "31.ToTimeOnly()");
			Assert.That(JsonNumber.Create(new TimeOnly(12, 34, 56)).ToDouble(), Is.EqualTo(45296), "(TIME).Value");
			Assert.That(JsonNumber.Create(45296).ToTimeOnly(), Is.EqualTo(new TimeOnly(12, 34, 56)), "(SECONDS).ToTimeOnly()");
			Assert.That(JsonNumber.Create(TimeOnly.MinValue).ToDouble(), Is.EqualTo(0d), "MinValue"); // by convention, MinValue == 0 == midnight
			Assert.That(JsonNumber.Create(TimeOnly.MaxValue).ToDouble(), Is.EqualTo(86399.9999999d), "MaxValue"); // by convention, MaxValue == NaN
			Assert.That(JsonNumber.NaN.ToTimeOnly(), Is.EqualTo(TimeOnly.MaxValue), "MaxValue"); // by convention, NaN == MaxValue

			// Instant
			//note: instants are converted into the number of days (floating point) since Unix Epoch
			Assert.That(JsonNumber.Zero.ToInstant(), Is.EqualTo(NodaTime.Instant.FromUtc(1970, 1, 1, 0, 0, 0)), "0.ToInstant()");
			Assert.That(JsonNumber.One.ToInstant(), Is.EqualTo(NodaTime.Instant.FromUtc(1970, 1, 1, 0, 0, 1)), "1.ToInstant()");
			Assert.That(JsonNumber.Create(86400).ToInstant(), Is.EqualTo(NodaTime.Instant.FromUtc(1970, 1, 2, 0, 0, 0)), "86400.ToInstant()");
			Assert.That(JsonNumber.Create(1484830412.854).ToInstant(), Is.EqualTo(NodaTime.Instant.FromDateTimeUtc(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc)))/*.Within(NodaTime.Duration.FromMilliseconds(1))*/, "(DAYS).ToInstant()");
			Assert.That(JsonNumber.Create(NodaTime.Instant.FromDateTimeUtc(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc))).ToDouble(), Is.EqualTo(1484830412.854), "(UTC).Value");
			Assert.That(JsonNumber.Create(NodaTime.Instant.MinValue).ToDouble(), Is.EqualTo(NodaTime.Instant.FromUtc(-9998, 1 , 1, 0, 0, 0).ToUnixTimeSeconds()), "MinValue");
			Assert.That(JsonNumber.Create(NodaTime.Instant.MaxValue).ToDouble(), Is.EqualTo(NodaTime.Instant.FromUtc(9999, 12, 31, 23, 59, 59).ToUnixTimeSeconds() + 0.999999999d), "MaxValue");
			Assert.That(JsonNumber.NaN.ToInstant(), Is.EqualTo(NodaTime.Instant.MaxValue), "MaxValue"); //par convention, NaN == MaxValue

			// String
			Assert.That(JsonNumber.Zero.Bind<string>(), Is.EqualTo("0"));
			Assert.That(JsonNumber.One.Bind<string>(), Is.EqualTo("1"));
			Assert.That(JsonNumber.Create(123).Bind<string>(), Is.EqualTo("123"));
			Assert.That(JsonNumber.Create(-123).Bind<string>(), Is.EqualTo("-123"));
			Assert.That(JsonNumber.Create(Math.PI).Bind<string>(), Is.EqualTo(Math.PI.ToString("R")));

			// auto cast
			JsonNumber v;

			v = JsonNumber.Create(int.MaxValue);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v.IsUnsigned, Is.False);
			Assert.That(v.ToInt32(), Is.EqualTo(int.MaxValue));
			Assert.That(v.Required<int>(), Is.EqualTo(int.MaxValue));
			Assert.That((int) v, Is.EqualTo(int.MaxValue));

			v = JsonNumber.Create(uint.MaxValue);
			Assert.That(v.IsUnsigned, Is.False, "uint.MaxValue is small enough to fit in a long");
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.ToUInt32(), Is.EqualTo(uint.MaxValue));
			Assert.That(v.Required<uint>(), Is.EqualTo(uint.MaxValue));
			Assert.That((uint) v, Is.EqualTo(uint.MaxValue));

			v = JsonNumber.Create(long.MaxValue);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v.IsUnsigned, Is.False);
			Assert.That(v.ToInt64(), Is.EqualTo(long.MaxValue));
			Assert.That(v.Required<long>(), Is.EqualTo(long.MaxValue));
			Assert.That((long) v, Is.EqualTo(long.MaxValue));

			v = JsonNumber.Create(ulong.MaxValue);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v.IsUnsigned, Is.True);
			Assert.That(v.ToUInt64(), Is.EqualTo(ulong.MaxValue));
			Assert.That(v.Required<ulong>(), Is.EqualTo(ulong.MaxValue));
			Assert.That((ulong) v, Is.EqualTo(ulong.MaxValue));

			v = JsonNumber.Create(Math.PI);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.True);
			Assert.That(v.IsUnsigned, Is.False);
			Assert.That(v.ToDouble(), Is.EqualTo(Math.PI));
			Assert.That(v.Required<double>(), Is.EqualTo(Math.PI));
			Assert.That((double) v, Is.EqualTo(Math.PI));

			v = JsonNumber.Create(1.234f);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.True);
			Assert.That(v.IsUnsigned, Is.False);
			Assert.That(v.ToSingle(), Is.EqualTo(1.234f));
			Assert.That(v.Required<float>(), Is.EqualTo(1.234f));
			Assert.That((float) v, Is.EqualTo(1.234f));

			v = (JsonNumber) JsonNumber.Return((int?) 123);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v.IsUnsigned, Is.False);
			Assert.That(v.ToInt32OrDefault(), Is.EqualTo(123));
			Assert.That(v.As<int?>(), Is.EqualTo(123));
			Assert.That((int?) v, Is.EqualTo(123));

			v = (JsonNumber) JsonNumber.Return((uint?) 123);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v.IsUnsigned, Is.False, "123u fits in a long");
			Assert.That(v.ToUInt32OrDefault(), Is.EqualTo(123));
			Assert.That(v.As<uint?>(), Is.EqualTo(123));
			Assert.That((uint?) v, Is.EqualTo(123));
		}

		[Test]
		public void Test_JsonNumber_CompareTo()
		{
			#pragma warning disable CS1718
			#pragma warning disable NUnit2010
			#pragma warning disable NUnit2043
			// ReSharper disable EqualExpressionComparison
			// ReSharper disable CannotApplyEqualityOperatorToType

			// use random numbers, so that we don't end up just testing reference equality between cached small numbers
			int[] numbers = [ NextInt32(), NextInt32(), NextInt32() ];
			Assume.That(numbers, Is.Unique);
			Array.Sort(numbers);

			JsonNumber x0 = JsonNumber.Create(numbers[0]);
			JsonNumber x1 = JsonNumber.Create(numbers[1]);
			JsonNumber x2 = JsonNumber.Create(numbers[2]);

			// JsonNumber vs JsonNumber

			Assert.That(x0 == x0, Is.True);
			Assert.That(x0 == x1, Is.False);
			Assert.That(x0 == x2, Is.False);

			Assert.That(x0 != x0, Is.False);
			Assert.That(x0 != x1, Is.True);
			Assert.That(x0 != x2, Is.True);

			Assert.That(x0 < x0, Is.False);
			Assert.That(x0 < x1, Is.True);
			Assert.That(x0 < x2, Is.True);

			Assert.That(x0 <= x0, Is.True);
			Assert.That(x0 <= x1, Is.True);
			Assert.That(x0 <= x2, Is.True);

			Assert.That(x0 > x0, Is.False);
			Assert.That(x0 > x1, Is.False);
			Assert.That(x0 > x2, Is.False);

			Assert.That(x0 >= x0, Is.True);
			Assert.That(x0 >= x1, Is.False);
			Assert.That(x0 >= x2, Is.False);

			// JsonNumber vs ValueType (no allocations)
			// => this comparisons should not allocate any JsonValue instance during the operation

			Expression<Func<JsonNumber, int, bool>> expr1 = (num, x) => num < x;
			Assert.That(expr1.Body.NodeType, Is.EqualTo(ExpressionType.LessThan));
			Assert.That(((BinaryExpression)expr1.Body).Method?.Name, Is.EqualTo("op_LessThan"));
			Assert.That(((BinaryExpression)expr1.Body).Method?.GetParameters()[0].ParameterType, Is.EqualTo(typeof(JsonNumber)));
			Assert.That(((BinaryExpression)expr1.Body).Method?.GetParameters()[1].ParameterType, Is.EqualTo(typeof(long)));

			JsonNumber x = 1;

			Assert.That(x < 1, Is.False);
			Assert.That(x <= 1, Is.True);
			Assert.That(x > 1, Is.False);
			Assert.That(x >= 1, Is.True);

			Assert.That(x < 2, Is.True);
			Assert.That(x <= 2, Is.True);
			Assert.That(x > 2, Is.False);
			Assert.That(x >= 2, Is.False);

			// ReSharper restore CannotApplyEqualityOperatorToType
			// ReSharper restore EqualExpressionComparison
			#pragma warning restore NUnit2043
			#pragma warning restore NUnit2010
			#pragma warning restore CS1718
		}

		[Test]
		public void Test_JsonNumber_Between()
		{
			JsonNumber json;

			json = (JsonNumber) 123;
			Assert.That(json.IsBetween(0, 100), Is.False);
			Assert.That(json.IsBetween(0, 200), Is.True);
			Assert.That(json.IsBetween(150, 200), Is.False);
			Assert.That(json.IsBetween(100, 123), Is.True);
			Assert.That(json.IsBetween(123, 150), Is.True);
			Assert.That(json.IsBetween(123, 123), Is.True);

			json = (JsonNumber) 123.4d;
			Assert.That(json.IsBetween(0, 100), Is.False);
			Assert.That(json.IsBetween(0, 200), Is.True);
			Assert.That(json.IsBetween(150, 200), Is.False);
			Assert.That(json.IsBetween(100, 123), Is.False);
			Assert.That(json.IsBetween(100, 124), Is.True);
			Assert.That(json.IsBetween(123, 150), Is.True);
			Assert.That(json.IsBetween(124, 150), Is.False);
		}

		[Test]
		public void Test_JsonNumber_ValueEquals()
		{
			// int
			Assert.That(JsonNumber.Return(123).ValueEquals<int>(123), Is.True);
			Assert.That(JsonNumber.Return(123).ValueEquals<int>(456), Is.False);
			Assert.That(JsonNumber.Return(int.MaxValue).ValueEquals<int>(int.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(int.MinValue).ValueEquals<int>(int.MinValue), Is.True);
			Assert.That(JsonNumber.Return(int.MaxValue).ValueEquals<int>(int.MinValue), Is.False);
			Assert.That(JsonNumber.Return(123).ValueEquals<int?>(123), Is.True);
			Assert.That(JsonNumber.Return(123).ValueEquals<int?>(456), Is.False);
			Assert.That(JsonNumber.Return(123).ValueEquals<int?>(null), Is.False);

			// uint
			Assert.That(JsonNumber.Return(123U).ValueEquals<uint>(123), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<uint>(456), Is.False);
			Assert.That(JsonNumber.Return(uint.MaxValue).ValueEquals<uint>(uint.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<uint?>(123), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<uint?>(456), Is.False);
			Assert.That(JsonNumber.Return(123U).ValueEquals<uint?>(null), Is.False);

			// long
			Assert.That(JsonNumber.Return(123L).ValueEquals<long>(123), Is.True);
			Assert.That(JsonNumber.Return(123L).ValueEquals<long>(456), Is.False);
			Assert.That(JsonNumber.Return(long.MaxValue).ValueEquals<long>(long.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(long.MinValue).ValueEquals<long>(long.MinValue), Is.True);
			Assert.That(JsonNumber.Return(long.MaxValue).ValueEquals<long>(long.MinValue), Is.False);
			Assert.That(JsonNumber.Return(123L).ValueEquals<long?>(123), Is.True);
			Assert.That(JsonNumber.Return(123L).ValueEquals<long?>(456), Is.False);
			Assert.That(JsonNumber.Return(123L).ValueEquals<long?>(null), Is.False);

			// ulong
			Assert.That(JsonNumber.Return(123UL).ValueEquals<ulong>(123), Is.True);
			Assert.That(JsonNumber.Return(123UL).ValueEquals<ulong>(456), Is.False);
			Assert.That(JsonNumber.Return(ulong.MaxValue).ValueEquals<ulong>(ulong.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(123UL).ValueEquals<ulong?>(123), Is.True);
			Assert.That(JsonNumber.Return(123UL).ValueEquals<ulong?>(456), Is.False);
			Assert.That(JsonNumber.Return(123UL).ValueEquals<ulong?>(null), Is.False);

			// short
			Assert.That(JsonNumber.Return(123).ValueEquals<short>(123), Is.True);
			Assert.That(JsonNumber.Return(123).ValueEquals<short>(456), Is.False);
			Assert.That(JsonNumber.Return(short.MaxValue).ValueEquals<short>(short.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(short.MinValue).ValueEquals<short>(short.MinValue), Is.True);
			Assert.That(JsonNumber.Return(short.MaxValue).ValueEquals<short>(short.MinValue), Is.False);
			Assert.That(JsonNumber.Return(123).ValueEquals<short?>(123), Is.True);
			Assert.That(JsonNumber.Return(123).ValueEquals<short?>(456), Is.False);
			Assert.That(JsonNumber.Return(123).ValueEquals<short?>(null), Is.False);

			// ushort
			Assert.That(JsonNumber.Return(123U).ValueEquals<ushort>(123), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<ushort>(456), Is.False);
			Assert.That(JsonNumber.Return(ushort.MaxValue).ValueEquals<ushort>(ushort.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<ushort?>(123), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<ushort?>(456), Is.False);
			Assert.That(JsonNumber.Return(123U).ValueEquals<ushort?>(null), Is.False);

			// float
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float>(1.23f), Is.True);
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float>(4.56f), Is.False);
			Assert.That(JsonNumber.Return(float.NaN).ValueEquals<float>(float.NaN), Is.True);
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float>(float.NaN), Is.False);
			Assert.That(JsonNumber.Return(float.MaxValue).ValueEquals<float>(float.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(float.MinValue).ValueEquals<float>(float.MinValue), Is.True);
			Assert.That(JsonNumber.Return(float.MaxValue).ValueEquals<float>(float.MinValue), Is.False);
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float?>(1.23f), Is.True);
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float?>(4.56f), Is.False);
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float?>(null), Is.False);

			// double
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double>(Math.PI), Is.True);
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double>(3.14), Is.False);
			Assert.That(JsonNumber.Return(double.NaN).ValueEquals<double>(double.NaN), Is.True);
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double>(double.NaN), Is.False);
			Assert.That(JsonNumber.Return(double.MaxValue).ValueEquals<double>(double.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(double.MinValue).ValueEquals<double>(double.MinValue), Is.True);
			Assert.That(JsonNumber.Return(double.MaxValue).ValueEquals<double>(double.MinValue), Is.False);
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double?>(Math.PI), Is.True);
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double?>(3.14), Is.False);
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double?>(null), Is.False);

			// decimal
			Assert.That(JsonNumber.Return(decimal.One).ValueEquals<decimal>(decimal.One), Is.True);
			Assert.That(JsonNumber.Return(decimal.One).ValueEquals<decimal>(decimal.Zero), Is.False);
			Assert.That(JsonNumber.Return(decimal.MaxValue).ValueEquals<decimal>(decimal.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(decimal.MinValue).ValueEquals<decimal>(decimal.MinValue), Is.True);
			Assert.That(JsonNumber.Return(decimal.MaxValue).ValueEquals<decimal>(decimal.MinValue), Is.False);
			Assert.That(JsonNumber.Return(decimal.One).ValueEquals<decimal?>(decimal.One), Is.True);
			Assert.That(JsonNumber.Return(decimal.One).ValueEquals<decimal?>(decimal.Zero), Is.False);
			Assert.That(JsonNumber.Return(decimal.One).ValueEquals<decimal?>(null), Is.False);
		}

		[Test]
		public void Test_JsonNumber_StrictEquals()
		{
			Assert.That(JsonNumber.Return(123).StrictEquals(123), Is.True);
			Assert.That(JsonNumber.Return(123).StrictEquals(123L), Is.True);
			Assert.That(JsonNumber.Return(123).StrictEquals(123.0), Is.True);

			Assert.That(JsonNumber.Return(0).StrictEquals(false), Is.False);
			Assert.That(JsonNumber.Return(0).StrictEquals(""), Is.False);
			Assert.That(JsonNumber.Return(1).StrictEquals(true), Is.False);
			Assert.That(JsonNumber.Return(123).StrictEquals("123"), Is.False);
		}

		[Test]
		public void Test_JsonNumber_RoundingBug()
		{
			// When serializing/deserializing a double with the form "7.5318246509562359", there is an issue when convertion from decimal to double (the ULPS will change due to the difference in precision)
			// => on vérifie que le JsonNumber est capable de gérer correctement ce problème

			double x = 7.5318246509562359d;
			Assert.That((double)((decimal)x), Is.Not.EqualTo(x), $"Check that {x:R} gets corrupted during roundtrip by the CLR");
			Assert.That(JsonNumber.Return(x).ToString(), Is.EqualTo(x.ToString("R")));
			Assert.That(((JsonNumber) JsonValue.Parse("7.5318246509562359")).ToDouble(), Is.EqualTo(x), $"Rounding Bug check: {x:R} should not change!");

			x = 3.8219629199346357;
			Assert.That((double)((decimal)x), Is.Not.EqualTo(x), $"Check that {x:R} gets corrupted during roundtrip by the CLR");
			Assert.That(JsonNumber.Return(x).ToString(), Is.EqualTo(x.ToString("R")));
			Assert.That(((JsonNumber) JsonValue.Parse("3.8219629199346357")).ToDouble(), Is.EqualTo(x), $"Rounding Bug check: {x:R} should not change!");

			// meme problème avec les float !
			float y = 7.53182459f;
			Assert.That((float)((decimal)y), Is.Not.EqualTo(y), $"Check that {y:R} gets corrupted during roundtrip by the CLR");
			Assert.That(JsonNumber.Return(y).ToString(), Is.EqualTo(y.ToString("R")));
			Assert.That(((JsonNumber) JsonValue.Parse("7.53182459")).ToSingle(), Is.EqualTo(y), $"Rounding Bug check: {y:R}");
		}

		[Test]
		public void Test_JsonNumber_Interning()
		{
			//NOTE: currently, interning should only be used for small numbers: -128..+127 et 0U...255U
			// => if this test fails, please check that this range hasn't changed !

			Assert.That(JsonNumber.Return(0), Is.SameAs(JsonNumber.Zero), "Zero");
			Assert.That(JsonNumber.Return(1), Is.SameAs(JsonNumber.One), "One");
			Assert.That(JsonNumber.Return(-1), Is.SameAs(JsonNumber.MinusOne), "MinusOne");
			Assert.That(JsonNumber.Return(42), Is.SameAs(JsonNumber.Return(42)), "42 should be in the small signed cache");
			Assert.That(JsonNumber.Return(-42), Is.SameAs(JsonNumber.Return(-42)), "-42 should be in the small signed cache");
			Assert.That(JsonNumber.Return(255), Is.SameAs(JsonNumber.Return(255)), "255 should be in the small signed cache");
			Assert.That(JsonNumber.Return(-128), Is.SameAs(JsonNumber.Return(-128)), "-255 should be in the small signed cache");

			// must also intern values in an array or list
			var arr = new int[10].ToJsonArray();
			Assert.That(arr, Is.Not.Null.And.Count.EqualTo(10), "array of zeroes");
			Assert.That(arr[0], Is.SameAs(JsonNumber.Zero));
			Assert.That(arr[0].ToInt32(), Is.EqualTo(0));
			for (int i = 1; i < arr.Count; i++)
			{
				Assert.That(arr[i], Is.SameAs(JsonNumber.Zero), $"arr[{i}]");
			}

			// list
			arr = new long[10].ToList().ToJsonArray();
			Assert.That(arr, Is.Not.Null.And.Count.EqualTo(10), "list of zeroes");
			Assert.That(arr[0], Is.SameAs(JsonNumber.Zero));
			Assert.That(arr[0].ToInt64(), Is.EqualTo(0));
			for (int i = 1; i < arr.Count; i++)
			{
				Assert.That(arr[i], Is.SameAs(arr[0]), $"arr[{i}]");
			}

			// sequence
			arr = Enumerable.Range(0, 10).Select(_ => 42U).ToJsonArray();
			Assert.That(arr, Is.Not.Null.And.Count.EqualTo(10), "sequence of same value");
			Assert.That(arr[0].ToUInt32(), Is.EqualTo(42U));
			for (int i = 1; i < arr.Count; i++)
			{
				Assert.That(arr[i], Is.SameAs(arr[0]), $"arr[{i}]");
			}

			// convert the same sequence twice should yield the same number singletons
			var t1 = new int[] { 0, 1, 42, -6, 3 };
			var t2 = new int[] { 0, 1, 42, -6, 3 };
			Assume.That(t1, Is.EqualTo(t2));
			var arr1 = t1.ToJsonArray();
			var arr2 = t2.ToJsonArray();
			Assert.That(arr1, Is.Not.Null.And.Count.EqualTo(t1.Length));
			Assert.That(arr2, Is.Not.Null.And.Count.EqualTo(t2.Length));
			for (int i = 0; i < t1.Length; i++)
			{
				Assert.That(arr1[i], Is.SameAs(arr2[i]), $"arr1[{i}] same as arr2[{i}]");
				Assert.That(arr1[i].ToInt32(), Is.EqualTo(t1[i]), $"arr1[{i}] == t1[{i}]");
			}
		}

		#endregion

		#region JsonArray...

		[Test]
		public void Test_JsonArray()
		{
			// [ ]
			var arr = new JsonArray();
			Assert.That(arr, Has.Count.Zero);
			Assert.That(arr.IsReadOnly, Is.False);

			// [ "hello" ]
			arr.Add("hello");
			Assert.That(arr, Has.Count.EqualTo(1));
			Assert.That(arr[0], Is.EqualTo(JsonString.Return("hello")));
			Assert.That(arr.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(arr.ToArray(), Is.EqualTo(new[] { JsonString.Return("hello") }));
			Assert.That(arr.ToArray<string>(), Is.EqualTo(new[] { "hello" }));
			Assert.That(arr.ToList<string>(), Is.EqualTo(new List<string> { "hello" }));
			Assert.That(arr.ToJsonTextCompact(), Is.EqualTo("""["hello"]"""));
			Assert.That(arr.Required<ValueTuple<string>>(), Is.EqualTo(ValueTuple.Create("hello")));

			// [ "hello", "world" ]
			arr.Add("world");
			Assert.That(arr, Has.Count.EqualTo(2));
			Assert.That(arr[0], Is.EqualTo(JsonString.Return("hello")));
			Assert.That(arr[1], Is.EqualTo(JsonString.Return("world")));
			Assert.That(arr.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(arr.Get<string>(1), Is.EqualTo("world"));
			Assert.That(arr.ToArray(), Is.EqualTo(new[] { JsonString.Return("hello"), JsonString.Return("world") }));
			Assert.That(arr.ToArray<string>(), Is.EqualTo(new[] { "hello", "world" }));
			Assert.That(arr.ToList<string>(), Is.EqualTo(new List<string> { "hello", "world" }));
			Assert.That(arr.ToJsonTextCompact(), Is.EqualTo("""["hello","world"]"""));

			// [ "hello", "le monde" ]
			arr[1] = "le monde";
			Assert.That(arr, Has.Count.EqualTo(2));
			Assert.That(arr[0], Is.EqualTo(JsonString.Return("hello")));
			Assert.That(arr[1], Is.EqualTo(JsonString.Return("le monde")));
			Assert.That(arr.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(arr.Get<string>(1), Is.EqualTo("le monde"));
			Assert.That(arr.ToArray(), Is.EqualTo(new[] { JsonString.Return("hello"), JsonString.Return("le monde") }));
			Assert.That(arr.ToArray<string>(), Is.EqualTo(new[] { "hello", "le monde" }));
			Assert.That(arr.ToList<string>(), Is.EqualTo(new List<string> { "hello", "le monde" }));
			Assert.That(arr.ToJsonTextCompact(), Is.EqualTo("""["hello","le monde"]"""));

			// [ "hello", "le monde", 123 ]
			arr.Add(123);
			Assert.That(arr, Has.Count.EqualTo(3));
			Assert.That(arr[0], Is.EqualTo(JsonString.Return("hello")));
			Assert.That(arr[1], Is.EqualTo(JsonString.Return("le monde")));
			Assert.That(arr[2], Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(arr.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(arr.Get<string>(1), Is.EqualTo("le monde"));
			Assert.That(arr.Get<int>(2), Is.EqualTo(123));
			Assert.That(arr.ToArray(), Is.EqualTo(new[] { JsonString.Return("hello"), JsonString.Return("le monde"), JsonNumber.Return(123) }));
			Assert.That(arr.ToJsonTextCompact(), Is.EqualTo("""["hello","le monde",123]"""));

			// [ "hello", 123 ]
			arr.RemoveAt(1);
			Assert.That(arr, Has.Count.EqualTo(2));
			Assert.That(arr[0], Is.EqualTo(JsonString.Return("hello")));
			Assert.That(arr[1], Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(arr.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(arr.Get<int>(1), Is.EqualTo(123));
			Assert.That(arr.ToArray(), Is.EqualTo(new[] { JsonString.Return("hello"), JsonNumber.Return(123) }));
			Assert.That(arr.ToJsonTextCompact(), Is.EqualTo("""["hello",123]"""));

			Assert.That(JsonArray.FromValues([ "A", "B", "C", "D" ]).ToArray<string>(), Is.EqualTo((string[]) [ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.FromValues([ "A", "B", "C", "D" ]).ToList<string>(),Is.EqualTo((List<string>) [ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToList<int>(), Is.EqualTo((List<int>) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToArray<double>(), Is.EqualTo((double[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToList<double>(), Is.EqualTo((List<double>) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToArray<string>(), Is.EqualTo((string[]) [ "1", "2", "3", "4" ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToList<string>(), Is.EqualTo((List<string>) [ "1", "2", "3", "4" ]));
			Assert.That(JsonArray.FromValues([ 1.1, 2.2, 3.3, 4.4 ]).ToArray<double>(), Is.EqualTo((double[]) [ 1.1, 2.2, 3.3, 4.4 ]));
			Assert.That(JsonArray.FromValues([ 1.1, 2.2, 3.3, 4.4 ]).ToList<double>(), Is.EqualTo((List<double>) [ 1.1, 2.2, 3.3, 4.4 ]));
			Assert.That(JsonArray.FromValues([ 1.1, 2.2, 3.3, 4.4 ]).ToArray<string>(), Is.EqualTo((string[]) [ "1.1", "2.2", "3.3", "4.4" ]));
			Assert.That(JsonArray.FromValues([ 1.1, 2.2, 3.3, 4.4 ]).ToList<string>(), Is.EqualTo((List<string>) [ "1.1", "2.2", "3.3", "4.4" ]));
			Assert.That(JsonArray.FromValues([ "1", "2", "3", "4" ]).ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues([ "1", "2", "3", "4" ]).ToList<int>(), Is.EqualTo((List<int>) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues([ true, false, true ]).ToArray<bool>(), Is.EqualTo((bool[]) [ true, false, true ]));
			Assert.That(JsonArray.FromValues([ true, false, true ]).ToList<bool>(), Is.EqualTo((List<bool>) [ true, false, true ]));

			Assert.That(JsonArray.FromValues<string>([ "A", "B", "C", "D" ]).ToArray<string>(), Is.EqualTo((string[]) [ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.FromValues<int>([ 1, 2, 3, 4 ]).ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues<long>([ 1, 2, 3, 4 ]).ToArray<long>(), Is.EqualTo((long[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues<double>([ 1.1, 2.2, 3.3, 4.4 ]).ToArray<double>(), Is.EqualTo((double[]) [ 1.1, 2.2, 3.3, 4.4 ]));
			Assert.That(JsonArray.FromValues<double>([ 1.1, 2.2, 3.3, 4.4 ]).ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues<bool>([ true, false, true ]).ToArray<bool>(), Is.EqualTo((bool[]) [ true, false, true ]));

			var guids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
			Assert.That(JsonArray.FromValues(guids.AsSpan()).ToArray<Guid>(), Is.EqualTo(guids));
			Assert.That(JsonArray.FromValues<Guid>(guids).ToArray<Guid>(), Is.EqualTo(guids));
			Assert.That(JsonArray.FromValues<Guid>(guids.AsSpan()).ToArray<Guid>(), Is.EqualTo(guids));
			Assert.That(JsonArray.FromValues<Guid>(guids.ToList()).ToArray<Guid>(), Is.EqualTo(guids));
			Assert.That(JsonArray.FromValues<Guid>((IEnumerable<Guid>) guids).ToArray<Guid>(), Is.EqualTo(guids));
			Assert.That(JsonArray.FromValues<Guid>(guids.Select(x => x)).ToArray<Guid>(), Is.EqualTo(guids));

			var uuids = Enumerable.Range(0, 5).Select(_ => Uuid128.NewUuid()).ToArray();
			Assert.That(JsonArray.FromValues(uuids.AsSpan()).ToArray<Uuid128>(), Is.EqualTo(uuids));
			Assert.That(JsonArray.FromValues<Uuid128>(uuids).ToArray<Uuid128>(), Is.EqualTo(uuids));
			Assert.That(JsonArray.FromValues<Uuid128>(uuids.AsSpan()).ToArray<Uuid128>(), Is.EqualTo(uuids));
			Assert.That(JsonArray.FromValues<Uuid128>(uuids.ToList()).ToArray<Uuid128>(), Is.EqualTo(uuids));
			Assert.That(JsonArray.FromValues<Uuid128>((IEnumerable<Uuid128>) uuids).ToArray<Uuid128>(), Is.EqualTo(uuids));
			Assert.That(JsonArray.FromValues<Uuid128>(uuids.Select(x => x)).ToArray<Uuid128>(), Is.EqualTo(uuids));

			Assert.That(JsonArray.FromValues((string[]) [ "A", "B", "C", "D" ]), IsJson.EqualTo([ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.FromValues((int[]) [ 1, 2, 3, 4 ]), IsJson.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues((ReadOnlySpan<string>) [ "A", "B", "C", "D" ]), IsJson.EqualTo([ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.FromValues((ReadOnlySpan<int>) [ 1, 2, 3, 4 ]), IsJson.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.ReadOnly.FromValues((string[]) [ "A", "B", "C", "D" ]), IsJson.ReadOnly.And.EqualTo([ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((int[]) [ 1, 2, 3, 4 ]), IsJson.ReadOnly.And.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.ReadOnly.FromValues((ReadOnlySpan<string>) [ "A", "B", "C", "D" ]), IsJson.ReadOnly.And.EqualTo([ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((ReadOnlySpan<int>) [ 1, 2, 3, 4 ]), IsJson.ReadOnly.And.EqualTo((int[]) [ 1, 2, 3, 4 ]));

			Assert.That(JsonArray.FromValues((char[]) [ 'a', 'b', 'c', 'd' ], s => new string(char.ToUpperInvariant(s), 3)), IsJson.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.FromValues((int[]) [ 1, 2, 3, 4 ], x => new string((char) (64 + x), x)), IsJson.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.FromValues((ReadOnlySpan<char>) [ 'a', 'b', 'c', 'd' ], s => new string(char.ToUpperInvariant(s), 3)), IsJson.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.FromValues((ReadOnlySpan<int>) [ 1, 2, 3, 4 ], x => new string((char) (64 + x), x)), IsJson.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.FromValues((IEnumerable<char>) [ 'a', 'b', 'c', 'd' ], s => new string(char.ToUpperInvariant(s), 3)), IsJson.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.FromValues((IEnumerable<char>) ((List<char>) [ 'a', 'b', 'c', 'd' ]), s => new string(char.ToUpperInvariant(s), 3)), IsJson.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.FromValues(((char[]) [ 'a', 'b', 'c', 'd' ]).Select(x => x), s => new string(char.ToUpperInvariant(s), 3)), IsJson.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((char[]) [ 'a', 'b', 'c', 'd' ], s => new string(char.ToUpperInvariant(s), 3)), IsJson.ReadOnly.And.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((int[]) [ 1, 2, 3, 4 ], x => new string((char) (64 + x), x)), IsJson.ReadOnly.And.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((ReadOnlySpan<char>) [ 'a', 'b', 'c', 'd' ], s => new string(char.ToUpperInvariant(s), 3)), IsJson.ReadOnly.And.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((ReadOnlySpan<int>) [ 1, 2, 3, 4 ], x => new string((char) (64 + x), x)), IsJson.ReadOnly.And.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((IEnumerable<int>) [ 1, 2, 3, 4 ], x => new string((char) (64 + x), x)), IsJson.ReadOnly.And.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((IEnumerable<int>) ((List<int>) [ 1, 2, 3, 4 ]), x => new string((char) (64 + x), x)), IsJson.ReadOnly.And.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues(((int[]) [ 1, 2, 3, 4 ]).Select(x => x), x => new string((char) (64 + x), x)), IsJson.ReadOnly.And.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
		}

		[Test]
		public void Test_JsonArray_Create()
		{
			{ // []
				var value = JsonArray.Create();
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(0));
				Assert.That(value.ToObject(), Is.EqualTo(Array.Empty<object>()));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("[]")));
			}

			{ // [ "Foo" ]
				var value = JsonArray.Create("Foo");
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(1));
				Assert.That(value[0], Is.EqualTo(JsonString.Return("Foo")));
				Assert.That(value.ToObject(), Is.EqualTo(new[] { "Foo" }));
				Assert.That(value.ToObject(), Is.InstanceOf<List<object>>());
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""["Foo"]""")));
			}

			{ // [ "Foo", 123 ]
				var value = JsonArray.Create("Foo", 123);
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(2));
				Assert.That(value[0], Is.EqualTo(JsonString.Return("Foo")));
				Assert.That(value[1], Is.EqualTo(JsonNumber.Return(123)));
				Assert.That(value.ToObject(), Is.EqualTo(new object[] { "Foo", 123 }));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""["Foo",123]""")));
			}

			{ // [ "Foo", [ 1, 2, 3 ], true ]
				var value = JsonArray.Create(
					"Foo",
					JsonArray.Create(1, 2, 3),
					true
				);
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(3));
				Assert.That(value[0], Is.EqualTo(JsonString.Return("Foo")));
				Assert.That(value[1], Is.EqualTo(JsonArray.Create(1, 2, 3)));
				Assert.That(value[2], Is.EqualTo(JsonBoolean.True));
				Assert.That(value.ToObject(), Is.EqualTo(new object[] { "Foo", new[] { 1, 2, 3 }, true }));
				Assert.That(value.ToObject(), Is.InstanceOf<List<object>>());
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""["Foo",[1,2,3],true]""")));
			}

			{ // [ "one", 2, "three", 4 ]
				var value = JsonArray.Create("one", 2, "three", 4);
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value, Has.Count.EqualTo(4));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""["one",2,"three",4]""")));
			}

			{ // null entries are converted to JsonNull.Null
				var arr = JsonArray.Create([ null, JsonNull.Null, JsonNull.Missing ]);
				Assert.That(arr[0], Is.Not.Null.And.SameAs(JsonNull.Null));
				Assert.That(arr[1], Is.Not.Null.And.SameAs(JsonNull.Null));
				Assert.That(arr[2], Is.Not.Null.And.SameAs(JsonNull.Missing));
				Assert.That(arr[3], Is.Not.Null.And.SameAs(JsonNull.Error));

				Assert.That(arr[^3], Is.Not.Null.And.SameAs(JsonNull.Null));
				Assert.That(arr[^2], Is.Not.Null.And.SameAs(JsonNull.Null));
				Assert.That(arr[^1], Is.Not.Null.And.SameAs(JsonNull.Missing));
				Assert.That(arr[^4], Is.Not.Null.And.SameAs(JsonNull.Error));
			}

			{ // mutating original array should NOT change the JsonArray created (and vice versa)
				var tmp = new JsonValue[] { "one", 2, "three" };
				var arr = JsonArray.Create(tmp);
				Assert.That(tmp[1], IsJson.Number.And.EqualTo(2));
				Assert.That(arr[1], IsJson.Number.And.EqualTo(2));
				tmp[1] = "two";
				Assert.That(tmp[1], IsJson.String.And.EqualTo("two"));
				Assert.That(arr[1], IsJson.Number.And.EqualTo(2));

				arr[2] = 3;
				Assert.That(tmp[2], IsJson.String.And.EqualTo("three"));
				Assert.That(arr[2], IsJson.Number.And.EqualTo(3));
			}

			{ // create read-only arrays with collection expression arguments

				//TODO: convert this into actual expressions once support for them drops!
				JsonArray immutable = JsonArray.Create(readOnly: true, [ 1, 2, 3 ]);
				Assert.That(immutable, IsJson.ReadOnly.And.EqualTo([ 1, 2, 3]));
				Assert.That(() => immutable.Add(4), Throws.InvalidOperationException);

				JsonArray mutable = JsonArray.Create(readOnly: false, [ 1, 2, 3 ]);
				Assert.That(mutable, IsJson.Mutable.And.EqualTo([ 1, 2, 3 ]));
				Assert.That(() => mutable.Add(4), Throws.Nothing);
				Assert.That(mutable, IsJson.Mutable.And.EqualTo([ 1, 2, 3, 4 ]));
			}
		}

		[Test]
		public void Test_JsonArray_Create_Compiler_Trivia()
		{
			Assert.That(JsonArray.Create([ "one" ]), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one") }));
			Assert.That(JsonArray.Create([ "one", "two" ]), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), JsonString.Return("two") }));
			Assert.That(JsonArray.Create([ "one", null, 123 ]), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), JsonNull.Null, JsonNumber.Return(123) }));
			Assert.That(JsonArray.Create(new JsonValue?[] { "before", "one", null, 123, "after" }.AsSpan(1, 3)), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), JsonNull.Null, JsonNumber.Return(123) }));

			Assert.That(JsonArray.Create("one"), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one") }));
			Assert.That(JsonArray.Create("one", "two"), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), JsonString.Return("two") }));
			Assert.That(JsonArray.Create("one", null, 123), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), JsonNull.Null, JsonNumber.Return(123) }));
			Assert.That(JsonArray.Create(1, 2, 3, 4, 5), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3), JsonNumber.Return(4), JsonNumber.Return(5) }));

			Assert.That(JsonArray.Create([ "one", JsonArray.Create([ "two", "three" ]) ]), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), new JsonArray() { JsonString.Return("two"), JsonString.Return("three") } }));
			Assert.That(JsonArray.Create("one", JsonArray.Create("two", "three")), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), new JsonArray() { JsonString.Return("two"), JsonString.Return("three") } }));

			Assert.That(JsonArray.ReadOnly.Create([ "one" ]), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one") }));
			Assert.That(JsonArray.ReadOnly.Create([ "one", "two" ]), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), JsonString.Return("two") }));
			Assert.That(JsonArray.ReadOnly.Create([ "one", null, 123 ]), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), JsonNull.Null, JsonNumber.Return(123) }));

			Assert.That(JsonArray.ReadOnly.Create("one"), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one") }));
			Assert.That(JsonArray.ReadOnly.Create("one", "two"), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), JsonString.Return("two") }));
			Assert.That(JsonArray.ReadOnly.Create("one", null, 123), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), JsonNull.Null, JsonNumber.Return(123) }));
			Assert.That(JsonArray.ReadOnly.Create(1, 2, 3, 4, 5), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3), JsonNumber.Return(4), JsonNumber.Return(5) }));

			Assert.That(JsonArray.ReadOnly.Create([ "one", JsonArray.ReadOnly.Create([ "two", "three" ]) ]), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), new JsonArray() { JsonString.Return("two"), JsonString.Return("three") } }));
			Assert.That(JsonArray.ReadOnly.Create("one", JsonArray.ReadOnly.Create("two", "three")), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), new JsonArray() { JsonString.Return("two"), JsonString.Return("three") } }));

			// check that Span<...> will use the Create(ReadOnlySpan<..>) overload
			Span<JsonValue?> buf = [ JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3) ];
			Assert.That(JsonArray.Create(buf), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3) }));

#if NET9_0_OR_GREATER

			// check that there is no ambiguous call with other types

			Assert.That(JsonArray.Create(Enumerable.Range(1, 3).Select(i => JsonNumber.Return(i))), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3) }));
			Assert.That(JsonArray.ReadOnly.Create(Enumerable.Range(1, 3).Select(i => JsonNumber.Return(i))), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3) }));

#endif
		}

		[Test]
		public void Test_JsonArray_Indexing()
		{
			var arr = JsonArray.Create([ "one", "two", "three" ]);
			Log(arr);

			// this[int]
			Assert.That(arr[0], IsJson.EqualTo("one"));
			Assert.That(arr[1], IsJson.EqualTo("two"));
			Assert.That(arr[2], IsJson.EqualTo("three"));
			Assert.That(arr[4], IsJson.Error);
			Assert.That(arr[-1], IsJson.Error);
			// this[Index]
			Assert.That(arr[^3], IsJson.EqualTo("one"));
			Assert.That(arr[^2], IsJson.EqualTo("two"));
			Assert.That(arr[^1], IsJson.EqualTo("three"));
			// ReSharper disable once ZeroIndexFromEnd
			Assert.That(arr[^0], IsJson.Error);
			Assert.That(arr[^4], IsJson.Error);

			// GetValue(int)
			Assert.That(arr.GetValue(0), IsJson.EqualTo("one"));
			Assert.That(arr.GetValue(1), IsJson.EqualTo("two"));
			Assert.That(arr.GetValue(2), IsJson.EqualTo("three"));
			Assert.That(() => arr.GetValue(3), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(() => arr.GetValue(-1), Throws.InstanceOf<IndexOutOfRangeException>());
			// GetValue(Index)
			Assert.That(arr.GetValue(^3), IsJson.EqualTo("one"));
			Assert.That(arr.GetValue(^2), IsJson.EqualTo("two"));
			Assert.That(arr.GetValue(^1), IsJson.EqualTo("three"));
			// ReSharper disable once ZeroIndexFromEnd
			Assert.That(() => arr.GetValue(^0), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(() => arr.GetValue(^4), Throws.InstanceOf<IndexOutOfRangeException>());

			// TryGetValue(int)
			Assert.That(arr.TryGetValue(0, out var res), Is.True.WithOutput(res).EqualTo("one"));
			Assert.That(arr.TryGetValue(1, out res), Is.True.WithOutput(res).EqualTo("two"));
			Assert.That(arr.TryGetValue(2, out res), Is.True.WithOutput(res).EqualTo("three"));
			Assert.That(arr.TryGetValue(-1, out res), Is.False.WithOutput(res).Default);
			Assert.That(arr.TryGetValue(4, out res), Is.False.WithOutput(res).Default);
			// TryGetValue(Index)
			Assert.That(arr.TryGetValue(^3, out res), Is.True.WithOutput(res).EqualTo("one"));
			Assert.That(arr.TryGetValue(^2, out res), Is.True.WithOutput(res).EqualTo("two"));
			Assert.That(arr.TryGetValue(^1, out res), Is.True.WithOutput(res).EqualTo("three"));
			Assert.That(arr.TryGetValue(^0, out res), Is.False.WithOutput(res).Default);
			Assert.That(arr.TryGetValue(^4, out res), Is.False.WithOutput(res).Default);

			// GetPath("[int]")
			Assert.That(arr.GetPathValue("[0]"), IsJson.EqualTo("one"));
			Assert.That(arr.GetPathValue("[1]"), IsJson.EqualTo("two"));
			Assert.That(arr.GetPathValue("[2]"), IsJson.EqualTo("three"));
			Assert.That(() => arr.GetPathValue("[3]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(arr.GetPathValueOrDefault("[0]"), IsJson.EqualTo("one"));
			Assert.That(arr.GetPathValueOrDefault("[1]"), IsJson.EqualTo("two"));
			Assert.That(arr.GetPathValueOrDefault("[2]"), IsJson.EqualTo("three"));
			Assert.That(arr.GetPathValueOrDefault("[3]"), IsJson.EqualTo(JsonNull.Error));
			// GetPath("[Index]")
			Assert.That(arr.GetPathValue("[^3]"), IsJson.EqualTo("one"));
			Assert.That(arr.GetPathValue("[^2]"), IsJson.EqualTo("two"));
			Assert.That(arr.GetPathValue("[^1]"), IsJson.EqualTo("three"));
			Assert.That(() => arr.GetPathValue("[^0]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => arr.GetPathValue("[^4]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(arr.GetPathValueOrDefault("[^3]"), IsJson.EqualTo("one"));
			Assert.That(arr.GetPathValueOrDefault("[^2]"), IsJson.EqualTo("two"));
			Assert.That(arr.GetPathValueOrDefault("[^1]"), IsJson.EqualTo("three"));
			Assert.That(arr.GetPathValueOrDefault("[^0]"), IsJson.EqualTo(JsonNull.Error));
			Assert.That(arr.GetPathValueOrDefault("[^4]"), IsJson.EqualTo(JsonNull.Error));

			Assert.That(arr.GetPath<string>("[0]"), Is.EqualTo("one"));
			Assert.That(arr.GetPath<string>("[1]"), Is.EqualTo("two"));
			Assert.That(arr.GetPath<string>("[2]"), Is.EqualTo("three"));
			Assert.That(() => arr.GetPath<string>("[3]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(arr.GetPath<string>("[^3]"), Is.EqualTo("one"));
			Assert.That(arr.GetPath<string>("[^2]"), Is.EqualTo("two"));
			Assert.That(arr.GetPath<string>("[^1]"), Is.EqualTo("three"));
			Assert.That(() => arr.GetPath<string>("[^4]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => arr.GetPath<string>("[0].foo"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => arr.GetPath<string>("foo"), Throws.InstanceOf<JsonBindingException>());

			Assert.That(arr.GetPath<string>("[0]", "not_found"), Is.EqualTo("one"));
			Assert.That(arr.GetPath<string>("[1]", "not_found"), Is.EqualTo("two"));
			Assert.That(arr.GetPath<string>("[2]", "not_found"), Is.EqualTo("three"));
			Assert.That(arr.GetPath<string>("[3]", "not_found"), Is.EqualTo("not_found"));
			Assert.That(arr.GetPath<string>("[^3]", "not_found"), Is.EqualTo("one"));
			Assert.That(arr.GetPath<string>("[^2]", "not_found"), Is.EqualTo("two"));
			Assert.That(arr.GetPath<string>("[^1]", "not_found"), Is.EqualTo("three"));
			Assert.That(arr.GetPath<string>("[^4]", "not_found"), Is.EqualTo("not_found"));
			Assert.That(arr.GetPath<string>("[0].foo", "not_found"), Is.EqualTo("not_found"));
			Assert.That(arr.GetPath<string>("foo", "not_found"), Is.EqualTo("not_found"));
		}

		[Test]
		public void Test_JsonArray_ToJsonArray()
		{
			// JsonValue[]
			var array = (new[] { JsonNumber.One, JsonBoolean.True, JsonString.Empty }).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array[0], Is.SameAs(JsonNumber.One));
			Assert.That(array[1], Is.SameAs(JsonBoolean.True));
			Assert.That(array[2], Is.SameAs(JsonString.Empty));

			// Span<JsonValue>
			{
				JsonValue[] items = [ JsonNumber.One, JsonBoolean.True, JsonString.Empty ];
				array = (items.AsSpan()).ToJsonArray();
				Assert.That(array, Is.Not.Null);
				Assert.That(array, Has.Count.EqualTo(3));
				Assert.That(array[0], Is.SameAs(items[0]));
				Assert.That(array[1], Is.SameAs(items[1]));
				Assert.That(array[2], Is.SameAs(items[2]));
			}

			// ICollection<JsonValue>
			array = Enumerable.Range(0, 10).Select(x => JsonNumber.Return(x)).ToList().ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));

			// IEnumerable<JsonValue>
			array = Enumerable.Range(0, 10).Select(x => JsonNumber.Return(x)).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));

			// another JsonArray
			array = array.ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));

			// JsonNumber[]
			{
				JsonNumber[] items = [ JsonNumber.Create(1), JsonNumber.Create(2), JsonNumber.Create(3) ];

				array = items.ToJsonArray();
				Assert.That(array, Is.Not.Null);
				Assert.That(array, Has.Count.EqualTo(3));
				Assert.That(array[0], Is.SameAs(items[0]));
				Assert.That(array[1], Is.SameAs(items[1]));
				Assert.That(array[2], Is.SameAs(items[2]));
			}

			// int[]
			array = new int[] { 1, 2, 3 }.ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));

			// ICollection<int>
			array = new List<int>([ 1, 2, 3 ]).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));

			// IEnumerable<int>
			array = Enumerable.Range(1, 3).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));
		}


		[Test]
		public void Test_JsonArray_Packable_ToJsonArray()
		{
			// IJsonPackable type
			var items = new DummyJsonCustomClass[]
			{
				new("foo"),
				new("bar"),
				new("baz")
			};

			{ // Packable[]
				var array = items.ToJsonArray();
				Dump(array);
				Assert.That(array, Has.Count.EqualTo(3));
				Assert.That(array[0]["custom"], Is.Json.EqualTo("foo"));
				Assert.That(array[1]["custom"], Is.Json.EqualTo("bar"));
				Assert.That(array[2]["custom"], Is.Json.EqualTo("baz"));
			}

			{ // ReadOnlySpan<Packable>
				var array = items.AsSpan().ToJsonArray();
				Dump(array);
				Assert.That(array, Has.Count.EqualTo(3));
				Assert.That(array[0]["custom"], Is.Json.EqualTo("foo"));
				Assert.That(array[1]["custom"], Is.Json.EqualTo("bar"));
				Assert.That(array[2]["custom"], Is.Json.EqualTo("baz"));
			}

			{ // IEnumerable<Packable>
				var array = items.Select(x => x).ToJsonArray();
				Dump(array);
				Assert.That(array, Has.Count.EqualTo(3));
				Assert.That(array[0]["custom"], Is.Json.EqualTo("foo"));
				Assert.That(array[1]["custom"], Is.Json.EqualTo("bar"));
				Assert.That(array[2]["custom"], Is.Json.EqualTo("baz"));
			}

			{ // TPackable.JsonPackArray([])
				var array = DummyJsonCustomClass.JsonPackArray(items);
				Dump(array);
				Assert.That(array, Has.Count.EqualTo(3));
				Assert.That(array[0]["custom"], Is.Json.EqualTo("foo"));
				Assert.That(array[1]["custom"], Is.Json.EqualTo("bar"));
				Assert.That(array[2]["custom"], Is.Json.EqualTo("baz"));
			}

			{ // TPackable.JsonPackArray(ReadOnlySpan)
				var array = DummyJsonCustomClass.JsonPackArray(items.AsSpan());
				Dump(array);
				Assert.That(array, Has.Count.EqualTo(3));
				Assert.That(array[0]["custom"], Is.Json.EqualTo("foo"));
				Assert.That(array[1]["custom"], Is.Json.EqualTo("bar"));
				Assert.That(array[2]["custom"], Is.Json.EqualTo("baz"));
			}

			{ // TPackable.JsonPackArray(IEnumerable<>)
				var array = DummyJsonCustomClass.JsonPackArray(items.Select(x => x));
				Dump(array);
				Assert.That(array, Has.Count.EqualTo(3));
				Assert.That(array[0]["custom"], Is.Json.EqualTo("foo"));
				Assert.That(array[1]["custom"], Is.Json.EqualTo("bar"));
				Assert.That(array[2]["custom"], Is.Json.EqualTo("baz"));
			}

		}

		[Test]
		public void Test_JsonArray_ToJsonArrayReadOnly()
		{
			// JsonValue[]
			var array = (new[] {JsonNumber.One, JsonBoolean.True, JsonString.Empty}).ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array[0], Is.SameAs(JsonNumber.One));
			Assert.That(array[1], Is.SameAs(JsonBoolean.True));
			Assert.That(array[2], Is.SameAs(JsonString.Empty));
			EnsureDeepImmutabilityInvariant(array);

			// Span<JsonValue>
			array = (new[] { JsonNumber.One, JsonBoolean.True, JsonString.Empty }.AsSpan()).ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array[0], Is.SameAs(JsonNumber.One));
			Assert.That(array[1], Is.SameAs(JsonBoolean.True));
			Assert.That(array[2], Is.SameAs(JsonString.Empty));
			EnsureDeepImmutabilityInvariant(array);

			// ICollection<JsonValue>
			array = Enumerable.Range(0, 10).Select(x => JsonNumber.Return(x)).ToList().ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));
			EnsureDeepImmutabilityInvariant(array);

			// IEnumerable<JsonValue>
			array = Enumerable.Range(0, 10).Select(x => JsonNumber.Return(x)).ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));
			EnsureDeepImmutabilityInvariant(array);

			// another JsonArray
			array = array.ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));
			EnsureDeepImmutabilityInvariant(array);

			// int[]
			array = new int[] {1, 2, 3}.ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] {1, 2, 3}));
			EnsureDeepImmutabilityInvariant(array);

			// ICollection<int>
			array = new List<int>([1, 2, 3]).ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] {1, 2, 3}));
			EnsureDeepImmutabilityInvariant(array);

			// IEnumerable<int>
			array = Enumerable.Range(1, 3).ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] {1, 2, 3}));
			EnsureDeepImmutabilityInvariant(array);
		}

		[Test]
		public void Test_JsonArray_Truncate()
		{
			Assert.That(new JsonArray().Truncate(0), IsJson.Empty);
			Assert.That(new JsonArray().Truncate(3), IsJson.EqualTo(JsonArray.Create([ null, null, null ])));
			Assert.That(JsonArray.Create([ 1, 2, 3 ]).Truncate(3), IsJson.EqualTo(JsonArray.Create([ 1, 2, 3 ])));
			Assert.That(JsonArray.Create([ 1, 2, 3 ]).Truncate(5), IsJson.EqualTo(JsonArray.Create([ 1, 2, 3, null, null ])));
			Assert.That(JsonArray.Create([ 1, 2, 3, 4, 5 ]).Truncate(3), IsJson.EqualTo(JsonArray.Create([ 1, 2, 3 ])));
			Assert.That(JsonArray.Create([ 1, 2, 3 ]).Truncate(0), IsJson.Empty);
		}

		[Test]
		public void Test_JsonArray_Combine()
		{
			Assert.That(JsonArray.Combine(JsonArray.ReadOnly.Empty, JsonArray.ReadOnly.Empty), Is.Not.SameAs(JsonArray.ReadOnly.Empty).And.Empty);
			Assert.That(JsonArray.Combine(JsonArray.Create("hello", "world"), JsonArray.ReadOnly.Empty), IsJson.EqualTo([ "hello", "world" ]));
			Assert.That(JsonArray.Combine(JsonArray.ReadOnly.Empty, JsonArray.Create("hello", "world")), IsJson.EqualTo([ "hello", "world" ]));
			Assert.That(JsonArray.Combine(JsonArray.Create("hello"), JsonArray.Create("world")), IsJson.EqualTo([ "hello", "world" ]));

			Assert.That(JsonArray.Combine(JsonArray.ReadOnly.Empty, JsonArray.ReadOnly.Empty, JsonArray.ReadOnly.Empty), Is.Not.SameAs(JsonArray.ReadOnly.Empty).And.Empty);
			Assert.That(JsonArray.Combine(JsonArray.Create("hello", "world"), JsonArray.ReadOnly.Empty, JsonArray.ReadOnly.Empty), IsJson.EqualTo([ "hello", "world" ]));
			Assert.That(JsonArray.Combine(JsonArray.ReadOnly.Empty, JsonArray.Create("hello", "world"), JsonArray.ReadOnly.Empty), IsJson.EqualTo([ "hello", "world" ]));
			Assert.That(JsonArray.Combine(JsonArray.ReadOnly.Empty, JsonArray.ReadOnly.Empty, JsonArray.Create("hello", "world")), IsJson.EqualTo([ "hello", "world" ]));
			Assert.That(JsonArray.Combine(JsonArray.Create("hello"), JsonArray.Create("world"), JsonArray.Create("!")), IsJson.EqualTo([ "hello", "world", "!" ]));
			Assert.That(JsonArray.Combine(JsonArray.Create(1), JsonArray.Create(2, 3), JsonArray.Create(4, 5, 6)), IsJson.EqualTo([ 1, 2, 3, 4, 5, 6 ]));
		}

		[Test]
		public void Test_JsonArray_AddRange_Of_JsonValues()
		{
			var array = new JsonArray();

			// add elements
			array.AddRange(JsonArray.Create(1, 2));
			Assert.That(array, Has.Count.EqualTo(2));
			Assert.That(array.ToArray<int>(), Is.EqualTo((int[]) [ 1, 2 ]));

			// add singleton
			array.AddRange(JsonArray.Create(3));
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3 ]));

			// add empty
			array.AddRange(JsonArray.Create());
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3 ]));

			// add empty (collection expression)
			array.AddRange([ ]);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3 ]));

			// array inception!
			array.AddRange(array);
			Assert.That(array, Has.Count.EqualTo(6));
			Assert.That(array.ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3, 1, 2, 3 ]));

			// capacity
			array = new JsonArray(5);
			Assert.That(array.Capacity, Is.EqualTo(5));

			array.AddRange([ 1, 2, 3 ]);
			Assert.That(array, Has.Count.EqualTo(3), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was enough");

			array.AddRange([ 4, 5 ]);
			Assert.That(array, Has.Count.EqualTo(5), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was still enough");

			array.AddRange([ 6 ]);
			Assert.That(array, Has.Count.EqualTo(6), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(10), "array.Capacity should have double");

			// errors
			Assert.That(() => array.AddRange(default(JsonValue[])!), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => array.AddRange(default(IEnumerable<JsonValue>)!), Throws.InstanceOf<ArgumentNullException>());
		}

		[Test]
		public void Test_JsonArray_AddRange_Of_T()
		{
			var array = new JsonArray();

			// add elements
			array.AddValues<int>(new [] { 1, 2 });
			Assert.That(array, Has.Count.EqualTo(2));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2 }));

			// add singleton
			array.AddValues<int>(new [] { 3 });
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));

			// add empty
			array.AddValues<int>(Array.Empty<int>());
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));

			// array inception!
			array.AddValues<int>(array.ToArray<int>());
			Assert.That(array, Has.Count.EqualTo(6));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3, 1, 2, 3 }));

			// capacity
			array = new JsonArray(5);
			Assert.That(array.Capacity, Is.EqualTo(5));
			array.AddValues<int>(new[] { 1, 2, 3 });
			Assert.That(array, Has.Count.EqualTo(3), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was enough");
			array.AddValues<int>(new[] { 4, 5 });
			Assert.That(array, Has.Count.EqualTo(5), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was still enough");
			array.AddValues<int>(new[] { 6 });
			Assert.That(array, Has.Count.EqualTo(6), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(10), "array.Capacity should have double");

			// with regular objects
			array = new JsonArray();
			array.AddValues(Enumerable.Range(1, 3).Select(x => new { Id = x, Name = x.ToString() }));
			Assert.That(array, Has.Count.EqualTo(3));
			for (int i = 0; i < array.Count; i++)
			{
				Assert.That(array[i], Is.Not.Null.And.InstanceOf<JsonObject>(), $"[{i}]");
				Assert.That(((JsonObject) array[i])["Id"], Is.EqualTo(JsonNumber.Return(i + 1)), $"[{i}].Id");
				Assert.That(((JsonObject) array[i])["Name"], Is.EqualTo(JsonString.Return((i + 1).ToString())), $"[{i}].Name");
				Assert.That(((JsonObject) array[i]), Has.Count.EqualTo(2), $"[{i}] Count");
			}

			// errors
			Assert.That(() => array.AddValues<int>(default(int[])!), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => array.AddValues<int>(default(IEnumerable<int>)!), Throws.InstanceOf<ArgumentNullException>());
		}

		[Test]
		public void Test_JsonArray_Capacity_Allocation()
		{
			var arr = new JsonArray();
			int old = arr.Capacity;
			int resizes = 0;
			for (int i = 0; i < 1000; i++)
			{
				arr.Add(i);
				if (arr.Capacity != old)
				{
					Log($"Added {arr.Count}th triggered a realloc to {arr.Capacity}");
					old = arr.Capacity;
					++resizes;
				}
#if FULL_DEBUG
				Log(" - {0}: {1:N1} % filled, {2:N0} bytes wasted", arr.Count, 100.0 * arr.Count / arr.Capacity, (arr.Capacity - arr.Count) * IntPtr.Size);
#endif

			}

			Log($"Array needed {resizes} to insert {arr.Count} items");
		}

		[Test]
		public void Test_JsonArray_Enumerable_Of_T()
		{
			{ // As<double>()
				var arr = new JsonArray(4) // with a larger capacity than the Count, to check that the iterator does not overflow past 3 elements!
				{
					123, 456, 789
				};

				var cast = arr.AsArrayOf<double>();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.EqualTo(0.0), "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.EqualTo(123.0), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.EqualTo(456.0), "#2");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.EqualTo(789.0), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.EqualTo(0.0), "After last MoveNext");
				}

				var res = new List<double>();
				foreach (var d in arr.AsArrayOf<double>())
				{
					Assert.That(res, Has.Count.LessThan(3));
					res.Add(d);
				}
				Assert.That(res, Is.EqualTo(new[] { 123.0, 456.0, 789.0 }));

				Assert.That(cast.ToArray(), Is.EqualTo(new [] { 123.0, 456.0, 789.0 }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<double> { 123.0, 456.0, 789.0 }));
			}
			{ // As<string>()
				var arr = new JsonArray(4) // with a larger capacity than the Count, to check that the iterator does not overflow past 3 elements!
				{
					"Hello", "World", "!!!"
				};

				var cast = arr.AsArrayOf<string>();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.EqualTo("Hello"), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.EqualTo("World"), "#2");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.EqualTo("!!!"), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}

				var res = new List<string>();
				foreach (var s in arr.AsArrayOf<string>())
				{
					Assert.That(res, Has.Count.LessThan(3));
					res.Add(s);
				}
				Assert.That(res, Is.EqualTo(new[] { "Hello", "World", "!!!" }));

				Assert.That(cast.ToArray(), Is.EqualTo(new[] { "Hello", "World", "!!!" }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<string> { "Hello", "World", "!!!" }));

			}
		}

		[Test]
		public void Test_JsonArray_AsObjects()
		{
			var a = new JsonObject { ["X"] = 0, ["Y"] = 0, ["Z"] = 0 };
			var b = new JsonObject { ["X"] = 1, ["Y"] = 1, ["Z"] = 0 };
			var c = new JsonObject { ["X"] = 0, ["Y"] = 0, ["Z"] = 1 };

			{ // all elements are objects
				var arr = new JsonArray(4) // with a larger capacity than the Count, to check that the iterator does not overflow past 3 elements!
				{
					a, b, c
				};

				var cast = arr.AsObjects();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.SameAs(b), "#2");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}

				Assert.That(cast.ToArray(), Is.EqualTo(new[] { a, b, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonObject> { a, b, c }));
			}

			{ // the second element is null
				var arr = JsonArray.Create(a, JsonNull.Null, c);

				var cast = arr.AsObjectsOrDefault();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.Null, "#2 should be null!");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}
				Assert.That(cast.ToArray(), Is.EqualTo(new JsonObject?[] { a, null, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonObject?> { a, null, c }));
			}

			{ // the second element is null
				var arr = JsonArray.Create(a, JsonNull.Null, c);

				var cast = arr.AsObjectsOrEmpty();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.SameAs(JsonObject.ReadOnly.Empty), "#2 should be empty singleton!");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}
				Assert.That(cast.ToArray(), Is.EqualTo(new[] { a, JsonObject.ReadOnly.Empty, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonObject?> { a, JsonObject.ReadOnly.Empty, c }));
			}

			{ // the second elements is null, but they are all required
				var arr = JsonArray.Create(a, null, c);

				var cast = arr.AsObjects();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<JsonBindingException>(), "#2 should throw because null is not allowed");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<JsonBindingException>(), "ToArray() should throw because null is not allowed");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<JsonBindingException>(), "ToList() should throw because null is not allowed");
			}

			{ // the second element is not an object (and not null)
				var arr = JsonArray.Create(a, 123, c);

				var cast = arr.AsObjects();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<JsonBindingException>(), "#2 should throw because it is not an object");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<JsonBindingException>(), "ToArray() should throw because it is not an object");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<JsonBindingException>(), "ToList() should throw because it is not an object");
			}

		}

		[Test]
		public void Test_JsonArray_AsArrays()
		{
			var a = JsonArray.Create(1, 0, 0);
			var b = JsonArray.Create(0, 1, 0);
			var c = JsonArray.Create(0, 0, 1);

			{ // all elements are arrays
				var arr = new JsonArray(4) // with a larger capacity than the Count, to check that the iterator does not overflow past 3 elements!
				{
					a, b, c
				};

				var cast = arr.AsArrays();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.SameAs(b), "#2");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}
				Assert.That(cast.ToArray(), Is.EqualTo(new JsonArray[] { a, b, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonArray> { a, b, c }));
			}

			{ // second element is null
				var arr = JsonArray.Create(a, JsonNull.Null, c);

				var cast = arr.AsArraysOrDefault();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.Null, "#2 should be null!");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}
				Assert.That(cast.ToArray(), Is.EqualTo(new [] { a, null, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonArray?> { a, null, c }));
			}

			{ // second element is null
				var arr = JsonArray.Create(a, JsonNull.Null, c);

				var cast = arr.AsArraysOrEmpty();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.SameAs(JsonArray.ReadOnly.Empty), "#2 should be empty singleton!");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}
				Assert.That(cast.ToArray(), Is.EqualTo(new [] { a, JsonArray.ReadOnly.Empty, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonArray?> { a, JsonArray.ReadOnly.Empty, c }));
			}

			{ // second element is null, and all are required
				var arr = JsonArray.Create(a, null, c);

				var cast = arr.AsArrays();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<JsonBindingException>(), "#2 should throw because null is not allowed");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<JsonBindingException>(), "ToArray() should throw because null is not allowed");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<JsonBindingException>(), "ToList() should throw because null is not allowed");
			}

			{ // second element is not an array, and not null
				var arr = JsonArray.Create(a, 123, c);

				var cast = arr.AsArrays();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<JsonBindingException>(), "#2 should throw because it is not an array");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<JsonBindingException>(), "ToArray() should throw because it is not an array");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<JsonBindingException>(), "ToList() should throw because it is not an array");
			}

		}

		[Test]
		public void Test_JsonArray_Cast()
		{

			using (Assert.EnterMultipleScope())
			{
				var cast = JsonArray.Create().AsArrayOf<int>();
				Assert.That(cast, Has.Count.EqualTo(0));
				Assert.That(cast.ToArray(), Is.Empty);
				Assert.That(cast.ToList(), Is.Empty);
			}

			using (Assert.EnterMultipleScope())
			{
				var arr = JsonArray.Create(123, 456, 789);

				var cast = arr.AsArrayOf<int>();

				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(123));
				Assert.That(cast[1], Is.EqualTo(456));
				Assert.That(cast[2], Is.EqualTo(789));
				Assert.That(cast[^1], Is.EqualTo(789));

				Assert.That(cast.ToArray(), Is.EqualTo((int[]) [ 123, 456, 789 ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<int>) [ 123, 456, 789 ]));

				int p = 0;
				foreach (var x in cast)
				{
					switch (p)
					{
						case 0:
						{
							Assert.That(x, Is.EqualTo(123));
							break;
						}
						case 1:
						{
							Assert.That(x, Is.EqualTo(456));
							break;
						}
						case 2:
						{
							Assert.That(x, Is.EqualTo(789));
							break;
						}
						default:
						{
							Assert.Fail("Should only iterate 3 items");
							break;
						}
					}
					++p;
				}
			}

			using (Assert.EnterMultipleScope())
			{ // should fail if no default and value if missing
				var cast = JsonArray.Create(123, null, 789).AsArrayOf<int>();
				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(123));
				Assert.That(() => cast[1], Throws.InstanceOf<JsonBindingException>());
				Assert.That(cast[2], Is.EqualTo(789));
				Assert.That(cast[^1], Is.EqualTo(789));
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => cast.ToList(), Throws.InstanceOf<JsonBindingException>());
			}

			using (Assert.EnterMultipleScope())
			{ // should return default value if missing
				var cast = JsonArray.Create(123, null, 789).Cast<int?>(null);
				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(123));
				Assert.That(cast[1], Is.Null);
				Assert.That(cast[2], Is.EqualTo(789));
				Assert.That(cast[^1], Is.EqualTo(789));
				Assert.That(cast.ToArray(), Is.EqualTo((int?[]) [ 123, null, 789 ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<int?>) [ 123, null, 789 ]));
			}

			using (Assert.EnterMultipleScope())
			{
				var cast = JsonArray.Create(123, null, 789).Cast<int>(-1);
				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(123));
				Assert.That(cast[1], Is.EqualTo(-1));
				Assert.That(cast[2], Is.EqualTo(789));
				Assert.That(cast[^1], Is.EqualTo(789));
				Assert.That(cast.ToArray(), Is.EqualTo((int[]) [ 123, -1, 789 ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<int>) [ 123, -1, 789 ]));
			}

			using (Assert.EnterMultipleScope())
			{
				var cast = JsonArray.Create("hello", "world", "!!!").AsArrayOf<string>();
				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo("hello"));
				Assert.That(cast[1], Is.EqualTo("world"));
				Assert.That(cast[2], Is.EqualTo("!!!"));
				Assert.That(cast[^1], Is.EqualTo("!!!"));
				Assert.That(cast.ToArray(), Is.EqualTo((string[]) [ "hello", "world", "!!!" ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<string>) [ "hello", "world", "!!!" ]));
			}

			using (Assert.EnterMultipleScope())
			{
				var cast = JsonArray.Create("hello", null, "!!!").Cast<string?>("???");
				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo("hello"));
				Assert.That(cast[1], Is.EqualTo("???"));
				Assert.That(cast[2], Is.EqualTo("!!!"));
				Assert.That(cast[^1], Is.EqualTo("!!!"));
				Assert.That(cast.ToArray(), Is.EqualTo((string[]) [ "hello", "???", "!!!" ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<string>) [ "hello", "???", "!!!" ]));
			}

			using (Assert.EnterMultipleScope())
			{ // we can cast to tuples
				var arr = JsonArray.Create([
					JsonArray.Create("one", 111),
					JsonArray.Create("two", 222),
					JsonArray.Create("three", 333)
				]);

				var cast = arr.AsArrayOf<(string, int)>();

				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(("one", 111)));
				Assert.That(cast[1], Is.EqualTo(("two", 222)));
				Assert.That(cast[2], Is.EqualTo(("three", 333)));
				Assert.That(cast[^1], Is.EqualTo(("three", 333)));
				Assert.That(cast.ToArray(), Is.EqualTo(((string, int)[]) [ ("one", 111), ("two", 222), ("three", 333) ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<(string, int)>) [ ("one", 111), ("two", 222), ("three", 333) ]));
			}

			using (Assert.EnterMultipleScope())
			{ // we can cast to anonymous types

				var a = new { GivenName = "James", FamilyName = "Bond" };
				var b = new { GivenName = "Hubert", FamilyName = "Bonisseur de La Bath" };
				var c = new { GivenName = "Janov", FamilyName = "Bondovicz" };

				var arr = JsonArray.FromValues([ a, b, c ]);

				var cast = arr.Cast(new { GivenName = "", FamilyName = "" });

				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(a));
				Assert.That(cast[1], Is.EqualTo(b));
				Assert.That(cast[2], Is.EqualTo(c));
				Assert.That(cast[^1], Is.EqualTo(c));
				Assert.That(cast.ToArray(), Is.EqualTo((object[]) [ a, b, c]));
				Assert.That(cast.ToList(), Is.EqualTo((List<object>) [ a, b,c ]));
			}
		}

		[Test]
		public void Test_JsonArray_Projection()
		{
			var arr = new JsonArray()
			{
				JsonObject.ReadOnly.FromObject(new { Id = 1, Name = "Walter White", Pseudo = "Einsenberg", Job = "Cook", Sickness = "Lung Cancer" }),
				JsonObject.ReadOnly.FromObject(new { Id = 2, Name = "Jesse Pinkman", Job = "Drug Dealer" }),
				JsonObject.ReadOnly.FromObject(new { Id = 3, Name = "Walter White, Jr", Pseudo = "Flynn", Sickness = "Cerebral Palsy" }),
				JsonObject.ReadOnly.FromObject(new { Foo = "bar", Version = 1 }), // completely unrelated object (probably a bug)
				JsonObject.ReadOnly.Empty, // empty object
				JsonNull.Null, // Null should not be changed
				JsonNull.Missing, // Missing should be converted to Null
				null, // null should be changed to Null
			};
			Log("arr = " + arr.ToJsonTextIndented());

			#region Pick (drop missing)...

			// if the key does not exist in the source, it will not be present in the result either

			var proj = arr.Pick([ "Id", "Name", "Pseudo", "Job", "Version" ]);

			Assert.That(proj, Is.Not.Null.And.Not.SameAs(arr));
			Log("proj = " + proj.ToJsonTextIndented());
			Assert.That(proj, Has.Count.EqualTo(arr.Count));

			JsonObject p;

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p.ContainsKey("Version"), Is.False);
			Assert.That(p, Has.Count.EqualTo(4));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p.ContainsKey("Pseudo"), Is.False);
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p.ContainsKey("Version"), Is.False);
			Assert.That(p, Has.Count.EqualTo(3));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p.ContainsKey("Job"), Is.False);
			Assert.That(p.ContainsKey("Version"), Is.False);
			Assert.That(p, Has.Count.EqualTo(3));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p.ContainsKey("Id"), Is.False);
			Assert.That(p.ContainsKey("Name"), Is.False);
			Assert.That(p.ContainsKey("Pseudo"), Is.False);
			Assert.That(p.ContainsKey("Job"), Is.False);
			Assert.That(p.Get<int>("Version"), Is.EqualTo(1));
			Assert.That(p, Has.Count.EqualTo(1));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p, Has.Count.EqualTo(0));

			Assert.That(proj[5], Is.Not.Null);
			Assert.That(proj[5].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[6], Is.Not.Null);
			Assert.That(proj[6].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[7], Is.Not.Null);
			Assert.That(proj[7].Type, Is.EqualTo(JsonType.Null));

			#endregion

			#region Pick (keep missing)...

			// if the key does not exist in the source, it will be replaced by JsonNull.Missing

			proj = arr.Pick(
				[ "Id", "Name", "Pseudo", "Job" ],
				keepMissing: true
			);

			Assert.That(proj, Is.Not.Null.And.Not.SameAs(arr));
			Log("proj = " + proj.ToJsonTextIndented());
			Assert.That(proj, Has.Count.EqualTo(arr.Count));

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p, Has.Count.EqualTo(4));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p, Has.Count.EqualTo(4));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p["Job"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p, Has.Count.EqualTo(4));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Name"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Job"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p, Has.Count.EqualTo(4));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Name"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Job"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p, Has.Count.EqualTo(4));

			Assert.That(proj[5], Is.Not.Null);
			Assert.That(proj[5].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[6], Is.Not.Null);
			Assert.That(proj[6].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[7], Is.Not.Null);
			Assert.That(proj[7].Type, Is.EqualTo(JsonType.Null));

			#endregion

			#region Pick (with JSON defaults)

			// if the key does not exist in the source, it will be replaced by the default value

			proj = arr.Pick(
				new JsonObject()
				{
					["Id"] = JsonNull.Error, // <= équivalent de null, mais qui peut être détecté spécifiquement
					["Name"] = JsonString.Return("John Doe"),
					["Pseudo"] = JsonNull.Null,
					["Job"] = JsonString.Return("NEET"),
					["Version"] = JsonNumber.Zero,
				});

			Assert.That(proj, Is.Not.Null.And.Not.SameAs(arr));
			Log("proj = " + proj.ToJsonTextIndented());
			Assert.That(proj, Has.Count.EqualTo(arr.Count));

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(1));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			Assert.That(proj[5], Is.Not.Null);
			Assert.That(proj[5].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[6], Is.Not.Null);
			Assert.That(proj[6].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[7], Is.Not.Null);
			Assert.That(proj[7].Type, Is.EqualTo(JsonType.Null));

			#endregion

			#region Pick (with object defaults)

			// if the key does not exist in the source, it is replaced by the default value present in the anonymous template

			proj = arr.Pick(
				new
				{
					Id = JsonNull.Error, // <= equivalent to null, but can be tested by the caller (it would not be produced by deserializing)
					Name = "John Doe",
					Pseudo = JsonNull.Null,
					Job = "NEET",
					Version = 0,
				});

			Assert.That(proj, Is.Not.Null.And.Not.SameAs(arr));
			Log("proj = " + proj.ToJsonTextIndented());
			Assert.That(proj, Has.Count.EqualTo(arr.Count));

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(1));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			Assert.That(proj[5], Is.Not.Null);
			Assert.That(proj[5].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[6], Is.Not.Null);
			Assert.That(proj[6].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[7], Is.Not.Null);
			Assert.That(proj[7].Type, Is.EqualTo(JsonType.Null));

			#endregion

		}

		[Test]
		public void Test_JsonArray_Flatten()
		{
			var array = JsonArray.Create
			(
				1,
				JsonArray.Create(2, 3, 4),
				5
			);

			var flat = array.Flatten();
			Assert.That(flat, Is.Not.Null);
			Assert.That(flat.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));

		}

		[Test]
		public void Test_JsonArray_ReadOnly_Empty()
		{
			Assert.That(JsonArray.ReadOnly.Empty.IsReadOnly, Is.True);
			//note: we don't want to attempt to modify the empty readonly singleton, because if the test fails, it will completely break ALL the reamining tests!

			static void CheckEmptyReadOnly(JsonArray arr, [CallerArgumentExpression(nameof(arr))] string? expression = null)
			{
				Assert.That(arr, Has.Count.Zero, expression);
				AssertIsImmutable(arr, expression);
				Assert.That(arr, Has.Count.Zero, expression);
			}

			CheckEmptyReadOnly(JsonArray.ReadOnly.Create());
			CheckEmptyReadOnly(JsonArray.ReadOnly.Create([]));
			CheckEmptyReadOnly(JsonArray.Create().ToReadOnly());
			CheckEmptyReadOnly(JsonArray.Copy(JsonArray.Create(), deep: false, readOnly: true));
			CheckEmptyReadOnly(JsonArray.Copy(JsonArray.Create(), deep: true, readOnly: true));

			var arr = JsonArray.Create("hello");
			arr.RemoveAt(0);
			CheckEmptyReadOnly(arr.ToReadOnly());
			CheckEmptyReadOnly(JsonArray.Copy(arr, deep: false, readOnly: true));
			CheckEmptyReadOnly(JsonArray.Copy(arr, deep: true, readOnly: true));
		}

		[Test]
		public void Test_JsonArray_ReadOnly()
		{
			// creating a readonly object with only immutable values should produce an immutable object

			// DEPRECATED: should use collection expressions instead: [ ... ]
			AssertIsImmutable(JsonArray.ReadOnly.Create("one"));
			AssertIsImmutable(JsonArray.ReadOnly.Create("one", "two"));
			AssertIsImmutable(JsonArray.ReadOnly.Create("one", "two", "three"));
			AssertIsImmutable(JsonArray.ReadOnly.Create("one", "two", "three", "four"));

			// collection expressions: should invoke the ReadOnlySpan<> overload
			AssertIsImmutable(JsonArray.ReadOnly.Create(["one"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create(["one", "two"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create(["one", "two", "three"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create(["one", "two", "three", "four"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create(["one", "two", "three", "four", "five"]));

			// params JsonValue[]
			AssertIsImmutable(JsonArray.ReadOnly.Create((JsonValue[]) ["one"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create((JsonValue[]) ["one", "two"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create((JsonValue[]) ["one", "two", "three"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create((JsonValue[]) ["one", "two", "three", "four"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create((JsonValue[]) ["one", "two", "three", "four", "five"]));

			AssertIsImmutable(JsonArray.ReadOnly.FromValues(Enumerable.Range(0, 10).Select(i => KeyValuePair.Create(i.ToString(), i))));
			AssertIsImmutable(JsonArray.ReadOnly.FromValues(Enumerable.Range(0, 10).Select(i => KeyValuePair.Create(i.ToString(), i)).ToArray()));
			AssertIsImmutable(JsonArray.ReadOnly.FromValues(Enumerable.Range(0, 10).Select(i => KeyValuePair.Create(i.ToString(), i)).ToList()));
			AssertIsImmutable(JsonArray.ReadOnly.FromValues(Enumerable.Range(0, 10).Select(i => KeyValuePair.Create(i.ToString(), i)).ToList()));

			// creating an immutable version of a writable object with only immutable should return an immutable object
			AssertIsImmutable(JsonArray.Create("one", 1).ToReadOnly());
			AssertIsImmutable(JsonArray.Create("one", 1, "two", 2, "three", 3).ToReadOnly());

			// parsing with JsonImmutable should return an already immutable object
			var obj = JsonObject.Parse("""{ "hello": "world", "foo": { "id": 123, "name": "Foo", "address" : { "street": 123, "city": "Paris" } }, "bar": [ 1, 2, 3 ], "baz": [ { "jazz": 42 } ] }""", CrystalJsonSettings.JsonReadOnly);
			AssertIsImmutable(obj);
			var foo = obj.GetObject("foo");
			AssertIsImmutable(foo);
			var address = foo.GetObject("address");
			AssertIsImmutable(address);
			var bar = obj.GetArray("bar");
			AssertIsImmutable(bar);
			var baz = obj.GetArray("baz");
			AssertIsImmutable(baz);
			var jazz = baz.GetObject(0);
			AssertIsImmutable(jazz);
		}

		[Test]
		public void Test_JsonArray_Freeze()
		{
			// ensure that, given a mutable JSON object, we can create an immutable version that is protected against any changes

			// the original object should be mutable
			var arr = new JsonArray
			{
				"hello",
				"world",
				new JsonObject { ["bar"] = "baz" },
				new JsonArray { 1, 2, 3 }
			};
			Assert.That(arr.IsReadOnly, Is.False);
			// the inner object and array should be mutable as well
			var innerObj = arr.GetObject(2);
			Assert.That(arr.IsReadOnly, Is.False);
			var innerArray = arr.GetArray(3);
			Assert.That(arr.IsReadOnly, Is.False);

			Assert.That(arr.ToJsonTextCompact(), Is.EqualTo("""["hello","world",{"bar":"baz"},[1,2,3]]"""));

			var arr2 = arr.Freeze();
			Assert.That(arr2, Is.SameAs(arr), "Freeze() should return the same instance");
			AssertIsImmutable(arr2, "obj.Freeze()");

			// the inner object should also have been frozen!
			var innerObj2 = arr2.GetObject(2);
			Assert.That(innerObj2, Is.SameAs(innerObj));
			Assert.That(innerObj2.IsReadOnly, Is.True);
			AssertIsImmutable(innerObj2, "(obj.Freeze())[\"foo\"]");

			// the inner array should also have been frozen!
			var innerArray2 = arr2.GetArray(3);
			Assert.That(innerArray2, Is.SameAs(innerArray));
			Assert.That(innerArray2.IsReadOnly, Is.True);
			AssertIsImmutable(innerArray2, "(obj.Freeze())[\"bar\"]");

			Assert.That(arr2.ToJsonTextCompact(), Is.EqualTo("""["hello","world",{"bar":"baz"},[1,2,3]]"""));

			// if we want to mutate, we have to create a copy
			var arr3 = arr2.Copy();

			// the copy should still be equal to the original
			Assert.That(arr3, Is.Not.SameAs(arr2), "it should return a new instance");
			Assert.That(arr3, Is.EqualTo(arr2), "It should still be equal");
			var innerObj3 = arr3.GetObject(2);
			Assert.That(innerObj3, Is.Not.SameAs(innerObj2), "inner object should be cloned");
			Assert.That(innerObj3, Is.EqualTo(innerObj2), "It should still be equal");
			var innerArray3 = arr3.GetArray(3);
			Assert.That(innerArray3, Is.Not.SameAs(innerArray2), "inner array should be cloned");
			Assert.That(innerArray3, Is.EqualTo(innerArray2), "It should still be equal");

			// should be aable to change the copy
			Assert.That(arr3, Has.Count.EqualTo(4));
			Assert.That(() => arr3.Add("bonjour"), Throws.Nothing);
			Assert.That(arr3, Has.Count.EqualTo(5));
			Assert.That(arr3, Is.Not.EqualTo(arr2), "It should still not be equal after the change");
			Assert.That(innerObj3, Is.EqualTo(innerObj2), "It should still be equal");
			Assert.That(innerArray3, Is.EqualTo(innerArray2), "It should still be equal");

			// should be able to mutate the inner object
			Assert.That(innerObj3, Has.Count.EqualTo(1));
			Assert.That(() => innerObj3["baz"] = "jazz", Throws.Nothing);
			Assert.That(innerObj3, Has.Count.EqualTo(2));
			Assert.That(innerObj3, Is.Not.EqualTo(innerObj2), "It should still not be equal after the change");

			// should be able to mutate the inner array
			Assert.That(innerArray3, Has.Count.EqualTo(3));
			Assert.That(() => innerArray3.Add(4), Throws.Nothing);
			Assert.That(innerArray3, Has.Count.EqualTo(4));
			Assert.That(innerArray3, Is.Not.EqualTo(innerArray2), "It should still not be equal after the change");

			// verify the final mutated version
			Assert.That(arr3.ToJsonTextCompact(), Is.EqualTo("""["hello","world",{"bar":"baz","baz":"jazz"},[1,2,3,4],"bonjour"]"""));
			// ensure the original is unmodified
			Assert.That(arr2.ToJsonTextCompact(), Is.EqualTo("""["hello","world",{"bar":"baz"},[1,2,3]]"""));
		}

		[Test]
		public void Test_JsonArray_Can_Mutate_Frozen()
		{
			// given an immutable object, check that we can create mutable version that will not modifiy the original
			var original = new JsonArray
			{
				"hello",
				"world",
				new JsonObject { ["bar"] = "baz" },
				new JsonArray { 1, 2, 3 }
			}.Freeze();
			Dump("Original", original);
			EnsureDeepImmutabilityInvariant(original);

			// create a "mutable" version of the entire tree
			var obj = original.ToMutable();
			Dump("Copy", obj);
			Assert.That(obj.IsReadOnly, Is.False, "Copy should be not be read-only!");
			Assert.That(obj, Is.Not.SameAs(original));
			Assert.That(obj, Is.EqualTo(original));
			Assert.That(obj[0], Is.SameAs(original[0]));
			Assert.That(obj[1], Is.SameAs(original[1]));
			Assert.That(obj.GetObject(2), Is.Not.SameAs(original.GetObject(2)));
			Assert.That(obj.GetArray(3), Is.Not.SameAs(original.GetArray(3)));
			EnsureDeepMutabilityInvariant(obj);

			obj[0] = "le monde";
			obj.GetObject(2).Remove("bar");
			obj.GetObject(2).Add("baz", "bar");
			obj.GetArray(3).Add(4);
			obj.Add(42);
			Dump("Mutated", obj);
			// ensure the copy have been mutated
			Assert.That(obj.Get<string>(0), Is.EqualTo("le monde"));
			Assert.That(obj.GetObject(2).Get<string?>("bar", null), Is.Null);
			Assert.That(obj.GetObject(2).Get<string>("baz"), Is.EqualTo("bar"));
			Assert.That(obj.GetArray(3), Has.Count.EqualTo(4));
			Assert.That(obj.Get<int>(4), Is.EqualTo(42));
			Assert.That(obj, Is.Not.EqualTo(original));

			// ensure the original is not mutated
			Dump("Original", original);
			Assert.That(original.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(original.GetObject(2).Get<string>("bar"), Is.EqualTo("baz"));
			Assert.That(original.GetObject(2).Get<string?>("baz", null), Is.Null);
			Assert.That(original.GetArray(3), Has.Count.EqualTo(3));
			Assert.That(original, Has.Count.EqualTo(4));
		}

		[Test]
		public void Test_JsonArray_CopyAndMutate()
		{
			// Test the "builder" API that can simplify making changes to a read-only arrays and publishing the new instance.
			// All methods will create return a new copy of the original, with the mutation applied, leaving the original untouched.
			// The new read-only copy should reuse the same JsonValue instances as the original, to reduce memory copies.

			var arr = JsonArray.ReadOnly.Empty;
			Assume.That(arr, IsJson.Empty);
			Assume.That(arr, IsJson.ReadOnly);
			Assume.That(arr[0], IsJson.Error);
			DumpCompact(arr);

			// copy and add first item
			var arr2 = arr.CopyAndAdd("world");
			DumpCompact(arr2);
			Assert.That(arr2, Is.Not.SameAs(arr));
			Assert.That(arr2, Has.Count.EqualTo(1));
			Assert.That(arr2, IsJson.ReadOnly);
			Assert.That(arr2, IsJson.EqualTo([ "world" ]));
			Assert.That(arr, IsJson.Empty);

			// copy and set second item
			var arr3 = arr2.CopyAndSet(1, "bar");
			DumpCompact(arr3);
			Assert.That(arr3, Is.Not.SameAs(arr2));
			Assert.That(arr3, Has.Count.EqualTo(2));
			Assert.That(arr3, IsJson.ReadOnly);
			Assert.That(arr3[0], Is.SameAs(arr2[0]));
			Assert.That(arr3[1], IsJson.EqualTo("bar"));
			Assert.That(arr2, IsJson.EqualTo([ "world" ]));
			Assert.That(arr, IsJson.Empty);

			// copy and set should overwrite existing item
			var arr4 = arr3.CopyAndSet(1, "baz");
			DumpCompact(arr4);
			Assert.That(arr4, Is.Not.SameAs(arr3));
			Assert.That(arr4, Has.Count.EqualTo(2));
			Assert.That(arr4, IsJson.ReadOnly);
			Assert.That(arr4[0], Is.EqualTo("world").And.SameAs(arr3[0]));
			Assert.That(arr4[1], IsJson.EqualTo("baz"));
			Assert.That(arr3, IsJson.EqualTo([ "world", "bar" ]));
			Assert.That(arr2, IsJson.EqualTo([ "world" ]));
			Assert.That(arr, IsJson.Empty);

			// copy and remove
			var arr5 = arr4.CopyAndRemove(0);
			DumpCompact(arr5);
			Assert.That(arr5, Is.Not.SameAs(arr4));
			Assert.That(arr5, Has.Count.EqualTo(1));
			Assert.That(arr5, IsJson.ReadOnly);
			Assert.That(arr5[0], IsJson.EqualTo("baz"));
			Assert.That(arr5[1], IsJson.Error);
			Assert.That(arr4, IsJson.EqualTo([ "world", "baz" ]));
			Assert.That(arr3, IsJson.EqualTo([ "world", "bar" ]));
			Assert.That(arr2, IsJson.EqualTo([ "world" ]));
			Assert.That(arr, IsJson.Empty);

			// copy and try removing last field
			var arr6 = arr5.CopyAndRemove(0, out var prev);
			DumpCompact(arr6);
			Assert.That(arr6, Is.Not.SameAs(arr5));
			Assert.That(arr6, Is.Empty);
			Assert.That(arr6, IsJson.ReadOnly);
			Assert.That(arr6[0], IsJson.Error);
			Assert.That(arr6[1], IsJson.Error);
			Assert.That(arr6, Is.SameAs(JsonArray.ReadOnly.Empty));
			Assert.That(prev, Is.SameAs(arr5[0]));
			Assert.That(arr5, IsJson.EqualTo([ "baz" ]));
			Assert.That(arr4, IsJson.EqualTo([ "world", "baz" ]));
			Assert.That(arr3, IsJson.EqualTo([ "world", "bar" ]));
			Assert.That(arr2, IsJson.EqualTo([ "world" ]));
			Assert.That(arr, IsJson.Empty);

			// maximize test coverage

			static void Check(
				JsonArray actual,
				IResolveConstraint expression,
				NUnitString message = default,
				[CallerArgumentExpression(nameof(actual))] string actualExpression = "",
				[CallerArgumentExpression(nameof(expression))] string constraintExpression = ""
			)
			{
				Dump(actualExpression, actual);
				Assert.That(actual: actual, expression: expression, message: message, actualExpression: actualExpression, constraintExpression: constraintExpression);
			}

			Check(JsonArray.ReadOnly.Empty.CopyAndAdd("hello"), IsJson.ReadOnly.And.EqualTo([ "hello" ]));
			Check(JsonArray.Create(["hello"]).CopyAndAdd("world"), IsJson.ReadOnly.And.EqualTo([ "hello", "world" ]));

			Check(JsonArray.ReadOnly.Empty.CopyAndSet(0, "hello"), IsJson.ReadOnly.And.EqualTo([ "hello" ]));
			Check(JsonArray.ReadOnly.Empty.CopyAndSet(1, "hello"), IsJson.ReadOnly.And.EqualTo([ null, "hello" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(0, "bonjour"), IsJson.ReadOnly.And.EqualTo([ "bonjour", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(1, "le monde"), IsJson.ReadOnly.And.EqualTo([ "hello", "le monde" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(2, "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", "!" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(3, "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", null, "!" ]));

			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(^2, "bonjour"), IsJson.ReadOnly.And.EqualTo([ "bonjour", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(^1, "le monde"), IsJson.ReadOnly.And.EqualTo([ "hello", "le monde" ]));

			Check(JsonArray.ReadOnly.Empty.CopyAndInsert(0, "hello"), IsJson.ReadOnly.And.EqualTo([ "hello" ]));
			Check(JsonArray.ReadOnly.Empty.CopyAndInsert(1, "hello"), IsJson.ReadOnly.And.EqualTo([ null, "hello" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(0, "say"), IsJson.ReadOnly.And.EqualTo([ "say", "hello", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(1, ", "), IsJson.ReadOnly.And.EqualTo([ "hello", ", ", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(2, "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", "!" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(3, "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", null, "!" ]));

			Check(JsonArray.ReadOnly.Empty.CopyAndInsert(new Index(0), "hello"), IsJson.ReadOnly.And.EqualTo([ "hello" ]));
			Check(JsonArray.ReadOnly.Empty.CopyAndInsert(new Index(1), "hello"), IsJson.ReadOnly.And.EqualTo([ null, "hello" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(^2, "say"), IsJson.ReadOnly.And.EqualTo([ "say", "hello", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(^1, ", "), IsJson.ReadOnly.And.EqualTo([ "hello", ", ", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(^0, "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", "!" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(new Index(3), "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", null, "!" ]));

			Check(JsonArray.ReadOnly.Empty.CopyAndRemove(0), Is.SameAs(JsonArray.ReadOnly.Empty));
			Check(JsonArray.ReadOnly.Empty.CopyAndRemove(1), Is.SameAs(JsonArray.ReadOnly.Empty));
			Check(JsonArray.Create(["hello", "world"]).CopyAndRemove(0), IsJson.ReadOnly.And.EqualTo([ "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndRemove(1), IsJson.ReadOnly.And.EqualTo([ "hello" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndRemove(2), IsJson.ReadOnly.And.EqualTo([ "hello", "world" ]));
		}

		[Test]
		public void Test_JsonArray_CopyAndConcat()
		{
			{ // Empty + Empty
				var orig = JsonArray.ReadOnly.Empty;
				var tail = JsonArray.ReadOnly.Empty;
				var arr = orig.CopyAndConcat(tail);
				Assert.That(arr, Is.SameAs(JsonArray.ReadOnly.Empty));
			}
			{ // Empty + TAIL
				var orig = JsonArray.ReadOnly.Empty;
				var tail = JsonArray.Create("hello", "world");
				var arr = orig.CopyAndConcat(tail);
				Assert.That(arr, Is.Json.ReadOnly.And.EqualTo([ "hello", "world" ]));
				Assert.That(arr, Is.Not.SameAs(orig));
			}
			{ // HEAD + Empty
				var orig = JsonArray.Create("hello", "world");
				var tail = JsonArray.ReadOnly.Empty;
				var arr = orig.CopyAndConcat(tail);
				Assert.That(arr, Is.Json.ReadOnly.And.EqualTo([ "hello", "world" ]));
				Assert.That(arr, Is.Not.SameAs(orig));
			}
			{ // HEAD + TAIL
				var orig = JsonArray.Create("say");
				var tail = JsonArray.Create("hello", "world");
				var arr = orig.CopyAndConcat(tail);
				Assert.That(arr, Is.Json.ReadOnly.And.EqualTo([ "say", "hello", "world" ]));
				Assert.That(orig, Is.Json.EqualTo([ "say" ]));
				Assert.That(tail, Is.Json.EqualTo([ "hello", "world" ]));
			}
			{
				Assert.That(JsonArray.ReadOnly.Create("hello").CopyAndConcat([ "world", "there" ]), Is.Json.EqualTo([ "hello", "world", "there" ]));
			}
			{ // Replace Root
				JsonArray root = JsonArray.ReadOnly.Empty;
				JsonArray v0 = root;
				var published = JsonArray.CopyAndConcat(ref root, JsonArray.Create([ "say" ]));
				Assert.That(root, Is.Not.SameAs(v0));
				Assert.That(root, Is.Json.ReadOnly.And.EqualTo([ "say" ]));
				Assert.That(published, Is.SameAs(root));
				Assert.That(v0, Is.Json.Empty);

				JsonArray v1 = root;
				published = JsonArray.CopyAndConcat(ref root, JsonArray.Create([ "hello" ]));
				Assert.That(root, Is.Not.SameAs(v1));
				Assert.That(root, Is.Json.ReadOnly.And.EqualTo([ "say", "hello" ]));
				Assert.That(published, Is.SameAs(root));
				Assert.That(v1, Is.Json.EqualTo([ "say" ]));

				JsonArray v2 = root;
				published = JsonArray.CopyAndConcat(ref root, JsonArray.Create([ "world" ]));
				Assert.That(root, Is.Not.SameAs(v2));
				Assert.That(root, Is.Json.ReadOnly.And.EqualTo([ "say", "hello", "world" ]));
				Assert.That(published, Is.SameAs(root));
				Assert.That(v2, Is.Json.EqualTo([ "say", "hello" ]));
				Assert.That(v1, Is.Json.EqualTo([ "say" ]));

				JsonArray v3 = root;
				published = JsonArray.CopyAndConcat(ref root, JsonArray.ReadOnly.Empty);
				Assert.That(root, Is.SameAs(v3));
				Assert.That(root, Is.Json.ReadOnly.And.EqualTo([ "say", "hello", "world" ]));
				Assert.That(published, Is.SameAs(root));
				Assert.That(v2, Is.Json.EqualTo([ "say", "hello" ]));
				Assert.That(v1, Is.Json.EqualTo([ "say" ]));
			}
		}

		[Test]
		public void Test_JsonArray_IndexOf()
		{
			using (Assert.EnterMultipleScope())
			{
				// find string
				Assert.That(JsonArray.Create().IndexOf("hello"), Is.EqualTo(-1));
				Assert.That(JsonArray.Create("hello").IndexOf("hello"), Is.EqualTo(0));
				Assert.That(JsonArray.Create("hello", 123).IndexOf("hello"), Is.EqualTo(0));
				Assert.That(JsonArray.Create(123, "hello").IndexOf("hello"), Is.EqualTo(1));
				Assert.That(JsonArray.Create("hello", "hello").IndexOf("hello"), Is.EqualTo(0));

				// find number
				Assert.That(JsonArray.Create(123).IndexOf(123), Is.EqualTo(0));
				Assert.That(JsonArray.Create(123, "hello").IndexOf(123), Is.EqualTo(0));
				Assert.That(JsonArray.Create("hello", 123).IndexOf(123), Is.EqualTo(1));
				Assert.That(JsonArray.Create(123).IndexOf(123), Is.EqualTo(0));
				Assert.That(JsonArray.Create(123, 123).IndexOf(123), Is.EqualTo(0));

				// find number
				Assert.That(JsonArray.Create(true).IndexOf(true), Is.EqualTo(0));
				Assert.That(JsonArray.Create(true).IndexOf(false), Is.EqualTo(-1));
				Assert.That(JsonArray.Create("hello", true).IndexOf(true), Is.EqualTo(1));
				Assert.That(JsonArray.Create(JsonNull.Null).IndexOf(false), Is.EqualTo(-1));
				Assert.That(JsonArray.Create("").IndexOf(false), Is.EqualTo(-1));
				Assert.That(JsonArray.Create(0).IndexOf(false), Is.EqualTo(-1));

				// find null-like
				Assert.That(JsonArray.Create("hello", JsonNull.Null, "world").IndexOf(null), Is.EqualTo(1), "Null is null-like");
				Assert.That(JsonArray.Create("hello", JsonNull.Null, "world").IndexOf(JsonNull.Null), Is.EqualTo(1));
				Assert.That(JsonArray.Create("hello", JsonNull.Null, "world").IndexOf(JsonNull.Missing), Is.EqualTo(-1), "Missing does not equal Null");
				Assert.That(JsonArray.Create("hello", JsonNull.Null, "world").IndexOf(JsonNull.Error), Is.EqualTo(-1), "Error does not equal Null");
				Assert.That(JsonArray.Create("hello", JsonNull.Missing, "world").IndexOf(null), Is.EqualTo(1), "Missing is null-like");
				Assert.That(JsonArray.Create("hello", JsonNull.Missing, "world").IndexOf(JsonNull.Null), Is.EqualTo(-1), "Null does not equal Missing");
				Assert.That(JsonArray.Create("hello", JsonNull.Missing, "world").IndexOf(JsonNull.Missing), Is.EqualTo(1));

				// find sub-object
				var obj = JsonObject.Create([ ("hello", "there") ]);
				Assert.That(JsonArray.Create("foo", obj, "bar").IndexOf(obj), Is.EqualTo(1));

				// find sub-array
				var arr = JsonArray.Create([ "hello", "there" ]);
				Assert.That(JsonArray.Create("foo", arr, "bar").IndexOf(arr), Is.EqualTo(1));

			}
		}

		[Test]
		public void Test_JsonArray_ValueEquals()
		{
			// note: do not confuse ValueEquals<TCollection>(TCollection) vs ValuesEqual<TItem>(IEnumerable<TItem>) !

			using (Assert.EnterMultipleScope())
			{
				var arr = JsonArray.ReadOnly.Empty;

				Assert.That(arr.ValueEquals<string[]>([ ]), Is.True);
				Assert.That(arr.ValueEquals<List<string>>([ ]), Is.True);
				Assert.That(arr.ValueEquals<IEnumerable<string>>([ ]), Is.True);

				Assert.That(arr.ValuesEqual((string[]) [ ]), Is.True);
				Assert.That(arr.ValuesEqual((ReadOnlySpan<string>) [ ]), Is.True);
				Assert.That(arr.ValuesEqual((List<string>) [ ]), Is.True);
				Assert.That(arr.ValuesEqual((IEnumerable<string>) [ ]), Is.True);

				Assert.That(arr.ValuesEqual(Enumerable.Empty<string>()), Is.True);
				Assert.That(arr.ValuesEqual(Enumerable.Empty<int>()), Is.True);
				Assert.That(arr.ValuesEqual(Enumerable.Empty<bool>()), Is.True);

				Assert.That(arr.ValuesEqual(new JsonArray()), Is.True);
			}
		
			using (Assert.EnterMultipleScope())
			{
				var arr = JsonArray.Create([ "foo", "bar" ]);

				Assert.That(arr.ValueEquals(JsonArray.Create("foo", "bar")), Is.True);
				Assert.That(arr.ValueEquals<string[]>([ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValueEquals<List<string>>([ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValueEquals<IEnumerable<string>>([ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValueEquals<IEnumerable<string>>(Enumerable.Range(0, 2).Select(i => i == 0 ? "foo" : "bar")), Is.True);

				Assert.That(arr.ValueEquals(JsonArray.Create("foo", "baz")), Is.False);
				Assert.That(arr.ValueEquals(JsonArray.Create("foo", "bar", "baz")), Is.False);
				Assert.That(arr.ValueEquals<string[]>([ "foo", "baz" ]), Is.False);
				Assert.That(arr.ValueEquals<List<string>>([ "foo", "bar", "baz" ]), Is.False);
				Assert.That(arr.ValueEquals<IEnumerable<string>>(Enumerable.Empty<string>()), Is.False);
				Assert.That(arr.ValueEquals<IEnumerable<string>>(Enumerable.Range(0, 1).Select(_ => "foo")), Is.False);
				Assert.That(arr.ValueEquals<IEnumerable<string>>(Enumerable.Range(0, 2).Select(i => i == 0 ? "foo" : "baz")), Is.False);
				Assert.That(arr.ValueEquals<IEnumerable<string>>(Enumerable.Range(0, 3).Select(i => i == 0 ? "foo" : i == 1 ? "bar" : "baz")), Is.False);

				Assert.That(arr.ValuesEqual([ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValuesEqual(JsonArray.Create("foo", "bar")), Is.True);
				Assert.That(arr.ValuesEqual((ReadOnlySpan<string>) [ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValuesEqual((string[]) [ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValuesEqual((List<string>) [ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValuesEqual(Enumerable.Range(0, 2).Select(i => i == 0 ? "foo" : "bar")), Is.True);

				Assert.That(arr.ValuesEqual(JsonArray.Create("foo", "baz")), Is.False);
				Assert.That(arr.ValuesEqual(JsonArray.Create("foo", "bar", "baz")), Is.False);
				Assert.That(arr.ValuesEqual((string[]) [ "foo", "baz" ]), Is.False);
				Assert.That(arr.ValuesEqual((List<string>) [ "foo", "bar", "baz" ]), Is.False);
				Assert.That(arr.ValuesEqual(Enumerable.Empty<string>()), Is.False);
				Assert.That(arr.ValuesEqual(Enumerable.Range(0, 1).Select(_ => "foo")), Is.False);
				Assert.That(arr.ValuesEqual(Enumerable.Range(0, 2).Select(i => i == 0 ? "foo" : "baz")), Is.False);
				Assert.That(arr.ValuesEqual(Enumerable.Range(0, 3).Select(i => i == 0 ? "foo" : i == 1 ? "bar" : "baz")), Is.False);
			}

			using (Assert.EnterMultipleScope())
			{
				var arr = JsonArray.Create([ 1, 2, 3 ]);

				Assert.That(arr.ValuesEqual(JsonArray.Create(1, 2, 3)), Is.True);

				Assert.That(arr.ValueEquals<int[]>([ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValueEquals<long[]>([ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValueEquals<double[]>([ 1.0, 2.0, 3.0 ]), Is.True);
				Assert.That(arr.ValueEquals<float[]>([ 1.0f, 2.0f, 3.0f ]), Is.True);
				Assert.That(arr.ValueEquals<List<int>>([ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValueEquals<List<long>>([ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValueEquals<List<double>>([ 1.0, 2.0, 3.0 ]), Is.True);
				Assert.That(arr.ValueEquals<List<float>>([ 1.0f, 2.0f, 3.0f ]), Is.True);

				Assert.That(arr.ValueEquals<int[]>([ 1, 2 ]), Is.False);
				Assert.That(arr.ValueEquals<int[]>([ 1, 2, 3, 4 ]), Is.False);
				Assert.That(arr.ValueEquals<string[]>([ "1", "2", "3" ]), Is.False);

				Assert.That(arr.ValuesEqual((ReadOnlySpan<int>) [ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValuesEqual((int[]) [ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValuesEqual((List<int>) [ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValuesEqual((IEnumerable<int>) [ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValuesEqual(Enumerable.Range(1, 3)), Is.True);

				Assert.That(arr.ValuesEqual<long>([ 1L, 2L, 3L ]), Is.True);
				Assert.That(arr.ValuesEqual<double>([ 1.0, 2.0, 3.0 ]), Is.True);
				Assert.That(arr.ValuesEqual<float>([ 1.0f, 2.0f, 3.0f ]), Is.True);
				Assert.That(arr.ValuesEqual<decimal>([ 1.0m, 2.0m, 3.0m ]), Is.True);

				// collection expression
				Assert.That(arr.ValuesEqual([ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValuesEqual([ 1L, 2L, 3L ]), Is.True);
				Assert.That(arr.ValuesEqual([ 1.0, 2.0, 3.0 ]), Is.True);
				Assert.That(arr.ValuesEqual([ 1.0f, 2.0f, 3.0f ]), Is.True);
				Assert.That(arr.ValuesEqual([ 1.0m, 2.0m, 3.0m ]), Is.True);
			}
		}

		[Test]
		public void Test_JsonArray_StrictEquals()
		{
			using (Assert.EnterMultipleScope())
			{
				var arr = JsonArray.ReadOnly.Empty;

				Assert.That(arr.StrictEquals(JsonArray.ReadOnly.Empty), Is.True);
				Assert.That(arr.StrictEquals(new JsonArray()), Is.True);
				Assert.That(arr.StrictEquals(JsonArray.Create([])), Is.True);

				Assert.That(arr.StrictEquals(JsonNull.Null), Is.False);
				Assert.That(arr.StrictEquals(JsonNull.Missing), Is.False);
				Assert.That(arr.StrictEquals(JsonNull.Error), Is.False);
				Assert.That(arr.StrictEquals(JsonBoolean.False), Is.False);
				Assert.That(arr.StrictEquals(JsonBoolean.True), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("")), Is.False);
				Assert.That(arr.StrictEquals(JsonObject.ReadOnly.Empty), Is.False);
				Assert.That(arr.StrictEquals(JsonString.Return("")), Is.False);

				Assert.That(arr.StrictEquals(default(ReadOnlySpan<JsonValue>)), Is.True);
				Assert.That(arr.StrictEquals(Array.Empty<JsonValue>()), Is.True);
				Assert.That(arr.StrictEquals(new List<JsonValue>()), Is.True);
				Assert.That(arr.StrictEquals(Enumerable.Empty<JsonValue>()), Is.True);
			}

			using (Assert.EnterMultipleScope())
			{
				var arr = JsonArray.Create([ "hello", 123, true ]);

				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123, true)), Is.True);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123L, true)), Is.True);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123.0, true)), Is.True);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123.0f, true)), Is.True);

				Assert.That(arr.StrictEquals(JsonNull.Null), Is.False);
				Assert.That(arr.StrictEquals(JsonNull.Missing), Is.False);
				Assert.That(arr.StrictEquals(JsonNull.Error), Is.False);
				Assert.That(arr.StrictEquals(JsonBoolean.False), Is.False);
				Assert.That(arr.StrictEquals(JsonBoolean.True), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.ReadOnly.Empty), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("world", 123, true)), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 456, true)), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123, false)), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123, true, "world")), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", "123", true)), Is.False);
				Assert.That(arr.StrictEquals(JsonObject.Create([ ("0", "hello"), ("1", 123), ("2", true) ])), Is.False);

				Assert.That(arr.StrictEquals((ReadOnlySpan<JsonValue>) [ "hello", 123, true ]), Is.True);
				Assert.That(arr.StrictEquals((JsonValue[]) [ "hello", 123, true ]), Is.True);
				Assert.That(arr.StrictEquals((List<JsonValue>) [ "hello", 123, true ]), Is.True);
				Assert.That(arr.StrictEquals(Enumerable.Range(0, 3).Select(i => ((ReadOnlySpan<JsonValue>) [ "hello", 123, true ])[i])), Is.True);
			}

			using (Assert.EnterMultipleScope())
			{
				var arr = JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.Create("foo", "bar", "baz"), JsonArray.Create(true, false), JsonObject.Create("hello", "there"));

				Assert.That(arr.StrictEquals(JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.Create("foo", "bar", "baz"), JsonArray.Create(true, false), JsonObject.Create("hello", "there"))), Is.True);
				Assert.That(arr.StrictEquals(JsonArray.Create(JsonArray.Create(1.0, 2.0f, 3m), JsonArray.Create("foo", "bar", "baz"), JsonArray.Create(true, false), JsonObject.Create("hello", "there"))), Is.True);

				Assert.That(arr.StrictEquals(JsonArray.Create(JsonArray.Create("1", 2, 3), JsonArray.Create("foo", "bar", "baz"), JsonArray.Create(true, false), JsonObject.Create("hello", "there"))), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.Create("foo", "baz", "bar"), JsonArray.Create(true, false), JsonObject.Create("hello", "there"))), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.Create("foo", "bar", "baz"), JsonArray.Create(1, 0), JsonObject.Create("hello", "there"))), Is.False);
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonArray.Create(default(string)).StrictEquals(JsonArray.Create(JsonNull.Null)), Is.True);
				Assert.That(JsonArray.Create(JsonNull.Null).StrictEquals(JsonArray.Create(default(string))), Is.True);
				Assert.That(JsonArray.Create(default(string)).StrictEquals(JsonArray.Create(JsonNull.Missing)), Is.False);
				Assert.That(JsonArray.Create(JsonNull.Missing).StrictEquals(JsonArray.Create(default(string))), Is.False);

				Assert.That(JsonArray.Create(0).StrictEquals(JsonArray.Create(false)), Is.False);
				Assert.That(JsonArray.Create(1).StrictEquals(JsonArray.Create(true)), Is.False);
				Assert.That(JsonArray.Create(false).StrictEquals(JsonArray.Create(0)), Is.False);
				Assert.That(JsonArray.Create(true).StrictEquals(JsonArray.Create(1)), Is.False);
				Assert.That(JsonArray.Create(false).StrictEquals(JsonArray.Create(JsonNull.Null)), Is.False);

				Assert.That(JsonArray.Create(Guid.Empty).StrictEquals(JsonArray.Create(JsonNull.Null)), Is.True); // Guid.Empty <=> null
				Assert.That(JsonArray.Create(JsonNull.Null).StrictEquals(JsonArray.Create(Guid.Empty)), Is.True); // Guid.Empty <=> null
			}
		}

		#endregion

		#region Checks...

		private static void AssertIsImmutable(JsonObject? obj, [CallerArgumentExpression(nameof(obj))] string? expression = "")
		{
			Assert.That(obj, Is.Not.Null);
			Assert.That(obj!.IsReadOnly, Is.True, "Object should be immutable: " + expression);
			Assert.That(() => obj.Clear(), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.Add("hello", "world"), Throws.InvalidOperationException);
			Assert.That(() => obj["hello"] = "world", Throws.InvalidOperationException, expression);
			Assert.That(() => obj.Add(KeyValuePair.Create("hello", JsonString.Return("world"))), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.AddRange(new [] { KeyValuePair.Create("hello", JsonString.Return("world")) }), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.TryAdd("hello", "world"), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.Remove("hello"), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.Remove("hello", out _), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.Remove(KeyValuePair.Create("hello", JsonString.Return("world"))), Throws.InvalidOperationException, expression);
		}

		private static void AssertIsImmutable(JsonArray? arr, [CallerArgumentExpression(nameof(arr))] string? expression = "")
		{
			if (arr is null)
			{
				Assert.That(arr, Is.Not.Null);
				return;
			}

			Assert.That(arr, IsJson.ReadOnly, expression);
			Assert.That(() => arr.Clear(), Throws.InvalidOperationException, expression);
			Assert.That(() => arr[0] = "world", Throws.InvalidOperationException, expression);
			Assert.That(() => arr.Set(0, "hello"), Throws.InvalidOperationException);
			Assert.That(() => arr.Add("hello"), Throws.InvalidOperationException);
			Assert.That(() => arr.AddValue("hello"), Throws.InvalidOperationException);
			Assert.That(() => arr.AddNull(), Throws.InvalidOperationException);
			Assert.That(() => arr.AddRange(Array.Empty<JsonValue?>().AsSpan()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddRange(Array.Empty<JsonValue>()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddRange(Enumerable.Empty<JsonValue>()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddValues<int>(Array.Empty<int>().AsSpan()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddValues<int>(Array.Empty<int>()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddValues(Enumerable.Empty<int>()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddValues(Enumerable.Empty<int>(), x => x.ToString()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.Insert(0, 123), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.RemoveAt(0), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.Remove("helo"), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.RemoveDuplicates(), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.RemoveAll(_ => true), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.KeepOnly(_ => true), Throws.InvalidOperationException, expression);
		}

		private static void EnsureDeepImmutabilityInvariant(JsonValue value, string? path = null)
		{
			switch (value)
			{
				case JsonObject obj:
				{
					EnsureDeepImmutabilityInvariant(obj, path);
					break;
				}
				case JsonArray arr:
				{
					EnsureDeepImmutabilityInvariant(arr, path);
					break;
				}
			}
		}

		private static void EnsureDeepImmutabilityInvariant(JsonObject obj, string? path = null)
		{
			Assert.That(obj.IsReadOnly, Is.True, $"Object at {path} should be immutable");

			foreach (var (k, v) in obj)
			{
				switch (v)
				{
					case JsonObject o:
					{
						EnsureDeepImmutabilityInvariant(o, path is not null ? (path + "." + k) : k);
						break;
					}
					case JsonArray a:
					{
						EnsureDeepImmutabilityInvariant(a, path is not null ? (path + "." + k) : k);
						break;
					}
				}
			}
		}

		private static void EnsureDeepImmutabilityInvariant(JsonArray arr, string? path = null)
		{
			Assert.That(arr.IsReadOnly, Is.True, $"Object at {path} should be immutable");

			for(int i = 0; i < arr.Count; i++)
			{
				switch (arr[i])
				{
					case JsonObject o:
					{
						EnsureDeepImmutabilityInvariant(o, path + "[" + i + "]");
						break;
					}
					case JsonArray a:
					{
						EnsureDeepImmutabilityInvariant(a, path + "[" + i + "]");
						break;
					}
				}
			}
		}

		private static void EnsureDeepMutabilityInvariant(JsonValue value, string? path = null)
		{
			switch (value)
			{
				case JsonObject obj:
				{
					EnsureDeepMutabilityInvariant(obj, path);
					break;
				}
				case JsonArray arr:
				{
					EnsureDeepMutabilityInvariant(arr, path);
					break;
				}
			}
		}

		private static void EnsureDeepMutabilityInvariant(JsonObject obj, string? path = null)
		{
			Assert.That(obj.IsReadOnly, Is.False, $"Object at {path} should be mutable");

			foreach (var (k, v) in obj)
			{
				switch (v)
				{
					case JsonObject o:
					{
						EnsureDeepMutabilityInvariant(o, path is not null ? (path + "." + k) : k);
						break;
					}
					case JsonArray a:
					{
						EnsureDeepMutabilityInvariant(a, path is not null ? (path + "." + k) : k);
						break;
					}
				}
			}
		}

		private static void EnsureDeepMutabilityInvariant(JsonArray arr, string? path = null)
		{
			Assert.That(arr.IsReadOnly, Is.False, $"Array at {path} should be mutable");

			for(int i = 0; i < arr.Count; i++)
			{
				switch (arr[i])
				{
					case JsonObject o:
					{
						EnsureDeepMutabilityInvariant(o, path + "[" + i + "]");
						break;
					}
					case JsonArray a:
					{
						EnsureDeepMutabilityInvariant(a, path + "[" + i + "]");
						break;
					}
				}
			}
		}

		#endregion

		#region JsonObject...

		[Test]
		public void Test_JsonObject()
		{
			var obj = new JsonObject();
			Assert.That(obj, Has.Count.EqualTo(0));
			Assert.That(obj.IsNull, Is.False);
			Assert.That(obj.IsDefault, Is.True);
			Assert.That(obj.ToJsonText(), Is.EqualTo("{ }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{}")));

			obj["Hello"] = "World";
			Assert.That(obj, Has.Count.EqualTo(1));
			Assert.That(obj.IsDefault, Is.False);
			Assert.That(obj.ContainsKey("Hello"), Is.True);
			Assert.That(obj["Hello"], Is.EqualTo(JsonString.Return("World")));
			Assert.That(obj.GetValue("Hello"), Is.EqualTo(JsonString.Return("World")));
			Assert.That(obj.ToJsonText(), Is.EqualTo("{ \"Hello\": \"World\" }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\"}")));

			obj.Add("Foo", 123);
			Assert.That(obj, Has.Count.EqualTo(2));
			Assert.That(obj.ContainsKey("Foo"), Is.True);
			Assert.That(obj["Foo"], Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(obj.GetValue("Foo"), Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(obj.ToJsonText(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 123 }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":123}")));

			obj.Set("Foo", 456);
			Assert.That(obj, Has.Count.EqualTo(2));
			Assert.That(obj.ContainsKey("Foo"), Is.True);
			Assert.That(obj["Foo"], Is.EqualTo(JsonNumber.Return(456)));
			Assert.That(obj.GetValue("Foo"), Is.EqualTo(JsonNumber.Return(456)));
			Assert.That(obj.ToJsonText(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 456 }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":456}")));

			obj.Add("Bar", true);
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.ContainsKey("Bar"), Is.True);
			Assert.That(obj["Bar"], Is.EqualTo(JsonBoolean.True));
			Assert.That(obj.GetValue("Bar"), Is.EqualTo(JsonBoolean.True));
			Assert.That(obj.ToJsonText(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 456, \"Bar\": true }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":456,\"Bar\":true}")));

			// case sensitive! ('Bar' != 'BAR')
			var sub = JsonObject.Create([ ("Alpha", 111), ("Omega", 999) ]);
			obj.Add("BAR", sub);
			Assert.That(obj, Has.Count.EqualTo(4));
			Assert.That(obj.ContainsKey("BAR"), Is.True);
			Assert.That(obj["BAR"], Is.SameAs(sub));
			Assert.That(obj.GetValue("BAR"), Is.SameAs(sub));
			Assert.That(obj.ToJsonText(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 456, \"Bar\": true, \"BAR\": { \"Alpha\": 111, \"Omega\": 999 } }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":456,\"Bar\":true,\"BAR\":{\"Alpha\":111,\"Omega\":999}}")));

			// ReadOnlySpan keys
			Assert.That(obj["Hello".AsSpan()], IsJson.EqualTo("World"));
			Assert.That(obj["NotHello".AsSpan(3)], IsJson.EqualTo("World"));

			// property names that require escaping ("..\.." => "..\\..", "\\?\..." => "\\\\?\\....")
			obj = new JsonObject();
			obj["""Hello\World"""] = 123;
			obj["""Hello"World"""] = 456;
			obj["""\\?\GLOBALROOT\Device\Foo\Bar"""] = 789;
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.ContainsKey("""Hello\World"""), Is.True);
			Assert.That(obj.ContainsKey("""Hello"World"""), Is.True);
			Assert.That(obj.ContainsKey("""\\?\GLOBALROOT\Device\Foo\Bar"""), Is.True);
			Assert.That(obj.Get<int>("""Hello\World"""), Is.EqualTo(123));
			Assert.That(obj.Get<int>("""Hello"World"""), Is.EqualTo(456));
			Assert.That(obj.Get<int>("""\\?\GLOBALROOT\Device\Foo\Bar"""), Is.EqualTo(789));
			Assert.That(obj.ToJsonText(), Is.EqualTo("""{ "Hello\\World": 123, "Hello\"World": 456, "\\\\?\\GLOBALROOT\\Device\\Foo\\Bar": 789 }"""));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("""{"Hello\\World":123,"Hello\"World":456,"\\\\?\\GLOBALROOT\\Device\\Foo\\Bar":789}""")));

			//note: we do not deserialize JsonNull singletons "Missing"/"Error" by default
			obj = JsonObject.Create([
				("Foo", JsonNull.Null),
				("Bar", JsonNull.Missing),
				("Baz", JsonNull.Error)
			]);
			Assert.That(obj.ToJsonText(), Is.EqualTo("{ \"Foo\": null }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Foo\":null}")));
		}

		[Test]
		public void Test_JsonObject_Create()
		{
			{ // Create()
				var value = JsonObject.Create();
				Assert.That(value.Type, Is.EqualTo(JsonType.Object));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.True);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(0));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("{}")));
			}

			{ // Create([ ])
				var value = JsonObject.Create([ ]);
				Assert.That(value.Type, Is.EqualTo(JsonType.Object));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.True);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(0));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("{}")));
			}

			{ // Create(("hello", 123))
				var value = JsonObject.Create(("hello", 123));
				Assert.That(value.Type, Is.EqualTo(JsonType.Object));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(1));
				Assert.That(value["hello"], IsJson.EqualTo(123));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""{"hello":123}""")));
			}

			{ // Create([ ("hello", 123), ("world", 456) ])
				var value = JsonObject.Create([ ("hello", 123), ("world", 456) ]);
				Assert.That(value.Type, Is.EqualTo(JsonType.Object));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(2));
				Assert.That(value["hello"], IsJson.EqualTo(123));
				Assert.That(value["world"], IsJson.EqualTo(456));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""{"hello":123,"world":456}""")));
			}

			{ // Create([ "hello": 123, "world": 456 ])
				//TODO: convert this into actual dictionary collection expressions once support for them drops!
				// => var value = (JsonObject) [ "hello": 123, "world": 456 ]
				var value = JsonObject.Create([ KeyValuePair.Create("hello", JsonNumber.Return(123)), KeyValuePair.Create("world", JsonNumber.Return(456)) ]);
				Assert.That(value.Type, Is.EqualTo(JsonType.Object));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(2));
				Assert.That(value["hello"], IsJson.EqualTo(123));
				Assert.That(value["world"], IsJson.EqualTo(456));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""{"hello":123,"world":456}""")));
			}

			{ // duplicate keys should keep last

				var value = JsonObject.Create([ KeyValuePair.Create("hello", JsonNumber.Return(123)), KeyValuePair.Create("world", JsonNumber.Return(456)), KeyValuePair.Create("hello", JsonNumber.Return(789)) ]);
				Assert.That(value.Type, Is.EqualTo(JsonType.Object));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(2));
				Assert.That(value["hello"], IsJson.EqualTo(789));
				Assert.That(value["world"], IsJson.EqualTo(456));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""{"hello":789,"world":456}""")));
			}

			{ // create read-only objects with collection expression arguments

				//TODO: convert this into actual dictionary collection expressions once support for them drops!
				// => var value = (JsonObject) [ with(readOnly: true), "hello": 123, "world": 456 ]
				JsonObject immutable = JsonObject.Create(readOnly: true, [ KeyValuePair.Create("hello", JsonNumber.Return(123)), KeyValuePair.Create("world", JsonNumber.Return(456)) ]);
				Assert.That(immutable, IsJson.ReadOnly);
				Assert.That(immutable["hello"], IsJson.EqualTo(123));
				Assert.That(immutable["world"], IsJson.EqualTo(456));
				Assert.That(immutable.Count, Is.EqualTo(2));
				Assert.That(() => { immutable["hello"] = 42; }, Throws.InvalidOperationException);

				// => var value = (JsonObject) [ with(readOnly: false), "hello": 123, "world": 456 ]
				JsonObject mutable = JsonObject.Create(readOnly: false, [ KeyValuePair.Create("hello", JsonNumber.Return(123)), KeyValuePair.Create("world", JsonNumber.Return(456)) ]);
				Assert.That(mutable, IsJson.Mutable);
				Assert.That(mutable["hello"], IsJson.EqualTo(123));
				Assert.That(mutable["world"], IsJson.EqualTo(456));
				Assert.That(mutable.Count, Is.EqualTo(2));
				Assert.That(() => { mutable["hello"] = 42; }, Throws.Nothing);
				Assert.That(mutable["hello"], IsJson.EqualTo(42));
			}
		}

		[Test]
		public void Test_JsonObject_Span_Keys()
		{
			Span<char> spanKey = [ 'H', 'e', 'l', 'l', 'o' ]; // we assume that it is allocated on the stack!
			ReadOnlyMemory<char> memKey = "Hello".AsMemory();


			{ // stackalloc'ed keys
				var obj = new JsonObject();
				obj[spanKey] = "World";
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj[spanKey], IsJson.EqualTo("World"));
				Assert.That(obj.TryGetValue(spanKey, out var value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(obj[memKey], IsJson.EqualTo("World"));
				Assert.That(obj.TryGetValue(memKey, out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
#if NET9_0_OR_GREATER
				Assert.That(obj.TryGetValue(spanKey, out var actualKey, out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(actualKey, Is.InstanceOf<string>().And.EqualTo("Hello"));
				Assert.That(obj.TryGetValue(memKey, out actualKey, out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(actualKey, Is.InstanceOf<string>().And.EqualTo("Hello"));
#endif
			}

			{ // sliced keys
				var obj = new JsonObject();
				obj["NotHello".AsSpan(3)] = "World";
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj["Hello".AsSpan()], IsJson.EqualTo("World"));
				Assert.That(obj["HelloNot".AsSpan(0, 5)], IsJson.EqualTo("World"));
				Assert.That(obj["Hello".AsMemory()], IsJson.EqualTo("World"));
				Assert.That(obj.TryGetValue("NotHelloNot".AsSpan(3, 5), out var value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(obj.TryGetValue("NotHelloNot".AsMemory(3, 5), out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
#if NET9_0_OR_GREATER
				Assert.That(obj.TryGetValue("NotHelloNot".AsSpan(3, 5), out var actualKey, out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(actualKey, Is.InstanceOf<string>().And.EqualTo("Hello"));
				Assert.That(obj.TryGetValue("NotHelloNot".AsMemory(3, 5), out actualKey, out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(actualKey, Is.InstanceOf<string>().And.EqualTo("Hello"));
#endif
			}

#if NET9_0_OR_GREATER
			{ // TryGetValue(span, ...) returns original key

				// create a unique key, add it to the object, and check that TryGetValue with a span will return the exact same instance (and not a copy!)
				string key = DateTime.Now.Ticks.ToString();
				var obj = new JsonObject();
				obj[key] = "World";
				Span<char> span = stackalloc char[key.Length];
				key.CopyTo(span);
				Assert.That(obj.TryGetValue(span, out var actualKey, out var value), Is.True);
				Assert.That(value, IsJson.EqualTo("World"));
				Assert.That(actualKey, Is.SameAs(key));
			}
			{ // TryGetValue(memory, ...) returns original key

				// create a unique key, add it to the object, and check that TryGetValue with a span will return the exact same instance (and not a copy!)
				string key = DateTime.Now.Ticks.ToString();
				var obj = new JsonObject();
				obj[key] = "World";
				var memKeySpliced = ("hello" + key + "world").AsMemory()[5..^5];
				Assert.That(obj.TryGetValue(memKeySpliced, out var actualKey, out var value), Is.True);
				Assert.That(value, IsJson.EqualTo("World"));
				Assert.That(actualKey, Is.SameAs(key));
			}
#endif

			{ // Set(span, JsonValue)
				var obj = new JsonObject();
				obj.Set(spanKey, "World");
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj[spanKey], IsJson.EqualTo("World"));
				Assert.That(obj[memKey], IsJson.EqualTo("World"));
			}

			{ // Set(memory, JsonValue)
				var obj = new JsonObject();
				obj.Set(memKey, "World");
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj[spanKey], IsJson.EqualTo("World"));
				Assert.That(obj[memKey], IsJson.EqualTo("World"));
			}

			{ // Set<T>(span, T)
				var obj = new JsonObject();
				obj.Set<string>(spanKey, "World");
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj[spanKey], IsJson.EqualTo("World"));
				Assert.That(obj[memKey], IsJson.EqualTo("World"));
			}

			{ // Set<T>(memory, T)
				var obj = new JsonObject();
				obj.Set<string>(memKey, "World");
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj[spanKey], IsJson.EqualTo("World"));
				Assert.That(obj[memKey], IsJson.EqualTo("World"));
			}

		}

		[Test]
		public void Test_JsonObject_Get()
		{
			var obj = new JsonObject
			{
				["Hello"] = "World",
				["Foo"] = 123,
				["Bar"] = true,
				["Baz"] = Math.PI,
				["Void"] = null,
				["Empty"] = "",
				["Space"] = "   ", // Space! Space? Space!!!
			};

			// strings
			Assert.That(obj.Get<string>("Hello"), Is.EqualTo("World"));
			Assert.That(obj.Get<string>("Hello", "not_found"), Is.EqualTo("World"));
			Assert.That(obj.Get<string>("XYZHello".AsSpan(3), "not_found"), Is.EqualTo("World"));

			// booleans
			Assert.That(obj.Get<bool>("Bar"), Is.True);
			Assert.That(obj.Get<bool>("Bar", false), Is.True);
			Assert.That(obj.Get<bool>("XYZBar".AsSpan(3), false), Is.True);

			// integers
			Assert.That(obj.Get<int>("Foo"), Is.EqualTo(123));
			Assert.That(obj.Get<int>("Foo", -1), Is.EqualTo(123));
			Assert.That(obj.Get<int>("XYZFoo".AsSpan(3), -1), Is.EqualTo(123));

			// floats
			Assert.That(obj.Get<double>("Baz"), Is.EqualTo(Math.PI));
			Assert.That(obj.Get<double>("Baz", double.NaN), Is.EqualTo(Math.PI));
			Assert.That(obj.Get<double>("XYZBaz".AsSpan(3), double.NaN), Is.EqualTo(Math.PI));

			// empty must return default(T) for ValueType instances (0, false, Guid.Empty, ...)
			Assert.That(obj.Get<string>("Empty"), Is.EqualTo(""), "'' -> string");
			Assert.That(obj.Get<int>("Empty"), Is.EqualTo(0), "'' -> int");
			Assert.That(obj.Get<bool>("Empty"), Is.False, "'' -> bool");
			Assert.That(obj.Get<double>("Empty"), Is.EqualTo(0.0), "'' -> double");
			Assert.That(obj.Get<Guid>("Empty"), Is.EqualTo(Guid.Empty), "'' -> Guid");
			Assert.That(obj.Get<string>("NotEmpty".AsSpan(3)), Is.EqualTo(""), "'' -> string");

			// empty must return default(T) for Nullable<T> instances (null)
			Assert.That(obj.Get<int?>("Empty", null), Is.Null, "'' -> int?");
			Assert.That(obj.Get<bool?>("Empty", null), Is.Null, "'' -> bool?");
			Assert.That(obj.Get<double?>("Empty", null), Is.Null, "'' -> double?");
			Assert.That(obj.Get<Guid?>("Empty", null), Is.Null, "'' -> Guid?");

			// missing + nullable
			Assert.That(obj.Get<string?>("olleH", null), Is.Null);
			Assert.That(obj.Get<int?>("olleH", null), Is.Null);
			Assert.That(obj.Get<bool?>("olleH", null), Is.Null);
			Assert.That(obj.Get<double?>("olleH", null), Is.Null);
			Assert.That(obj.Get<string?>("olleH".AsSpan(), null), Is.Null);

			// null + nullable
			Assert.That(() => obj.Get<string>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(() => obj.Get<int>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(() => obj.Get<bool>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(() => obj.Get<double>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(obj.Get<string?>("Void", null), Is.Null);
			Assert.That(obj.Get<int?>("Void", null), Is.Null);
			Assert.That(obj.Get<bool?>("Void", null), Is.Null);
			Assert.That(obj.Get<double?>("Void", null), Is.Null);

			// missing + required: true
			Assert.That(() => obj.Get<string>("olleH"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("olleH"));
			Assert.That(() => obj.Get<int>("olleH"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("olleH"));
			Assert.That(() => obj.Get<int?>("olleH"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("olleH"));

			// null + required: true
			Assert.That(() => obj.Get<string>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(() => obj.Get<int>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(() => obj.Get<int?>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));

			// type conversions (number to string, ...)
			Assert.That(obj.Get<string>("Foo"), Is.EqualTo("123"));
			Assert.That(obj.Get<string>("Bar"), Is.EqualTo("true"));
			Assert.That(obj.Get<string>("Baz"), Is.EqualTo("3.141592653589793"));

		}

		[Test]
		public void Test_JsonObject_GetString()
		{
			// Le type string a un traitement spécial vis-à-vis des chaines vides, ce qui justifie la présence de GetString(..) (et pas GetBool, GetInt, ...)
			// - required: si true, rejette null/missing
			// - notEmpty: si true, rejette les chaines vides ou composées uniquement d'espaces

			// note: on peut avoir required:false et notEmpty:true pour des champs optionnels "si présent, alors ne doit pas être vide" (ex: un Guid optionnel, etc...)

			var obj = new JsonObject
			{
				// "Missing": not present
				["Hello"] = "World",
				["Void"] = null,
				["Empty"] = "",
				["Space"] = "   ", // Space! Space? Space!!!
			};

			//Get<string>(..) se comporte comme les autres (ne considère que null/missing)
			Assert.That(obj.Get<string?>("Missing", null), Is.Null);
			Assert.That(() => obj.Get<string>("Missing"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.Get<string?>("Void", null), Is.Null);
			Assert.That(() => obj.Get<string>("Void"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.Get<string>("Empty"), Is.EqualTo(""));
			Assert.That(obj.Get<string>("Space"), Is.EqualTo("   "));
		}

		[Test]
		public void Test_JsonObject_GetPath()
		{
			// GetPath(...) est une sorte d'équivalent à SelectSingleNode(..) qui prend un chemin de type "Foo.Bar.Baz" pour dire "le champ Baz du champ Bar du champ Foo de l'objet actuel
			// ex: obj.GetPath("Foo.Bar.Baz") est l'équivalent de obj["Foo"]["Baz"]["Baz"]

			JsonValue value;

			var obj = JsonObject.FromObject(new
			{
				Hello = "World",
				Coords = new { X = 1, Y = 2, Z = 3 },
				Foo = new { Bar = new { Baz = 123 } },
				Values = new[] {"a", "b", "c"},
				Items = JsonArray.Create(
					JsonObject.Create("Value", "one"),
					JsonObject.Create("Value", "two"),
					JsonObject.Create("Value", "three")
				),
			});
			Dump(obj);

			// Direct descendants...

			value = obj.GetPathValueOrDefault("Hello", null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.String));
			Assert.That(value.Required<string>(), Is.EqualTo("World"));
			Assert.That(obj.GetPath<string>("Hello"), Is.EqualTo("World"));

			value = obj.GetPathValueOrDefault("Coords", null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Object));

			value = obj.GetPathValueOrDefault("Values", null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Array));

			value = obj.GetPathValueOrDefault("NotFound", null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Null));
			Assert.That(value, Is.EqualTo(JsonNull.Missing));

			// Children

			Assert.That(obj.GetPath<int>("Coords.X"), Is.EqualTo(1));
			Assert.That(obj.GetPath<int>("Coords.Y"), Is.EqualTo(2));
			Assert.That(obj.GetPath<int>("Coords.Z"), Is.EqualTo(3));
			Assert.That(() => obj.GetPath<int>("Coords.NotFound"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.GetPath<int>("XYZ.Coords.Z".AsMemory(4)), Is.EqualTo(3));

			Assert.That(obj.GetPath<int?>("Foo.Bar.Baz", null), Is.EqualTo(123));
			Assert.That(obj.GetPath<int?>("Foo.Bar.NotFound", null), Is.Null);
			Assert.That(obj.GetPath<int?>("Foo.NotFound.Baz", null), Is.Null);
			Assert.That(obj.GetPath<int?>("NotFound.Bar.Baz", null), Is.Null);
			Assert.That(obj.GetPath<int?>("Foo.Bar.Baz.XYZ".AsMemory()[..^4], null), Is.EqualTo(123));

			// Array Indexing

			Assert.That(obj.GetPath<string>("Values[0]"), Is.EqualTo("a"));
			Assert.That(obj.GetPath<string>("Values[1]"), Is.EqualTo("b"));
			Assert.That(obj.GetPath<string>("Values[2]"), Is.EqualTo("c"));
			Assert.That(() => obj.GetPath<string>("Values[3]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>("Values[2].NotFound"), Throws.InstanceOf<JsonBindingException>());

			Assert.That(obj.GetPath<string?>("Items[0].Value", null), Is.EqualTo("one"));
			Assert.That(obj.GetPath<string?>("Items[1].Value", null), Is.EqualTo("two"));
			Assert.That(obj.GetPath<string?>("Items[2].Value", null), Is.EqualTo("three"));
			Assert.That(obj.GetPath<string?>("Items[0].NotFound", null), Is.Null);
			Assert.That(obj.GetPath<string?>("Items[3]", null), Is.Null);
			Assert.That(obj.GetPath<string?>("Items[3].Value", null), Is.Null);

			// Required
			Assert.That(() => obj.GetPath<int>("NotFound.Bar.Baz"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<int>("Coords.NotFound"), Throws.InstanceOf<JsonBindingException>());

			obj = new JsonObject
			{
				["X"] = default(string),
				["Y"] = default(Guid?),
				["Z"] = JsonNull.Missing
			};
			Assert.That(() => obj.GetPath<string>("X"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<Guid>("Y"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>("Z"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>("間"), Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_JsonObject_GetPath_JsonPath()
		{
			// GetPath(...) est une sorte d'équivalent à SelectSingleNode(..) qui prend un chemin de type "Foo.Bar.Baz" pour dire "le champ Baz du champ Bar du champ Foo de l'objet actuel
			// ex: obj.GetPath("Foo.Bar.Baz") est l'équivalent de obj["Foo"]["Baz"]["Baz"]

			JsonValue value;

			var obj = JsonObject.FromObject(new
			{
				Hello = "World",
				Coords = new { X = 1, Y = 2, Z = 3 },
				Foo = new { Bar = new { Baz = 123 } },
				Values = new[] {"a", "b", "c"},
				Items = JsonArray.Create(
					JsonObject.Create("Value", "one"),
					JsonObject.Create("Value", "two"),
					JsonObject.Create("Value", "three")
				),
			});
			Dump(obj);

			// Direct descendants...

			value = obj.GetPathValueOrDefault(JsonPath.Create("Hello"), null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.String));
			Assert.That(value.Required<string>(), Is.EqualTo("World"));
			Assert.That(obj.GetPath<string>("Hello"), Is.EqualTo("World"));

			value = obj.GetPathValueOrDefault(JsonPath.Create("Coords"), null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Object));

			value = obj.GetPathValueOrDefault(JsonPath.Create("Values"), null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Array));

			value = obj.GetPathValueOrDefault(JsonPath.Create("NotFound"), null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Null));
			Assert.That(value, Is.EqualTo(JsonNull.Missing));

			// Children

			Assert.That(obj.GetPath<int>(JsonPath.Create("Coords.X")), Is.EqualTo(1));
			Assert.That(obj.GetPath<int>(JsonPath.Create("Coords.Y")), Is.EqualTo(2));
			Assert.That(obj.GetPath<int>(JsonPath.Create("Coords.Z")), Is.EqualTo(3));
			Assert.That(() => obj.GetPath<int>(JsonPath.Create("Coords.NotFound")), Throws.InstanceOf<JsonBindingException>());

			Assert.That(obj.GetPath<int?>(JsonPath.Create("Foo.Bar.Baz"), null), Is.EqualTo(123));
			Assert.That(obj.GetPath<int?>(JsonPath.Create("Foo.Bar.NotFound"), null), Is.Null);
			Assert.That(obj.GetPath<int?>(JsonPath.Create("Foo.NotFound.Baz"), null), Is.Null);
			Assert.That(obj.GetPath<int?>(JsonPath.Create("NotFound.Bar.Baz"), null), Is.Null);

			// Array Indexing

			Assert.That(obj.GetPath<string>(JsonPath.Create("Values[0]")), Is.EqualTo("a"));
			Assert.That(obj.GetPath<string>(JsonPath.Create("Values[1]")), Is.EqualTo("b"));
			Assert.That(obj.GetPath<string>(JsonPath.Create("Values[2]")), Is.EqualTo("c"));
			Assert.That(() => obj.GetPath<string>(JsonPath.Create("Values[3]")), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>(JsonPath.Create("Values[2].NotFound")), Throws.InstanceOf<JsonBindingException>());

			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[0].Value"), null), Is.EqualTo("one"));
			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[1].Value"), null), Is.EqualTo("two"));
			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[2].Value"), null), Is.EqualTo("three"));
			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[0].NotFound"), null), Is.Null);
			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[3]"), null), Is.Null);
			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[3].Value"), null), Is.Null);

			// Required
			Assert.That(() => obj.GetPath<int>(JsonPath.Create("NotFound.Bar.Baz")), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<int>(JsonPath.Create("Coords.NotFound")), Throws.InstanceOf<JsonBindingException>());

			obj = new JsonObject
			{
				["X"] = default(string),
				["Y"] = default(Guid?),
				["Z"] = JsonNull.Missing
			};
			Assert.That(() => obj.GetPath<string>(JsonPath.Create("X")), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<Guid>(JsonPath.Create("Y")), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>(JsonPath.Create("Z")), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>(JsonPath.Create("間")), Throws.InstanceOf<JsonBindingException>());

			// test that we can access field names that require espacing
			obj = new JsonObject
			{
				["foo.bar"] = "baz",
				["[42]"] = 42,
				[@"\o/"] = "Hello, there!",
			};
			// incorrectly escaped in the path
			Assert.That(obj.GetPathValueOrDefault("foo.bar"), IsJson.Null);
			Assert.That(obj.GetPathValueOrDefault("[42]"), IsJson.Null);
			Assert.That(obj.GetPathValueOrDefault(@"\o/"), IsJson.Null);
			// properly escaped in the path
			Assert.That(obj.GetPathValueOrDefault(@"foo\.bar"), IsJson.EqualTo("baz"));
			Assert.That(obj.GetPathValueOrDefault(@"\[42\]"), IsJson.EqualTo(42));
			Assert.That(obj.GetPathValueOrDefault(@"\\o/"), IsJson.EqualTo("Hello, there!"));
		}

		[Test]
		public void Test_JsonObject_SetPath()
		{
			var obj = JsonObject.Create();

			// create
			obj.SetPath("Hello", "World");
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(1));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Hello": "World" }""")));
			Assert.That(obj.GetPath<string>("Hello"), Is.EqualTo("World"));

			// update
			obj.SetPath("Hello", "Le Monde!");
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(1));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Hello": "Le Monde!" }""")));
			Assert.That(obj.GetPath<string>("Hello"), Is.EqualTo("Le Monde!"));

			// add other
			obj.SetPath("Level", 9001);
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(2));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Hello": "Le Monde!", "Level": 9001 }""")));
			Assert.That(obj.GetPath<int>("Level"), Is.EqualTo(9001));

			// null => JsonNull.Null
			obj.SetPath("Hello", null);
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(2));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Hello": null, "Level": 9001 }""")));
			Assert.That(() => obj.GetPath<string>("Hello"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.GetPath<string?>("Hello", null), Is.Null);

			// remove
			obj.RemovePath("Hello");
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(1));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Level": 9001 }""")));
			Assert.That(() => obj.GetPath<string>("Hello"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.GetPath<string?>("Hello", null), Is.Null);

			obj.RemovePath("Level");
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(0));
			Assert.That(() => obj.GetPath<int>("Level"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.GetPath<int?>("Level", null), Is.Null);
		}

		[Test]
		public void Test_JsonObject_SetPath_SubObject()
		{
			var obj = JsonObject.Create();
			obj.SetPath("Foo.Bar", 123);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foo": { "Bar": 123 } }""")));
			Assert.That(obj.GetPath<int>("Foo.Bar"), Is.EqualTo(123));

			obj.SetPath("Foo.Baz", 456);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foo": { "Bar": 123, "Baz": 456 } }""")));
			Assert.That(obj.GetPath<int>("Foo.Baz"), Is.EqualTo(456));

			obj.RemovePath("Foo.Bar");
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foo": { "Baz": 456 } }""")));
			Assert.That(obj.GetPath<int?>("Foo.Bar", null), Is.Null);

			obj.RemovePath("Foo");
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(0));
		}

		[Test]
		public void Test_JsonObject_SetPath_SubArray()
		{
			var obj = JsonObject.Create();

			obj.SetPath("Foos[0]", 123);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos": [ 123 ] }""")));
			Assert.That(obj.GetPath<int?>("Foos[0]", null), Is.EqualTo(123));

			obj.SetPath("Foos[1]", 456);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos": [ 123, 456 ] }""")));
			Assert.That(obj.GetPath<int?>("Foos[1]", null), Is.EqualTo(456));

			obj.SetPath("Foos[3]", 789); //skip one
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos": [ 123, 456, null, 789 ] }""")));
			Assert.That(obj.GetPath<int?>("Foos[2]", null), Is.Null);
			Assert.That(obj.GetPath<int?>("Foos[3]", null), Is.EqualTo(789));
		}

		[Test]
		public void Test_JsonObject_SetPath_SubArray_Of_Objects()
		{
			var obj = JsonObject.Create();

			obj.SetPath("Foos[0]", JsonObject.Create([ ("X", 1), ("Y", 2), ("Z", 3) ]));
			DumpCompact(obj);
			Assert.That(obj, IsJson.EqualTo(JsonValue.Parse("""{ "Foos" : [ { "X": 1, "Y": 2, "Z": 3 } ] }""")));
			Assert.That(obj.GetPathValueOrDefault("Foos[0]"), IsJson.EqualTo(JsonValue.Parse("""{ "X": 1, "Y": 2, "Z": 3 }""")));

			obj.SetPath("Foos[2]", JsonObject.Create([ ("X", 4), ("Y", 5), ("Z", 6) ]));
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos" : [ { "X": 1, "Y": 2, "Z": 3 }, null, { "X": 4, "Y": 5, "Z": 6 } ] }""")));
			Assert.That(obj.GetPathValueOrDefault("Foos[1]"), IsJson.ExplicitNull);
			Assert.That(obj.GetPathValueOrDefault("Foos[2]"), IsJson.EqualTo(JsonValue.Parse("""{ "X": 4, "Y": 5, "Z": 6 }""")));

			// auto-created
			obj = JsonObject.Create();
			obj.SetPath("Foos[0].X", 1);
			obj.SetPath("Foos[0].Y", 2);
			obj.SetPath("Foos[0].Z", 3);
			DumpCompact(obj);
			Assert.That(obj, IsJson.EqualTo(JsonValue.Parse("""{ "Foos" : [ { "X": 1, "Y": 2, "Z": 3 } ] }""")));
			Assert.That(obj.GetPath<int?>("Foos[0].X", null), Is.EqualTo(1));
			Assert.That(obj.GetPath<int?>("Foos[0].Y", null), Is.EqualTo(2));
			Assert.That(obj.GetPath<int?>("Foos[0].Z", null), Is.EqualTo(3));

			obj.SetPath("Foos[2].X", 4);
			obj.SetPath("Foos[2].Y", 5);
			obj.SetPath("Foos[2].Z", 6);
			DumpCompact(obj);
			Assert.That(obj, IsJson.EqualTo(JsonValue.Parse("""{ "Foos" : [ { "X": 1, "Y": 2, "Z": 3 }, null, { "X": 4, "Y": 5, "Z": 6 } ] }""")));
			Assert.That(obj.GetPath<int?>("Foos[2].X", null), Is.EqualTo(4));
			Assert.That(obj.GetPath<int?>("Foos[2].Y", null), Is.EqualTo(5));
			Assert.That(obj.GetPath<int?>("Foos[2].Z", null), Is.EqualTo(6));
		}

		[Test]
		public void Test_JsonObject_SetPath_SubArray_Of_Arrays()
		{
			{
				var obj = JsonObject.Create();
				obj.SetPath("Matrix[0][2]", 1);
				obj.SetPath("Matrix[1][1]", 2);
				obj.SetPath("Matrix[2][0]", 3);
				DumpCompact(obj);
				Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Matrix" : [ [ null, null, 1 ], [ null, 2 ], [ 3 ] ] }""")));
				Assert.That(obj.GetPath<int?>("Matrix[0][2]"), Is.EqualTo(1));
				Assert.That(obj.GetPath<int?>("Matrix[1][1]"), Is.EqualTo(2));
				Assert.That(obj.GetPath<int?>("Matrix[2][0]"), Is.EqualTo(3));

				obj.SetPath("Matrix[0][0]", 4);
				obj.SetPath("Matrix[2][1]", 5);
				DumpCompact(obj);
				Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Matrix" : [ [ 4, null, 1 ], [ null, 2 ], [ 3, 5 ] ] }""")));
				Assert.That(obj.GetPath<int?>("Matrix[0][0]"), Is.EqualTo(4));
				Assert.That(obj.GetPath<int?>("Matrix[2][1]"), Is.EqualTo(5));
			}

			{
				var obj = JsonObject.Create();
				obj.SetPath("Foos[0][2].Bar", 123);
				DumpCompact(obj);
				Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos" : [ [ null, null, { "Bar": 123 } ] ] }""")));
				Assert.That(obj.GetPath<int>("Foos[0][2].Bar"), Is.EqualTo(123));
				obj.SetPath("Foos[0][2].Bar", 456);
				DumpCompact(obj);
				Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos" : [ [ null, null, { "Bar": 456 } ] ] }""")));
				Assert.That(obj.GetPath<int>("Foos[0][2].Bar"), Is.EqualTo(456));
			}

			{
				var obj = JsonObject.Create();
				obj.SetPath("Foos[0].Bar[2]", 123);
				DumpCompact(obj);
				Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos" : [ { "Bar": [ null, null, 123 ] } ] }""")));
				Assert.That(obj.GetPath<int>("Foos[0].Bar[2]"), Is.EqualTo(123));
			}
		}

		[Test]
		public void Test_JsonObject_GetOrCreateObject()
		{
			var root = JsonObject.Create();
			var foo = root.GetOrCreateObject("Foo");
			Assert.That(foo, Is.Not.Null, "Foo");
			Assert.That(root, Is.EqualTo(JsonValue.Parse("""{ "Foo": {} }""")));

			root.GetOrCreateObject("Bar").Set("Baz", 123);
			Assert.That(root, Is.EqualTo(JsonValue.Parse("""{ "Foo": {}, "Bar": { "Baz": 123 } }""")));

			root.GetOrCreateObject("Bar").Set("Hello", "World");
			Assert.That(root, Is.EqualTo(JsonValue.Parse("""{ "Foo": {}, "Bar": {"Baz":123, "Hello": "World" } }""")));

			root = JsonObject.Create();
			root.GetOrCreateObject("Narf.Zort.Poit").Set("MDR", "LOL");
			Assert.That(root, Is.EqualTo(JsonValue.Parse("""{ "Narf": { "Zort": { "Poit": { "MDR": "LOL" } } } }""")));

			// on doit pouvoir écraser un null
			root = JsonObject.Create();
			root["Bar"] = JsonNull.Null;
			var bar = root.GetOrCreateObject("Bar");
			Assert.That(bar, Is.Not.Null, "Bar");
			bar.Set("Hello", "World");
			Assert.That(root, Is.EqualTo(JsonValue.Parse("""{ "Bar": { "Hello": "World" } }""")));

			// par contre on doit pas pouvoir écraser un non-object
			root = JsonObject.Create("Baz", "Hello");
			Assert.That(
				() => root.GetOrCreateObject("Baz"),
				Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The specified key 'Baz' exists, but is of type String instead of expected Object"),
				"Expected error message (can change!)"
			);
		}

		[Test]
		public void Test_JsonObject_MergeWith()
		{
			static void Merge(JsonObject root, JsonObject obj)
			{
				Log($"  {root}");
				Log($"+ {obj}");
				root.MergeWith(obj);
				Log($"= {root}");
				Log();
			}

			{ // Add a new field
				// { Foo: 123 } u { Bar: 456 } => { Foo: 123, Bar: 456 }
				var root = new JsonObject { ["Foo"] = 123 };
				var obj = new JsonObject { ["Bar"] = 456 };
				Merge(root, obj);
				Assert.That(root.ToJsonTextCompact(), Is.EqualTo("""{"Foo":123,"Bar":456}"""));
			}

			{ // Overwrite an existing field
				// { Foo: 123 } u { Foo: 456 } => { Foo: 456 }
				var root = new JsonObject { ["Foo"] = 123 };
				var obj = new JsonObject { ["Foo"] = 456 };
				Merge(root, obj);
				Assert.That(root.ToJsonTextCompact(), Is.EqualTo("""{"Foo":456}"""));
			}

			{ // Merging a field with Null will remove it, if keepNull == false
				// { Foo: { Bar: 42 } } u { Foo: null } => { }
				var root = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 42 } };
				var obj = new JsonObject { ["Foo"] = JsonNull.Null };
				Merge(root, obj);
				Assert.That(root, IsJson.Empty);
			}
			{ // Merging a field with Null will set it to null, if keepNull == true
				// { Foo: { Bar: 42 } } u { Foo: null } => { Foo: null }
				var root = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 42 } };
				var obj = new JsonObject { ["Foo"] = JsonNull.Null };
				root.MergeWith(obj, keepNull: true);
				Assert.That(root.ToJsonTextCompact(), Is.EqualTo("""{"Foo":null}"""));
			}
			{ // Merging a field with Missing will remove it, even if keepNull == true
				// { Foo: { Bar: 42 } } u { Foo: missing } => { }
				var root = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 42 } };
				var obj = new JsonObject { ["Foo"] = JsonNull.Missing };
				root.MergeWith(obj, keepNull: true); // should have no effect!
				Assert.That(root, IsJson.Empty);
			}

			{ // Merge the contents of a child object
				// { Foo: { Bar: 123 } } u  { Foo: { Baz: 456 } } => { Foo: { Bar: 123, Baz: 456 } }
				var root = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 123 } };
				var obj = new JsonObject { ["Foo"] = new JsonObject { ["Baz"] = 456 } };
				Merge(root, obj);
				Assert.That(root, IsJson.EqualTo(new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 123, ["Baz"] = 456 } }));
			}
			{
				var root = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 123, ["Baz"] = 456 } };
				var obj = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = null, ["Jazz"] = 789 } };
				Merge(root, obj);
				Assert.That(root, IsJson.EqualTo(new JsonObject { ["Foo"] = new JsonObject { ["Baz"] = 456, ["Jazz"] = 789 } }));
			}

			{ // Merge the contents of two child arrays
				var root = new JsonObject { ["Foos"] = JsonArray.Create([
					new JsonObject() { ["x"] = 1, ["y"] = 0, ["z"] = 0 },
					new JsonObject() { ["x"] = 0, ["y"] = 1, ["z"] = 0 },
				])};
				var obj = new JsonObject { ["Foos"] = JsonArray.Create([
					JsonObject.ReadOnly.Empty, // do no change
					new JsonObject() { ["y"] = -1, ["z"] = 1 }, // set y to -1, add z
					new JsonObject() { ["x"] = 0, ["y"] = 1, ["z"] = 0 }, // add new point
				]) };
				Merge(root, obj);
				Assert.That(root, IsJson.EqualTo(new JsonObject { ["Foos"] = JsonArray.Create([
					new JsonObject() { ["x"] = 1, ["y"] = 0, ["z"] = 0 },
					new JsonObject() { ["x"] = 0, ["y"] = -1, ["z"] = 1 },
					new JsonObject() { ["x"] = 0, ["y"] = 1, ["z"] = 0 },
				]) }));
			}

			{ // truncate the last elements of a child array
				var root = new JsonObject { ["Foos"] = JsonArray.Create(1, 2, 3, 4, 5) };
				var obj = new JsonObject { ["Foos"] = JsonArray.Create(1, null, -3, null, null) };
				Merge(root, obj);
				Assert.That(root, IsJson.EqualTo(new JsonObject { ["Foos"] = JsonArray.Create(1, null, -3) }));
			}
		}

		[Test]
		public void Test_JsonObject_Diff()
		{
			static void Verify(JsonObject a, JsonObject b, JsonObject expected)
			{
				Log($"before: {a}");
				Log($"after : {b}");
				var patch = a.ComputePatch(b);
				Log($"patch : {patch}");

				Assert.That(patch, Is.EqualTo(expected), "Patch does not match expected value");

				// applying the diff on 'a' should produce 'b'!
				var b2 = a.Copy();
				b2.ApplyPatch(patch);
				Log("merged: " + b2);
				Assert.That(b2, IsJson.EqualTo(b));

				Log();
			}

			{ // no differences
				var a = new JsonObject()
				{
					["Foo"] = 123,
					["Bar"] = 456
				};
				var b = new JsonObject()
				{
					["Foo"] = 123,
					["Bar"] = 456
				};
				Verify(a, b, JsonObject.ReadOnly.Empty);
			}
			{ // field added
				var a = new JsonObject()
				{
					["Foo"] = 123
				};
				var b = new JsonObject()
				{
					["Foo"] = 123,
					["Bar"] = 456
				};
				Verify(a, b, new JsonObject { ["Bar"] = 456 });
			}
			{ // field changed
				var a = new JsonObject()
				{
					["Foo"] = 123
				};
				var b = new JsonObject()
				{
					["Foo"] = 456
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = 456
				});
			}
			{ // field removed
				var a = new JsonObject()
				{
					["Foo"] = 123,
					["Bar"] = 456
				};
				var b = new JsonObject()
				{
					["Bar"] = 456
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = null
				});
			}
			{ // child objects changed
				var a = new JsonObject()
				{
					["A"] = JsonObject.Create([ ("x", 1), ("y", 0), ("z", 0) ]),
					["B"] = JsonObject.Create([ ("x", 0), ("y", 1), ("z", 0) ]),
					["C"] = JsonObject.Create([ ("x", 0), ("y", 0), ("z", 1) ]),
				};
				var b = new JsonObject()
				{
					["A"] = JsonObject.Create([ ("x", 1), ("y",  0), ("z",  0) ]),
					["B"] = JsonObject.Create([ ("x", 0), ("y", -1), ("z",  0) ]),
					["D"] = JsonObject.Create([ ("x",-1), ("y", -1), ("z", -1) ]),
				};
				Verify(a, b, new JsonObject
				{
					["B"] = JsonObject.Create([ ("y", -1) ]),
					["C"] = null,
					["D"] = JsonObject.Create([ ("x", -1), ("y", -1), ("z", -1) ]),
				});
			}
			{ // child arrays added
				var a = JsonObject.Create();
				var b = JsonObject.Create(
				[
					("Foo", JsonArray.Create(1, 2, 3, 4, 5))
				]);
				Verify(a, b, JsonObject.Create(
				[
					("Foo", JsonArray.Create(1, 2, 3, 4, 5))
				]));
			}
			{ // child arrays added (was empty before)
				var a = JsonObject.Create(
				[
					("Foo", JsonArray.ReadOnly.Empty)
				]);
				var b = JsonObject.Create(
				[
					("Foo", JsonArray.Create(1, 2, 3, 4, 5))
				]);
				Verify(a, b, JsonObject.Create(
				[
					("Foo", JsonArray.Create(1, 2, 3, 4, 5))
				]));
			}
			{ // child arrays cleared
				var a = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, 2, 3, 4, 5)
				};
				var b = new JsonObject
				{
					["Foo"] = JsonArray.ReadOnly.Empty
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = JsonArray.ReadOnly.Empty
				});
			}
			{ // child arrays changed (only literals, with items added)
				var a = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, 2, 3)
				};
				var b = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, -2, 3, 4, 5)
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = new JsonObject
					{
						["__patch"] = 5,
						["1"] = -2,
						["3"] = 4,
						["4"] = 5,
					}
				});
			}
			{ // child arrays changed (only literals, same size, same tail)
				var a = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, 2, 3, 4, 5)
				};
				var b = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, -2, 3, 4, 5)
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = new JsonObject()
					{
						["__patch"] = 5,
						["1"] = -2,
					}
				});
			}
			{ // child arrays changed (only literals, items removed at the end)
				var a = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, 2, 3, 4, 5)
				};
				var b = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, 2, 3)
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = new JsonObject()
					{
						["__patch"] = 3,
					}
				});
			}
			{ // child arrays changed (with sub-objects)
				var a = new JsonObject
				{
					["Foo"] = JsonArray.Create([
						JsonObject.Create([ ("x", 1), ("y", 0), ("z", 0) ]),
						JsonObject.Create([ ("x", 0), ("y", 1), ("z", 0) ]),
						JsonObject.Create([ ("x", 0), ("y", 0), ("z", 1) ]),
					])
				};
				var b = new JsonObject
				{ 
					["Foo"] = JsonArray.Create([
						JsonObject.Create([ ("x",  1), ("y",  0), ("z",  0) ]), // unchanged
						JsonObject.Create([ ("x",  0), ("y", -1), ("z",  0) ]), // y changed
						JsonObject.Create([ ("x",  0), ("y",  0) ]),            // z removed
						JsonObject.Create([ ("x", -1), ("y", -1), ("z", -1) ]), // added
					])
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = new JsonObject()
					{
						["__patch"] = 4,
						["1"] = JsonObject.Create([ ("y", -1) ]), // y changed
						["2"] = JsonObject.Create([ ("z", null) ]), // z removed
						["3"] = JsonObject.Create([ ("x", -1), ("y", -1), ("z", -1) ]), // added
					}
				});
			}
		}

		[Test]
		public void Test_JsonObject_GetOrCreateArray()
		{
			var root = JsonObject.Create();

			var foo = root.GetOrCreateArray("Foo");
			Assert.That(foo, Is.Not.Null, "Foo");
			Assert.That(foo, Has.Count.EqualTo(0), "foo.Count");
			Assert.That(root.ToJsonText(CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Foo":[]}"""));

			foo.AddValue(123);
			Assert.That(root.ToJsonText(CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Foo":[123]}"""));

			root.GetOrCreateArray("Foo").AddValue(456);
			Assert.That(root.ToJsonText(CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Foo":[123,456]}"""));

			root = JsonObject.Create();
			root.GetOrCreateArray("Narf.Zort.Poit").AddValue(789);
			Assert.That(root.ToJsonText(CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Narf":{"Zort":{"Poit":[789]}}}"""));

			// on doit pouvoir écraser un null
			root = JsonObject.Create();
			root["Bar"] = JsonNull.Null;
			var bar = root.GetOrCreateArray("Bar");
			Assert.That(bar, Is.Not.Null, "Bar");
			bar.AddValue("Hello");
			bar.AddValue("World");
			Assert.That(root.ToJsonText(CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Bar":["Hello","World"]}"""));

			// par contre on doit pas pouvoir écraser un non-object
			root = JsonObject.Create();
			root.Set("Baz", "Hello");
			Assert.That(
				() => root.GetOrCreateArray("Baz"),
				Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The specified key 'Baz' exists, but is of type String instead of expected Array"),
				"Expected error message (can change!)"
			);
		}

		[Test]
		public void Test_JsonObject_Project()
		{
			var obj = JsonObject.FromObject(new
			{
				Id = 1,
				Name = "Walter White",
				Pseudo = "Einsenberg",
				Occupation = "Chemistry Teacher",
				Hobby = "Cook"
			});
			Dump(obj);

			JsonObject p;

			p = obj.Pick([ "Id", "Name" ]);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p["Id"], IsJson.EqualTo(1));
			Assert.That(p["Name"], IsJson.EqualTo("Walter White"));
			Assert.That(p, Has.Count.EqualTo(2));
			Assert.That(p, IsJson.Mutable);
			// the original should not be changed
			Assert.That(obj, Has.Count.EqualTo(5));

			p = obj.Pick([ "Id" ]);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p["Id"], IsJson.EqualTo(1));
			Assert.That(p, Has.Count.EqualTo(1));

			p = obj.Pick([ "Id", "NotFound" ]);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p["Id"], IsJson.EqualTo(1));
			Assert.That(p.ContainsKey("NotFound"), Is.False);
			Assert.That(p, Has.Count.EqualTo(1));

			p = obj.Pick([ "Id", "NotFound" ], keepMissing: true);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p["Id"], IsJson.EqualTo(1));
			Assert.That(p.ContainsKey("NotFound"), Is.True);
			Assert.That(p["NotFound"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p, Has.Count.EqualTo(2));

			p = obj.Pick([ "NotFound" ]);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p, Has.Count.EqualTo(0));

			p = obj.Pick([ "NotFound" ], keepMissing: true);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p.ContainsKey("NotFound"), Is.True);
			Assert.That(p["NotFound"], IsJson.Missing);
			Assert.That(p, Has.Count.EqualTo(1));


			{ // test that we keep the "readonly-ness" or the original
				var source = JsonObject.ReadOnly.Create([("foo", 123), ("bar", 456)]);
				Assume.That(source, IsJson.ReadOnly);
				p = source.Pick(["bar"]);
				Assert.That(p, IsJson.ReadOnly);
				Assert.That(source, IsJson.ReadOnly);
			}
		}

		[Test]
		public void Test_JsonObject_ReadOnly_Empty()
		{
			Assert.That(JsonObject.ReadOnly.Empty.IsReadOnly, Is.True);
			//note: we don't want to attempt to modify the empty readonly singleton, because if the test fails, it will completely break ALL the reamining tests!

			static void CheckEmptyReadOnly(JsonObject obj, [CallerArgumentExpression(nameof(obj))] string? expression = null)
			{
				Assert.That(obj, Has.Count.Zero, expression);
				AssertIsImmutable(obj, expression);
				Assert.That(obj, Has.Count.Zero, expression);
			}

			CheckEmptyReadOnly(JsonObject.ReadOnly.Create());
			CheckEmptyReadOnly(JsonObject.ReadOnly.Create(Array.Empty<KeyValuePair<string, JsonValue>>()));
			CheckEmptyReadOnly(JsonObject.ReadOnly.Create(new Dictionary<string, JsonValue>()));
			CheckEmptyReadOnly(JsonObject.Create().ToReadOnly());
			CheckEmptyReadOnly(JsonObject.Copy(JsonObject.Create(), deep: false, readOnly: true));
			CheckEmptyReadOnly(JsonObject.Copy(JsonObject.Create(), deep: true, readOnly: true));

			var obj = JsonObject.Create("hello", "world");
			obj.Remove("hello");
			CheckEmptyReadOnly(obj.ToReadOnly());
			CheckEmptyReadOnly(JsonObject.Copy(obj, deep: false, readOnly: true));
			CheckEmptyReadOnly(JsonObject.Copy(obj, deep: true, readOnly: true));
		}

		[Test]
		public void Test_JsonObject_ReadOnly()
		{
			// creating a readonly object with only immutable values should produce an immutable object
			AssertIsImmutable(JsonObject.ReadOnly.Create("one", 1));
			AssertIsImmutable(JsonObject.ReadOnly.Create(("one", 1)));
			AssertIsImmutable(JsonObject.ReadOnly.Create([ ("one", 1) ]));
			AssertIsImmutable(JsonObject.ReadOnly.Create([ ("one", 1), ("two", 2) ]));
			AssertIsImmutable(JsonObject.ReadOnly.Create([ ("one", 1), ("two", 2), ("three", 3)]));
			AssertIsImmutable(JsonObject.ReadOnly.Create([ ("one", 1), ("two", 2), ("three", 3), ("four", 4)]));
			AssertIsImmutable(JsonObject.ReadOnly.Create([ ("one", 1), ("two", 2), ("three", 3), ("four", 4), ("five", 5) ]));
			AssertIsImmutable(JsonObject.ReadOnly.FromValues(Enumerable.Range(0, 10).Select(i => KeyValuePair.Create(i.ToString(), i))));

			// creating an immutable version of a writable object with only immutable should return an immutable object
			AssertIsImmutable(JsonObject.Create("one", 1).ToReadOnly());
			AssertIsImmutable(JsonObject.Create([ ("one", 1), ("two", 2), ("three", 3) ]).ToReadOnly());

			// parsing with JsonImmutable should return an already immutable object
			var obj = JsonObject.Parse("""{ "hello": "world", "foo": { "id": 123, "name": "Foo", "address" : { "street": 123, "city": "Paris" } }, "bar": [ 1, 2, 3 ], "baz": [ { "jazz": 42 } ] }""", CrystalJsonSettings.JsonReadOnly);
			AssertIsImmutable(obj);
			var foo = obj.GetObject("foo");
			AssertIsImmutable(foo);
			var address = foo.GetObject("address");
			AssertIsImmutable(address);
			var bar = obj.GetArray("bar");
			AssertIsImmutable(bar);
			var baz = obj.GetArray("baz");
			AssertIsImmutable(baz);
			var jazz = baz.GetObject(0);
			AssertIsImmutable(jazz);
		}

		[Test]
		public void Test_JsonObject_Freeze()
		{
			// ensure that, given a mutable JSON object, we can create an immutable version that is protected against any changes

			// the original object should be mutable
			var obj = new JsonObject
			{
				["hello"] = "world",
				["foo"] = new JsonObject { ["bar"] = "baz" },
				["bar"] = new JsonArray { 1, 2, 3 }
			};
			Assert.That(obj.IsReadOnly, Is.False);
			// the inner children 'foo' and 'bar' should be mutable as well
			var foo = obj.GetObject("foo");
			Assert.That(obj.IsReadOnly, Is.False);
			var bar = obj.GetArray("bar");
			Assert.That(obj.IsReadOnly, Is.False);

			Assert.That(obj.ToJsonTextCompact(), Is.EqualTo("""{"hello":"world","foo":{"bar":"baz"},"bar":[1,2,3]}"""));

			var obj2 = obj.Freeze();
			Assert.That(obj2, Is.SameAs(obj), "Freeze() should return the same instance");
			AssertIsImmutable(obj2, "obj.Freeze()");

			// the inner object 'foo' should also have been frozen!
			var foo2 = obj2.GetObject("foo");
			Assert.That(foo2, Is.SameAs(foo));
			Assert.That(foo2.IsReadOnly, Is.True);
			AssertIsImmutable(foo2, "(obj.Freeze())[\"foo\"]");

			// the inner array 'bar' should also have been frozen!
			var bar2 = obj2.GetArray("bar");
			Assert.That(bar2, Is.SameAs(bar));
			Assert.That(bar2.IsReadOnly, Is.True);
			AssertIsImmutable(bar2, "(obj.Freeze())[\"bar\"]");

			Assert.That(obj2.ToJsonTextCompact(), Is.EqualTo("""{"hello":"world","foo":{"bar":"baz"},"bar":[1,2,3]}"""));

			// if we want to mutate, we have to create a copy
			var obj3 = obj2.Copy();

			// the copy should still be equal to the original
			Assert.That(obj3, Is.Not.SameAs(obj2), "it should return a new instance");
			Assert.That(obj3, Is.EqualTo(obj2), "It should still be equal");
			var foo3 = obj3.GetObject("foo");
			Assert.That(foo3, Is.Not.SameAs(foo2), "inner object should be cloned");
			Assert.That(foo3, Is.EqualTo(foo2), "It should still be equal");
			var bar3 = obj3.GetArray("bar");
			Assert.That(bar3, Is.Not.SameAs(bar2), "inner array should be cloned");
			Assert.That(bar3, Is.EqualTo(bar2), "It should still be equal");

			// should be aable to change the copy
			Assert.That(obj3, Has.Count.EqualTo(3));
			Assert.That(() => obj3.Add("bonjour", "le monde"), Throws.Nothing);
			Assert.That(obj3, Has.Count.EqualTo(4));
			Assert.That(obj3, Is.Not.EqualTo(obj2), "It should still not be equal after the change");
			Assert.That(foo3, Is.EqualTo(foo2), "It should still be equal");
			Assert.That(bar3, Is.EqualTo(bar2), "It should still be equal");

			// should be able to mutate the inner object
			Assert.That(foo3, Has.Count.EqualTo(1));
			Assert.That(() => foo3["baz"] = "jazz", Throws.Nothing);
			Assert.That(foo3, Has.Count.EqualTo(2));
			Assert.That(foo3, Is.Not.EqualTo(foo2), "It should still not be equal after the change");

			// should be able to mutate the inner array
			Assert.That(bar3, Has.Count.EqualTo(3));
			Assert.That(() => bar3.Add(4), Throws.Nothing);
			Assert.That(bar3, Has.Count.EqualTo(4));
			Assert.That(bar3, Is.Not.EqualTo(bar2), "It should still not be equal after the change");

			// verify the final mutated version
			Assert.That(obj3.ToJsonTextCompact(), Is.EqualTo("""{"hello":"world","foo":{"bar":"baz","baz":"jazz"},"bar":[1,2,3,4],"bonjour":"le monde"}"""));
			// ensure the original is unmodified
			Assert.That(obj2.ToJsonTextCompact(), Is.EqualTo("""{"hello":"world","foo":{"bar":"baz"},"bar":[1,2,3]}"""));
		}

		[Test]
		public void Test_JsonObject_Can_Mutate_Frozen()
		{
			// given an immutable object, check that we can create mutable version that will not modifiy the original
			var original = new JsonObject
			{
				["hello"] = "world",
				["foo"] = new JsonObject { ["bar"] = "baz" },
				["bar"] = new JsonArray { 1, 2, 3 }
			}.Freeze();
			Dump("Original", original);
			EnsureDeepImmutabilityInvariant(original);

			// create a "mutable" version of the entire tree
			var obj = original.ToMutable();
			Dump("Copy", obj);
			Assert.That(obj.IsReadOnly, Is.False, "Copy should be not be read-only!");
			Assert.That(obj, Is.Not.SameAs(original));
			Assert.That(obj, Is.EqualTo(original));
			Assert.That(obj["foo"], Is.Not.SameAs(original["foo"]));
			Assert.That(obj["bar"], Is.Not.SameAs(original["bar"]));
			EnsureDeepMutabilityInvariant(obj);

			obj["hello"] = "le monde";
			obj["level"] = 42;
			obj.GetObject("foo").Remove("bar");
			obj.GetObject("foo").Add("baz", "bar");
			obj.GetArray("bar").Add(4);
			Dump("Mutated", obj);
			Assert.That(obj, Is.Not.EqualTo(original));

			// ensure the original is not mutated
			Assert.That(original["hello"], IsJson.EqualTo("world"));
			Assert.That(original["level"], IsJson.Null);
			Assert.That(original["foo"]["bar"], IsJson.EqualTo("baz"));
			Assert.That(original["foo"]["baz"], IsJson.Null);
			Assert.That(original.GetArray("bar"), Has.Count.EqualTo(3));
		}

		[Test]
		public void Test_JsonObject_CopyAndMutate()
		{
			// Test the "builder" API that can simplify making changes to a read-only object and publishing the new instance.
			// All methods will create return a new copy of the original, with the mutation applied, leaving the original untouched.
			// The new read-only copy should reuse the same JsonValue instances as the original, to reduce memory copies.

			var obj = JsonObject.ReadOnly.Empty;
			Assume.That(obj, IsJson.Empty);
			Assume.That(obj, IsJson.ReadOnly);
			Assume.That(obj["hello"], IsJson.Missing);
			DumpCompact(obj);

			// copy and add first field
			var obj2 = obj.CopyAndAdd("hello", "world");
			DumpCompact(obj2);
			Assert.That(obj2, Is.Not.SameAs(obj));
			Assert.That(obj2, Has.Count.EqualTo(1));
			Assert.That(obj2, IsJson.ReadOnly);
			Assert.That(obj2["hello"], IsJson.EqualTo("world"));
			Assert.That(obj, IsJson.Empty);
			Assert.That(obj["hello"], IsJson.Missing);

			// copy and set second field
			var obj3 = obj2.CopyAndSet("XYZfoo".AsSpan(3), "bar");
			DumpCompact(obj3);
			Assert.That(obj3, Is.Not.SameAs(obj2));
			Assert.That(obj3, Has.Count.EqualTo(2));
			Assert.That(obj3, IsJson.ReadOnly);
			Assert.That(obj3["hello"], Is.SameAs(obj2["hello"]));
			Assert.That(obj3["foo"], IsJson.EqualTo("bar"));
			Assert.That(obj2, Has.Count.EqualTo(1));
			Assert.That(obj2["hello"], IsJson.EqualTo("world"));
			Assert.That(obj2["foo"], IsJson.Missing);
			Assert.That(obj, IsJson.Empty);

			// copy and add existing field should fail
			Assert.That(() => obj3.CopyAndAdd("foo", "baz"), Throws.ArgumentException.With.Message.Contains("foo"));
			Assert.That(obj3, Has.Count.EqualTo(2));
			Assert.That(obj3["foo"], IsJson.EqualTo("bar"));

			// copy and set should overwrite existing field
			var obj4 = obj3.CopyAndSet("XYZfoo".AsMemory(3), "baz");
			DumpCompact(obj4);
			Assert.That(obj4, Is.Not.SameAs(obj3));
			Assert.That(obj4, Has.Count.EqualTo(2));
			Assert.That(obj4, IsJson.ReadOnly);
			Assert.That(obj4["hello"], Is.EqualTo("world").And.SameAs(obj3["hello"]));
			Assert.That(obj4["foo"], IsJson.EqualTo("baz"));
			Assert.That(obj3, Has.Count.EqualTo(2));
			Assert.That(obj3["hello"], Is.EqualTo("world").And.SameAs(obj2["hello"]));
			Assert.That(obj3["foo"], IsJson.EqualTo("bar"));
			Assert.That(obj2, Has.Count.EqualTo(1));
			Assert.That(obj2["hello"], Is.EqualTo("world"));
			Assert.That(obj2["foo"], IsJson.Missing);
			Assert.That(obj, IsJson.Empty);
			Assert.That(obj["foo"], IsJson.Missing);

			// copy and remove
			var obj5 = obj4.CopyAndRemove("hello");
			DumpCompact(obj5);
			Assert.That(obj5, Is.Not.SameAs(obj4));
			Assert.That(obj5, Has.Count.EqualTo(1));
			Assert.That(obj5, IsJson.ReadOnly);
			Assert.That(obj5["hello"], IsJson.Missing);
			Assert.That(obj5["foo"], IsJson.EqualTo("baz"));
			Assert.That(obj4, Has.Count.EqualTo(2));
			Assert.That(obj4["hello"], IsJson.EqualTo("world"));
			Assert.That(obj4["foo"], IsJson.EqualTo("baz"));
			Assert.That(obj3, Has.Count.EqualTo(2));
			Assert.That(obj3["hello"], IsJson.EqualTo("world"));
			Assert.That(obj3["foo"], IsJson.EqualTo("bar"));
			Assert.That(obj2, Has.Count.EqualTo(1));
			Assert.That(obj2["hello"], IsJson.EqualTo("world"));
			Assert.That(obj2["foo"], IsJson.Missing);
			Assert.That(obj, IsJson.Empty);
			Assert.That(obj["foo"], IsJson.Missing);

			// copy and try removing last field
			var obj6 = obj5.CopyAndRemove("foo", out var prev);
			DumpCompact(obj6);
			Assert.That(obj6, Is.Not.SameAs(obj5));
			Assert.That(obj6, Is.Empty);
			Assert.That(obj6, IsJson.ReadOnly);
			Assert.That(obj6["hello"], IsJson.Missing);
			Assert.That(obj6["foo"], IsJson.Missing);
			Assert.That(obj6, Is.SameAs(JsonObject.ReadOnly.Empty));
			Assert.That(prev, Is.SameAs(obj5["foo"]));
			Assert.That(obj5, Has.Count.EqualTo(1));
			Assert.That(obj5["hello"], IsJson.Missing);
			Assert.That(obj5["foo"], IsJson.EqualTo("baz"));
			Assert.That(obj4, Has.Count.EqualTo(2));
			Assert.That(obj4["hello"], IsJson.EqualTo("world"));
			Assert.That(obj4["foo"], IsJson.EqualTo("baz"));
			Assert.That(obj3, Has.Count.EqualTo(2));
			Assert.That(obj3["hello"], IsJson.EqualTo("world"));
			Assert.That(obj3["foo"], IsJson.EqualTo("bar"));
			Assert.That(obj2, Has.Count.EqualTo(1));
			Assert.That(obj2["hello"], IsJson.EqualTo("world"));
			Assert.That(obj2["foo"], IsJson.Missing);
			Assert.That(obj, IsJson.Empty);
			Assert.That(obj["foo"], IsJson.Missing);

		}

		[Test]
		public async Task Test_JsonObject_CopyAndPublish()
		{
			var prev = JsonObject.ReadOnly.Empty;

			JsonObject published = prev;

			var obj = JsonObject.CopyAndAdd(ref published, "hello", "world");
			Assert.That(obj, Is.SameAs(published));
			Assert.That(published, Is.Not.SameAs(prev));
			Assert.That(obj, IsJson.ReadOnly);
			Assert.That(obj["hello"], IsJson.EqualTo("world"));

			prev = published;
			obj = JsonObject.CopyAndSet(ref published, "foo", "bar");
			Assert.That(obj, Is.SameAs(published));
			Assert.That(published, Is.Not.SameAs(prev));
			Assert.That(obj, IsJson.ReadOnly);
			Assert.That(obj["hello"], IsJson.EqualTo("world"));
			Assert.That(obj["foo"], IsJson.EqualTo("bar"));
			Assert.That(prev["foo"], IsJson.Missing);

			// attempts to verify the thread safety by spinnig N threads that will all add M fields, and checking that the result is an object with N x M unique fields

			published = JsonObject.ReadOnly.Empty;

			var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			const int N = 10;
			const int M = 100;

			var keys = Enumerable.Range(0, N).Select(idx => Enumerable.Range(0, M).Select(i => $"{idx}_{i}").ToArray()).ToArray();

			var workers = keys.Select(async row =>
			{
				// precompute a maximum so that we can ensure the most contention between threads!
				await go.Task.ConfigureAwait(false);
				var value = JsonBoolean.True;
				foreach(var key in row)
				{
					JsonObject.CopyAndAdd(ref published, key, value);
				}
			}).ToList();

			go.TrySetResult();

			await WhenAll(workers, TimeSpan.FromSeconds(30));
			// Ensure that all the keys and values are accounted for.
			Assert.That(published, Has.Count.EqualTo(N * M));
			foreach (var row in keys)
			{
				foreach (var key in row)
				{
					if (!published.ContainsKey(key))
					{
						Dump(published);
						Assert.That(published, Does.ContainKey(key));
					}
				}
			}
		}

		[Test]
		public void Test_JsonObject_StrictEquals()
		{
			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonObject.ReadOnly.Empty.StrictEquals(JsonObject.ReadOnly.Empty), Is.True);
				Assert.That(JsonObject.ReadOnly.Empty.StrictEquals(new JsonObject()), Is.True);
				Assert.That(new JsonObject().StrictEquals(JsonObject.ReadOnly.Empty), Is.True);

				Assert.That(JsonObject.ReadOnly.Empty.StrictEquals(JsonObject.Create("hello", "world")), Is.False);
				Assert.That(JsonObject.ReadOnly.Empty.StrictEquals(JsonObject.Create("hello", null)), Is.True);
			}
			using (Assert.EnterMultipleScope())
			{
				var obj = JsonObject.Create([ ("hello", "world"), ("foo", 123), ("bar", true) ]);

				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123), ("bar", true) ])), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123L), ("bar", true) ])), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123.0), ("bar", true) ])), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123.0f), ("bar", true) ])), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123m), ("bar", true) ])), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("bar", true), ("foo", 123), ("hello", "world") ])), Is.True);

				Assert.That(obj.StrictEquals(JsonObject.ReadOnly.Empty), Is.False);
				Assert.That(obj.StrictEquals(new JsonObject()), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123), ("bar", true), ("baz", 456) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "there"), ("foo", 123), ("bar", true) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 456), ("bar", true) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", "123"), ("bar", true) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123), ("bar", false) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", 123), ("foo", true), ("bar", "world") ])), Is.False);

				Assert.That(obj.StrictEquals(new Dictionary<string, JsonValue> { { "hello", "world" }, { "foo", 123 }, { "bar", true } }), Is.True);
				Assert.That(obj.StrictEquals(new Dictionary<string, JsonValue> { { "hello", "there" }, { "foo", 123 }, { "bar", true } }), Is.False);
				Assert.That(obj.StrictEquals(new Dictionary<string, JsonValue> { { "hello", "world" }, { "foo", 456 }, { "bar", true } }), Is.False);
				Assert.That(obj.StrictEquals(new Dictionary<string, JsonValue> { { "hello", "world" }, { "foo", 123 }, { "bar", false } }), Is.False);
			}
			using (Assert.EnterMultipleScope())
			{
				var obj = JsonObject.Create("foo", JsonObject.Create("bar", JsonArray.Create("baz", 123, true)));

				Assert.That(obj.StrictEquals(JsonObject.Create("foo", JsonObject.Create("bar", JsonArray.Create("baz", 123, true)))), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create("foo", JsonObject.Create("bar", JsonArray.Create("baz", "123", true)))), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create("foo", JsonObject.Create("bar", JsonArray.Create("baz", 123, 456)))), Is.False);
			}
			using (Assert.EnterMultipleScope())
			{
				// check that explicit null/missing are equivalent to "not present" on the other side
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", null) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", null) ])), Is.True);
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", JsonNull.Missing) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", JsonNull.Null) ])), Is.True);

				// but if one is null/missing, the other should not have a value
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", 456) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", null) ])), Is.False);
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", 456) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", JsonNull.Missing) ])), Is.False);
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", null) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", 456) ])), Is.False);
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", JsonNull.Missing) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", 456) ])), Is.False);
			}
		}

		#endregion

		#region JsonValue.FromValue/FromObject...

		[Test]
		public void Test_JsonValue_FromValue_Basic_Types()
		{
			// FromValue<T>(T)
			Assert.That(JsonValue.FromValue(null), Is.InstanceOf<JsonNull>());
			Assert.That(JsonValue.FromValue(DBNull.Value), Is.InstanceOf<JsonNull>());
			Assert.That(JsonValue.FromValue(123), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(123456L), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(123.4f), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(123.456d), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(uint)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(ulong)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(sbyte)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(byte)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(short)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(ushort)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(decimal)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(false), Is.InstanceOf<JsonBoolean>());
			Assert.That(JsonValue.FromValue(true), Is.InstanceOf<JsonBoolean>());
			Assert.That(JsonValue.FromValue("hello"), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(string.Empty), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(Guid.NewGuid()), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(IPAddress.Loopback), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(DateTime.Now), Is.InstanceOf<JsonDateTime>());
			Assert.That(JsonValue.FromValue(TimeSpan.FromMinutes(1)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(DateOnly.FromDateTime(DateTime.Now)), Is.InstanceOf<JsonDateTime>());
			Assert.That(JsonValue.FromValue(TimeOnly.FromDateTime(DateTime.Now)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ")), Is.InstanceOf<JsonString>());

			// FromValue(object)
			Assert.That(JsonValue.FromValue(default(object)!), Is.InstanceOf<JsonNull>());
			Assert.That(JsonValue.FromValue((object) DBNull.Value), Is.InstanceOf<JsonNull>());
			Assert.That(JsonValue.FromValue((object) 123), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) 123456L), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) 123.4f), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) 123.456d), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(uint)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(ulong)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(sbyte)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(byte)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(short)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(ushort)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(decimal)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) false), Is.InstanceOf<JsonBoolean>());
			Assert.That(JsonValue.FromValue((object) true), Is.InstanceOf<JsonBoolean>());
			Assert.That(JsonValue.FromValue((object) "hello"), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object) string.Empty), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object) Guid.NewGuid()), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object) IPAddress.Loopback), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object) DateTime.Now), Is.InstanceOf<JsonDateTime>());
			Assert.That(JsonValue.FromValue((object) TimeSpan.FromMinutes(1)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) DateOnly.FromDateTime(DateTime.Now)), Is.InstanceOf<JsonDateTime>());
			Assert.That(JsonValue.FromValue((object) TimeOnly.FromDateTime(DateTime.Now)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ")), Is.InstanceOf<JsonString>());
		}

		[Test]
		public void Test_JsonValue_FromObject_Enums()
		{
			var result = JsonValue.FromValue(DateTimeKind.Utc);
			Assert.That(result, Is.InstanceOf<JsonString>());
			Assert.That(result.ToString(), Is.EqualTo("Utc"));
		}

		[Test]
		public void Test_JsonValue_FromObject_Nullable()
		{
			//note: nullable<int> is boxed into Int32 if it had a value, so we lose the knowledge that it was a Nullable<int> when calling FromObject(object)
			// => we will force the type by calling FromObject(..., typeof(int?)) in order to ensure that it works as intended

			Assert.That(JsonValue.FromValue(null, typeof(int?)), Is.InstanceOf<JsonNull>());
			int? x = 123;
			Assert.That(JsonValue.FromValue(x, typeof(int?)), Is.InstanceOf<JsonNumber>());

			Assert.That(JsonValue.FromValue(null, typeof(DateTime?)), Is.InstanceOf<JsonNull>());
			DateTime? d = DateTime.Now;
			Assert.That(JsonValue.FromValue(d, typeof(DateTime?)), Is.InstanceOf<JsonDateTime>());

			Assert.That(JsonValue.FromValue(null, typeof(Guid?)), Is.InstanceOf<JsonNull>());
			Guid? g = Guid.NewGuid();
			Assert.That(JsonValue.FromValue(g, typeof(Guid?)), Is.InstanceOf<JsonString>());

			Assert.That(JsonValue.FromValue(null, typeof(DateTimeKind?)), Is.InstanceOf<JsonNull>());
			DateTimeKind? k = DateTimeKind.Utc;
			Assert.That(JsonValue.FromValue(k, typeof(DateTimeKind?)), Is.InstanceOf<JsonString>());
		}

		[Test]
		public void Test_JsonValue_FromObject_Lists()
		{
			{ // array of primitive type
				var j = JsonValue.FromValue(new[] { 1, 42, 77 });
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j); // => [ 1, 42, 77 ]
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(3));
				Assert.That(arr[0], IsJson.EqualTo(1));
				Assert.That(arr[1], IsJson.EqualTo(42));
				Assert.That(arr[2], IsJson.EqualTo(77));
			}

			{ // list of primitive type
				var j = JsonValue.FromValue(new List<int> { 1, 42, 77 });
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j); // => [ 1, 42, 77 ]
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(3));
				Assert.That(arr[0], IsJson.EqualTo(1));
				Assert.That(arr[1], IsJson.EqualTo(42));
				Assert.That(arr[2], IsJson.EqualTo(77));
			}

			{ // array of ref type
				var j = JsonValue.FromValue(new[] { "foo", "bar", "baz" });
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j); // [ "foo", "bar", "baz" ]
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(3));
				Assert.That(arr[0], IsJson.EqualTo("foo"));
				Assert.That(arr[1], IsJson.EqualTo("bar"));
				Assert.That(arr[2], IsJson.EqualTo("baz"));
			}

			{ // special collection (read only)
				var j = JsonValue.FromValue(new ReadOnlyCollection<string>(new List<string> { "foo", "bar", "baz" }));
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j); // [ "foo", "bar", "baz" ]
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(3));
				Assert.That(arr, IsJson.EqualTo([ "foo", "bar", "baz" ]));
				Assert.That(arr, IsJson.EqualTo(arr));
				Assert.That(arr[0], IsJson.EqualTo("foo"));
				Assert.That(arr[1], IsJson.EqualTo("bar"));
				Assert.That(arr[2], IsJson.EqualTo("baz"));
			}

			{ // LINQ query
				var j = JsonValue.FromValue(Enumerable.Range(1, 10));
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j); // => [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 ]
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(10));
				Assert.That(arr, IsJson.EqualTo(Enumerable.Range(1, 10)));
			}

			{ // LINQ query
				var j = JsonValue.FromValue(Enumerable.Range(1, 3).Select(x => new KeyValuePair<int, char>(x, (char)(64 + x))).ToList());
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j);
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(3));
				//TODO: BUGBUG: for now, will return [ { Key: .., Value: .. }, .. ] instead of [ [ .., .. ], .. ]
			}
		}

		[Test]
		public void Test_JsonValue_FromObject_Dictionary()
		{
			//string keys...

			var j = JsonValue.FromValue(new Dictionary<string, int> {{"foo", 11}, {"bar", 22}, {"baz", 33}});
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonObject>());
			Log(j); // { "foo": 11, "bar": 22, "baz": 33 }
			var obj = j.AsObject();
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.Get<int>("foo"), Is.EqualTo(11));
			Assert.That(obj.Get<int>("bar"), Is.EqualTo(22));
			Assert.That(obj.Get<int>("baz"), Is.EqualTo(33));

			var g1 = Guid.NewGuid();
			var g2 = Guid.NewGuid();
			var g3 = Guid.NewGuid();
			j = JsonValue.FromValue(new Dictionary<string, Guid> { { "foo", g1 }, { "bar", g2 }, { "baz", g3 } });
			Log(j); // { "foo": ..., "bar": ..., "baz": ... }
			obj = j.AsObject();
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.Get<Guid>("foo"), Is.EqualTo(g1));
			Assert.That(obj.Get<Guid>("bar"), Is.EqualTo(g2));
			Assert.That(obj.Get<Guid>("baz"), Is.EqualTo(g3));

			var dic = Enumerable.Range(0, 3).Select(x => new {Id = x, Name = "User#" + x.ToString(), Level = x * 9000}).ToDictionary(x => x.Name);
			obj = JsonObject.FromObject(dic);
			Log(obj);
			Assert.That(obj, Has.Count.EqualTo(3));

			// non-string keys...

			j = JsonValue.FromValue(new Dictionary<int, string> { [11] = "foo", [22] = "bar", [33] = "baz" });
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonObject>());
			Log(j); // { "11": "foo", "22": "bar", "33": "baz" }
			obj = j.AsObject();
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.Get<string>("11"), Is.EqualTo("foo"));
			Assert.That(obj.Get<string>("22"), Is.EqualTo("bar"));
			Assert.That(obj.Get<string>("33"), Is.EqualTo("baz"));

			// we can also convert directly to JsonObject
			obj = JsonObject.FromObject(new Dictionary<string, int> { ["foo"] = 11, ["bar"] = 22, ["baz"] = 33 });
			Assert.That(obj, Is.Not.Null);
			Log(obj);
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.Get<int>("foo"), Is.EqualTo(11));
			Assert.That(obj.Get<int>("bar"), Is.EqualTo(22));
			Assert.That(obj.Get<int>("baz"), Is.EqualTo(33));
		}

		[Test]
		public void Test_JsonValue_FromObject_STuples()
		{
			// STuple<...>
			Assert.That(JsonValue.FromValue(STuple.Empty).ToJsonText(), Is.EqualTo("[ ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123)).ToJsonText(), Is.EqualTo("[ 123 ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123, "Hello")).ToJsonText(), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123, "Hello", true)).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123, "Hello", true, -1.5)).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123, "Hello", true, -1.5, 'Z')).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));

			// (ITuple) STuple<...>
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Empty).ToJsonText(), Is.EqualTo("[ ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123)).ToJsonText(), Is.EqualTo("[ 123 ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123, "Hello")).ToJsonText(), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123, "Hello", true)).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123, "Hello", true, -1.5)).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z')).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));

			// custom tuple types
			Assert.That(JsonValue.FromValue(new ListTuple<int>([1, 2, 3])).ToJsonText(), Is.EqualTo("[ 1, 2, 3 ]"));
			Assert.That(JsonValue.FromValue(new ListTuple<string>(["foo", "bar", "baz"])).ToJsonText(), Is.EqualTo("[ \"foo\", \"bar\", \"baz\" ]"));
			Assert.That(JsonValue.FromValue(new ListTuple<object>(["hello world", 123, false])).ToJsonText(), Is.EqualTo("[ \"hello world\", 123, false ]"));
			Assert.That(JsonValue.FromValue(new LinkedTuple<int>(STuple.Create(1, 2), 3)).ToJsonText(), Is.EqualTo("[ 1, 2, 3 ]"));
			Assert.That(JsonValue.FromValue(new JoinedTuple<STuple<int, int>, STuple<int>>(STuple.Create(1, 2), STuple.Create(3))).ToJsonText(), Is.EqualTo("[ 1, 2, 3 ]"));
		}

		[Test]
		public void Test_JsonValue_FromObject_ValueTuples()
		{
			// STuple<...>
			Assert.That(JsonValue.FromValue(ValueTuple.Create()).ToJsonText(), Is.EqualTo("[ ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123)).ToJsonText(), Is.EqualTo("[ 123 ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123, "Hello")).ToJsonText(), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123, "Hello", true)).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123, "Hello", true, -1.5)).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123, "Hello", true, -1.5, 'Z')).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));

			// (ITuple) STuple<...>
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create()).ToJsonText(), Is.EqualTo("[ ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123)).ToJsonText(), Is.EqualTo("[ 123 ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello")).ToJsonText(), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true)).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true, -1.5)).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z')).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))).ToJsonText(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));
		}

		[Test]
		public void Test_JsonValue_FromObject_AnonymousType()
		{
			var value = new { foo = 123, bar = false, hello = "world" };
			var result = JsonValue.FromValue(value);
			Assert.That(result, Is.InstanceOf<JsonObject>());

			var obj = result.AsObject();
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.Get<int>("foo"), Is.EqualTo(123));
			Assert.That(obj.Get<bool>("bar"), Is.False);
			Assert.That(obj.Get<string>("hello"), Is.EqualTo("world"));
		}

		[Test]
		public void Test_JsonValue_FromObject_CustomClass()
		{
			var agent = new DummyJsonClass()
			{
				Name = "James Bond",
				Index = 7,
				Size = 123456789,
				Height = 1.8f,
				Amount = 0.07d,
				Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc),
				Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc),
				DateOfBirth = new DateOnly(1920, 11, 11),
				State = DummyJsonEnum.Bar,
			};

			var v = JsonValue.FromValue(agent);
			Assert.That(v, Is.Not.Null.And.Property("Type").EqualTo(JsonType.Object));

			Log(v.ToJsonTextIndented());

			var j = (JsonObject)v;
			Assert.That(j["Name"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.String));
			Assert.That(j["Index"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.Number));
			Assert.That(j["Size"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.Number));
			Assert.That(j["Height"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.Number));
			Assert.That(j["Amount"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.Number));
			Assert.That(j["Created"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.DateTime));
			Assert.That(j["Modified"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.DateTime));
			Assert.That(j["DateOfBirth"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.DateTime));
			Assert.That(j["State"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.String));
			//TODO: ignore defaults?
			//Assert.That(j, Has.Count.EqualTo(8));

			Assert.That(j.Get<string>("Name"), Is.EqualTo(agent.Name), ".Name");
			Assert.That(j.Get<int>("Index"), Is.EqualTo(agent.Index), ".Index");
			Assert.That(j.Get<long>("Size"), Is.EqualTo(agent.Size), ".Size");
			Assert.That(j.Get<float>("Height"), Is.EqualTo(agent.Height), ".Height");
			Assert.That(j.Get<double>("Amount"), Is.EqualTo(agent.Amount), ".Amount");
			Assert.That(j.Get<DateTime>("Created"), Is.EqualTo(agent.Created), ".Created");
			Assert.That(j.Get<DateTime>("Modified"), Is.EqualTo(agent.Modified), ".Modified");
			Assert.That(j.Get<DateOnly>("DateOfBirth"), Is.EqualTo(agent.DateOfBirth), ".DateOfBirth");
			Assert.That(j.Get<DummyJsonEnum>("State"), Is.EqualTo(agent.State), ".State");
		}

		[Test]
		public void Test_JsonValue_FromValue_DerivedClassMember()
		{
			var x = new DummyOuterDerivedClass()
			{
				Id = 7,
				Agent = new DummyDerivedJsonClass("Janov Bondovicz")
				{
					Name = "James Bond",
					Index = 7,
					Size = 123456789,
					Height = 1.8f,
					Amount = 0.07d,
					Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc),
					Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc),
					DateOfBirth = new DateOnly(1920, 11, 11),
					State = DummyJsonEnum.Bar,
				},
			};

			var j = JsonValue.FromValue((object)x);
			Assert.That(j, Is.Not.Null);
			Log(j.ToJsonTextIndented());
			Assert.That(j.Type, Is.EqualTo(JsonType.Object), "FromObject((TClass)obj) should return a JsonObject");
			var obj = (JsonObject)j;
			Assert.That(obj.Get<string?>("$type", null), Is.Null, "Not on top level");
			Assert.That(obj.ContainsKey("Agent"));
			Assert.That(obj.GetPathValue("Agent.$type").ToString(), Is.EqualTo("spy"), "On sub-object");
			var y = obj.Required<DummyOuterDerivedClass>();
			Assert.That(y.Agent, Is.Not.Null.And.InstanceOf<DummyDerivedJsonClass>());
			Assert.That(y.Agent.Name, Is.EqualTo("James Bond"));

			j = JsonValue.FromValue((object) x.Agent);
			Assert.That(j, Is.Not.Null);
			Log(j.ToJsonTextIndented());
			Assert.That(j.Type, Is.EqualTo(JsonType.Object), "FromObject((TDerived)obj) should return a JsonObject");
			obj = (JsonObject)j;
			Assert.That(obj.Get<string?>("$type", null), Is.EqualTo("spy"), "FromObject(foo) should output the $type if one is required");
			var z = obj.Required<DummyDerivedJsonClass>();
			Assert.That(z, Is.Not.Null.And.InstanceOf<DummyDerivedJsonClass>());
			Assert.That(z.Name, Is.EqualTo("James Bond"));

			j = JsonValue.FromValue<DummyJsonBaseClass>(x.Agent);
			Assert.That(j, Is.Not.Null);
			Log(j.ToJsonTextIndented());
			Assert.That(j.Type, Is.EqualTo(JsonType.Object), "FromValue<TClass>() should return a JsonObject");
			obj = (JsonObject)j;
			Assert.That(obj.Get<string?>("$type", null), Is.EqualTo("spy"), "FromValue<TBase>((TDerived)foo) should output the $type");
			var w = obj.Required<DummyJsonBaseClass>();
			Assert.That(w, Is.Not.Null.And.InstanceOf<DummyDerivedJsonClass>());
			Assert.That(w.Name, Is.EqualTo("James Bond"));

		}

		[Test]
		public void Test_JsonValue_FromObject_JsonValue()
		{
			// When calling FromValue<T>(..) on an instance which is already a JsonValue, we should return the same instance

			JsonValue value;

			value = JsonNull.Null;
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));

			value = JsonString.Return("hello world");
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));

			value = JsonNumber.Return(12345);
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));

			value = JsonNumber.Return(12345678L);
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));

			value = JsonNumber.Return(Math.PI);
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));

			value = JsonNumber.Return(float.NaN);
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));
		}

		#endregion

		[Test]
		public void Test_JsonValue_Equals()
		{
			// To simplify unit tests, we want to be able to write "Assert.That(<JsonValue>, Is.EqualTo(<any type>)" to bypass the need to call ".Get<any_type>(...)"
			// Unfortunately, there are a few cases that do not work as inteded, like Is.True or Is.Null/Is.Not.Null

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

			Assert.That(obj["str"], Is.EqualTo("world"));
			Assert.That(obj["int"], Is.EqualTo(42));
			Assert.That(obj["true"], Is.EqualTo(true)); // note: Is.True cannot work because it does true.Equals(actual) instead of actual.Equals(true) :(
			Assert.That(obj["zero"], Is.Zero); // but Is.Zero is fine because it's an alias for EqualTo(0)
			Assert.That(obj["id"], Is.EqualTo(id));
#if DISABLED // this is currently broken in NUnit 4.3.2, see https://github.com/nunit/nunit/issues/4954
			Assert.That(obj["date"], Is.EqualTo(now));
#endif
			Assert.That(obj["null"], Is.EqualTo(JsonNull.Null));

			var top = new JsonObject
			{
				["foo"] = obj.Copy(),
				["bar"] = JsonArray.Create(obj.Copy()),
				["null"] = null, // explicit null
			};

			Assert.That(top["foo"], Is.Not.Null);
			Assert.That(top["foo"]["str"], Is.EqualTo("world"));
			Assert.That(top["foo"]["str"], Is.EqualTo("world"));
			Assert.That(top["foo"]["int"], Is.EqualTo(42));
			Assert.That(top["foo"]["true"], Is.EqualTo(true));
			Assert.That(top["foo"]["false"], Is.EqualTo(false));
			Assert.That(top["foo"]["zero"], Is.Zero);
			Assert.That(top["foo"]["id"], Is.EqualTo(id));
#if DISABLED // this is currently broken in NUnit 4.3.2, see https://github.com/nunit/nunit/issues/4954
			Assert.That(top["foo"]["date"], Is.EqualTo(now));
#endif

			Assert.That(top["bar"], Is.Not.Null);
			Assert.That(top["bar"][0], Is.Not.Null);
			Assert.That(top["bar"][0]["str"], Is.EqualTo("world"));
			Assert.That(top["bar"][0]["str"], Is.EqualTo("world"));
			Assert.That(top["bar"][0]["int"], Is.EqualTo(42));
			Assert.That(top["bar"][0]["true"], Is.EqualTo(true));
			Assert.That(top["bar"][0]["false"], Is.EqualTo(false));
			Assert.That(top["bar"][0]["zero"], Is.Zero);
			Assert.That(top["bar"][0]["id"], Is.EqualTo(id));
#if DISABLED // this is currently broken in NUnit 4.3.2, see https://github.com/nunit/nunit/issues/4954
			Assert.That(top["bar"][0]["date"], Is.EqualTo(now));
#endif

			// ISSUES: the following statement unfortunately will not work as intended:

			// - Is.True is implemented as true.Equals((object) actual) so it will never be able to work, must use Is.Equal(true) instead :(
			// Assert.That(obj["true"], Is.True); // FAIL!
			// Assert.That(obj["false"], Is.False); // FAIL!

			// - Is.Null is implemented as (object) actual == null, so it cannot work either (since we return JsonNull.Missing which is not a null reference
			// Assert.That(top["not_found"], Is.Null); // FAIL!
			// Assert.That(top["foo"], Is.Not.Null); // will never fail
			// Assert.That(top["null"], Is.Null); // FAIL!
			// Assert.That(top["null"], Is.Not.Null); // PASS even though we would expect the reverse!
			// - Is.EqualTo(null) fails for a similar reason because it expected null reference, and will not attempt to call obj.Equals(null)
			// Assert.That(obj["null"], Is.EqualTo(null)); //FAIL!
		}

	}

}
