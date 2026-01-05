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

namespace SnowBank.Runtime
{
	using System.IO;
	using System.Runtime.CompilerServices;
	using SnowBank.Buffers;

	//TODO: maybe move this to a more global namespace?

	/// <summary>Interface for expressions or AST nodes that can generate a human-readable description of their part in a query or command</summary>
	[PublicAPI]
	public interface ICanExplain
	{

		/// <summary>Adds a human-readable description of this node into the log</summary>
		/// <param name="builder"></param>
		void Explain(ExplanationBuilder builder);

	}

	/// <summary>Builder for generating text descriptions of types that implement <see cref="ICanExplain"/></summary>
	[PublicAPI]
	public sealed class ExplanationBuilder
	{

		/// <summary>Destination buffer</summary>
		public TextWriter Output { get; }
		
		/// <summary>Current depth level (add a TAB (<c>'\t'</c>) for each depth level</summary>
		public int Depth { get; private set; }
		
		/// <summary>If <see langword="true"/> (default), also explain any children of this node; otherwise, only give a brief one-line summary.</summary>
		public bool Recursive { get; }
		
		/// <summary>Prefix added to the beginning of each line</summary>
		public string? Prefix { get; }

		/// <summary>Constructs a new <see cref="ExplanationBuilder"/></summary>
		public ExplanationBuilder(TextWriter output, bool recursive = true, string? prefix = null)
		{
			this.Output = output;
			this.Recursive = recursive;
			this.Prefix = prefix;
			this.Indentation = prefix ?? "";
		}

		/// <summary>Current indentation level</summary>
		public string Indentation { get; private set; }

		private Stack<string> Previous { get; } = [ ];
		
		/// <summary>Appends a new line to the buffer</summary>
		public void WriteLine(string message) => this.Output.WriteLine(this.Indentation + message);

		/// <summary>Appends a new line to the buffer</summary>
		public void WriteLine(ref DefaultInterpolatedStringHandler message) => this.Output.WriteLine(this.Indentation + message.ToStringAndClear());

		/// <summary>Appends a new line to the buffer, with an extra indentation level</summary>
		public void WriteChildrenLine(string message)
		{
			Enter();
			WriteLine(message);
			Leave();
		}

		/// <summary>Appends a new line to the buffer, with an extra indentation level</summary>
		public void WriteChildrenLine(ref DefaultInterpolatedStringHandler message)
		{
			Enter();
			WriteLine(ref message);
			Leave();
		}

		/// <summary>Explain a child of the current instance</summary>
		public void ExplainChild<TExplainable>(TExplainable? child, string? label = null)
			where TExplainable : ICanExplain
		{
			if (child != null)
			{
				if (label != null)
				{
					WriteLine(label);
				}
				Enter();
				child.Explain(this);
				Leave();
			}
		}
		
		/// <summary>Explain one or more children of the current instance</summary>
		public void ExplainChildren<TExplainable>(ReadOnlySpan<TExplainable?> children)
			where TExplainable : ICanExplain
		{
			Enter();
			foreach (var child in children)
			{
				child?.Explain(this);
			}
			Leave();
		}

		/// <summary>Explain one or more children of the current instance</summary>
		public void ExplainChildren<TExplainable>(TExplainable?[]? children)
			where TExplainable : ICanExplain
		{
			if (children == null) return;
			ExplainChildren(new ReadOnlySpan<TExplainable?>(children));
		}

		/// <summary>Explain one or more children of the current instance</summary>
		public void ExplainChildren<TExplainable>(IEnumerable<TExplainable?> children)
			where TExplainable : ICanExplain
		{
			if (children.TryGetSpan(out var span))
			{
				ExplainChildren(span);
				return;
			}
			
			Enter();
			foreach (var child in children)
			{
				child?.Explain(this);
			}
			Leave();
		}

		/// <summary>Increase the indentation level</summary>
		public void Enter()
		{
			++this.Depth;
			this.Previous.Push(this.Indentation);
			this.Indentation = this.Prefix + new string('\t', this.Depth - 1) + "- ";
		}

		/// <summary>Decrease the indentation level</summary>
		public void Leave()
		{
			--this.Depth;
			this.Indentation = this.Previous.Pop();
		}
		
	}

	/// <summary>Extensions methods for <see cref="ICanExplain"/> types</summary>
	public static class CanExplainExtensions
	{
		/// <summary>Returns a human-readable description of what this node does</summary>
		/// <param name="node">Node that should explain itself</param>
		/// <param name="recursive">If <see langword="true"/> (default), also explain any children of this node; otherwise, only give a brief one-line summary.</param>
		/// <param name="prefix">Prefix added to the beginning of each line</param>
		/// <returns>Description of this node</returns>
		/// <remarks>The text will usually end with an extra newline (<c>\r\n</c>)</remarks>
		public static string Explain(this ICanExplain node, bool recursive = true, string? prefix = null)
		{
			var sw = new StringWriter();
			var builder = new ExplanationBuilder(sw, recursive, prefix);
			node.Explain(builder);
			return sw.ToString().Trim();
		}

	}

}
