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

namespace FoundationDB.Client
{
	/// <summary>Class that exposes the content of a FoundationDB .cluster file</summary>
	[DebuggerDisplay("{RawValue,nq}")]
	[PublicAPI]
	public sealed class FdbClusterConnectionString
	{
		/// <summary>The raw value of the file</summary>
		internal string RawValue { get; }

		/// <summary>Cluster Identifier</summary>
		public string Id { get; }

		/// <summary>Logical description of the database</summary>
		public string Description { get; }

		/// <summary>List of coordination servers</summary>
		public FdbEndPoint[] Coordinators { get; }

		private FdbClusterConnectionString(string rawValue, string description, string identifier, FdbEndPoint[] coordinators)
		{
			Contract.Debug.Requires(rawValue != null && description != null && identifier != null && coordinators != null);
			this.RawValue = rawValue;
			this.Description = description;
			this.Id = identifier;
			this.Coordinators = coordinators;
		}

		/// <summary>Cluster file with already parsed components</summary>
		/// <param name="description"></param>
		/// <param name="identifier"></param>
		/// <param name="coordinators"></param>
		public FdbClusterConnectionString(string description, string identifier, IEnumerable<FdbEndPoint> coordinators)
		{
			Contract.NotNull(description);
			Contract.NotNull(identifier);
			Contract.NotNull(coordinators);

			this.Description = description;
			this.Id = identifier;
			this.Coordinators = coordinators.ToArray(); // create a copy of the array

			this.RawValue = string.Format(
				CultureInfo.InvariantCulture,
				"{0}:{1}@{2}",
				this.Description,
				this.Id,
				string.Join(",", this.Coordinators.Select(kvp => string.Format(CultureInfo.InvariantCulture, "{0}:{1}", kvp.Address, kvp.Port)))
			);
		}

		/// <summary>Parse the content of a .cluster file</summary>
		/// <param name="rawValue">First line of a .cluster file</param>
		/// <returns>Parsed cluster file instance</returns>
		public static FdbClusterConnectionString Parse(string rawValue)
		{
			if (string.IsNullOrEmpty(rawValue)) throw new FormatException("Cluster file descriptor cannot be empty.");

			int p = rawValue.IndexOf(':');
			if (p < 0) throw new FormatException("Missing ':' after description field.");
			string description = rawValue[..p].Trim();
			if (description.Length == 0) throw new FormatException("Empty description field.");

			int q  = rawValue.IndexOf('@', p + 1);
			if (q < 0) throw new FormatException("Missing '@' after identifier field.");
			string identifier = rawValue.Substring(p + 1, q - p - 1).Trim();
			if (identifier.Length == 0) throw new FormatException("Empty description field.");

			string[] pairs = rawValue[(q + 1)..].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			// the expected format is "{IP}:{PORT}" or "{IP}:{PORT}:tls"
			var coordinators = pairs
				.Select(pair => FdbEndPoint.TryParse(pair, out var ep) ? ep : throw new FormatException($"Invalid coordinator address: '{pair}'"))
				.ToArray();

			if (coordinators.Length == 0) throw new FormatException("Empty coordination server list.");

			return new FdbClusterConnectionString(rawValue, description, identifier, coordinators);
		}

		/// <summary>Returns the raw text of the cluster file</summary>
		public override string ToString()
		{
			return this.RawValue;
		}

		/// <summary>Computes the hashcode of the cluster file</summary>
		public override int GetHashCode()
		{
			return this.RawValue.GetHashCode();
		}

		/// <summary>Check if this cluster file is equal to another object</summary>
		public override bool Equals(object? obj)
		{
			return obj is FdbClusterConnectionString cf && string.Equals(this.RawValue, cf.RawValue);
		}

	}

}
