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

namespace FoundationDB.Layers.FullText
{
	using System;
	using System.Collections.Generic;

	/// <summary>Minimal text analyzer for the experimental full-text index: it lowercases the input,
	/// splits it into terms on any run of non letter-or-digit characters, and drops a small set of stop-words.</summary>
	/// <remarks>This is intentionally simple (tutorial-grade). It is the seam where a richer analyzer
	/// (stemming, n-grams, language rules) would later be plugged in.</remarks>
	[PublicAPI]
	public sealed class TextAnalyzer
	{

		private static readonly HashSet<string> CommonStopWords = new(StringComparer.Ordinal)
		{
			"a", "an", "and", "the", "or", "of", "to", "in", "is", "it",
			"for", "on", "with", "as", "at", "by", "be", "this", "that",
		};

		// NOTE: declared AFTER CommonStopWords on purpose: static initializers run in textual order,
		// and the constructor reads CommonStopWords, so it must already be assigned.
		/// <summary>Default analyzer instance (ordinal, English-ish stop-words, no stemming).</summary>
		public static TextAnalyzer Default { get; } = new();

		/// <summary>Creates a new analyzer.</summary>
		/// <param name="stopWords">Words to ignore. If <see langword="null"/>, a small default set is used. Pass an empty set to keep everything.</param>
		/// <param name="minTokenLength">Terms shorter than this are dropped (default 1, i.e. keep all non-empty terms).</param>
		public TextAnalyzer(IEnumerable<string>? stopWords = null, int minTokenLength = 1)
		{
			this.StopWords = stopWords is null
				? CommonStopWords
				: new HashSet<string>(stopWords, StringComparer.Ordinal);
			this.MinTokenLength = Math.Max(1, minTokenLength);
		}

		private HashSet<string> StopWords { get; }

		private int MinTokenLength { get; }

		/// <summary>Splits <paramref name="text"/> into normalized terms (lowercased, stop-words removed), in order of appearance.</summary>
		public List<string> Analyze(string? text)
		{
			var terms = new List<string>();
			if (string.IsNullOrEmpty(text))
			{
				return terms;
			}

			int start = -1;
			for (int i = 0; i <= text.Length; i++)
			{
				bool isTermChar = i < text.Length && char.IsLetterOrDigit(text[i]);
				if (isTermChar)
				{
					if (start < 0) start = i;
					continue;
				}

				if (start >= 0)
				{
					int length = i - start;
					if (length >= this.MinTokenLength)
					{
						string term = text.AsSpan(start, length).ToString().ToLowerInvariant();
						if (!this.StopWords.Contains(term))
						{
							terms.Add(term);
						}
					}
					start = -1;
				}
			}

			return terms;
		}

	}

}
