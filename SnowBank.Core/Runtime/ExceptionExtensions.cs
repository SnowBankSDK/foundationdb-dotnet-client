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

namespace SnowBank.Runtime
{

	/// <summary>Helper methods for working with exceptions</summary>
	public static class ExceptionExtensions
	{

		/// <summary>Tests if this is considered to be a "fatal" error that cannot be handled by a local catch block</summary>
		/// <returns><c>true</c> if <paramref name="self"/> is of type <see cref="ThreadAbortException"/>, <see cref="OutOfMemoryException "/>, <see cref="StackOverflowException"/>, or an <see cref="AggregateException"/> that contains either one of these types</returns>
		[Pure]
		public static bool IsFatalError([NotNullWhen(true)] this Exception? self)
		{
			return self is ThreadAbortException || self is OutOfMemoryException || self is StackOverflowException || (self is AggregateException && IsFatalError(self.InnerException));
		}

		/// <summary>Tests if this type of exception is likely to be caused by a bug, and should be handled with more scrutiny (addintional logging, intentional debugger break, ...)</summary>
		/// <returns><c>true</c> if <paramref name="self"/> is of type <see cref="NullReferenceException"/>, <see cref="ArgumentNullException"/>, <see cref="ArgumentOutOfRangeException"/>, <see cref="IndexOutOfRangeException"/> or any suspicious type.</returns>
		/// <remarks>Errors that are considered "critical" (ex: <see cref="OutOfMemoryException"/>, <see cref="StackOverflowException"/>, ...) are matched by <see cref="IsFatalError"/></remarks>
		public static bool IsLikelyBug([NotNullWhen(true)] this Exception? self)
		{
			return self is NullReferenceException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException || (self is AggregateException && IsLikelyBug(self.InnerException));
		}

		/// <summary>Returns the first non-aggregate exception in the tree of inner-exceptions of an <see cref="AggregateException"/></summary>
		/// <param name="self">AggregateException to unfold</param>
		/// <returns>First exception found by walking the tree of <see cref="AggregateException.InnerExceptions"/> that is not itself an <see cref="AggregateException"/></returns>
		public static Exception GetFirstConcreteException(this AggregateException self)
		{
			// in the vast majority of the cases, we will have a single concrete exception inside the AggEx
			var e = self.GetBaseException();
			if (e is not AggregateException) return e;

			// If not, this could be a tree with multiple branches that we will have to walk through...
			var list = new Queue<AggregateException>();
			list.Enqueue(self);
			while (list.Count > 0)
			{
				foreach (var e2 in list.Dequeue().InnerExceptions)
				{
					if (e2 is null) continue;
					if (e2 is not AggregateException x) return e2; // first concrete exception!
					list.Enqueue(x);
				}
			}
			// uhoh ?
			return self;
		}

		/// <summary>Rethrows the first non-aggregate exception in the tree of inner-exceptions of an <see cref="AggregateException"/></summary>
		/// <param name="self">AggregateException to unfold</param>
		[ContractAnnotation("self:null => null")]
		[return: NotNullIfNotNull("self")]
		public static Exception? Unwrap(this AggregateException? self)
		{
			return self != null ? GetFirstConcreteException(self) : null;
		}

	}

}
