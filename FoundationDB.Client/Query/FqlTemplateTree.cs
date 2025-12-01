#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

#if NET8_0_OR_GREATER

namespace FoundationDB.Client
{

	/// <summary>Represents "template" for a key/value that can be decoded into a better text representation</summary>
	public sealed class FqlTemplateExpression
	{

		/// <summary>Name of the template (ideally unique per "schema")</summary>
		public string Name { get; }

		/// <summary>Expression for the tuples that match this template</summary>
		public FqlTupleExpression Expression { get; }

		/// <summary>Hinter for the values of keys that match this template</summary>
		public FqlValueTypeHinter Hinter { get; }

		public FqlTemplateExpression(string name, FqlTupleExpression expr, FqlValueTypeHinter hinter)
		{
			this.Name = name;
			this.Expression = expr;
			this.Hinter = hinter;
		}

		public FqlTemplateExpression(string name, FqlTupleExpression expr, FdbValueTypeHint hint)
		{
			this.Name = name;
			this.Expression = expr;
			this.Hinter = (_) => hint;
		}

	}

	/// <summary>Tree that can match a key against a collection of <see cref="FqlTemplateExpression"/>></summary>
	public sealed class FqlTemplateTree
	{

		/// <summary>Root node of the tree</summary>
		private Node Root { get; }

		/// <summary>List of the templates matched by this tree</summary>
		private FqlTemplateExpression[] Templates { get; }

		internal FqlTemplateTree(Node root, FqlTemplateExpression[] templates)
		{
			this.Root = root;
			this.Templates = templates;
		}

		/// <summary>Finds the expression that matches the given tuple</summary>
		/// <param name="tuple">Tuple to match against the tree</param>
		/// <param name="template">Receives the expression that was matched</param>
		/// <returns><c>true</c> if an expression matched the tuple; otherwise, <c>false</c></returns>
		public bool TryMatch(SpanTuple tuple, [MaybeNullWhen(false)] out FqlTemplateExpression template)
		{
			var node = this.Root;

			int n = tuple.Count;

			// holds a "catchall" clause that would have been encountered at one point
			int? candidate = null;

			for (int i = 0; i < n; i++)
			{

				if (node.CatchAll is not null)
				{
					candidate = node.CatchAll;
				}

				var next = node.Evaluate(tuple);
				if (next == null)
				{
					goto no_direct_match;
				}
				node = next;
			}

			if (node.Match is not null)
			{ // we have a direct match
				template = this.Templates[node.Match.Value];
				return true;
			}

		no_direct_match:

			if (node.CatchAll is not null)
			{
				candidate = node.CatchAll;
			}
			if (candidate is not null)
			{
				template = this.Templates[candidate.Value];
				return true;
			}

			template = null;
			return false;
		}

		/// <summary>Internal node in a <see cref="FqlTemplateTree"/></summary>
		[DebuggerDisplay("Depth={Depth}, Label={Label}")]
		internal sealed record Node
		{

			/// <summary>Index of the matched item in the tuple</summary>
			public required int Depth { get; init; }

			/// <summary>Label (for debugging purpose)</summary>
			public string? Label { get; init; }

			/// <summary>If non-null, list of conditions that lead to the next nodes</summary>
			public Dictionary<FqlTupleItem, Node>? Children { get; set; }

			/// <summary>If not-null, matching expression if evaluation ends up in this node (not on a MaybeMore)</summary>
			public int? Match { get; set; }

			/// <summary>If not-null, matching expression if evaluation ends up in this node, and the last item is a MaybeMore</summary>
			public int? CatchAll { get; set; }

