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

namespace SnowBank.Testing.Framework
{
	using System.Reflection;
	using System.Runtime.ExceptionServices;

	/// <summary>Class that is able to invoke any <see cref="System.Delegate"/> using an <see cref="IServiceProvider"/> to instantiate the parameters</summary>
	/// <remarks>When the delegate is invoked, it will fetch all arguments from the services provider, and pass them to the original delegate</remarks>
	/// <example>
	/// <code lang="c#">
	/// // in setup code...
	/// var invoker = MagicDelegate.Create((HttpContext context, IFoo foo, IBar bar, IClock clock, ILogger&lt;Baz> logger) => { ... });
	/// // at runtime
	/// var result = invoker.Invoke(services);
	/// </code>
	/// </example>
	public sealed record MagicDelegate
	{

		/// <summary>Original delegate</summary>
		public required Delegate Handler { get; init; }

		/// <summary>Descriptions of the parameters that the original delegate accepts</summary>
		public required (Type Type, bool Required, object? Default)[] Parameters { get; init; }

		/// <summary>Type of values returned by the original delegate</summary>
		public required Type ReturnType { get; init; }

		/// <summary>If true, the delegate return a <see cref="System.Threading.Tasks.Task"/> that should be awaiter</summary>
		public bool IsAsync => this.Awaiter != null;

		internal Func<object?, Task<object?>>? Awaiter { get; init; }

		/// <summary>Hook, invoked before computing the arguments</summary>
		/// <remarks>The hook returns an opaque state that will be passed to <see cref="OnAfter"/></remarks>
		public Func<IServiceProvider, object?>? OnBefore { get; init; }

		/// <summary>Hook invoked after the invokation has been completed</summary>
		/// <remarks>If passed the same instance that was returned by <see cref="OnBefore"/></remarks>
		public Action<object?>? OnAfter { get; init; }

		/// <summary>Hook invoked to review/update the arguments have been instantiated, before passing them to the delegate</summary>
		public Func<object?[], object?[]>? OnInvoking { get; init; }

		/// <summary>Hook invoked to review/update the result returned by the delegate</summary>
		public Func<object?, object?>? OnResult { get; init; }

		/// <summary>Hook invoked in the case where the delegate threw an exception</summary>
		public Func<ExceptionDispatchInfo, (bool Handled, object? Result, Exception? Error)>? OnError { get; init; }

		/// <summary>Invoke the delegate, using the specified <see cref="IServiceProvider"/></summary>
		/// <param name="services">Provider used to instantiate the arguments. You should create a scope if this delegate is to be executed inside a scoped request, or operation</param>
		/// <returns>Value returned by the delegate. The expected type is available in <see cref="ReturnType"/>. Will be <see langword="null"/> for void-returning delegates</returns>
		public object? Invoke(IServiceProvider services)
		{
			Contract.NotNull(services);

			var state = this.OnBefore?.Invoke(services);
			try
			{
				var args = CreateArguments(services, this.Parameters);

				args = this.OnInvoking?.Invoke(args) ?? args;

				object? res;
				try
				{
					res = this.Handler.DynamicInvoke(args);
				}
				catch (Exception e)
				{
					if (this.OnError == null)
					{
						throw;
					}

					var edi = ExceptionDispatchInfo.Capture(e);
					var t = this.OnError(edi);
					if (!t.Handled)
					{
						throw;
					}

					if (t.Error != null)
					{
						throw t.Error;
					}

					res = t.Result;
				}

				if (this.Awaiter != null)
				{ // should have called InvokeAsync() ?
					res = this.Awaiter(res).GetAwaiter().GetResult();
				}

				if (this.OnResult != null)
				{
					res = this.OnResult(res);
				}
				return res;
			}
			finally
			{
				this.OnAfter?.Invoke(state);
			}
		}

		public async Task<object?> InvokeAsync(IServiceProvider services, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			var res = Invoke(services);

			if (this.Awaiter == null)
			{
				ct.ThrowIfCancellationRequested();
				return res;
			}

			var t = this.Awaiter(res);
			return await t;
		}

		internal static object?[] CreateArguments(IServiceProvider services, (Type Type, bool Required, object? Default)[] prms)
		{
			var args = new object?[prms.Length];
			IServiceProviderIsService? checker = null;

			for (int i = 0; i < prms.Length; i++)
			{
				var prm = prms[i];
				if (prm.Required)
				{
					args[i] = services.GetRequiredService(prm.Type);
				}
				else if (prm.Default == null)
				{
					args[i] = services.GetService(prm.Type);
				}
				else 
				{
					checker ??= services.GetRequiredService<IServiceProviderIsService>();
					if (checker.IsService(prm.Type))
					{
						args[i] = services.GetService(prm.Type) ?? prm.Default;
					}
					else
					{
						args[i] = prm.Default;
					}
				}
			}
			return args;
		}

		internal static (Type Type, bool Required, object? Default)[] ComputeArguments(Delegate handler)
		{
			var prms = handler.Method.GetParameters();
			var args = new (Type, bool, object?)[prms.Length];
			for (int i = 0; i < prms.Length; i++)
			{
				var prm = prms[i];

				var isNullable = prm.GetCustomAttributes(typeof(NullableAttribute), inherit: true).Length != 0;
				var type = prm.ParameterType;
				object? defaultValue = prm.HasDefaultValue ? prm.DefaultValue : null;
				bool required = !isNullable && defaultValue == null;

				args[i] = (type, required, defaultValue);
			}
			return args;
		}

		private static async Task<object?> Await<TResult>(object? res)
		{
			if (res == null)
			{
				return null;
			}
			Task<TResult> tr = (Task<TResult>) res;
			return await tr.ConfigureAwait(false);
		}

		private static readonly MethodInfo AwaiterMethod = typeof(MagicDelegate).GetMethod(nameof(Await), BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Missing Awaiter factory method!");

		public static MagicDelegate Create(Delegate handler)
		{
			var args = ComputeArguments(handler);

			Func<object?, Task<object?>>? awaiter = null;
			var returnType = handler.Method.ReturnType;
			if (returnType.IsAssignableTo(typeof(Task)))
			{ // this is a task that will need to be awaited!

				if (returnType.IsGenericType)
				{ // Task<T>
					var genArgs = returnType.GetGenericArguments();
					if (genArgs.Length == 1)
					{
						awaiter = AwaiterMethod.MakeGenericMethod(genArgs).CreateDelegate<Func<object?, Task<object?>>>();
					}
				}
				else
				{
					awaiter = async (res) =>
					{
						if (res == null) return null;
						await ((Task) res);
						return null;
					};
				}
			}

			return new MagicDelegate()
			{
				Handler = handler,
				Parameters = args,
				ReturnType = handler.Method.ReturnType,
				Awaiter = awaiter,
			};
		}

	}

}
