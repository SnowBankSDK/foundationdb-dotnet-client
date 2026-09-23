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

namespace FoundationDB.Analyzers
{
	using System.Linq;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>FoundationDB.Client types resolved once per compilation.</summary>
	internal sealed class FdbSymbols
	{

		private FdbSymbols(Compilation compilation, INamedTypeSymbol database)
		{
			this.IFdbDatabase = database;
			this.IFdbDatabaseProvider = compilation.GetTypeByMetadataName("FoundationDB.Client.IFdbDatabaseProvider");
			this.IFdbDatabaseScopeProvider = compilation.GetTypeByMetadataName("FoundationDB.Client.IFdbDatabaseScopeProvider");
			this.IFdbReadOnlyTransaction = compilation.GetTypeByMetadataName("FoundationDB.Client.IFdbReadOnlyTransaction");
			this.IFdbTransaction = compilation.GetTypeByMetadataName("FoundationDB.Client.IFdbTransaction");
			this.IFdbReadOnlyRetryable = compilation.GetTypeByMetadataName("FoundationDB.Client.IFdbReadOnlyRetryable");
			this.IFdbRetryable = compilation.GetTypeByMetadataName("FoundationDB.Client.IFdbRetryable");
			this.IKeySubspace = compilation.GetTypeByMetadataName("FoundationDB.Client.IKeySubspace");
			this.KeySubspace = compilation.GetTypeByMetadataName("FoundationDB.Client.KeySubspace");
			this.FdbDirectorySubspace = compilation.GetTypeByMetadataName("FoundationDB.Client.FdbDirectorySubspace");
			this.ISubspaceLocation = compilation.GetTypeByMetadataName("FoundationDB.Client.ISubspaceLocation");
			this.IFdbLayer1 = compilation.GetTypeByMetadataName("FoundationDB.Client.IFdbLayer`1");
			this.IFdbLayer2 = compilation.GetTypeByMetadataName("FoundationDB.Client.IFdbLayer`2");
			this.IFdbKey = compilation.GetTypeByMetadataName("FoundationDB.Client.IFdbKey");
			this.FdbValue = compilation.GetTypeByMetadataName("FoundationDB.Client.FdbValue");
			this.FdbWatch = compilation.GetTypeByMetadataName("FoundationDB.Client.FdbWatch");
			this.FdbTransactionExtensions = compilation.GetTypeByMetadataName("FoundationDB.Client.FdbTransactionExtensions");
			this.FdbKeyExtensions = compilation.GetTypeByMetadataName("FoundationDB.Client.FdbKeyExtensions");
			this.ServiceCollectionExtensions = compilation.GetTypeByMetadataName("FoundationDB.DependencyInjection.FdbDatabaseServiceCollectionExtensions");
			this.AspireComponentExtensions = compilation.GetTypeByMetadataName("Microsoft.Extensions.Hosting.FdbAspireComponentExtensions");
			this.Slice = compilation.GetTypeByMetadataName("System.Slice");
			this.CancellationToken = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
		}

		/// <summary>Resolves the symbols, or returns null when the compilation does not reference FoundationDB.Client.</summary>
		public static FdbSymbols? TryCreate(Compilation compilation)
		{
			var database = compilation.GetTypeByMetadataName("FoundationDB.Client.IFdbDatabase");
			return database is null ? null : new FdbSymbols(compilation, database);
		}

		public INamedTypeSymbol IFdbDatabase { get; }

		public INamedTypeSymbol? IFdbDatabaseProvider { get; }

		public INamedTypeSymbol? IFdbDatabaseScopeProvider { get; }

		public INamedTypeSymbol? IFdbReadOnlyTransaction { get; }

		public INamedTypeSymbol? IFdbTransaction { get; }

		public INamedTypeSymbol? IFdbReadOnlyRetryable { get; }

		public INamedTypeSymbol? IFdbRetryable { get; }

		public INamedTypeSymbol? IKeySubspace { get; }

		public INamedTypeSymbol? KeySubspace { get; }

		public INamedTypeSymbol? FdbDirectorySubspace { get; }

		public INamedTypeSymbol? ISubspaceLocation { get; }

		public INamedTypeSymbol? IFdbLayer1 { get; }

		public INamedTypeSymbol? IFdbLayer2 { get; }

		public INamedTypeSymbol? IFdbKey { get; }

		public INamedTypeSymbol? FdbValue { get; }

		public INamedTypeSymbol? FdbWatch { get; }

