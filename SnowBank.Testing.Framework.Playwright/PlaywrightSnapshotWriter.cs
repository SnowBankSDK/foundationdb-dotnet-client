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

namespace SnowBank.Testing.Framework.Playwright
{
	using System.Text;
	using Microsoft.Playwright;
	using NUnit.Framework;
	using SnowBank.Diagnostics.Contracts;

	/// <summary>Writes browser snapshots (a full-page PNG per shot plus an HTML contact sheet) to the per-test output directory.</summary>
	/// <remarks>Dependency-free: PNG bytes come from Playwright, the contact sheet is generated HTML (no imaging library). Capture is a diagnostic aid and never throws into the test.</remarks>
	public sealed class PlaywrightSnapshotWriter
	{
		/// <summary>One captured shot, as recorded in the contact sheet.</summary>
		public readonly record struct SnapshotRecord(string FileName, string Label, string CapturedAtIso);

		public PlaywrightSnapshotWriter(string browserId, PlaywrightSnapshotOptions options)
		{
			Contract.NotNullOrEmpty(browserId);
			Contract.NotNull(options);
			this.Options = options;
			var baseDir = options.OutputDirectory ?? ResolveDefaultBaseDirectory();
			this.OutputDirectory = Path.Combine(baseDir, options.Subfolder, browserId);
		}

		private PlaywrightSnapshotOptions Options { get; }

		private List<SnapshotRecord> Shots { get; } = [];

		/// <summary>Directory the shots and the contact sheet are written to.</summary>
		public string OutputDirectory { get; }

		/// <summary>Captures a full-page PNG of <paramref name="page"/>, named from <paramref name="label"/>, and records it for the contact sheet. Returns the written file path.</summary>
		public async Task<string> CaptureAsync(IPage page, string label, CancellationToken ct)
		{
			Contract.NotNull(page);
			ct.ThrowIfCancellationRequested();

			Directory.CreateDirectory(this.OutputDirectory);
			string fileName = $"{this.Shots.Count + 1:D3}-{Sanitize(label)}.png";
			string path = Path.Combine(this.OutputDirectory, fileName);

			byte[] png = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = this.Options.FullPage }).ConfigureAwait(false);
			await File.WriteAllBytesAsync(path, png, ct).ConfigureAwait(false);

			this.Shots.Add(new SnapshotRecord(fileName, label, DateTimeOffset.UtcNow.ToString("O")));
			return path;
		}

		/// <summary>Writes (or refreshes) the <c>index.html</c> contact sheet embedding every captured shot. No-op when the contact sheet is disabled or nothing was captured.</summary>
		public async Task WriteContactSheetAsync(CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			if (!this.Options.ContactSheet || this.Shots.Count == 0) return;

			Directory.CreateDirectory(this.OutputDirectory);
			string html = BuildContactSheetHtml(this.Shots);
			await File.WriteAllTextAsync(Path.Combine(this.OutputDirectory, "index.html"), html, ct).ConfigureAwait(false);
		}

		/// <summary>Builds the contact-sheet HTML (a CSS grid, one cell per shot). Exposed for testing and reuse.</summary>
		public static string BuildContactSheetHtml(IReadOnlyList<SnapshotRecord> shots)
		{
			Contract.NotNull(shots);
			var sb = new StringBuilder();
			sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>Snapshots</title><style>");
			sb.Append("body{font-family:sans-serif;margin:1rem}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:1rem}");
			sb.Append(".cell{border:1px solid #ccc;border-radius:6px;padding:.5rem}.cell img{width:100%;height:auto;display:block}.cap{font-size:.85rem;margin-top:.25rem;color:#333}");
			sb.Append("</style></head><body><h1>Snapshots</h1><div class=\"grid\">");
			foreach (var shot in shots)
			{
				sb.Append("<div class=\"cell\"><a href=\"").Append(Escape(shot.FileName)).Append("\"><img src=\"").Append(Escape(shot.FileName)).Append("\" alt=\"").Append(Escape(shot.Label)).Append("\"/></a>");
				sb.Append("<div class=\"cap\">").Append(Escape(shot.Label)).Append(" <span>").Append(Escape(shot.CapturedAtIso)).Append("</span></div></div>");
			}
			sb.Append("</div></body></html>");
			return sb.ToString();
		}

		private static string ResolveDefaultBaseDirectory()
		{
			// mirrors SimpleTest.GetTemporaryPath: the NUnit test output dir, per running test
			var context = TestContext.CurrentContext;
			string basePath = context != null! ? context.TestDirectory : Environment.CurrentDirectory;
			if (basePath.IndexOf(@"\bin\Debug", StringComparison.OrdinalIgnoreCase) > 0 || basePath.IndexOf(@"\bin\Release", StringComparison.OrdinalIgnoreCase) > 0)
			{
				basePath = Path.Combine(basePath, "TestOutput");
			}
			string leaf = (context?.Test.MethodName ?? context?.Test.ClassName ?? "snapshots").Replace(".", "_").Replace("`", "_");
			return Path.Combine(basePath, leaf);
		}

		private static string Sanitize(string label)
		{
			if (string.IsNullOrEmpty(label)) return "shot";
			var sb = new StringBuilder(label.Length);
			foreach (char c in label)
			{
				sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');
			}
			return sb.ToString();
		}

		private static string Escape(string s) =>
			s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
	}
}
