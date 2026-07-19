
namespace SnowBank.SourceAnalysis
{
	using Microsoft.CodeAnalysis;

	/// <summary>
	/// Descriptor for diagnostic instances using structural equality comparison.
	/// Provides a work-around for https://github.com/dotnet/roslyn/issues/68291.
	/// </summary>
	public readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
	{
		public readonly DiagnosticDescriptor Descriptor;

		public readonly Location? Location;

		public readonly object?[] MessageArgs;

		public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location, object?[] messageArgs)
		{
			this.Descriptor = descriptor;
			this.MessageArgs = messageArgs;
			this.Location = location;
		}

		public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, object?[]? messageArgs)
		{
			Location? trimmedLocation = location is null ? null : GetTrimmedLocation(location);

			return new DiagnosticInfo(descriptor, trimmedLocation, messageArgs ?? []);

			// Creates a copy of the Location instance that does not capture a reference to Compilation.
			static Location GetTrimmedLocation(Location location)
				=> Location.Create(location.SourceTree?.FilePath ?? "", location.SourceSpan, location.GetLineSpan().Span);
		}

		public Diagnostic CreateDiagnostic()
			=> Diagnostic.Create(this.Descriptor, this.Location, this.MessageArgs);

		public readonly override bool Equals(object? obj) => obj is DiagnosticInfo info && this.Equals(info);

		public readonly bool Equals(DiagnosticInfo other)
		{
			return this.Descriptor.Equals(other.Descriptor) &&
			       this.MessageArgs.SequenceEqual(other.MessageArgs) &&
			       this.Location == other.Location;
		}

		public override int GetHashCode()
		{
			int hashCode = this.Descriptor.GetHashCode();
			foreach (object? messageArg in this.MessageArgs)
			{
				hashCode = CombineHash(hashCode, messageArg?.GetHashCode() ?? 0);
			}

			hashCode = CombineHash(hashCode, this.Location?.GetHashCode() ?? 0);
			return hashCode;
		}
	    
		private static int CombineHash(int h1, int h2)
		{
			// RyuJIT optimizes this to use the ROL instruction
			// Related GitHub pull request: https://github.com/dotnet/coreclr/pull/1830
			uint rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
			return ((int)rol5 + h1) ^ h2;
		}
	    
	}
	
}
