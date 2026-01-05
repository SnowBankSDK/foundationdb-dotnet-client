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
	/// <summary>Settings used when establishing the connection with a FoundationDB cluster</summary>
	[DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
	public sealed record FdbConnectionOptions
	{

		/// <summary>Full path to a specific 'fdb.cluster' file</summary>
		/// <remarks>
		/// <para>This should be a valid path, accessible with read and write permissions by the process.</para>
		/// <para>This property and <see cref="ConnectionString"/> are mutually exclusive.</para>
		/// </remarks>
		public string? ClusterFile { get; set; }

		/// <summary>Connection string to the cluster</summary>
		/// <remarks>
		/// <para>The format of this string is the same as the content of a <c>.cluster</c> file.</para>
		/// <para>This property and <see cref="ClusterFile"/> are mutually exclusive.</para>
		/// </remarks>
		public string? ConnectionString { get; set; }

		/// <summary>If <see langword="true"/>, opens a read-only view of the database</summary>
		/// <remarks>If set to <see langword="true"/>, only read-only transactions will be allowed on the database instance</remarks>
		public bool ReadOnly { get; set; }

		/// <summary>Default timeout for all transactions, in milliseconds precision (or infinite if 0)</summary>
		public TimeSpan DefaultTimeout { get; set; } // sec

		/// <summary>Default maximum number of retries for all transactions (or infinite if 0)</summary>
		public int DefaultRetryLimit { get; set; }

		/// <summary>Default maximum retry delay for all transactions (or infinite if 0)</summary>
		public int DefaultMaxRetryDelay { get; set; }

		/// <summary>Default Tracing options for all transactions</summary>
		/// <remarks><see cref="FdbTracingOptions.Default"/> by default</remarks>
		public FdbTracingOptions DefaultTracing { get; set; } = FdbTracingOptions.Default;

		/// <summary>Default root location used by the database (empty prefix by default)</summary>
		/// <remarks>If specified, all started transactions will be automatically rooted to this location.</remarks>
		public FdbPath? Root { get; set; }

		/// <summary>If set, specify the datacenter ID that was passed to fdbserver processes running in the same datacenter as this client, for better location-aware load balancing.</summary>
		public string? DataCenterId { get; set; }

		/// <summary>If set, specify the machine ID that was passed to fdbserver processes running on the same machine as this client, for better location-aware load balancing.</summary>
		public string? MachineId { get; set; }

		/// <inheritdoc />
		public override string ToString()
		{
			var sb = new FdbConnectionStringBuilder();
			sb.Add("cluster_file", this.ClusterFile ?? "default");
			if (this.Root != null) sb.Add("root", this.Root.ToString());
			if (this.ReadOnly) sb.Add("readonly");
			if (this.DefaultTimeout > TimeSpan.Zero) sb.Add("timeout", this.DefaultTimeout.TotalSeconds);
			if (this.DefaultRetryLimit > 0) sb.Add("retry_limit", this.DefaultRetryLimit);
			if (this.DefaultMaxRetryDelay > 0) sb.Add("retry_delay", this.DefaultMaxRetryDelay);
			if (this.DefaultTracing != FdbTracingOptions.Default) sb.Add("tracing", (int) this.DefaultTracing);
			sb.Add("dc_id", this.DataCenterId);
			sb.Add("machine_id", this.MachineId);
			return sb.Build();
		}

	}

	internal sealed class FdbConnectionStringBuilder
	{

		public FdbConnectionStringBuilder(StringBuilder? text = null)
		{
			this.Text = text ?? new StringBuilder();
		}

		public readonly StringBuilder Text;

		public string Build() => this.Text.ToString();

		public void Add(string key, string? value)
		{
			if (value == null) return;
			if (value.IndexOf(' ') >= 0 || value.IndexOf(';') >= 0)
			{ // encode '"' into '\"'
				value = "\"" + value.Replace("\"", "\\\"") + "\"";
			}

			if (this.Text.Length > 0) this.Text.Append("; ");
			this.Text.Append(key).Append('=').Append(value);
		}

		public void Add(string key, bool value)
		{
			if (this.Text.Length > 0) this.Text.Append("; ");
			this.Text.Append(key).Append(value ? "=true" : "=false");
		}

		public void Add(string key, long value)
		{
			if (this.Text.Length > 0) this.Text.Append("; ");
			this.Text.Append(key).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));
		}

		public void Add(string key, double value)
		{
			if (this.Text.Length > 0) this.Text.Append("; ");
			this.Text.Append(key).Append('=').Append(value.ToString("R", CultureInfo.InvariantCulture));
		}

		public void Add(string key, TimeSpan value)
		{
			if (this.Text.Length > 0) this.Text.Append("; ");
			this.Text.Append(key).Append('=').Append(value.TotalSeconds.ToString("R", CultureInfo.InvariantCulture));
		}

		public void Add(string key)
		{
			if (this.Text.Length > 0) this.Text.Append("; ");
			this.Text.Append(key);
		}


	}

}