			/// <summary>Evaluate the next node that matches the tuple at the current depth</summary>
			public Node? Evaluate(SpanTuple tuple)
			{
				if (this.Children is not null)
				{
					var candidateType = tuple.GetElementType(this.Depth);
					foreach (var (item, next) in this.Children)
					{
						switch (item.Type)
						{
							case FqlItemType.Integer:
							{
								if (candidateType == TupleSegmentType.Integer
								    && item.Matches(tuple.GetSpan(this.Depth)))
								{
									return next;
								}
								break;
							}
							case FqlItemType.String:
							{
								if (candidateType == TupleSegmentType.UnicodeString
								    && item.Matches(tuple.GetSpan(this.Depth)))
								{
									return next;
								}
								break;
							}
							case FqlItemType.Tuple:
							{
								if (candidateType == TupleSegmentType.Tuple
								    && item.Matches(tuple.GetSpan(this.Depth)))
								{
									return next;
								}
								break;
							}
							case FqlItemType.Bytes:
							{
								if (candidateType == TupleSegmentType.ByteString
								    && item.Matches(tuple.GetSpan(this.Depth)))
								{
									return next;
								}
								break;
							}
							case FqlItemType.VStamp:
							{
								if (candidateType is (TupleSegmentType.VersionStamp80 or TupleSegmentType.VersionStamp96)
								    && item.Matches(tuple.GetSpan(this.Depth)))
								{
									return next;
								}
								break;
							}

							case FqlItemType.Variable:
							{
								var types = (FqlVariableTypes) item.Value!;

								switch (candidateType)
								{
									case TupleSegmentType.ByteString:
									{
										if (types.HasFlag(FqlVariableTypes.Bytes)) return next;
										break;
									}
									case TupleSegmentType.UnicodeString:
									{
										if (types.HasFlag(FqlVariableTypes.String)) return next;
										break;
									}
									case TupleSegmentType.Integer:
									{
										if (types.HasFlag(FqlVariableTypes.Int)) return next;
										break;
									}
									case TupleSegmentType.Single or TupleSegmentType.Double or TupleSegmentType.Decimal:
									{
										if (types.HasFlag(FqlVariableTypes.Num)) return next;
										break;
									}
									case TupleSegmentType.Boolean:
									{
										if (types.HasFlag(FqlVariableTypes.Bool)) return next;
										break;
									}
									case TupleSegmentType.Uuid128:
									{
										if (types.HasFlag(FqlVariableTypes.Uuid)) return next;
										break;
									}
									case TupleSegmentType.VersionStamp80 or TupleSegmentType.VersionStamp96:
									{
										if (types.HasFlag(FqlVariableTypes.VStamp)) return next;
										break;
									}
									case TupleSegmentType.Uuid64:
									{ //REVIEW: is this correct? we treat Uuid64 like a UInt64
										if (types.HasFlag(FqlVariableTypes.Int)) return next;
										break;
									}
									case TupleSegmentType.Tuple:
									{
										if (types.HasFlag(FqlVariableTypes.Tuple)) return next;
										break;
									}
								}
								break;
							}
						}
					}
				}

				return null;

			}

		}

		/// <summary>Creates a new <see cref="Builder"/> that can compile a set of <see cref="FqlTemplateExpression"/> into a <see cref="FqlTemplateTree"/></summary>
		public static Builder CreateBuilder() => new();

		/// <summary>Builder for generating a tree of multiple <see cref="FqlTemplateExpression"/></summary>
		public sealed class Builder
		{

			/// <summary>Templates added to this builder</summary>
			public List<FqlTemplateExpression> Templates { get; } = [ ];

			public void Add(string name, FqlTupleExpression expr, FqlValueTypeHinter? hinter = null)
			{
				Contract.NotNullOrEmpty(name);
				Contract.NotNull(expr);

				hinter ??= (_) => FdbValueTypeHint.None;
				var template = new FqlTemplateExpression(name, expr, hinter);
				Add(template);
			}

			public void Add(FqlTemplateExpression template)
			{
				Contract.NotNull(template);
				this.Templates.Add(template);
			}

			/// <summary>Compiles the templates into a <see cref="FqlTemplateTree"/></summary>
			public FqlTemplateTree BuildTree()
			{
				var root = new Node()
				{
					Depth = 0,
					Label = "/",
				};

				for (int exprIdx = 0; exprIdx < this.Templates.Count; exprIdx++)
				{
					var template = this.Templates[exprIdx];

					var node = root!;

					bool lastIsMaybeMore = false;
					foreach (var item in template.Expression.Items)
					{
						if (item.Type == FqlItemType.MaybeMore)
						{
							lastIsMaybeMore = true;
							break;
						}

						var children = node.Children ??= [ ];

						ref var next = ref CollectionsMarshal.GetValueRefOrAddDefault(children, item, out var exists);
						if (!exists)
						{
							next = new()
							{
								Depth = node.Depth + 1,
								Label = item.ToString(),
							};
						}
						node = next!;
					}

					if (lastIsMaybeMore)
					{
						node.CatchAll = exprIdx;
					}
					else
					{
						node.Match = exprIdx;
					}
				}

				return new(root, [ ..this.Templates ]);
			}

		}

		/// <summary>Enumerates the templates used by this tree</summary>
		public IEnumerable<FqlTemplateExpression> GetTemplates()
		{
			return this.Templates;
		}

	}

	public delegate FdbValueTypeHint FqlValueTypeHinter(SpanTuple tuple);

}

#endif
