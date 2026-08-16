using System.Globalization;
using System.Text;

namespace SnowBank.DocGen
{
	// A CloudLayer-side "island": a custom fenced block whose build-time renderer emits a finished,
	// correctly-sized static SVG (the first frame, so it paints instantly with no layout shift), which a
	// deferred client script then animates in place. This is the seam a Vue/D3 component plugs into; here
	// it is a hand-authored SVG + a small d3 animation, kept dependency-light for the prototype.
	public static class IslandBlocks
	{
		public static IReadOnlyDictionary<string, FenceRenderer> Renderers { get; } =
			new Dictionary<string, FenceRenderer>(StringComparer.OrdinalIgnoreCase)
			{
				["txn-flow"] = RenderTxnFlow,
			};

		// A FoundationDB transaction: get a read version, read from storage, then commit through the
		// proxy (conflict check at the resolver, made durable at the transaction logs). The dsl is
		// ignored for the prototype; the shape is fixed.
		public static string RenderTxnFlow(string dsl)
		{
			var sb = new StringBuilder();
			sb.Append("<div class=\"island txn-flow\">");
			// step indicator at the top of the card (fixed height, so advancing the text never shifts layout)
			sb.Append("<div class=\"tf-step\">");
			sb.Append("<span class=\"tf-num\">1</span>");
			sb.Append("<span class=\"tf-label\">Get a read version: the client asks a proxy for the latest committed version.</span>");
			sb.Append("</div>");
			sb.Append("<svg viewBox=\"0 0 760 340\" role=\"img\" aria-label=\"FoundationDB transaction flow\">");

			// edges first (drawn behind the node boxes); packets animate along these by id
			sb.Append(Edge("e_cp", 100, 90, 340, 170));   // client <-> proxy
			sb.Append(Edge("e_cs", 100, 90, 100, 250));   // client <-> storage (reads)
			sb.Append(Edge("e_pr", 340, 170, 590, 90));   // proxy <-> resolver (conflict check)
			sb.Append(Edge("e_pt", 340, 170, 590, 250));  // proxy <-> tlog (durable)

			sb.Append(Node("n-client", 100, 90, "Client", "your app"));
			sb.Append(Node("n-storage", 100, 250, "Storage", "reads"));
			sb.Append(Node("n-proxy", 340, 170, "Proxy", "GRV + commit"));
			sb.Append(Node("n-resolver", 590, 90, "Resolver", "conflict check"));
			sb.Append(Node("n-tlog", 590, 250, "Log", "make durable"));

			sb.Append("</svg>");
			sb.Append("</div>");
			return sb.ToString();
		}

		private static string Edge(string id, double x1, double y1, double x2, double y2)
			=> $"<path id=\"{id}\" class=\"tf-edge\" d=\"M{N(x1)},{N(y1)} L{N(x2)},{N(y2)}\"/>";

		private static string Node(string id, double cx, double cy, string title, string sub)
		{
			double x = cx - 64, y = cy - 25;
			return $"<g class=\"tf-node\" id=\"{id}\">" +
				$"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"128\" height=\"50\" rx=\"9\"/>" +
				$"<text x=\"{N(cx)}\" y=\"{N(cy - 3)}\" text-anchor=\"middle\" class=\"tf-title\">{title}</text>" +
				$"<text x=\"{N(cx)}\" y=\"{N(cy + 15)}\" text-anchor=\"middle\" class=\"tf-sub\">{sub}</text>" +
				"</g>";
		}

		private static string N(double v) => v.ToString(CultureInfo.InvariantCulture);
	}
}
