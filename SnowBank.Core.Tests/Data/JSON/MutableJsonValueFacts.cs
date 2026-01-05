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

namespace SnowBank.Data.Json.Tests
{

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public sealed class MutableJsonValueFacts : SimpleTest
	{

		public class FakeMutableContext : IMutableJsonContext
		{

			public List<(string Op, JsonPath Path, JsonValue? Argument)> Changes { get; } = [ ];

			public bool Logged { get; set; }

			/// <inheritdoc />
			public bool HasMutations => this.Changes.Count > 0;

			/// <inheritdoc />
			public int Count => this.Changes.Count;

			/// <inheritdoc />
			public JsonObject NewObject() => JsonObject.Create();

			/// <inheritdoc />
			public JsonArray NewArray() => new JsonArray();

			/// <inheritdoc />
			public MutableJsonValue FromJson(JsonValue value) => new(this, null, default, value);

			/// <inheritdoc />
			public MutableJsonValue FromJson(MutableJsonValue parent, JsonPathSegment segment, JsonValue? value) => new(this, parent, segment, value ?? JsonNull.Null);

			private void RecordMutation((string Op, JsonPath Path, JsonValue? Arg) record)
			{
				this.Changes.Add(record);
				if (this.Logged) Log($"# {record}");
			}

			/// <inheritdoc />
			public void RecordInsertAt(IJsonProxyNode instance, Index offset, JsonValue argument) => RecordMutation(("insert", instance.GetPath(JsonPathSegment.Create(offset)), argument));

			/// <inheritdoc />
			public void RecordUpdate(IJsonProxyNode instance, JsonPathSegment child, JsonValue argument) => RecordMutation(("update", instance.GetPath(child), argument));

			/// <inheritdoc />
			public void RecordPatch(IJsonProxyNode instance, JsonPathSegment child, JsonValue argument) => RecordMutation(("patch", instance.GetPath(child), argument));

			/// <inheritdoc />
			public void RecordDelete(IJsonProxyNode instance, JsonPathSegment child) => RecordMutation(("delete", instance.GetPath(child), null));

			/// <inheritdoc />
			public void RecordTruncate(IJsonProxyNode instance, int length) => RecordMutation(("truncate", instance.GetPath(), length));

			/// <inheritdoc />
			public void Reset() => this.Changes.Clear();

		}

		private static JsonObject GetSampleObject() => JsonObject.ReadOnly.Create(
		[
			("hello", "world"),
			("level", 8001),
			("point", JsonObject.ReadOnly.Create([ ("x", 123), ("y", 456), ("z", 789) ])),
			("foo", JsonObject.ReadOnly.Create("bar", JsonObject.Create("baz", true))),
			("items", JsonArray.ReadOnly.Create(["one", "two", "three", "four"])),
		]);

		[Test]
		public void Test_Mutable_Object_Basics()
		{
			var source = GetSampleObject();
			var obj = MutableJsonValue.Untracked(source);
			Assert.That(obj, Is.Not.Null);
			Assert.That(obj.GetContext(), Is.Null);

			using (Assert.EnterMultipleScope())
			{ // the root should reflect the wrapped json
				Assert.That(obj.ToJsonValue(), Is.SameAs(source));
				Assert.That(obj.Type, Is.EqualTo(JsonType.Object));

				Assert.That(obj.IsRoot(), Is.True);
				Assert.That(obj.IsNullOrMissing(), Is.False);
				Assert.That(obj.Exists(), Is.True);
				Assert.That(obj.IsRoot(), Is.True);
				Assert.That(obj.GetParent(), Is.Null);
				Assert.That(obj.GetPath(), Is.EqualTo(JsonPath.Empty));

				Assert.That(obj.Equals(source), Is.True);
			}

			using (Assert.EnterMultipleScope())
			{ // direct child that contains a string literal
				var hello = obj["hello"];
				Assert.That(hello, Is.Not.Null);
				Assert.That(hello.ToJsonValue(), Is.SameAs(source["hello"]));
				Assert.That(hello.Type, Is.EqualTo(JsonType.String));
				Assert.That(hello.Exists(), Is.True);
				Assert.That(hello.IsNullOrMissing(), Is.False);
				Assert.That(hello.As<string>(), Is.EqualTo("world"));
				Assert.That(hello.IsRoot(), Is.False);
				Assert.That(hello.GetParent(), Is.SameAs(obj));
				Assert.That(hello.GetPath(), Is.EqualTo("hello"));
				Assert.That(hello.Equals(JsonString.Return("world")), Is.True);

				Assert.That(obj.Get("hello").As<string>(), Is.EqualTo("world"));
				Assert.That(obj.Get("!!!hello???".AsMemory(3, 5)).As<string>(), Is.EqualTo("world"));
				Assert.That(obj.Get<string>("hello"), Is.EqualTo("world"));
				Assert.That(obj.Get<string>("!!!hello???".AsMemory(3, 5)), Is.EqualTo("world"));
				Assert.That(obj.GetValue("hello"), IsJson.EqualTo("world"));
				Assert.That(obj.GetValue("!!!hello???".AsMemory(3, 5)), IsJson.EqualTo("world"));
			}

			using (Assert.EnterMultipleScope())
			{ // direct child that contains a number literal
				var level = obj["level"];
				Assert.That(level, Is.Not.Null);
				Assert.That(level.ToJsonValue(), Is.SameAs(source["level"]));
				Assert.That(level.Type, Is.EqualTo(JsonType.Number));
				Assert.That(level.Exists(), Is.True);
				Assert.That(level.IsNullOrMissing(), Is.False);
				Assert.That(level.As<int>(), Is.EqualTo(8001));
				Assert.That(level.IsRoot(), Is.False);
				Assert.That(level.GetParent(), Is.SameAs(obj));
				Assert.That(level.GetPath(), Is.EqualTo("level"));
				Assert.That(level.Equals(JsonNumber.Return(8001)), Is.True);

				Assert.That(obj.Get("level").As<int>(), Is.EqualTo(8001));
				Assert.That(obj.Get("!!!level???".AsMemory(3, 5)).As<int>(), Is.EqualTo(8001));
				Assert.That(obj.Get<int>("level"), Is.EqualTo(8001));
				Assert.That(obj.Get<int>("!!!level???".AsMemory(3, 5)), Is.EqualTo(8001));
				Assert.That(obj.GetValue("level"), IsJson.EqualTo(8001));
				Assert.That(obj.GetValue("!!!level???".AsMemory(3, 5)), IsJson.EqualTo(8001));
			}

			using (Assert.EnterMultipleScope())
			{ // nested object
				var point = obj["point"];
				Assert.That(point, Is.Not.Null);
				Assert.That(point.ToJsonValue(), Is.SameAs(source["point"]));
				Assert.That(point.Type, Is.EqualTo(JsonType.Object));
				Assert.That(point.Exists(), Is.True);
				Assert.That(point.IsNullOrMissing(), Is.False);
				Assert.That(point.IsRoot(), Is.False);
				Assert.That(point.GetParent(), Is.SameAs(obj));
				Assert.That(point.GetPath(), Is.EqualTo("point"));

				Assert.That(point.Count, Is.EqualTo(3));
				Assert.That(point.ContainsKey("x"), Is.True);
				Assert.That(point.ContainsKey("y"), Is.True);
				Assert.That(point.ContainsKey("z"), Is.True);
				Assert.That(point.ContainsKey("w"), Is.False);
			}

			using (Assert.EnterMultipleScope())
			{ // field of nested object
				var point = obj["point"];
				var x = point["x"];

				Assert.That(x, Is.Not.Null);
				Assert.That(x.Exists(), Is.True);
				Assert.That(x.IsNullOrMissing(), Is.False);
				Assert.That(x.Type, Is.EqualTo(JsonType.Number));
				Assert.That(x.IsRoot(), Is.False);
				Assert.That(x.GetParent(), Is.SameAs(point));
				Assert.That(x.GetPath(), Is.EqualTo("point.x"));

				Assert.That(x.As<int>(), Is.EqualTo(123));
				Assert.That(x.Equals(JsonNumber.Return(123)), Is.True);
			}

			using (Assert.EnterMultipleScope())
			{ // nested array
				var items = obj["items"];
				Assert.That(items, Is.Not.Null);
				Assert.That(items.ToJsonValue(), Is.SameAs(source["items"]));
				Assert.That(items.Type, Is.EqualTo(JsonType.Array));
				Assert.That(items.Exists(), Is.True);
				Assert.That(items.IsNullOrMissing(), Is.False);
				Assert.That(items.IsRoot(), Is.False);
				Assert.That(items.GetParent(), Is.SameAs(obj));
				Assert.That(items.GetPath(), Is.EqualTo("items"));

				Assert.That(items.Count, Is.EqualTo(4));
				Assert.That(items.Get<string>(0), Is.EqualTo("one"));
				Assert.That(items.Get<string>(1), Is.EqualTo("two"));
				Assert.That(items.Get<string>(2), Is.EqualTo("three"));
				Assert.That(items.Get<string>(3), Is.EqualTo("four"));
				Assert.That(() => items.Get<string>(4), Throws.InstanceOf<JsonBindingException>());
				Assert.That(items.Get<string?>(4, null), Is.Null);
			}

			using (Assert.EnterMultipleScope())
			{ // item of nested array
				var items = obj["items"];
				var item = items[2];
				Assert.That(item, Is.Not.Null);
				Assert.That(item.Type, Is.EqualTo(JsonType.String));
				Assert.That(item.ToJsonValue(), Is.SameAs(source["items"][2]));
				Assert.That(item.IsRoot(), Is.False);
				Assert.That(item.GetParent(), Is.SameAs(items));
				Assert.That(item.GetPath(), Is.EqualTo("items[2]"));

				Assert.That(item.Exists(), Is.True);
				Assert.That(item.IsNullOrMissing(), Is.False);
				Assert.That(item.Required<string>(), Is.EqualTo("three"));
				Assert.That(item.As<string>(), Is.EqualTo("three"));
				Assert.That(item.As<string>("missing"), Is.EqualTo("three"));
			}

		}

		[Test]
		public void Test_Fill_Empty_Mutable_Object()
		{
			var tr = new FakeMutableContext() { Logged = true };
			var obj = tr.FromJson(tr.NewObject());

			obj["hello"].Set("world");
			obj["level"].Set(8001);
			obj["point"]["x"].Set(123);
			obj["point"].Set("y", 456);
			obj.Set(JsonPath.Create("point.z"), 789);
			obj["foo"]["bar"].Set("baz", true);
			obj["items"][1].Set("two");

			Dump(obj.ToJsonValue());

			Assert.That(
				obj.ToJsonValue(),
				IsJson.Object.And.EqualTo(
					JsonObject.Create(
						[
							("hello", "world"),
							("level", 8001),
							("point", JsonObject.Create(
								[
									("x", 123),
									("y", 456),
									("z", 789)
								]
							)),
							("foo", JsonObject.Create("bar", JsonObject.Create("baz", true))),
							("items", JsonArray.Create(null, "two")),
						]
					)
				)
			);

			foreach (var (op, path, arg) in tr.Changes)
			{
				Log($"- {op}, `{path}`, {arg:Q}");
			}
		}

	}

}
