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

namespace SnowBank.Analyzers
{
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.Linq;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using Microsoft.CodeAnalysis.Diagnostics;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>Reports a view into a pooled buffer that leaves the using scope of its owner (SBK1001), and a pooled writer, allocator, or owner local that is never disposed (SBK2001).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class PooledBufferAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Members of the pooled types that return a view into the rented buffer.</summary>
		private static readonly ImmutableHashSet<string> ViewMembers = ImmutableHashSet.Create("Data", "Span", "Memory", "WrittenSlice", "WrittenSpan", "ToSlice", "ToSpan", "GetSlice", "Allocate");

		/// <summary>Methods that return a SliceOwner over a rented buffer (SliceOwner.Wrap never rents).</summary>
		private static readonly ImmutableHashSet<string> OwnerFactories = ImmutableHashSet.Create("Create", "Copy", "FromBytes", "ToSliceOwner");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(SbkDescriptors.PooledBufferEscape, SbkDescriptors.PooledBufferNeverReturned);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var symbols = Symbols.TryCreate(start.Compilation);
				if (symbols is null) return;

				// SBK2001
				start.RegisterOperationAction(ctx => AnalyzeDeclarator(ctx, (IVariableDeclaratorOperation) ctx.Operation, symbols), OperationKind.VariableDeclarator);

