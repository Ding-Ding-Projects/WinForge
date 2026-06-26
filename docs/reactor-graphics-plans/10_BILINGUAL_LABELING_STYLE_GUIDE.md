<!--
WinForge Reactor Graphics Planning Pack
Scope: educational / fictionalized nuclear power plant simulator graphics and UI planning.
Safety boundary: do not include real plant-specific setpoints, security layouts, cable routes,
exact emergency operating procedures, or real-world operating instructions. Use fictional values,
abstracted logic, and clearly marked simulation-only labels.
-->
# Plan 10 — Bilingual Labeling and Visual Style Guide

## Goal

Standardize how reactor graphics use English and 繁體中文／粵語 labels so the interface feels intentional, readable, and consistent across WinForge.

## Label pattern

Use this default pattern for graphic labels:

```text
English label
粵語 label
```

For tight spaces, use compact slash form:

```text
Core / 爐心
Heat Removal / 排熱
Alarm / 警報
```

## Recommended label tokens

| Concept | English | 繁體中文／粵語 |
|---|---|---|
| Reactor core | Reactor Core | 反應堆爐心 |
| Reactor vessel | Reactor Vessel | 反應堆壓力容器 |
| Steam generator | Steam Generator | 蒸汽發生器 |
| Pressurizer | Pressurizer | 穩壓器 |
| Turbine generator | Turbine Generator | 汽輪發電機 |
| Condenser | Condenser | 凝汽器 |
| Feedwater | Feedwater | 給水 |
| Containment | Containment | 安全殼 |
| Heat removal | Heat Removal | 排熱 |
| Alarm | Alarm | 警報 |
| Scenario | Scenario | 情景 |
| Historian replay | Historian Replay | 歷史回放 |
| Simulation only | Simulation Only | 只供模擬 |

## Visual hierarchy

| Level | Usage | Text treatment |
|---|---|---|
| H1 screen title | page/screen title | English first, 粵語 second |
| H2 card title | dashboard cards | compact bilingual form |
| Component label | SVG component name | two-line label if space allows |
| Tooltip | explanation | full bilingual text |
| Alarm message | active alert | short English + 粵語 summary |
| Coach text | training guidance | plain language, no procedure commands |

## Accessibility rules

- Every SVG graphic needs a `<title>` and `<desc>`.
- Use `aria-label` or accessible text mirrors for dynamic graphics.
- Do not encode critical meaning with color alone; use icons, text, or patterns.
- Provide reduced-motion mode for animated heat-flow arrows and flashing alarms.
- Keep alarm flashing subtle in demo mode.

## Example SVG label markup

```xml
<g id="reactor-vessel-label" class="component-label">
  <text id="reactor-vessel-label-en">Reactor Vessel</text>
  <text id="reactor-vessel-label-yue" dy="1.2em">反應堆壓力容器</text>
</g>
```

## Style tokens

```css
:root {
  --reactor-bg: var(--winforge-surface-bg);
  --reactor-panel: var(--winforge-card-bg);
  --reactor-text: var(--winforge-text);
  --reactor-muted: var(--winforge-text-muted);
  --reactor-normal-pattern: none;
  --reactor-watch-pattern: dashed;
  --reactor-challenged-pattern: diagonal-hatch;
  --reactor-trip-pattern: cross-hatch;
}
```

## Graphic-generation prompt

> Create a bilingual English + Cantonese label style sheet for a fictional nuclear simulator. Include component labels, alarm labels, dashboard tiles, tooltips, and accessibility states. Clean WinUI-inspired vector style. No real setpoints or real plant identifiers.

## Acceptance criteria

- Labels do not clip in English-leading or 粵語-leading mode.
- Every graphic has accessible text.
- State colors are paired with icon/pattern alternatives.
- Simulator-only labeling is always visible in screenshots.
- Terms are consistent across plant mimic, control room, rooms, and scenario cards.
