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

namespace Aspire.Hosting.ApplicationModel
{

	/// <summary>Specify the versioning rules for the FoundationDB docker image</summary>
	public enum FdbVersionPolicy
	{
		/// <summary>Use the exact version specified</summary>
		Exact = 0,

		/// <summary>Select the latest version published to the docker registry.</summary>
		/// <remarks>Please note that there is no guarantee that the latest version is stable or is compatible with the selected API level!</remarks>
		Latest,

		/// <summary>Select the latest compatible version published to the docker registry, that is greater than or equal to the version requested.</summary>
		/// <remarks>
		/// <para>For example, if version <c>6.0.3</c> is requested, but <c>7.4.4</c> is currently available, it will be used instead.</para>
		/// <para>If a newer version is available, but is known to break compatibility (by removing support for the API level selected), then it will not be included in the selection process.</para>
		/// <para>Please note that there is no guarantee that the latest major version will be compatible with this version of the .NET binding, and that request API level is still supported!</para>
		/// </remarks>
		LatestMajor,

		/// <summary>Select the latest compatible minor version published to the docker registry, that is greater than or equal to the version requested.</summary>
		/// <remarks>
		/// <para>For example, if version <c>7.1.3</c> is requested, but <c>7.4.4</c> is the latest <c>7.x</c> version available, it will be used even if there is a more recent <c>8.x</c> version.</para>
		/// <para>If a newer version is available, but is known to break compatibility (by removing support for the API level selected), then it will not be included in the selection process.</para>
		/// </remarks>
		LatestMinor,
		
		/// <summary>Select the latest stable patch version available for the minor version requested.</summary>
		/// <remarks>
		/// <para>For example, if version <c>7.3.5</c> is requested, but <c>7.3.70</c> is the latest <c>7.3.x</c> version available, it will be used even if there is a more recent <c>7.4.x</c> or <c>8.x</c> version.</para>
		/// <para>If a newer version is available, but is known to break compatibility (by removing support for the API level selected), then it will not be included in the selection process.</para>
		/// </remarks>
		LatestPatch,

	}

}
