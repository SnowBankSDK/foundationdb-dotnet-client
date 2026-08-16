// Build-time syntax highlighting with Shiki + VS Code's "dark-plus" theme (same TextMate grammars
// VS Code uses, so the output matches the editor). Reads JSON [{id, lang, code}] on stdin, returns
// JSON [{id, html}] of static, inline-styled HTML. No client script, no runtime cost.
import { createHighlighter } from 'shiki';

const ALIAS = { 'c#': 'csharp', 'cs': 'csharp', 'console': 'bash', 'shell': 'bash', 'sh': 'bash', 'shellscript': 'bash' };
const canon = (l) => { l = (l || '').toLowerCase(); return ALIAS[l] || l; };

function readStdin() {
	return new Promise((res) => { let s = ''; process.stdin.setEncoding('utf8'); process.stdin.on('data', d => s += d); process.stdin.on('end', () => res(s)); });
}

const items = JSON.parse((await readStdin()) || '[]');
const wanted = [...new Set(items.map(i => canon(i.lang)))].filter(Boolean);
const known = new Set(['csharp', 'json', 'xml', 'bash', 'javascript', 'typescript', 'yaml', 'sql', 'html', 'css']);
const langs = wanted.filter(l => known.has(l));

const hl = await createHighlighter({ themes: ['dark-plus'], langs });
// Drop the <pre> element's inline style (its background/color); the page controls the code-block
// background via CSS. Token spans keep their inline colors (that is the syntax highlighting itself).
const stripPre = { pre(node) { delete node.properties.style; } };
const out = items.map((it) => {
	const lang = canon(it.lang);
	const use = langs.includes(lang) ? lang : 'text';
	try { return { id: it.id, html: hl.codeToHtml(it.code, { lang: use, theme: 'dark-plus', transformers: [stripPre] }) }; }
	catch (e) { return { id: it.id, error: String((e && e.message) || e) }; }
});
process.stdout.write(JSON.stringify(out));
