// ============================================================
// SnowBank docs: client-side renderer for the custom fenced blocks
//   ```fdb-fql    -> colored FQL / tuple dump
//   ```fdb-bytes  -> tuple binary-encoding strip
//   ```fdb-diff   -> before/after keyspace diff
// The tuple encoding and the FQL token rules are effectively constant,
// so the author supplies the pre-chunked bytes / text and this only
// formats them. No encoder here.
// NOTE: the FQL token colors mirror FoundationDB.Client/Query/FqlSyntaxHighlighter.cs
//       (see the block CSS in main.css). Keep them in sync if the FQL palette changes.
// ============================================================

const KINDS = ['fdb-bytes', 'fdb-diff', 'fdb-fql'];

// docfx "modern" template hooks
export default {
	start() { renderAll(); },
	configureHljs(hljs) {
		// keep highlight.js from choking on our custom languages
		KINDS.forEach(l => { try { hljs.registerLanguage(l, () => ({ name: l, contains: [] })); } catch { /* ignore */ } });
	},
};

// fallback in case the template hook is not invoked
if (typeof window !== 'undefined') window.addEventListener('load', renderAll);

function renderAll() {
	document.querySelectorAll('pre > code').forEach(code => {
		const cls = [...code.classList];
		const kind = KINDS.find(k => cls.includes('lang-' + k) || cls.includes('language-' + k));
		if (!kind) return;
		const pre = code.closest('pre');
		if (!pre || pre.dataset.fdb === '1') return;
		const dsl = code.textContent.replace(/^\n+/, '').replace(/\s+$/, '');
		const div = document.createElement('div');
		div.className = 'fdb-block';
		div.dataset.fdb = '1';
		div.innerHTML = ({ 'fdb-bytes': renderBytes, 'fdb-diff': renderDiff, 'fdb-fql': renderFql })[kind](dsl);
		pre.replaceWith(div);
	});
}

const esc = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

// ---- shared FQL colorizer (fixed token rules) ----
const RE = /(\/\/[^\n]*)|(-"(?:[^"\\]|\\.)*")|(\+"(?:[^"\\]|\\.)*")|('')|("(?:[^"\\]|\\.)*")|(<[^>]*>)|(\.\.\.)|([A-Za-z_]\w*:-?\d+)|(-?\d+)|([(),])|(=)/g;
const colorVar = v => `<span class="f-var">&lt;</span><span class="f-type">${esc(v.slice(1, -1))}</span><span class="f-var">&gt;</span>`;
const colorConst = t => { const i = t.indexOf(':'); return `<span class="f-name">${esc(t.slice(0, i))}</span><span class="f-sep">:</span><span class="f-int">${esc(t.slice(i + 1))}</span>`; };
function fql(text) {
	let out = '', last = 0, m; RE.lastIndex = 0;
	while ((m = RE.exec(text))) {
		out += esc(text.slice(last, m.index));
		if (m[1]) out += `<span class="f-cmt">${esc(m[1])}</span>`;
		else if (m[2]) out += `<span class="dt">${esc(m[2].slice(1))}</span>`;
		else if (m[3]) out += `<span class="it">${esc(m[3].slice(1))}</span>`;
		else if (m[4]) out += `<span class="f-str">''</span>`;
		else if (m[5]) out += `<span class="f-str">${esc(m[5])}</span>`;
		else if (m[6]) out += colorVar(m[6]);
		else if (m[7]) out += `<span class="f-dir">...</span>`;
		else if (m[8]) out += colorConst(m[8]);
		else if (m[9]) out += `<span class="f-int">${m[9]}</span>`;
		else if (m[10]) out += `<span class="f-sep">${m[10]}</span>`;
		else if (m[11]) out += `<span class="f-op">=</span>`;
		last = m.index + m[0].length;
	}
	return out + esc(text.slice(last));
}

