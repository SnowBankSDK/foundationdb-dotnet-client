#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Data.Json.Binary.Tests
{
	using NodaTime;

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public class JsonPlayground : SimpleTest
	{

		[Test]
		public void Test_Create_JsonObject_Then_Serialize_To_Bytes()
		{
			var now = this.Clock.GetCurrentInstant();
			var createdAt = now.Minus(Duration.FromDays(1000));
			var stamp = Guid.NewGuid();

			// Create a new JsonObject from scratch
			Log("Create user JSON Object...");

			// note: this is the recommended syntax _before_ dictionary collection expressions are available
			var user = JsonObject.Create(
			[
				("id", 7),
				("email", "bond@mi6.local"),
				("familyName", "Bond"),
				("givenName", "James"),
				("displayName", "BOND, James Bond"),
				("metadata", JsonObject.Create(
				[
					("stamp", stamp),
					("createdAt", createdAt),
					("modifiedAt", createdAt),
					("confirmed", false),
					("locked", false),
				])),
				("roles", JsonArray.Create([ "spy", "double_agent" ])),
			]);
			Dump(user);

			// Verify the content of the document
			Assert.That(user.Get<int>("id"), Is.EqualTo(7));
			Assert.That(user.Get<string>("email"), Is.EqualTo("bond@mi6.local"));
			Assert.That(user["metadata"].Get<bool>("confirmed"), Is.False);
			Assert.That(user["metadata"].Get<Guid>("stamp"), Is.EqualTo(stamp));
			Assert.That(user["metadata"].Get<NodaTime.Instant>("createdAt"), Is.EqualTo(createdAt));
			Assert.That(user["metadata"].Get<NodaTime.Instant>("modifiedAt"), Is.EqualTo(createdAt));

			// Serialize to bytes
			Log("Serialize to bytes...");
			Slice jsonBytes = user.ToJsonSlice();
			DumpHexa(jsonBytes);

			Assert.That(jsonBytes[0], Is.EqualTo('{'));
			Assert.That(jsonBytes[^1], Is.EqualTo('}'));
			Assert.That(jsonBytes.ToString(), Does.Contain("\"bond@mi6.local\""));
			Assert.That(jsonBytes.ToString(), Does.Contain("\"Bond\""));
			Assert.That(jsonBytes.ToString(), Does.Contain("\"James\""));
			Assert.That(jsonBytes.ToString(), Does.Contain("\"BOND, James Bond\""));
			Assert.That(jsonBytes.ToString(), Does.Contain(stamp.ToString()));
			Assert.That(jsonBytes.ToString(), Does.Contain("\"double_agent\""));
		}

		[Test]
		public void Test_Parse_JsonObject_Then_Update_Then_Serialize_To_Bytes()
		{
			// Read from HTTP request body, database, or file...
			Slice originalBytes = Slice.FromBytes("""{ "id": 7, "email": "bond@mi6.local", "familyName": "Bond", "displayName": "BOND, James Bond", "metadata": { "stamp": "83b25d8b-ac8c-487e-8188-903fcb3adffc", "createdAt": "2022-09-18T13:23:26.5942626Z", "modifiedAt": "2022-09-18T13:23:26.5942626Z", "confirmed": false, "locked": false }, "roles": [ "spy", "double_agent", "unconfirmed" ] }"""u8);
			DumpHexa(originalBytes);

			// Parse document
			Log("Parsing document...");
			var user = JsonObject.Parse(originalBytes);
			Dump(user);

			// Verify the content of the document
			Assert.That(user.Get<int>("id"), Is.EqualTo(7));
			Assert.That(user.Get<string>("email"), Is.EqualTo("bond@mi6.local"));
			Assert.That(user["metadata"].Get<bool>("confirmed"), Is.False);
			Assert.That(user["metadata"].Get<Guid>("stamp"), Is.EqualTo(Guid.Parse("83b25d8b-ac8c-487e-8188-903fcb3adffc")));
			Assert.That(user["metadata"].Get<NodaTime.Instant>("modifiedAt"), Is.EqualTo(user["metadata"].Get<NodaTime.Instant>("createdAt")));
			Assert.That(user.Get<string[]>("roles"), Is.EqualTo([ "spy", "double_agent", "unconfirmed"]));

			// Update the object
			Log("Update the user...");
			var stamp = Guid.NewGuid();
			var now = this.Clock.GetCurrentInstant();

			// => confirm the account
			var metadata = user.GetObject("metadata");
			metadata.Set("confirmed", true);
			metadata.Set("modifiedAt", now);
			metadata.Set("stamp", stamp);
			// update the roles
			user.GetArray("roles").Remove("unconfirmed");
			user.GetArray("roles").Add("user");
			Dump(user);

			// Ensure the changes where performed correctly
			Assert.That(user["metadata"].Get<bool>("confirmed"), Is.True);
			Assert.That(user["metadata"].Get<Guid>("stamp"), Is.EqualTo(stamp));
			Assert.That(user["metadata"].Get<NodaTime.Instant>("modifiedAt"), Is.Not.EqualTo(user["metadata"].Get<NodaTime.Instant>("createdAt")));
			Assert.That(user.Get<string[]>("roles"), Is.EqualTo([ "spy", "double_agent", "user"]));

			// Serialize back to bytes
			Log("Serialize to bytes...");
			var jsonBytes = user.ToJsonSlice();
			DumpHexa(jsonBytes);

			Assert.That(jsonBytes[0], Is.EqualTo('{'));
			Assert.That(jsonBytes[^1], Is.EqualTo('}'));
			Assert.That(jsonBytes.ToString(), Does.Contain(stamp.ToString()));
			Assert.That(jsonBytes.ToString(), Does.Contain("\"user\""));

			// => Send to HTTP response body, database, or file
		}

	}
}
