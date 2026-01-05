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

#pragma warning disable CA1822 // Mark members as static

namespace SnowBank.Testing
{
	using System.Runtime.CompilerServices;
	using NUnit.Framework.Constraints;

	/// <summary>Set of extension methods that implement custom NUnit Constraints</summary>
	public static class TestConstraintsExtensions
	{

		extension(Throws)
		{

			/// <summary>Creates a constraint specifying the type of exception expected, capturing the exception in the process.</summary>
			/// <typeparam name="TException">Type of the expected exception.</typeparam>
			/// <param name="capture">Receives a "box" that will be filled with the captured exception, once the constraint is evaluated</param>
			/// <remarks>Example:<code lang="c#">
			/// Assert.That(() => sut.SomeMethod(/*...*/), Throws.InstanceOf&lt;ArgumentNullException>(out var captured));
			/// // captured.Exception contains the exception that was thrown by SomeMethod()
			/// Assert.That(captured.Exception.Message, Is.EqualTo(/*...*/));
			/// Assert.That(captured.Exception.InnerException, Is./*...*/);
			/// </code></remarks>
			public static Constraint InstanceOf<TException>(out Pokeball<TException> capture)
				where TException : Exception
			{
				return Throws.InstanceOf<TException>().Catch(out capture);
			}

		}

		extension(InstanceOfTypeConstraint constraint)
		{

			/// <summary>Captures any exception thrown during the assertion, and exposes it so that it can be inspected by the test method</summary>
			/// <param name="capture">Box that will receive the exception, after this assertion completes</param>
			/// <remarks>Example: <code>
			/// Assert.That(() => sut.SomeMethod(), Throws.Exception.Catch(out var captured));
			/// // captured.Exception contains the exception that was thrown by SomeMethod()
			/// Assert.That(captured.Exception.Message, Is.EqualTo(/*...*/));
			/// Assert.That(captured.Exception.InnerException, Is.Not.Null);
			/// </code></remarks>
			/// <see cref="TestConstraintsExtensions.InstanceOf{TException}"/>
			/// <see cref="TestConstraintsExtensions.Catch{TException}(InstanceOfTypeConstraint,out Pokeball{TException})"/>
			public Constraint Catch(out Pokeball<Exception> capture)
			{
				capture = new();
				var x = new CatchExceptionConstraint<Exception>(capture);
				constraint.Builder?.Append(x);
				return x;
			}

			/// <summary>Captures any exception thrown during the assertion, and exposes it so that it can be inspected by the test method</summary>
			/// <typeparam name="TException">Type of the expected exception.</typeparam>
			/// <param name="capture">Box that will receive the exception, after this assertion completes</param>
			/// <remarks>Example: <code>
			/// Assert.That(() => sut.SomeMethod(someArg: null), Throws.InstanceOf&lt;ArgumentNullException>.Catch&lt;ArgumentNullException>(out var captured));
			/// // captured.Exception contains the exception that was thrown by SomeMethod()
			/// Assert.That(captured.Exception.ParamName, Is.EqualTo("someArg"));
			/// </code></remarks>
			/// <see cref="TestConstraintsExtensions.InstanceOf{TException}"/>
			public Constraint Catch<TException>(out Pokeball<TException> capture)
				where TException : Exception
			{
				capture = new();
				var x = new CatchExceptionConstraint<TException>(capture);
				constraint.Builder?.Append(x);
				return x;
			}
		}

		extension(ResolvableConstraintExpression expr)
		{

			/// <summary>Captures any exception thrown during the assertion, and exposes it so that it can be inspected by the test method</summary>
			/// <param name="capture">Box that will receive the exception, after this assertion completes</param>
			public ResolvableConstraintExpression Catch(out Pokeball<Exception> capture)
			{
				capture = new();
				expr.Append(new CatchExceptionConstraint<Exception>(capture));
				return expr;
			}

			/// <summary>Captures any exception thrown during the assertion, and exposes it so that it can be inspected by the test method</summary>
			/// <param name="capture">Box that will receive the exception, after this assertion completes</param>
			public ResolvableConstraintExpression Catch<TException>(out Pokeball<TException> capture)
				where TException : Exception
			{
				capture = new();
				expr.Append(new CatchExceptionConstraint<TException>(capture));
				return expr;
			}
		}

		extension(Constraint self)
		{

			/// <summary>Continues an assertion using the result of a capture <see langword="out"/> argument of a method call, instead of the original actual value</summary>
			/// <remarks>This is equivalent to a logical <c>AND</c> but with both sides testing a different value.</remarks>
			/// <example><code lang="c#">
			/// Assert.That(dict.TryGetValue("hello", out var value), Is.True.WithOutput(value).EqualTo("world"));
			/// Assert.That(dict.TryGetValue("not_found", out var value), Is.False.WithOutput(value).Default);
			/// </code></example>
			public ConstraintExpression WithOutput<T>(T? actual, [CallerArgumentExpression(nameof(actual))] string literal = "")
			{
				var builder = self.Builder;
				if (builder == null)
				{
					builder = new();
					builder.Append(self);
				}
				builder.Append(new WithOutputOperator<T>(actual, literal));
				return new(builder);
			}

		}

	}

}
