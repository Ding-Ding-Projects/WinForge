// 小型 DOM 工具 · Tiny DOM helpers shared by all rooms.
export function el(tag, cls, txt) {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (txt != null) e.textContent = txt;
  return e;
}
export function panel(titleKey, t) {
  const p = el("div", "panel");
  const h = el("h3");
  h.textContent = t(titleKey);
  h._key = titleKey;
  p.appendChild(h);
  p._title = h;
  return p;
}

// labelled readout block
export function readout(kLabel) {
  const r = el("div", "readout");
  const k = el("div", "k", kLabel);
  const line = el("div");
  const v = el("span", "v", "—");
  const u = el("span", "u", "");
  line.appendChild(v); line.appendChild(u);
  r.appendChild(k); r.appendChild(line);
  r.setVal = (val, unit, sev) => {
    v.textContent = val;
    if (unit !== undefined) u.textContent = unit;
    v.className = "v" + (sev ? " val-" + sev : "");
  };
  r.setLabel = (s) => { k.textContent = s; };
  r._k = k;
  return r;
}

// toggle button bound to a control action
export function toggle(label, onChange) {
  const b = el("div", "toggle");
  const led = el("span", "led");
  const lab = el("span", null, label);
  b.appendChild(led); b.appendChild(lab);
  b._on = false; b._lab = lab;
  b.setState = (on, warn) => {
    b._on = on;
    b.className = "toggle" + (on ? " on" : "") + (warn ? " warn" : "");
  };
  b.onclick = () => onChange(!b._on);
  return b;
}

export function slider(min, max, step, onInput) {
  const s = el("input");
  s.type = "range"; s.min = min; s.max = max; s.step = step;
  s.oninput = () => onInput(parseFloat(s.value));
  return s;
}

export function button(label, cls, onClick) {
  const b = el("button", cls, label);
  b.onclick = onClick;
  return b;
}