// ---- fdb-fql ----
function renderFql(dsl) {
	const rows = dsl.split('\n').map(l => l.trim().length ? `<div class="fline">${fql(l)}</div>` : `<div class="fline" style="height:.55em"></div>`).join('');
	return `<div class="fql-block">${rows}</div>`;
}

// ---- fdb-bytes ----
const BTYPE = { int: 'int', str: 'str', vs: 'vs', uuid: 'uuid', dir: 'dir', bytes: 'int', bool: 'int', nil: 'int', tuple: 'str' };
function renderBytes(dsl) {
	let tuple = null; const items = [];
	for (const line of dsl.split('\n')) {
		if (!line.trim().length) continue;
		if (/^tuple\s*:/.test(line)) { tuple = line.replace(/^tuple\s*:\s*/, ''); continue; }
		let main = line, cap = '';
		const h = line.indexOf('#'); if (h >= 0) { cap = line.slice(h + 1).trim(); main = line.slice(0, h); }
		const toks = main.trim().match(/'[^']*'|<[^>]*>|\S+/g) || [];
		items.push({ type: toks.shift(), toks, cap });
	}
	let html = '<div class="bytes-card">';
	if (tuple) html += `<div class="tuple">${fql(tuple)}</div>`;
	html += '<div class="strip">';
	let blob = false;
	for (const it of items) {
		const cls = BTYPE[it.type] || 'int';
		let cells = '';
		for (const t of it.toks) {
			if (t === '...') { blob = true; cells += `<div class="cell dot">&#8230;</div>`; }
			else if (t[0] === '.') cells += `<div class="cell mk">${esc(t.slice(1))}</div>`;
			else if (t[0] === "'" && t[t.length - 1] === "'") for (const ch of t.slice(1, -1)) cells += `<div class="cell ch">'${esc(ch)}'</div>`;
			else if (t[0] === '<') { blob = true; const inner = t.slice(1, -1); const c = inner.indexOf(':'); const label = c < 0 ? inner : inner.slice(0, c); const n = c < 0 ? '' : inner.slice(c + 1); cells += `<div class="cell blob">&#10216;${esc(label)}${n ? ` &middot; ${esc(n)} bytes` : ''}&#10217;</div>`; }
			else cells += `<div class="cell">${esc(t)}</div>`;
		}
		const cap = it.cap ? `<div class="cap">${esc(it.cap).replace(/^(\S+)/, '<b>$1</b>')}</div>` : '';
		html += `<div class="grp ${cls}"><div class="cells">${cells}</div>${cap}</div>`;
	}
	html += '</div>';
	if (!blob) {
		const raw = [];
		for (const it of items) for (const t of it.toks) {
			if (t[0] === '.') raw.push(t.slice(1));
			else if (t[0] === "'") { for (const ch of t.slice(1, -1)) raw.push(ch.charCodeAt(0).toString(16).padStart(2, '0').toUpperCase()); }
			else raw.push(t);
		}
		html += `<div class="raw"><span class="lbl">raw</span>${raw.join(' ')}</div>`;
	}
	return html + '</div>';
}

// ---- fdb-diff ----
function renderDiff(dsl) {
	let title = ''; const rows = [];
	for (const raw of dsl.split('\n')) {
		if (!raw.trim().length) continue;
		if (/^title\s*:/.test(raw)) { title = raw.replace(/^title\s*:\s*/, '').trim(); continue; }
		const g = raw[0], text = raw.slice(2);
		let cls, gut;
		if (g === '-') { cls = 'del'; gut = '<span class="g-del">- </span>'; }
		else if (g === '+') { cls = 'add'; gut = '<span class="g-add">+ </span>'; }
		else if (g === '~') { cls = 'mod'; gut = '  '; }
		else { cls = 'ctx'; gut = '  '; }
		rows.push(`<div class="ln ${cls}">${gut}${fql(text)}</div>`);
	}
	return `<div class="diff">${title ? `<div class="diff-title">${esc(title)}</div>` : ''}<div class="diff-body">${rows.join('')}</div></div>`;
}
