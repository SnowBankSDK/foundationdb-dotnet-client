using System.Text;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace SnowBank.DocGen
{
	// A renderer for one custom fenced block: given the raw text inside ```<name> ... ```, return HTML.
	public delegate string FenceRenderer(string dsl);

	// Reusable seam: intercept fenced code blocks. A fence whose info names a registered renderer emits
	// bespoke HTML (the custom blocks); a languaged fence gets its build-time Shiki HTML via the injected
	// lookup; anything else falls back to the default plain <pre>. The custom block set and the highlight
	// lookup are injected, so a different doc set can register its own blocks without touching this class.
	public sealed class FencedBlockExtension : IMarkdownExtension
	{
		private IReadOnlyDictionary<string, FenceRenderer> Renderers { get; }
		private Func<string, string, string?> Highlight { get; }

		public FencedBlockExtension(IReadOnlyDictionary<string, FenceRenderer> renderers, Func<string, string, string?> highlight)
		{
			this.Renderers = renderers;
			this.Highlight = highlight;
		}

		public void Setup(MarkdownPipelineBuilder pipeline) { }

		public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
		{
			if (renderer is not HtmlRenderer html) return;

			// Take over from the default code-block renderer and keep it as the last-resort fallback.
			var fallback = html.ObjectRenderers.OfType<HtmlObjectRenderer<CodeBlock>>().FirstOrDefault();
			if (fallback != null) html.ObjectRenderers.Remove(fallback);
			html.ObjectRenderers.Insert(0, new FencedBlockRenderer(this.Renderers, this.Highlight, fallback));
		}
	}

	internal sealed class FencedBlockRenderer : HtmlObjectRenderer<CodeBlock>
	{
		private IReadOnlyDictionary<string, FenceRenderer> Renderers { get; }
		private Func<string, string, string?> Highlight { get; }
		private HtmlObjectRenderer<CodeBlock>? Fallback { get; }

		public FencedBlockRenderer(IReadOnlyDictionary<string, FenceRenderer> renderers, Func<string, string, string?> highlight, HtmlObjectRenderer<CodeBlock>? fallback)
		{
			this.Renderers = renderers;
			this.Highlight = highlight;
			this.Fallback = fallback;
		}

		protected override void Write(HtmlRenderer renderer, CodeBlock obj)
		{
			if (obj is FencedCodeBlock fenced && !string.IsNullOrWhiteSpace(fenced.Info))
			{
				var name = fenced.Info.Trim();
				if (this.Renderers.TryGetValue(name, out var render))
				{
					renderer.Write("<div class=\"fdb-block\">").Write(render(GetText(fenced))).Write("</div>").WriteLine();
					return;
				}
				// languaged fence: use the build-time Shiki HTML if we have it for this (lang, code)
				var lang = name.Split(' ')[0];
				var highlighted = this.Highlight(lang, GetText(fenced));
				if (highlighted != null) { renderer.Write(highlighted).WriteLine(); return; }
			}

			if (this.Fallback != null) this.Fallback.Write(renderer, obj);
		}

		private static string GetText(LeafBlock block)
		{
			var sb = new StringBuilder();
			var lines = block.Lines.Lines;
			int count = block.Lines.Count;
			for (int i = 0; i < count; i++) sb.Append(lines[i].Slice.ToString()).Append('\n');
			// main.js stripped leading newlines and trailing whitespace before rendering.
			return sb.ToString().TrimStart('\n').TrimEnd();
		}
	}

	// Upgrades ASCII box art (raw <pre> blocks) to Unicode box-drawing characters at build time, so the
	// corners, T-junctions and vertical bars connect into solid lines. Content is left alone: a '-' is
	// only rewritten on a pure border line (spaces/'+'/'-'), and a '|' only where it has a vertical
	// neighbour, so text like a UUID "773166b7-de74-..." keeps its own dashes.
	public sealed class BoxArtExtension : IMarkdownExtension
	{
		public void Setup(MarkdownPipelineBuilder pipeline) { }

		public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
		{
			if (renderer is not HtmlRenderer html) return;
			var fallback = html.ObjectRenderers.OfType<HtmlObjectRenderer<HtmlBlock>>().FirstOrDefault();
			if (fallback != null) html.ObjectRenderers.Remove(fallback);
			html.ObjectRenderers.Insert(0, new BoxArtHtmlBlockRenderer(fallback));
		}
	}

	internal sealed class BoxArtHtmlBlockRenderer : HtmlObjectRenderer<HtmlBlock>
	{
		private HtmlObjectRenderer<HtmlBlock>? Fallback { get; }

		public BoxArtHtmlBlockRenderer(HtmlObjectRenderer<HtmlBlock>? fallback) => this.Fallback = fallback;

		protected override void Write(HtmlRenderer renderer, HtmlBlock obj)
		{
			var text = GetText(obj);
			if (text.Contains("<pre"))
			{
				renderer.Write(BoxArt.Upgrade(text));
				return;
			}
			if (this.Fallback != null) this.Fallback.Write(renderer, obj);
			else renderer.Write(text);
		}

		private static string GetText(LeafBlock block)
		{
			var sb = new StringBuilder();
			var lines = block.Lines.Lines;
			int count = block.Lines.Count;
			for (int i = 0; i < count; i++) sb.Append(lines[i].Slice.ToString()).Append('\n');
			return sb.ToString();
		}
	}

	public static class BoxArt
	{
		public static string Upgrade(string text)
		{
			var lines = text.Replace("\r\n", "\n").Split('\n');
			int rows = lines.Length;

			char Get(int r, int c) => (r < 0 || r >= rows || c < 0 || c >= lines[r].Length) ? ' ' : lines[r][c];
			static bool Vert(char ch) => ch == '|' || ch == '+';
			static bool Horiz(char ch) => ch == '-' || ch == '+';

			var outLines = new string[rows];
			for (int r = 0; r < rows; r++)
			{
				var line = lines[r];
				bool border = line.Trim().Length > 0 && line.Contains('+') && line.All(ch => ch is ' ' or '+' or '-');
				var sb = new StringBuilder(line.Length);
				for (int c = 0; c < line.Length; c++)
				{
					char ch = line[c];
					if (ch == '+') sb.Append(Junction(Vert(Get(r - 1, c)), Vert(Get(r + 1, c)), Horiz(Get(r, c - 1)), Horiz(Get(r, c + 1))));
					else if (ch == '-' && border) sb.Append('─'); // ─
					else if (ch == '|' && (Vert(Get(r - 1, c)) || Vert(Get(r + 1, c)))) sb.Append('│'); // │
					else sb.Append(ch);
				}
				outLines[r] = sb.ToString();
			}
			return string.Join("\n", outLines);
		}

		private static char Junction(bool up, bool down, bool left, bool right) => (up, down, left, right) switch
		{
			(true, true, true, true) => '┼',   // ┼
			(true, true, true, false) => '┤',   // ┤
			(true, true, false, true) => '├',   // ├
			(false, true, true, true) => '┬',   // ┬
			(true, false, true, true) => '┴',   // ┴
			(false, true, false, true) => '┌',  // ┌
			(false, true, true, false) => '┐',  // ┐
			(true, false, false, true) => '└',  // └
			(true, false, true, false) => '┘',  // ┘
			(true, true, false, false) => '│',  // │
			(false, false, true, true) => '─',  // ─
			_ => '+',
		};
	}
}
