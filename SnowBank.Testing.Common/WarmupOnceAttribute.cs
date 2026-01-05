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

namespace SnowBank.Testing
{
	using NUnit.Framework.Interfaces;
	using NUnit.Framework.Internal;

	public interface ITestWarmupHelper
	{
		static abstract void Execute(TestExecutionContext context);
	}

	/// <summary>
	/// Sets the both current and current UI Culture to Invariant on an assembly, test fixture or test method for the duration of a test.
	/// The culture remains set until the test or fixture completes and is then reset to its original value.
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	public class WarmupOnceAttribute<THelper> : PropertyAttribute, IApplyToContext
		where THelper : ITestWarmupHelper
	{

		// ReSharper disable once StaticMemberInGenericType
		private static TaskCompletionSource? InitTask;

		public WarmupOnceAttribute()
			: base("WarmupOnce", typeof(THelper).FullName!)
		{
		}

		void IApplyToContext.ApplyToContext(TestExecutionContext context)
		{
			// the first one must execute the warmup code,
			// but if we are running test in //, it is possible that multiple threads start executing at the same time
			// => only one must run the helper, the other threads must wait

			bool youAreIt = false;

			var tcs = WarmupOnceAttribute<THelper>.InitTask;
			if (tcs == null)
			{
				lock (this)
				{
					tcs = WarmupOnceAttribute<THelper>.InitTask;

					if (tcs == null)
					{
						tcs = new TaskCompletionSource();
						WarmupOnceAttribute<THelper>.InitTask = tcs;
						youAreIt = true;
					}
				}
			}

			if (!youAreIt)
			{
				if (!tcs.Task.IsCompletedSuccessfully)
				{
					tcs.Task.GetAwaiter().GetResult();
				}

				return;
			}

			try
			{
				THelper.Execute(context);
				tcs.TrySetResult();
			}
			catch (Exception e)
			{
				tcs.TrySetException(e);
			}
		}

	}
}
