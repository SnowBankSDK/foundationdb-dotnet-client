using System.Text;
using System.Text.RegularExpressions;

namespace SnowBank.DocGen
{
	// Build-time port of the client-side renderers in
	// FoundationDB/Documentation/templates/snowbank/public/main.js (renderBytes / renderDiff / fql).
	// The output HTML uses the same class names as main.css (.fdb-block, .bytes-card, .fql-block, .diff),
	// so the existing stylesheet styles it with no client script. Each renderer returns the INNER html;
	// the fenced-block renderer wraps it in <div class="fdb-block">, exactly as main.js did.
	public static class FdbBlocks
	{
		// The FoundationDB-side registration of the custom fenced blocks. In the real generator this is
		// where the FoundationDB doc set plugs its blocks into the reusable pipeline; CloudLayer would
		// register its own set the same way.
		public static IReadOnlyDictionary<string, FenceRenderer> Renderers { get; } =
			new Dictionary<string, FenceRenderer>(StringComparer.OrdinalIgnoreCase)
			{
				["fdb-bytes"] = RenderBytes,
				["fdb-diff"] = RenderDiff,
				["fdb-fql"] = RenderFql,
			};

		private static string Esc(string s)
			=> s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

		// ---- shared FQL colorizer (fixed token rules, mirrors main.js `fql`) ----
		// The token classes (f-cmt, f-str, f-var, f-type, f-dir, f-name, f-sep, f-int, f-op, dt, it)
		// map to the colors in main.css, which in turn mirror FqlSyntaxHighlighter.cs.
		private static readonly Regex FqlRe = new(
			@"(//[^\n]*)" +
			@"|(-""(?:[^""\\]|\\.)*"")" +
			@"|(\+""(?:[^""\\]|\\.)*"")" +
			@"|('')" +
			@"|(""(?:[^""\\]|\\.)*"")" +
			@"|(<[^>]*>)" +
			@"|(\.\.\.)" +
			@"|([A-Za-z_]\w*:-?\d+)" +
			@"|(-?\d+)" +
			@"|([(),])" +
			@"|(=)",
			RegexOptions.Compiled);

		private static string ColorVar(string v)
			=> $"<span class=\"f-var\">&lt;</span><span class=\"f-type\">{Esc(v.Substring(1, v.Length - 2))}</span><span class=\"f-var\">&gt;</span>";

		private static string ColorConst(string t)
		{
			int i = t.IndexOf(':');
			return $"<span class=\"f-name\">{Esc(t.Substring(0, i))}</span><span class=\"f-sep\">:</span><span class=\"f-int\">{Esc(t.Substring(i + 1))}</span>";
		}

		private static string Fql(string text)
		{
			var sb = new StringBuilder();
			int last = 0;
			foreach (Match m in FqlRe.Matches(text))
			{
				sb.Append(Esc(text.Substring(last, m.Index - last)));
				var g = m.Groups;
				if (g[1].Success) sb.Append($"<span class=\"f-cmt\">{Esc(g[1].Value)}</span>");
				else if (g[2].Success) sb.Append($"<span class=\"dt\">{Esc(g[2].Value.Substring(1))}</span>");
				else if (g[3].Success) sb.Append($"<span class=\"it\">{Esc(g[3].Value.Substring(1))}</span>");
				else if (g[4].Success) sb.Append("<span class=\"f-str\">''</span>");
				else if (g[5].Success) sb.Append($"<span class=\"f-str\">{Esc(g[5].Value)}</span>");
				else if (g[6].Success) sb.Append(ColorVar(g[6].Value));
				else if (g[7].Success) sb.Append("<span class=\"f-dir\">...</span>");
				else if (g[8].Success) sb.Append(ColorConst(g[8].Value));
				else if (g[9].Success) sb.Append($"<span class=\"f-int\">{g[9].Value}</span>");
				else if (g[10].Success) sb.Append($"<span class=\"f-sep\">{g[10].Value}</span>");
				else if (g[11].Success) sb.Append("<span class=\"f-op\">=</span>");
				last = m.Index + m.Length;
			}
			sb.Append(Esc(text.Substring(last)));
			return sb.ToString();
		}

		// ---- fdb-fql ----
		public static string RenderFql(string dsl)
		{
			var rows = new StringBuilder();
			foreach (var line in dsl.Split('\n'))
			{
				if (line.Trim().Length != 0) rows.Append($"<div class=\"fline\">{Fql(line)}</div>");
				else rows.Append("<div class=\"fline\" style=\"height:.55em\"></div>");
			}
			return $"<div class=\"fql-block\">{rows}</div>";
		}

		// ---- fdb-bytes ----
		private static readonly Dictionary<string, string> BType = new()
		{
			["int"] = "int", ["str"] = "str", ["vs"] = "vs", ["uuid"] = "uuid", ["dir"] = "dir",
			["bytes"] = "int", ["bool"] = "int", ["nil"] = "int", ["tuple"] = "str",
		};