				// SBK1001: the escape sites
				start.RegisterOperationAction(ctx => AnalyzeEscape(ctx, ((IReturnOperation) ctx.Operation).ReturnedValue, symbols), OperationKind.Return, OperationKind.YieldReturn);
				start.RegisterOperationAction(ctx =>
				{
					var op = (ISimpleAssignmentOperation) ctx.Operation;
					if (op.Target is IFieldReferenceOperation or IPropertyReferenceOperation) AnalyzeEscape(ctx, op.Value, symbols);
				}, OperationKind.SimpleAssignment);
				start.RegisterOperationAction(ctx =>
				{
					var op = (IInvocationOperation) ctx.Operation;
					if (op.TargetMethod.Name == "Add")
					{
						foreach (var argument in op.Arguments) AnalyzeEscape(ctx, argument.Value, symbols);
					}
					AnalyzeCapture(ctx, op, symbols);
				}, OperationKind.Invocation);
				start.RegisterOperationAction(ctx => AnalyzeCapture(ctx, ctx.Operation, symbols), OperationKind.PropertyReference);
			});
		}

		#region SBK1001...

		private static void AnalyzeEscape(OperationAnalysisContext ctx, IOperation? value, Symbols symbols)
		{
			if (value is null || !TryFindView(value, symbols, out var access, out var owner)) return;
			// a view read inside a lambda that does not declare the owner is reported once, by the capture check
			if (IsCaptured(access, owner)) return;
			ctx.ReportDiagnostic(Diagnostic.Create(SbkDescriptors.PooledBufferEscape, value.Syntax.GetLocation(), value.Syntax.ToString()));
		}

		private static void AnalyzeCapture(OperationAnalysisContext ctx, IOperation op, Symbols symbols)
		{
			if (GetViewOwner(op, symbols) is not { } owner || !IsCaptured(op, owner)) return;
			ctx.ReportDiagnostic(Diagnostic.Create(SbkDescriptors.PooledBufferEscape, op.Syntax.GetLocation(), op.Syntax.ToString()));
		}

		/// <summary>Follows the receivers of the expression, through one local, down to a view member of a pooled local declared with using.</summary>
		private static bool TryFindView(IOperation value, Symbols symbols, out IOperation access, out ILocalSymbol owner)
		{
			access = value;
			owner = null!;
			var current = Unwrap(value);
			bool hopped = false;
			for (int depth = 0; depth < 16; depth++)
			{
				if (current is ILocalReferenceOperation local)
				{
					// the owner itself, or a second local: outside the single-local ceiling
					if (hopped || IsPooledUsingLocal(local.Local, symbols, current.SemanticModel)) return false;
					hopped = true;
					if (GetInitializer(local.Local, current.SemanticModel) is not { } initializer || IsAssignedAgain(current, local.Local)) return false;
					current = Unwrap(initializer);
					continue;
				}
				if (!symbols.IsViewType(current.Type)) return false;
				if (GetViewOwner(current, symbols) is { } found)
				{
					access = current;
					owner = found;
					return true;
				}
				var receiver = GetReceiver(current);
				if (receiver is null) return false;
				current = Unwrap(receiver);
			}
			return false;
		}

		/// <summary>The pooled local declared with using whose view member the expression reads directly, or null.</summary>
		private static ILocalSymbol? GetViewOwner(IOperation op, Symbols symbols)
		{
			string? name = op switch
			{
				IPropertyReferenceOperation property => property.Property.Name,
				IInvocationOperation invocation => invocation.TargetMethod.Name,
				_ => null,
			};
			if (name is null || !ViewMembers.Contains(name)) return null;
			if (GetReceiver(op) is not { } receiver || Unwrap(receiver) is not ILocalReferenceOperation { Local: { } local }) return null;
			return IsPooledUsingLocal(local, symbols, op.SemanticModel) ? local : null;
		}

		/// <summary>The operation sits in a lambda that does not declare the local.</summary>
		private static bool IsCaptured(IOperation op, ILocalSymbol local)
		{
			for (var current = op.Parent; current is not null; current = current.Parent)
			{
				if (current is IAnonymousFunctionOperation lambda)
				{
					return !local.DeclaringSyntaxReferences.Any(r => lambda.Syntax.Span.Contains(r.Span));
				}
			}
			return false;
		}

		/// <summary>The local is declared with using and holds a pooled owner, writer, or allocator.</summary>
		private static bool IsPooledUsingLocal(ILocalSymbol local, Symbols symbols, SemanticModel? model)
		{
			if (GetDeclarator(local) is not { } declarator || !IsUsingDeclaration(declarator)) return false;
			var type = local.Type;
			if (symbols.IsPooledType(type)) return true;
			if (!SymbolEqualityComparer.Default.Equals(type, symbols.SliceWriter)) return false;
			// a SliceWriter rents only when created with a pool
			return GetInitializer(local, model) is { } initializer && IsPooledCreation(initializer, symbols);
		}

		#endregion

		#region SBK2001...

		private static void AnalyzeDeclarator(OperationAnalysisContext ctx, IVariableDeclaratorOperation op, Symbols symbols)
		{
			if (op.Initializer?.Value is not { } initializer || !IsPooledCreation(initializer, symbols)) return;
			if (op.Syntax is not VariableDeclaratorSyntax declarator || IsUsingDeclaration(declarator)) return;

			var local = op.Symbol;
			bool disposed = false;
			foreach (var reference in EnumerateReferences(op, local))
			{
				var parent = reference.Parent;
				while (parent is IConversionOperation) parent = parent!.Parent;
				switch (parent)
				{
					case IInvocationOperation invocation when invocation.Instance == reference || (reference.Parent is IConversionOperation && invocation.Instance == reference.Parent):
						if (invocation.TargetMethod.Name is "Dispose" or "Release" or "ToSliceOwner") disposed = true;
						break;
					case IArgumentOperation { Parameter.Ordinal: 0, Parent: IInvocationOperation { TargetMethod.IsExtensionMethod: true } }:
					case IPropertyReferenceOperation:
					case IFieldReferenceOperation:
						break;
					case IUsingOperation:
						disposed = true;
						break;
					default:
						// returned, stored, passed, or copied into another local: another owner may dispose it
						return;
				}
			}
			if (disposed) return;
			ctx.ReportDiagnostic(Diagnostic.Create(SbkDescriptors.PooledBufferNeverReturned, declarator.GetLocation(), local.Name));
		}

		#endregion

		/// <summary>The expression creates a pooled writer or allocator, a SliceWriter with a pool, or a SliceOwner over a rented buffer.</summary>
		private static bool IsPooledCreation(IOperation initializer, Symbols symbols)
		{
			switch (Unwrap(initializer))
			{
				case IObjectCreationOperation creation:
					if (symbols.IsPooledType(creation.Type)) return true;
					if (!SymbolEqualityComparer.Default.Equals(creation.Type, symbols.SliceWriter)) return false;
					var pool = creation.Arguments.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.Parameter?.Type.OriginalDefinition, symbols.ArrayPool));
					if (pool is null) return false;
					var value = Unwrap(pool.Value);
					return value is not IDefaultValueOperation && value.ConstantValue is not { HasValue: true, Value: null };
				case IInvocationOperation invocation:
					return OwnerFactories.Contains(invocation.TargetMethod.Name) && SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ReturnType, symbols.SliceOwner);
				default:
					return false;
			}
		}

		private static bool IsUsingDeclaration(VariableDeclaratorSyntax declarator)
			=> declarator.Parent is VariableDeclarationSyntax { Parent: LocalDeclarationStatementSyntax { UsingKeyword.RawKind: not 0 } or UsingStatementSyntax };

		private static VariableDeclaratorSyntax? GetDeclarator(ILocalSymbol local)
			=> local.DeclaringSyntaxReferences.Length == 1 ? local.DeclaringSyntaxReferences[0].GetSyntax() as VariableDeclaratorSyntax : null;

		private static IOperation? GetInitializer(ILocalSymbol local, SemanticModel? model)
			=> model is not null && GetDeclarator(local) is { Initializer.Value: { } syntax } && syntax.SyntaxTree == model.SyntaxTree ? model.GetOperation(syntax) : null;

		private static bool IsAssignedAgain(IOperation op, ILocalSymbol local)
			=> GetRoot(op).Descendants().Any(o => o is IAssignmentOperation { Target: ILocalReferenceOperation target } && SymbolEqualityComparer.Default.Equals(target.Local, local));

		private static IEnumerable<ILocalReferenceOperation> EnumerateReferences(IOperation op, ILocalSymbol local)
			=> GetRoot(op).Descendants().OfType<ILocalReferenceOperation>().Where(r => SymbolEqualityComparer.Default.Equals(r.Local, local));

		private static IOperation GetRoot(IOperation op)
		{
			while (op.Parent is not null) op = op.Parent;
			return op;
		}

		/// <summary>The instance of a member access, or the first argument of an extension method call.</summary>
		private static IOperation? GetReceiver(IOperation op) => op switch
		{
			IPropertyReferenceOperation property => property.Instance,
			IInvocationOperation { Instance: { } instance } => instance,
			IInvocationOperation { TargetMethod.IsExtensionMethod: true } invocation => invocation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0)?.Value,
			_ => null,
		};

		private static IOperation Unwrap(IOperation op)
		{
			while (op is IConversionOperation { IsImplicit: true } conversion) op = conversion.Operand;
			return op;
		}

		/// <summary>SnowBank.Core buffer types and the view types, resolved once per compilation.</summary>
		private sealed class Symbols
		{

			public INamedTypeSymbol SliceOwner { get; }

			public INamedTypeSymbol SliceWriter { get; }

			public INamedTypeSymbol ArrayPool { get; }

			private readonly ImmutableArray<INamedTypeSymbol> m_pooledTypes;

			private readonly ImmutableArray<INamedTypeSymbol> m_viewTypes;

			private Symbols(INamedTypeSymbol sliceOwner, INamedTypeSymbol sliceWriter, INamedTypeSymbol arrayPool, ImmutableArray<INamedTypeSymbol> pooledTypes, ImmutableArray<INamedTypeSymbol> viewTypes)
			{
				this.SliceOwner = sliceOwner;
				this.SliceWriter = sliceWriter;
				this.ArrayPool = arrayPool;
				m_pooledTypes = pooledTypes;
				m_viewTypes = viewTypes;
			}

			public static Symbols? TryCreate(Compilation compilation)
			{
				var sliceOwner = compilation.GetTypeByMetadataName("System.SliceOwner");
				var sliceWriter = compilation.GetTypeByMetadataName("SnowBank.Buffers.SliceWriter");
				var arrayPool = compilation.GetTypeByMetadataName("System.Buffers.ArrayPool`1");
				var slice = compilation.GetTypeByMetadataName("System.Slice");
				if (sliceOwner is null || sliceWriter is null || arrayPool is null || slice is null) return null;
				var pooled = Resolve(compilation, "System.SliceOwner", "SnowBank.Buffers.PooledSliceWriter", "SnowBank.Buffers.PooledSliceAllocator");
				var views = Resolve(compilation, "System.Slice", "System.Span`1", "System.ReadOnlySpan`1", "System.Memory`1", "System.ReadOnlyMemory`1", "System.ArraySegment`1");
				return new(sliceOwner, sliceWriter, arrayPool, pooled, views);
			}

			private static ImmutableArray<INamedTypeSymbol> Resolve(Compilation compilation, params string[] names)
				=> names.Select(compilation.GetTypeByMetadataName).Where(t => t is not null).ToImmutableArray()!;

			/// <summary>SliceOwner, PooledSliceWriter, or PooledSliceAllocator.</summary>
			public bool IsPooledType(ITypeSymbol? type)
				=> type is not null && m_pooledTypes.Any(t => SymbolEqualityComparer.Default.Equals(t, type));

			/// <summary>Slice, a span, a memory, or an array segment.</summary>
			public bool IsViewType(ITypeSymbol? type)
				=> type is not null && m_viewTypes.Any(t => SymbolEqualityComparer.Default.Equals(t, type.OriginalDefinition));

		}

	}
}