		public INamedTypeSymbol? FdbTransactionExtensions { get; }

		public INamedTypeSymbol? FdbKeyExtensions { get; }

		public INamedTypeSymbol? ServiceCollectionExtensions { get; }

		/// <summary>The Aspire client integration, null when the compilation does not reference FoundationDB.Aspire.</summary>
		public INamedTypeSymbol? AspireComponentExtensions { get; }

		public INamedTypeSymbol? Slice { get; }

		public INamedTypeSymbol? CancellationToken { get; }

		/// <summary>The type is <paramref name="target"/>, derives from it, or implements it.</summary>
		public static bool IsOrImplements(ITypeSymbol? type, INamedTypeSymbol? target)
		{
			if (type is null || target is null) return false;
			for (var t = type; t is not null; t = t.BaseType)
			{
				if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, target)) return true;
			}
			return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, target));
		}

		/// <summary>The lambda is the handler of ReadAsync, WriteAsync, or ReadWriteAsync on a database, a transaction retry loop, or a database provider.</summary>
		public bool IsRetryLoopHandler(IAnonymousFunctionOperation lambda) => GetRetryLoopInvocation(lambda) is not null;

		/// <summary>The ReadAsync, WriteAsync, or ReadWriteAsync invocation that the lambda is the handler of, or null when it is not a retry-loop handler.</summary>
		public IInvocationOperation? GetRetryLoopInvocation(IAnonymousFunctionOperation lambda)
		{
			IOperation? current = lambda.Parent;
			while (current is IDelegateCreationOperation or IConversionOperation) current = current.Parent;
			if (current is not IArgumentOperation { Parent: IInvocationOperation invocation }) return null;
			return IsRetryLoopInvocation(invocation) ? invocation : null;
		}

		/// <summary>The invocation is ReadAsync, WriteAsync, or ReadWriteAsync on a database, a transaction retry loop, or a database provider.</summary>
		public bool IsRetryLoopInvocation(IInvocationOperation invocation)
		{
			var method = invocation.TargetMethod;
			if (method.Name is not ("ReadAsync" or "WriteAsync" or "ReadWriteAsync")) return false;
			var receiver = method.IsExtensionMethod ? method.Parameters.FirstOrDefault()?.Type : method.ContainingType;
			if (method.ReducedFrom is { } reduced) receiver = reduced.Parameters.FirstOrDefault()?.Type;
			return IsOrImplements(receiver, this.IFdbReadOnlyRetryable)
				|| IsOrImplements(receiver, this.IFdbRetryable)
				|| IsOrImplements(receiver, this.IFdbDatabaseScopeProvider);
		}

		/// <summary>The first lambda that encloses the operation, or null when it is not inside a lambda.</summary>
		public static IAnonymousFunctionOperation? GetEnclosingLambda(IOperation op)
		{
			for (var current = op.Parent; current is not null; current = current.Parent)
			{
				if (current is IAnonymousFunctionOperation lambda) return lambda;
			}
			return null;
		}

		/// <summary>The receiver of an invocation: the instance, or the first argument of an extension method.</summary>
		public static IOperation? GetReceiver(IInvocationOperation op)
		{
			if (op.Instance is not null) return op.Instance;
			if (!op.TargetMethod.IsExtensionMethod) return null;
			return op.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0)?.Value;
		}

		/// <summary>The operation without its implicit conversions.</summary>
		public static IOperation Unwrap(IOperation op)
		{
			while (op is IConversionOperation { IsImplicit: true } conversion) op = conversion.Operand;
			return op;
		}

		/// <summary>The local, parameter, field, or property that the expression reads, or null for any other expression.</summary>
		public static ISymbol? GetReferencedSymbol(IOperation op) => Unwrap(op) switch
		{
			ILocalReferenceOperation local => local.Local,
			IParameterReferenceOperation parameter => parameter.Parameter,
			IFieldReferenceOperation field => field.Field,
			IPropertyReferenceOperation property => property.Property,
			_ => null,
		};

		/// <summary>The initializer of the local's declaration, or null when the local has none in source.</summary>
		public static IOperation? GetLocalInitializer(ILocalSymbol local, SemanticModel? model)
		{
			if (model is null) return null;
			foreach (var reference in local.DeclaringSyntaxReferences)
			{
				if (model.GetOperation(reference.GetSyntax()) is IVariableDeclaratorOperation { Initializer.Value: { } value }) return value;
			}
			return null;
		}

	}
}
