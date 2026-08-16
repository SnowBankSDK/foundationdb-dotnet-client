// Build-time mermaid -> static SVG. Reads JSON [{id, code}] on stdin, launches the installed Chrome
// once (via puppeteer-core, no chromium download), renders every diagram, writes JSON [{id, svg}] to
// stdout. The .NET generator inlines the SVG so the page ships a finished diagram (no client script).
import puppeteer from 'puppeteer-core';

const CHROME = process.env.CHROME_PATH
	|| 'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe';
const MERMAID = 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';

function readStdin() {
	return new Promise((resolve) => {
		let s = '';
		process.stdin.setEncoding('utf8');
		process.stdin.on('data', (d) => (s += d));
		process.stdin.on('end', () => resolve(s));
	});
}

const items = JSON.parse((await readStdin()) || '[]');
const browser = await puppeteer.launch({ executablePath: CHROME, headless: 'new', args: ['--no-sandbox', '--disable-gpu'] });
try {
	const page = await browser.newPage();
	await page.setContent('<!doctype html><html><body></body></html>');
	const out = await page.evaluate(async (items, MERMAID) => {
		const mermaid = (await import(MERMAID)).default;
		// Size boxes with the SAME font the docs page renders the SVG text in, or labels get clipped
		// (mermaid measures text at render time; the page displays it in system-ui).
		mermaid.initialize({
			startOnLoad: false,
			securityLevel: 'loose',
			theme: 'neutral',
			// SVG <text> labels (not foreignObject HTML): they scale correctly when the responsive SVG is
			// resized, and they don't inherit the article's <p> margins. foreignObject labels clip here.
			htmlLabels: false,
			flowchart: { htmlLabels: false },
			// left-align message labels and widen the columns so a message spanning a middle lifeline
			// doesn't overlap it
			sequence: { messageAlign: 'left', actorMargin: 110, boxMargin: 8 },
			fontFamily: 'system-ui, -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
		});
		const res = [];
		for (const it of items) {
			try { const { svg } = await mermaid.render('m' + it.id, it.code); res.push({ id: it.id, svg }); }
			catch (e) { res.push({ id: it.id, error: String(e && e.message || e) }); }
		}
		return res;
	}, items, MERMAID);
	process.stdout.write(JSON.stringify(out));
} finally {
	await browser.close();
}