		private static readonly Regex ByteTokRe = new(@"'[^']*'|<[^>]*>|\S+", RegexOptions.Compiled);
		private static readonly Regex TuplePrefix = new(@"^tuple\s*:\s*", RegexOptions.Compiled);
		private static readonly Regex FirstWord = new(@"^(\S+)", RegexOptions.Compiled);

		public static string RenderBytes(string dsl)
		{
			string? tuple = null;
			var items = new List<(string Type, List<string> Toks, string Cap)>();
			foreach (var line in dsl.Split('\n'))
			{
				if (line.Trim().Length == 0) continue;
				if (Regex.IsMatch(line, @"^tuple\s*:")) { tuple = TuplePrefix.Replace(line, ""); continue; }
				var main = line;
				var cap = "";
				int h = line.IndexOf('#');
				if (h >= 0) { cap = line.Substring(h + 1).Trim(); main = line.Substring(0, h); }
				var toks = ByteTokRe.Matches(main.Trim()).Select(x => x.Value).ToList();
				if (toks.Count == 0) continue;
				var type = toks[0];
				toks.RemoveAt(0);
				items.Add((type, toks, cap));
			}

			var html = new StringBuilder("<div class=\"bytes-card\">");
			if (tuple != null) html.Append($"<div class=\"tuple\">{Fql(tuple)}</div>");
			html.Append("<div class=\"strip\">");
			bool blob = false;
			foreach (var it in items)
			{
				var cls = BType.TryGetValue(it.Type, out var c) ? c : "int";
				var cells = new StringBuilder();
				foreach (var t in it.Toks)
				{
					if (t == "...") { blob = true; cells.Append("<div class=\"cell dot\">&#8230;</div>"); }
					else if (t[0] == '.') cells.Append($"<div class=\"cell mk\">{Esc(t.Substring(1))}</div>");
					else if (t.Length >= 2 && t[0] == '\'' && t[^1] == '\'')
					{
						foreach (var ch in t.Substring(1, t.Length - 2))
							cells.Append($"<div class=\"cell ch\">'{Esc(ch.ToString())}'</div>");
					}
					else if (t[0] == '<')
					{
						blob = true;
						var inner = t.Substring(1, t.Length - 2);
						int cc = inner.IndexOf(':');
						var label = cc < 0 ? inner : inner.Substring(0, cc);
						var n = cc < 0 ? "" : inner.Substring(cc + 1);
						cells.Append($"<div class=\"cell blob\">&#10216;{Esc(label)}{(n.Length > 0 ? $" &middot; {Esc(n)} bytes" : "")}&#10217;</div>");
					}
					else cells.Append($"<div class=\"cell\">{Esc(t)}</div>");
				}
				var capHtml = it.Cap.Length > 0
					? $"<div class=\"cap\">{FirstWord.Replace(Esc(it.Cap), "<b>$1</b>")}</div>"
					: "";
				html.Append($"<div class=\"grp {cls}\"><div class=\"cells\">{cells}</div>{capHtml}</div>");
			}
			html.Append("</div>");

			if (!blob)
			{
				var raw = new List<string>();
				foreach (var it in items)
					foreach (var t in it.Toks)
					{
						if (t[0] == '.') raw.Add(t.Substring(1));
						else if (t[0] == '\'') foreach (var ch in t.Substring(1, t.Length - 2)) raw.Add(((int)ch).ToString("X2"));
						else raw.Add(t);
					}
				html.Append($"<div class=\"raw\"><span class=\"lbl\">raw</span>{string.Join(" ", raw)}</div>");
			}
			html.Append("</div>");
			return html.ToString();
		}

		// ---- fdb-diff ----
		public static string RenderDiff(string dsl)
		{
			string title = "";
			var rows = new StringBuilder();
			foreach (var raw in dsl.Split('\n'))
			{
				if (raw.Trim().Length == 0) continue;
				if (Regex.IsMatch(raw, @"^title\s*:")) { title = Regex.Replace(raw, @"^title\s*:\s*", "").Trim(); continue; }
				char g = raw[0];
				var text = raw.Length >= 2 ? raw.Substring(2) : "";
				string cls, gut;
				if (g == '-') { cls = "del"; gut = "<span class=\"g-del\">- </span>"; }
				else if (g == '+') { cls = "add"; gut = "<span class=\"g-add\">+ </span>"; }
				else if (g == '~') { cls = "mod"; gut = "  "; }
				else { cls = "ctx"; gut = "  "; }
				rows.Append($"<div class=\"ln {cls}\">{gut}{Fql(text)}</div>");
			}
			var titleHtml = title.Length > 0 ? $"<div class=\"diff-title\">{Esc(title)}</div>" : "";
			return $"<div class=\"diff\">{titleHtml}<div class=\"diff-body\">{rows}</div></div>";
		}
	}
}
