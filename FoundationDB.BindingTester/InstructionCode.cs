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

namespace FoundationDB.Client.Testing
{

	public enum InstructionCode
	{
		Invalid = 0,

		// data operations
		Push,
		Dup,
		EmptyStack,
		Swap,
		Pop,
		Sub,
		Concat,
		LogStack,

		// foundationdb operations
		NewTransaction,
		UseTransaction,
		OnError,
		Get,
		GetKey,
		GetRange,
		GetReadVersion,
		GetVersionstamp,
		Set,
		SetReadVersion,
		Clear,
		ClearRange,
		AtomicOp,
		ReadConflictRange,
		WriteConflictRange,
		ReadConflictKey,
		WriteConflictKey,
		DisableWriteConflict,
		Commit,
		Reset,
		Cancel,
		GetCommittedVersion,
		GetApproximateSize,
		WaitFuture,
		GetEstimatedRangeSize,
		GetRangeSplitPoints,

		TuplePack,
		TuplePackWithVersionstamp,
		TupleUnpack,
		TupleRange,
		TupleSort,
		EncodeFloat,
		EncodeDouble,
		DecodeFloat,
		DecodeDouble,

		// Thread Operations
		StartThread,
		WaitEmpty,

		// misc
		UnitTests,

		// Directory/Subspace/Layer Creation
		DirectoryCreateSubspace,
		DirectoryCreateLayer,
		DirectoryCreateOrOpen,
		DirectoryCreate,
		DirectoryOpen,

		// Directory Management
		DirectoryChange,
		DirectorySetErrorIndex,

		// Directory Operations
		DirectoryMove,
		DirectoryMoveTo,
		DirectoryRemove,
		DirectoryRemoveIfExists,
		DirectoryList,
		DirectoryExists,

		// Subspace operation
		DirectoryPackKey,
		DirectoryUnpackKey,
		DirectoryRange,
		DirectoryContains,
		DirectoryOpenSubspace,

		// Directory Logging
		DirectoryLogSubspace,
		DirectoryLogDirectory,

		// Other
		DirectoryStripPrefix,

		// Tenants
		TenantCreate,
		TenantDelete,
		TenantSetActive,
		TenantClearActive,
		TenantList,
	}

}
