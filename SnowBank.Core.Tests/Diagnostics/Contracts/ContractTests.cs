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

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace SnowBank.Diagnostics.Contracts.Tests
{
	using System.Diagnostics.CodeAnalysis;

	[TestFixture]
	[Category("Core-SDK")]
	[SetInvariantCulture]
	[Parallelizable(ParallelScope.None)]
	public class ContractTests : SimpleTest
	{
		private bool m_status;

		[SetUp]
		public void Before()
		{
			m_status = Contract.IsUnitTesting;
		}

		[TearDown]
		public void After()
		{
			Contract.IsUnitTesting = m_status;
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_NotNull_Throws_ArgumentNullException()
		{
			ArgumentNullException anex;

			#region NotNull(string)

			Assert.DoesNotThrow(() => { Contract.NotNull("foobar", paramName: "value"); });
			Assert.DoesNotThrow(() => { Contract.NotNull("", paramName: "value"); });

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(string), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(string), message: "You shall not pass!", paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("You shall not pass! (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(string), message: "You shall not pass!", paramName: "offset"); });
			Assert.That(anex.Message, Is.EqualTo("You shall not pass! (Parameter 'offset')"));
			Assert.That(anex.ParamName, Is.EqualTo("offset"));

			#endregion

			#region NotNull<T>(T)

			Assert.DoesNotThrow(() => { Contract.NotNull(new Dictionary<int, string>(), paramName: "foo"); });
			Assert.DoesNotThrow(() => { Contract.NotNull(new byte[1], paramName: "foo"); });

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(Stream), paramName: "stream"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: stream != null  Value cannot be null. (Parameter 'stream')"));
			Assert.That(anex.ParamName, Is.EqualTo("stream"));

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(Dictionary<int, string>), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(byte[]), message: "You shall not pass!", paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("You shall not pass! (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(byte[]), message: "You shall not pass!", paramName: "source"); });
			Assert.That(anex.Message, Is.EqualTo("You shall not pass! (Parameter 'source')"));
			Assert.That(anex.ParamName, Is.EqualTo("source"));

			#endregion

		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_NotNullOrEmpty_Throws_Correct_Exception()
		{
			// STRINGS

			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty("foobar", paramName: "x"); });

			// si c'est null => ArgumentNullException
			var anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNullOrEmpty(default(string), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			// si c'est empty => ArgumentException
			var aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrEmpty(String.Empty, paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.Length > 0  String cannot be empty. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));

			// ARRAYS

			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty(new [] { 1, 2, 3 }, paramName: "x"); });

			// si c'est null => ArgumentNullException
			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNullOrEmpty(default(int[]), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			// si c'est empty => ArgumentException
			aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrEmpty(new int[0], paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.Count > 0  Collection cannot be empty. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));

			// Collections

			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty(new List<int> { 1, 2, 3 }, paramName: "x"); });

			// si c'est null => ArgumentNullException
			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNullOrEmpty(default(List<int>), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			// si c'est empty => ArgumentException
			aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrEmpty(new List<int>(), paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.Count > 0  Collection cannot be empty. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));

		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_NotNullOrWhiteSpace_Throws_Correct_Exception()
		{
			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty("foobar", paramName: "x"); });
			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty("  foobar", paramName: "x"); });
			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty("\0", paramName: "x"); }); //note: '\0' n'est PAS considéré comme du whitespace!

			// si c'est null => ArgumentNullException
			var anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNullOrWhiteSpace(default(string), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			// si c'est empty => ArgumentException
			var aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrWhiteSpace(String.Empty, paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.Length > 0  String cannot be empty. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));

			// si c'est des espace => ArgumentException
			aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrWhiteSpace("  ", paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.All(c => !char.IsWhiteSpace(c))  String cannot contain only whitespaces. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));

			// si c'est n'importe quel type de "whitespace" => ArgumentException
			aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrWhiteSpace("\t \r\n  \n", paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.All(c => !char.IsWhiteSpace(c))  String cannot contain only whitespaces. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_Positive_Throws_ArgumentException()
		{

			Assert.DoesNotThrow(() => { Contract.Positive(0, paramName: "x"); });
			Assert.DoesNotThrow(() => { Contract.Positive(123, paramName: "x"); });
			Assert.DoesNotThrow(() => { Contract.Positive(0L, paramName: "x"); });
			Assert.DoesNotThrow(() => { Contract.Positive(123L, paramName: "x"); });

			var aex = Assert.Throws<ArgumentException>(() => { Contract.Positive(-1, message: "le message", paramName: "x"); });
			Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'x')"));
			Assert.That(aex.ParamName, Is.EqualTo("x"));

			aex = Assert.Throws<ArgumentException>(() => { Contract.Positive(-1L, message: "le message", paramName: "x"); });
			Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'x')"));
			Assert.That(aex.ParamName, Is.EqualTo("x"));
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_GreaterThan_Throws_ArgumentException()
		{
			Assert.Multiple(() =>
			{
				Assert.DoesNotThrow(() => { Contract.GreaterThan(1, 0, "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterThan(66, 42, "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterThan(1L, 0, "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterThan(1L + int.MaxValue, int.MaxValue, "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterThan(int.MaxValue, int.MaxValue - 1, "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterThan(0, int.MinValue, "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterThan(long.MaxValue, long.MaxValue - 1, "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterThan(0, long.MinValue, "foo"); });

				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { int foo = 0; Contract.GreaterThan(foo, 1); });
					Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo > 1  The specified value is too small. (Parameter 'foo')\r\nActual value was 0."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { long foo = 0; Contract.GreaterThan(foo, 0L); });
					Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo > 0L  Non-Zero Positive number required. (Parameter 'foo')\r\nActual value was 0."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { int foo = 0; Contract.GreaterThan(foo, 1, "le message"); });
					Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')\r\nActual value was 0."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { long foo = 0; Contract.GreaterThan(foo, 0L, "le message"); });
					Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')\r\nActual value was 0."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
				}
			});
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_GreaterOrEqual_Throws_ArgumentException()
		{
			Assert.Multiple(() =>
			{
				Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(1, 0, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(66, 42, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(1L, 0, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(1L + int.MaxValue, int.MaxValue, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(int.MaxValue, int.MaxValue - 1, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(0, int.MinValue, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(long.MaxValue, long.MaxValue - 1, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(0, long.MinValue, valueExpression: "foo"); });

				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { int foo = 0; Contract.GreaterOrEqual(foo, 1); });
					Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo >= 1  The specified value is too small. (Parameter 'foo')\r\nActual value was 0."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
					Assert.That(aex.ActualValue, Is.EqualTo(0));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { long foo = -1L; Contract.GreaterOrEqual(foo, 0L); });
					Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo >= 0L  Positive number required. (Parameter 'foo')\r\nActual value was -1."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
					Assert.That(aex.ActualValue, Is.EqualTo(-1L));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { int foo = 0; Contract.GreaterOrEqual(foo, 1, message: "le message"); });
					Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')\r\nActual value was 0."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
					Assert.That(aex.ActualValue, Is.EqualTo(0));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { long foo = -1L; Contract.GreaterOrEqual(foo, 0L, message: "le message"); });
					Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')\r\nActual value was -1."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
					Assert.That(aex.ActualValue, Is.EqualTo(-1L));
				}
			});
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_LessThan_Throws_ArgumentException()
		{
			Assert.Multiple(() =>
			{
				Assert.DoesNotThrow(() => { Contract.LessThan(0, 1, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessThan(42, 66, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessThan(-1, 0, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessThan(-1L, 0, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessThan(int.MaxValue - 1, int.MaxValue, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessThan(long.MaxValue - 1, long.MaxValue, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessThan(int.MinValue, 0, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessThan(long.MinValue, 0, valueExpression: "foo"); });

				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => Contract.LessThan(1, 0, valueExpression: "foo"));
					Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo < 0  The specified value is too big. (Parameter 'foo')\r\nActual value was 1."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
					Assert.That(aex.ActualValue, Is.EqualTo(1));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => Contract.LessThan(1L, 0L, valueExpression: "foo"));
					Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo < 0L  The specified value is too big. (Parameter 'foo')\r\nActual value was 1."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
					Assert.That(aex.ActualValue, Is.EqualTo(1));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => Contract.LessThan(2, 1, message: "le message", valueExpression: "foo"));
					Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')\r\nActual value was 2."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
					Assert.That(aex.ActualValue, Is.EqualTo(2));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => Contract.LessThan(2L, 0L, message: "le message", valueExpression: "foo"));
					Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')\r\nActual value was 2."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
					Assert.That(aex.ActualValue, Is.EqualTo(2));
				}
			});
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_LessOrEqual_Throws_ArgumentException()
		{

			Assert.Multiple(() =>
			{
				Assert.DoesNotThrow(() => { Contract.LessOrEqual(1, 2, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessOrEqual(42, 66, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessOrEqual(0L, 1L, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessOrEqual(int.MaxValue, 1L + int.MaxValue, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessOrEqual(int.MaxValue - 1, int.MaxValue, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessOrEqual(int.MinValue, 0, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessOrEqual(long.MaxValue - 1, long.MaxValue, valueExpression: "foo"); });
				Assert.DoesNotThrow(() => { Contract.LessOrEqual(long.MinValue, 0, valueExpression: "foo"); });

				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { Contract.LessOrEqual(2, 1, valueExpression: "foo"); });
					Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo <= 1  The specified value is too big. (Parameter 'foo')\r\nActual value was 2."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { Contract.LessOrEqual(1L, 0L, valueExpression: "foo"); });
					Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo <= 0L  The specified value is too big. (Parameter 'foo')\r\nActual value was 1."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { Contract.LessOrEqual(2, 1, message: "le message", valueExpression: "foo"); });
					Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')\r\nActual value was 2."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
				}
				{
					var aex = Assert.Throws<ArgumentOutOfRangeException>(() => { Contract.LessOrEqual(1L, 0L, message: "le message", valueExpression: "foo"); });
					Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')\r\nActual value was 1."));
					Assert.That(aex.ParamName, Is.EqualTo("foo"));
				}
			});
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_Between_Throws_ArgumentException()
		{
			Assert.DoesNotThrow(() => Contract.Between(0, 0, 2, valueExpression: "foo"));
			Assert.DoesNotThrow(() => Contract.Between(1, 0, 2, valueExpression: "foo"));
			Assert.DoesNotThrow(() => Contract.Between(2, 0, 2, valueExpression: "foo"));
			Assert.DoesNotThrow(() => Contract.Between(0, int.MinValue, int.MaxValue, valueExpression: "foo"));
			Assert.DoesNotThrow(() => Contract.Between(-10, -42, +10, valueExpression: "foo"));

			Assert.That(
				() => Contract.Between(11, 0, 10, valueExpression: "foo"),
				Throws.InstanceOf<ArgumentException>()
					.With.Message.EqualTo("Precondition failed: 0 <= foo <= 10  The specified value was outside the specified range. (Parameter 'foo')")
					.And.Property("ParamName").EqualTo("foo")
			);

			Assert.That(
				() => Contract.Between(-1, 0, 10, valueExpression: "foo"),
				Throws.InstanceOf<ArgumentException>()
					.With.Message.EqualTo("Precondition failed: 0 <= foo <= 10  The specified value was outside the specified range. (Parameter 'foo')")
					.And.Property("ParamName").EqualTo("foo")
			);
		}

		[Test]
		public void Test_Contract_Requires_And_Asserts_Map_To_NUnit_Assertion_When_Under_Test()
		{
			// il y a de la magie dans le constructeur de Contract, qui détecte si on est dans un test unitaire,
			// et qui se met a envoeyr des AssertionException, au lieu de ContractException, pour simplifier les tests.

			// Cette fonction peut etre activée ou désactiver via Contract.IsUnderTesting :

			// si on l'active, on obtient une AssertionException
			Contract.IsUnitTesting = true;
			Assert.Throws<AssertionException>(() => { Contract.Requires(false); });

			// si par contre on désactive ce mode, on doit obtenir une ContractException !
			Contract.IsUnitTesting = false;
			Assert.Throws<ContractException>(() => { Contract.Requires(false); });
		}

		[Test]
		public void Test_Contract_Requires_Will_Throw_A_ContractException()
		{
			// note: on désactive le testing mode, pour être en situation "réelle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Requires(true); });

			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Requires(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Precondition failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(System.Diagnostics.Contracts.ContractFailureKind.Precondition));
		}

		[Test]
		public void Test_Contract_Assert_Will_Throw_A_ContractException()
		{
			// note: on désactive le testing mode, pour être en situation "réelle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Assert(true); });

			// note: Contract.Assert(...) fait un Debug.Fail(...) pour spammer les logs
			int x = 69;
			Trace.WriteLine("====== vvv IGNORER L'ASSERTION QUI EST JUSTE APRES (C'EST NORMAL! :) vvv ======");
			var cex = Assert.Throws<ContractException>(() => { Contract.Assert(x == 42, "le message"); });
			Trace.WriteLine("====== ^^^ IGNORER L'ASSERTION QUI EST JUSTE AVANT (C'EST NORMAL! :) ^^^ ======");
			Assert.That(cex.Message, Is.EqualTo("Assertion failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(System.Diagnostics.Contracts.ContractFailureKind.Assert));
		}

		[Test]
		public void Test_Contract_Ensures_Will_Throw_A_ContractException()
		{
			// note: on désactive le testing mode, pour être en situation "réelle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Ensures(true); });

			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Ensures(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Postcondition failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(System.Diagnostics.Contracts.ContractFailureKind.Postcondition));
		}

		[Test]
		public void Test_Contract_Invariant_Will_Throw_A_ContractException()
		{
			// note: on désactive le testing mode, pour être en situation "réelle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Invariant(true); });

			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Invariant(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Invariant failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(System.Diagnostics.Contracts.ContractFailureKind.Invariant));
		}

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_Contract_Debug_Requires_And_Asserts_Map_To_NUnit_Assertion_When_Under_Test()
		{
			// il y a de la magie dans le constructeur de Contract, qui détecte si on est dans un test unitaire,
			// et qui se met a envoeyr des AssertionException, au lieu de ContractException, pour simplifier les tests.

			// Cette fonction peut etre activée ou désactiver via Contract.IsUnderTesting :

			// si on l'active, on obtient une AssertionException
			Contract.IsUnitTesting = true;
			Assert.Throws<AssertionException>(() => { Contract.Debug.Requires(false); });

			// si par contre on désactive ce mode, on doit obtenir une ContractException !
			Contract.IsUnitTesting = false;
			Assert.Throws<ContractException>(() => { Contract.Debug.Requires(false); });
		}

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_Contract_Debug_Requires_Will_Throw_A_ContractException()
		{
			// note: on désactive le testing mode, pour être en situation "réelle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Debug.Requires(true); });

			// ReSharper disable once RedundantAssignment
			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Debug.Requires(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Precondition failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(System.Diagnostics.Contracts.ContractFailureKind.Precondition));
		}

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_Contract_Debug_Assert_Will_Throw_A_ContractException()
		{
			// note: on désactive le testing mode, pour être en situation "réelle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Debug.Assert(true); });

			// note: Contract.Assert(...) fait un Debug.Fail(...) pour spammer les logs
			// ReSharper disable once RedundantAssignment
			int x = 69;
			Trace.WriteLine("====== vvv IGNORER L'ASSERTION QUI EST JUSTE APRES (C'EST NORMAL! :) vvv ======");
			var cex = Assert.Throws<ContractException>(() => { Contract.Debug.Assert(x == 42, "le message"); });
			Trace.WriteLine("====== ^^^ IGNORER L'ASSERTION QUI EST JUSTE AVANT (C'EST NORMAL! :) ^^^ ======");
			Assert.That(cex.Message, Is.EqualTo("Assertion failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(System.Diagnostics.Contracts.ContractFailureKind.Assert));
		}

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_Contract_Debug_Ensures_Will_Throw_A_ContractException()
		{
			// note: on désactive le testing mode, pour être en situation "réelle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Debug.Ensures(true); });

			// ReSharper disable once RedundantAssignment
			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Debug.Ensures(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Postcondition failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(System.Diagnostics.Contracts.ContractFailureKind.Postcondition));
		}

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_Contract_Debug_Invariant_Will_Throw_A_ContractException()
		{
			// note: on désactive le testing mode, pour être en situation "réelle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Debug.Invariant(true); });

			// ReSharper disable once RedundantAssignment
			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Debug.Invariant(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Invariant failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(System.Diagnostics.Contracts.ContractFailureKind.Invariant));
		}

	}

}
