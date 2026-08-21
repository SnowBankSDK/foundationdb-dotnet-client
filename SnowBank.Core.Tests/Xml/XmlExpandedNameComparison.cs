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

// This file is compiled for the net472 validation target too: see the remark on ReferenceDcsWire.cs.

namespace SnowBank.Data.Xml.Tests
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Xml.Linq;

	/// <summary>
	/// Compares two XML documents on their EXPANDED NAMES: every element and attribute resolved to its
	/// (namespace URI, local name) pair, with namespace declarations and prefix spellings ignored.
	/// </summary>
	/// <remarks>
	/// <para>This is the acceptance rule for the namespaced DataContract format, and byte equality is not. Two documents
	/// that differ only in which prefix stands for a namespace, or in which element carries a declaration, are the same
	/// document to every reader: a prefix is resolved through the declarations in scope and never matched as text. Holding
	/// the emitter to the bytes of one particular writer would therefore pin a choice that carries no meaning, and would
	/// forbid omitting a declaration nothing uses.</para>
	/// <para>What IS compared: the expanded name of every element, in document order; the expanded name and value of every
	/// attribute that is not a declaration, as a set, since attribute order carries no meaning either; and the text content
	/// of every element.</para>
	/// <para><b>The value of a type annotation is compared as a qualified name, not as text.</b> An <c>i:type</c> holds a
	/// prefix and a local name, so its text differs between two documents that name the same type. Its value is resolved
	/// through the declarations in scope on its own element and compared as a pair, which is what a reader does with it.
	/// That rule is specific to this one attribute, because it is the one attribute of this format whose value is a name.</para>
	/// <para>A mismatch is reported as the path to the node that differs and what differs about it. The caller still has both
	/// documents, and the byte difference stays worth printing: it is what a human reads first.</para>
	/// </remarks>
	internal static class XmlExpandedNameComparison
	{

		/// <summary>Namespace of the attributes whose values this format writes as qualified names</summary>
		private static readonly XNamespace Instance = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

		/// <summary>Compares two documents on their expanded names</summary>
		/// <param name="expected">Reference document</param>
		/// <param name="actual">Document under test</param>
		/// <param name="difference">Description of the first difference found, when this returns <see langword="false"/></param>
		public static bool AreEquivalent(XDocument expected, XDocument actual, out string difference)
		{
			if (expected.Root is null || actual.Root is null)
			{
				difference = $"one document has no root element (expected: {(expected.Root is null ? "none" : "present")}, actual: {(actual.Root is null ? "none" : "present")})";
				return expected.Root is null && actual.Root is null;
			}

			return CompareElements(expected.Root, actual.Root, "/", out difference);
		}

		/// <summary>Asserts equivalence, and prints both documents when they differ</summary>
		/// <param name="expected">Reference document, as text</param>
		/// <param name="actual">Document under test, as text</param>
		/// <param name="because">What the comparison is proving, added to the failure message</param>
		public static void AssertEquivalent(string expected, string actual, string because)
		{
			if (AreEquivalent(XDocument.Parse(expected), XDocument.Parse(actual), out string difference))
			{
				return;
			}

			Assert.Fail(
				$"""
				 {because}
				 The two documents are not equivalent: {difference}
				 Expected: {expected}
				 Actual:   {actual}
				 """);
		}

		private static bool CompareElements(XElement expected, XElement actual, string path, out string difference)
		{
			if (expected.Name != actual.Name)
			{
				difference = $"at {path}: expected element {Describe(expected.Name)}, found {Describe(actual.Name)}";
				return false;
			}

			string here = path == "/" ? "/" + expected.Name.LocalName : path + "/" + expected.Name.LocalName;

			if (!CompareAttributes(expected, actual, here, out difference))
			{
				return false;
			}

			if (!string.Equals(TextOf(expected), TextOf(actual), StringComparison.Ordinal))
			{
				difference = $"at {here}: expected text '{TextOf(expected)}', found '{TextOf(actual)}'";
				return false;
			}

			var expectedChildren = expected.Elements().ToList();
			var actualChildren = actual.Elements().ToList();

			if (expectedChildren.Count != actualChildren.Count)
			{
				difference = $"at {here}: expected {expectedChildren.Count} child element(s), found {actualChildren.Count}";
				return false;
			}

			for (int i = 0; i < expectedChildren.Count; i++)
			{
				if (!CompareElements(expectedChildren[i], actualChildren[i], here, out difference))
				{
					return false;
				}
			}

			difference = "";
			return true;
		}

		private static bool CompareAttributes(XElement expected, XElement actual, string path, out string difference)
		{
			var expectedAttributes = Significant(expected);
			var actualAttributes = Significant(actual);

			foreach (var pair in expectedAttributes)
			{
				if (!actualAttributes.TryGetValue(pair.Key, out string? found))
				{
					difference = $"at {path}: attribute {pair.Key} is missing";
					return false;
				}

				if (!string.Equals(pair.Value, found, StringComparison.Ordinal))
				{
					difference = $"at {path}: attribute {pair.Key} expected '{pair.Value}', found '{found}'";
					return false;
				}
			}

			foreach (var pair in actualAttributes)
			{
				if (!expectedAttributes.ContainsKey(pair.Key))
				{
					difference = $"at {path}: unexpected attribute {pair.Key}='{pair.Value}'";
					return false;
				}
			}

			difference = "";
			return true;
		}

		/// <summary>Returns the attributes that carry meaning, keyed by expanded name, with qualified-name values resolved</summary>
		private static Dictionary<string, string> Significant(XElement element)
		{
			var result = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (var attribute in element.Attributes())
			{
				if (attribute.IsNamespaceDeclaration)
				{ // a declaration says nothing about the document's content: it only binds an alias
					continue;
				}

				result[Describe(attribute.Name)] = IsQualifiedNameValue(attribute.Name)
					? ResolveQualifiedName(element, attribute.Value)
					: attribute.Value;
			}

			return result;
		}

		/// <summary>Whether an attribute's VALUE is a qualified name rather than text</summary>
		private static bool IsQualifiedNameValue(XName name) => name.Namespace == Instance && name.LocalName == "type";

		/// <summary>Resolves <paramref name="value"/> as a qualified name against the declarations in scope on <paramref name="element"/></summary>
		private static string ResolveQualifiedName(XElement element, string value)
		{
			int colon = value.IndexOf(':');
			if (colon < 0)
			{ // no prefix: a qualified name resolves against the default namespace
				return Describe(element.GetDefaultNamespace() + value);
			}

			string prefix = value.Substring(0, colon);
			string localName = value.Substring(colon + 1);
			var ns = element.GetNamespaceOfPrefix(prefix);

			return ns is null
				// an unbound prefix is a defect of the document, so it is reported as it stands rather than resolved away
				? "{unbound:" + prefix + "}" + localName
				: Describe(ns + localName);
		}

		/// <summary>Renders an expanded name as <c>{uri}local</c>, the notation the XML specifications use for one</summary>
		private static string Describe(XName name) => name.NamespaceName.Length == 0 ? name.LocalName : "{" + name.NamespaceName + "}" + name.LocalName;

		/// <summary>Returns the concatenated text content of an element, excluding the text of its children</summary>
		private static string TextOf(XElement element)
		{
			var sb = new StringBuilder();
			foreach (var node in element.Nodes())
			{
				if (node is XText text)
				{
					sb.Append(text.Value);
				}
			}
			return sb.ToString();
		}

	}

}
