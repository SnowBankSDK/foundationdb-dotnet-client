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

	/// <summary>Directory Partition that will use its own Directory Layer instance to manage any subdirectories</summary>
	public class FdbDirectoryPartition : FdbDirectorySubspace
	{

		/// <summary><c>"partition"</c></summary>
		public const string LayerId = "partition";

		/// <summary>Slice with the ASCII string "partition"</summary>
		internal static readonly Slice LayerIdBytes = Slice.FromBytes("partition"u8);

		internal FdbDirectoryPartition(FdbDirectoryLayer.DirectoryDescriptor descriptor, FdbDirectoryLayer.PartitionDescriptor parent, ISubspaceContext? context, bool cached)
			: base(descriptor, context, cached)
		{
			Contract.NotNull(parent);
			this.Parent = parent;
		}

		internal static FdbDirectoryLayer.DirectoryDescriptor MakePartition(FdbDirectoryLayer.DirectoryDescriptor descriptor)
		{
			var partition = new FdbDirectoryLayer.PartitionDescriptor(descriptor.Path, descriptor.Prefix, descriptor.Partition);
			return new(descriptor.DirectoryLayer, descriptor.Path, descriptor.Prefix, descriptor.Layer, partition, descriptor.ValidationChain);
		}

		/// <summary>Descriptor of the partition directory in its parent partition</summary>
		internal FdbDirectoryLayer.PartitionDescriptor Parent { get; }

		internal bool IsTopLevel => this.Descriptor.Path.IsRoot;

		/// <inheritdoc />
		protected override Slice GetKeyPrefix()
		{
			// only "/" is allowed for legacy reasons
			return this.IsTopLevel
				? base.GetKeyPrefix()
				: throw ThrowHelper.InvalidOperationException($"Cannot create keys in the root of directory partition {this.Path}.");
		}

		/// <inheritdoc />
		public override FdbSubspaceKeyRange ToRange(bool inclusive = false)
		{
			// only "/" is allowed for legacy reasons
			return this.IsTopLevel
				? base.ToRange(inclusive)
				: throw ThrowHelper.InvalidOperationException($"Cannot create a key range in the root of directory partition {this.Path}.");
		}

		/// <inheritdoc />
		public override bool Contains(ReadOnlySpan<byte> key)
		{
			// only "/" is allowed for legacy reasons
			return this.IsTopLevel
				? base.Contains(key)
				: throw ThrowHelper.InvalidOperationException($"Cannot check whether a key belongs to the root of directory partition {this.Path}");
		}

		internal override FdbDirectoryLayer.PartitionDescriptor GetEffectivePartition()
		{
			return this.Parent;
		}

		internal override FdbDirectorySubspace WithContext(ISubspaceContext context)
		{
			Contract.NotNull(context);

			if (context == this.Context) return this;
			return new FdbDirectoryPartition(this.Descriptor, this.Parent, context, true);
		}

		public override bool IsPartition => true;

		public override string ToString()
		{
			return $"DirectoryPartition(path={this.Descriptor.Path.ToString()}, prefix={FdbKey.Dump(GetPrefixUnsafe())})";
		}

	}

}
