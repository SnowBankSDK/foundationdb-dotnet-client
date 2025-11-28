#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System.IO;
	using FoundationDB.Client.Native;

	public static partial class Fdb
	{

		//REVIEW: consider changing this to an instance class so that we could do a Fluent API ? ex: Fdb.Options.WithFoo(...).WithBar(...).WithBaz(...)

		/// <summary>Global settings for the FoundationDB binding</summary>
		[PublicAPI]
		public static class Options
		{

			#region Native Library Preloading...

			/// <summary>Custom path from where to load the native C API library. If <see langword="null"/>, let the CLR find the dll. If String.Empty let Win32's LoadLibrary find the correct dll, else use the specified path to load the library</summary>
			//REVIEW: change this into a get-only, and force people to call SetNativeLibPath(..)?
			public static string? NativeLibPath = string.Empty;

			/// <summary>Disable preloading of the native C API library. The CLR will handle the binding of the library.</summary>
			/// <remarks>This *must* be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void DisableNativeLibraryPreloading()
			{
				Fdb.Options.NativeLibPath = null;
			}

			/// <summary>Enable automatic preloading of the native C API library. The operating system will handle the binding of the library</summary>
			/// <remarks>This *must* be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void EnableNativeLibraryPreloading()
			{
				Fdb.Options.NativeLibPath = string.Empty;
			}

			/// <summary>Preload the native C API library from a specific path (relative of absolute) where the fdb_c.dll library is located</summary>
			/// <example>
			/// Fdb.Options.SetNativeLibPath(@".\libs\x64") will attempt to load ".\libs\x64\fdb_c.dll"
			/// Fdb.Options.SetNativeLibPath(@".\libs\x64\my_custom_fdb_c.dll") will attempt to load ".\libs\x64\my_custom_fdb_c.dll"
			/// </example>
			/// <remarks>
			/// This <b>*must*</b> be called before the start of the network thread, otherwise it won't have any effects.
			/// You can specify the path to a library with a custom file name by making sure that <paramref name="path"/> ends with ".dll". If not, then either <c>fdb_c.dll</c> (on Windows) or <c>fdb_c.so</c> (on Linux) will be appended to the path.
			/// </remarks>
			public static void SetNativeLibPath(string path)
			{
				Contract.NotNullOrWhiteSpace(path);

				// note: the file name MUST be "fdb_c" (with the proper extension on the platform, so for ex 'fdb_c.dll' on Windows)
				// or else the binding of the native methods will not recognize that the library has already been preloaded!

				// name must match what the binder will expect
				var expectedFileName = GetExpectedNativeLibraryName();
				var fileName = Path.GetFileName(path);

				if (File.Exists(path))
				{ // this is a file
					if (fileName != expectedFileName)
					{ 
						throw new ArgumentException($"The name of the file must match exactly '{expectedFileName}' but was '{fileName}'. If you need to handle multiple versions, they should be placed in a dedicated folder.");
					}
				}
				else if (global::System.IO.Directory.Exists(path))
				{ // this is a folder, add the expected file name
					path = Path.Combine(path, expectedFileName);
					if (!File.Exists(path))
					{
						throw new ArgumentException($"The specified folder must contain a file named '{expectedFileName}'.");
					}
				}
				else
				{ // path is not found
					throw new ArgumentException($"The path '{path}' must reference either a file with name '{expectedFileName}', or a folder that contains a file named '{expectedFileName}'.");
				}

				//TODO: throw if native library has already been loaded
				Fdb.Options.NativeLibPath = path;
			}

			/// <summary>Returns the expected native library file native for the current OS platform</summary>
			/// <returns><c>"fdb_c.dll"</c> on Windows, <c>"fdb_c.so"</c> on Linux, etc...</returns>
			public static string GetExpectedNativeLibraryName()
			{
				if (OperatingSystem.IsWindows())
				{
					return "fdb_c.dll";
				}
				if (OperatingSystem.IsMacOS())
				{
					return "libfdb_c.dylib";
				}
				return "libfdb_c.so";
			}

			#endregion

			#region Trace Path...

			/// <summary>Default path to the network thread tracing file</summary>
			public static string? TracePath;

			/// <summary>Sets the custom path where the logs will be stored</summary>
			/// <remarks>This *must* be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTracePath(string outputDirectory)
			{
				Fdb.Options.TracePath = outputDirectory;
			}

			#endregion

			#region TLS...

			/// <summary>Content of the TLS root and client certificates used for TLS connections (none by default)</summary>
			public static Slice TlsCertificateBytes { get; private set; }

			/// <summary>Path to the TLS root and client certificates used for TLS connections (none by default)</summary>
			public static string? TlsCertificatePath { get; private set; }

			/// <summary>Path to the Private Key used for TLS connections (none by default)</summary>
			public static Slice TlsPrivateKeyBytes { get; private set; }

			/// <summary>Path to the Private Key used for TLS connections (none by default)</summary>
			public static string? TlsPrivateKeyPath { get; private set; }

			/// <summary>Passphrase for encrypted TLS private key.</summary>
			public static string? TlsPassword { get; private set; }

			/// <summary>Pattern used to verify certificates of TLS peers (none by default)</summary>
			public static Slice TlsVerificationPattern { get; private set; }

			/// <summary>Content of the certificate authority bundle</summary>
			public static Slice TlsCaBytes { get; private set; }

			/// <summary>Path to the certificate authority bundle</summary>
			public static string? TlsCaPath { get; private set; }

			/// <summary>Sets the path to the root certificate and public key for TLS connections</summary>
			/// <remarks>This *must* be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTlsCertificate(Slice bytes)
			{
				Fdb.Options.TlsCertificateBytes = bytes;
				Fdb.Options.TlsCertificatePath = null;
			}

			/// <summary>Sets the path to the root certificate and public key for TLS connections</summary>
			/// <remarks>This *must* be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTlsCertificate(string path)
			{
				Fdb.Options.TlsCertificatePath = path;
				Fdb.Options.TlsCertificateBytes = Slice.Nil;
			}

			/// <summary>Sets the path to the private key for TLS connections</summary>
			/// <remarks>This must be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTlsPrivateKey(Slice bytes, string? password = null)
			{
				Fdb.Options.TlsPrivateKeyBytes = bytes;
				Fdb.Options.TlsPrivateKeyPath = null;
				if (password != null) Fdb.Options.TlsPassword = password;
			}

			/// <summary>Sets the path to the private key for TLS connections</summary>
			/// <remarks>This must be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTlsPrivateKey(string path, string? password = null)
			{
				Fdb.Options.TlsPrivateKeyPath = path;
				Fdb.Options.TlsPrivateKeyBytes = Slice.Nil;
				if (password != null) Fdb.Options.TlsPassword = password;
			}

			/// <summary>Sets the pattern with which to verify certificates of TLS peers</summary>
			/// <remarks>This must be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTlsVerificationPattern(Slice pattern)
			{
				Fdb.Options.TlsVerificationPattern = pattern;
			}

			/// <summary>Use TLS to secure the connections to the cluster</summary>
			/// <param name="certificateBytes">Content of the root certificate and public key</param>
			/// <param name="privateKeyBytes">Content of the private key</param>
			/// <param name="verificationPattern">Verification with which to verify certificates of TLS peers (optional)</param>
			/// <param name="password">Passphrase for the encrypted private key (optional)</param>
			public static void UseTls(Slice certificateBytes, Slice privateKeyBytes, Slice verificationPattern = default, string? password = null)
			{
				Fdb.Options.TlsCertificateBytes = certificateBytes;
				Fdb.Options.TlsCertificatePath = null;
				Fdb.Options.TlsPrivateKeyBytes = privateKeyBytes;
				Fdb.Options.TlsPrivateKeyPath = null;
				Fdb.Options.TlsVerificationPattern = verificationPattern;
				Fdb.Options.TlsPassword = password;
			}

			/// <summary>Use TLS to secure the connections to the cluster</summary>
			/// <param name="certificatePath">Path to the root certificate and public key</param>
			/// <param name="privateKeyPath">Path to the private key</param>
			/// <param name="verificationPattern">Verification with which to verify certificates of TLS peers (optional)</param>
			/// <param name="password">Passphrase for the encrypted private key (optional)</param>
			public static void UseTls(string certificatePath, string privateKeyPath, Slice verificationPattern = default, string? password = null)
			{
				Fdb.Options.TlsCertificatePath = certificatePath;
				Fdb.Options.TlsCertificateBytes = Slice.Nil;
				Fdb.Options.TlsPrivateKeyPath = privateKeyPath;
				Fdb.Options.TlsPrivateKeyBytes = Slice.Nil;
				Fdb.Options.TlsVerificationPattern = verificationPattern;
				Fdb.Options.TlsPassword = password;
			}

			#endregion

		}

	}

}
