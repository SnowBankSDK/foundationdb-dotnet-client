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

// ReSharper disable IdentifierTypo

namespace SnowBank.Testing
{
	using System.Net;
	using System.Net.Http;
	using System.Net.Sockets;
	using NUnit.Framework.Constraints;

	/// <summary>Box that captures any exception thrown by the current assertion</summary>
	/// <typeparam name="TException">Type of the expected exception</typeparam>
	public sealed class Pokeball<TException> where TException : Exception
	{
		private Exception? CapturedException { get; set; }

		public bool HasException => this.CapturedException != null;

		public TException Exception => (this.CapturedException ?? throw new AssertionException("No exception was captured!")) as TException ?? throw new AssertionException($"Captured exception was of type {this.CapturedException.GetType().GetFriendlyName()} instead of expected type {typeof(TException).GetFriendlyName()}");

		internal void Capture(Exception? e) => this.CapturedException = e;

		public override string ToString()
		{
			if (this.CapturedException == null) return "<none>";
			var sb = new StringBuilder();
			var x = this.CapturedException;
			while (x != null)
			{
				if (sb.Length != 0) sb.Append(" -> ");
				sb.Append('[').Append(x.GetType().GetFriendlyName());
				switch (x)
				{
					case ArgumentException argEx:
						if (argEx.ParamName != null) sb.Append(':').Append(argEx.ParamName);
						break;
					case HttpRequestException httpEx:
						if (httpEx.StatusCode != null) sb.Append(':').Append(httpEx.StatusCode);
						break;
					case WebException webEx:
						sb.Append(':').Append(webEx.Status);
						break;
					case SocketException sockEx:
						sb.Append(':').Append(sockEx.SocketErrorCode);
						break;
				}
				sb.Append("] `").Append(x.Message).Append('`');
				x = x.InnerException;
			}
			return sb.ToString();
		}

	}

	internal class CatchExceptionConstraint<TException> : Constraint
		where TException : Exception
	{

		/// <summary>Box that will store the captured exception</summary>
		public Pokeball<TException> Ball { get; }

		public CatchExceptionConstraint(Pokeball<TException> ball)
		{
			this.Ball = ball;
		}

		public override string Description => $"Catch<{typeof(TException).GetFriendlyName()}>";

		public override ConstraintResult ApplyTo<TActual>(TActual actual)
		{
			if (typeof(TActual) != typeof(Exception))
			{
				throw new InvalidOperationException($"Unexpected type {typeof(TActual).GetFriendlyName()} while expecting {typeof(TException).GetFriendlyName()} exception.");
			}
			this.Ball.Capture((Exception?) (object?) actual);
			// success!
			return new(this, this.Ball.Exception, true);
		}

	}

}
