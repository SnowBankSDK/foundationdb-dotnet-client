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

#if NET8_0_OR_GREATER

namespace FoundationDB.Client
{
	using System.Numerics;

	public static class FqlSyntaxHighlighter
	{
		// These FQL token colors are mirrored by the documentation renderer:
		// Documentation/templates/snowbank/public/main.css (the .fdb-block .f-* rules) and main.js.
		// If this palette changes, update the docs to match.
		private const string COLOR_NAME = "#6699CC";
		private const string COLOR_ITEM = "#F2777A";
		private const string COLOR_STRING = "#99CC99";
		private const string COLOR_TYPE = "#CC99CC";
		private const string COLOR_SEPARATOR = "#66CCCC";
		private const string COLOR_DECIMAL = "#FFCC66";

		public static string GetMarkup(FqlTupleItem item) => item.Type switch
		{
			FqlItemType.Nil => $"[{COLOR_TYPE}]nil[/]",
			FqlItemType.Variable => $"[{COLOR_ITEM}]<[/][{COLOR_TYPE}]{(item.Name != null ? (item.Name + ":") : "")}{FqlTupleItem.ToVariableTypeLiteral((FqlVariableTypes) item.Value!)}[/][{COLOR_ITEM}]>[/]",
			FqlItemType.MaybeMore => $"[{COLOR_ITEM}]...[/]",
			FqlItemType.Boolean => ((bool) item.Value!) ? $"[{COLOR_TYPE}]true[/]" : $"[{COLOR_TYPE}]false[/]",
			FqlItemType.Integer => $"[{COLOR_ITEM}]" + item.Value switch
			{
				int x        => x.ToString(null, CultureInfo.InvariantCulture),
				uint x       => x.ToString(null, CultureInfo.InvariantCulture),
				long x       => x.ToString(null, CultureInfo.InvariantCulture),
				ulong x      => x.ToString(null, CultureInfo.InvariantCulture),
				Int128 x     => x.ToString(null, CultureInfo.InvariantCulture),
				UInt128 x    => x.ToString(null, CultureInfo.InvariantCulture),
				BigInteger x => x.ToString(null, CultureInfo.InvariantCulture),
				_ => throw new InvalidOperationException("Invalid Int storage type"),
			} + "[/]",
			FqlItemType.Number => $"[{COLOR_ITEM}]" + item.Value switch
			{
				Half x    => x.ToString("R", CultureInfo.InvariantCulture),
				float x   => x.ToString("R", CultureInfo.InvariantCulture),
				double x  => x.ToString("R", CultureInfo.InvariantCulture),
				decimal x => x.ToString("R", CultureInfo.InvariantCulture),
				_ => throw new InvalidOperationException("Invalid Int storage type"),
			} + "[/]",
			FqlItemType.String => $"[{COLOR_STRING}]\"{((string) item.Value!).Replace("\"", "\\\"")}\"[/]",
			FqlItemType.Uuid => $"[{COLOR_ITEM}]{((Uuid128) item.Value!):B}[/]",
			FqlItemType.Bytes => $"[{COLOR_ITEM}]0[{COLOR_DECIMAL}]x[/]{((Slice) item.Value!).ToHexStringLower()}[/]",
			FqlItemType.Tuple => ((FqlTupleExpression) item.Value!).ToString(),
			_ => $"[red]<?{item.Type}?>[/]",
		};

		public static string GetMarkup(FqlTupleExpression expr)
		{
			var items = CollectionsMarshal.AsSpan(expr.Items);

			if (items.Length == 0)
			{
				return $"[{COLOR_SEPARATOR}]()[/]";
			}

			var sb = new StringBuilder();
			sb.Append($"[{COLOR_SEPARATOR}]([/]");

			sb.Append(GetMarkup(items[0]));
			for(int i = 1; i < items.Length; i++)
			{
				sb.Append($"[{COLOR_SEPARATOR}],[/]");
				sb.Append(GetMarkup(items[i]));
			}

			sb.Append($"[{COLOR_SEPARATOR}])[/]");
			return sb.ToString();
		}

		public static string GetMarkup(FqlDirectoryExpression dir)
		{
			var sb = new StringBuilder();

			var segments = CollectionsMarshal.AsSpan(dir.Segments);

			if (segments.Length == 0) return "";
			if (segments.Length == 1 && segments[0].IsRoot) return $"[{COLOR_SEPARATOR}]/[/]";

			bool needsSlash = false;
			for(int i = 0; i < segments.Length; i++)
			{
				if (needsSlash) sb.Append('/');
				switch (segments[i].Type)
				{
					case FqlPathSegmentType.Root: sb.Append($"[{COLOR_SEPARATOR}]/[/]"); break;
					case FqlPathSegmentType.Literal: sb.Append($"[{COLOR_NAME}]{segments[i].ToString()}[/]"); needsSlash = true; break;
					case FqlPathSegmentType.Any: sb.Append($"[{COLOR_TYPE}]<>[/]"); needsSlash = true; break;
					default: sb.Append("<???>"); needsSlash = true; break;
				}
			}

			return sb.ToString();
		}

		public static string GetMarkup(FqlQuery query)
		{
			return
			    (query.Directory != null ? GetMarkup(query.Directory) : "") 
			  + (query.Tuple != null ? GetMarkup(query.Tuple) : "");
		}

	}

}

#endif
