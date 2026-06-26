<!--
WinForge Reactor Graphics Planning Pack
Scope: educational / fictionalized nuclear power plant simulator graphics and UI planning.
Safety boundary: do not include real plant-specific setpoints, security layouts, cable routes,
exact emergency operating procedures, or real-world operating instructions. Use fictional values,
abstracted logic, and clearly marked simulation-only labels.
-->
# Plan 11 — Graphics Backlog and GitHub Issue Seeds

## Goal

Convert the graphics plans into manageable issues and milestones.

## Milestone 0 — Realism foundation

These items come from `13_REACTOR_REALISM_GAP_FILL_PLAN.md`. Do them before advanced graphics if the
reactor module/runtime does not exist yet, or use them as a gap audit if a reactor branch is merged.

| Issue | Title | Acceptance criteria |
|---|---|---|
| REAL-001 | Create reactor module shell and headless core boundary | module compiles; core runs without WinUI; simulation-only label visible |
| REAL-002 | Add deterministic plant snapshot and command contract | save/load, sequence, scenario clock, normalized overview states |
| REAL-003 | Implement stable neutronics and reactivity budget | cold shutdown, startup, and at-power traces remain bounded and deterministic |
| REAL-004 | Close primary/secondary heat balance | load and trip scenarios show coherent heat-path trends |
| REAL-005 | Add decay heat and post-trip heat removal | trip scenarios retain meaningful heat-removal challenge after power falls |
| REAL-006 | Add protection/safety state machines | channel health, latches, first-out, reset, and safety-function states visible |
| REAL-007 | Add electrical support model | grid loss, emergency support, and DC categories drive HMI/scenarios |
| REAL-008 | Add chemistry/radiation/leak model | boron, water quality, leak, and activity concepts feed panels and alarms |
| REAL-009 | Add scenario injector and replay buffer | scenarios are deterministic, replayable, and debriefable |
| REAL-010 | Add validation suite and golden traces | headless tests cover steady states, scenarios, alarm lifecycle, and replay |

## Milestone 1 — Visual foundation

| Issue | Title | Acceptance criteria |
|---|---|---|
| GFX-001 | Add plant mimic v2 SVG | primary/secondary/containment/heat-path visible; bilingual labels; safe normalized status tiles |
| GFX-002 | Add reactor graphics CSS tokens | normal/watch/challenged/trip states; light/dark support; reduced motion |
| GFX-003 | Add graphics metadata schema | each SVG has id, title, area, bindings, safeMode |
| GFX-004 | Add bilingual label JSON | English + 粵語 labels for components and cards |

## Milestone 2 — Control room and alarms

| Issue | Title | Acceptance criteria |
|---|---|---|
| GFX-005 | Add overview wall graphic | wall display, plant mode strip, alarm banner, trends |
| GFX-006 | Add alarm dashboard wireframe | current/acknowledged/standing/cleared states; no real setpoints |
| GFX-007 | Add trend ribbons and event markers | normalized data series; event marker overlay |
| GFX-008 | Add historian replay graphics | timeline, event list, debrief integration |

## Milestone 3 — Facility and training

| Issue | Title | Acceptance criteria |
|---|---|---|
| GFX-009 | Add safe facility map | hub-and-spoke educational map; no real layout/security detail |
| GFX-010 | Add room cards | room illustration, purpose, related systems, scenario links |
| GFX-011 | Add scenario cards | briefing/live/debrief graphic set |
| GFX-012 | Add training coach panel | optional hints; conceptual questions only |

## Milestone 4 — Physics, fuel, water, waste

| Issue | Title | Acceptance criteria |
|---|---|---|
| GFX-013 | Add reactivity balance graphic | rod/boron/Doppler/moderator/xenon concept cards |
| GFX-014 | Add heat-balance graphic | normalized heat path and loss path |
| GFX-015 | Add fuel cycle exhibit graphic | fabrication → fresh fuel → core → spent fuel → waste concept |
| GFX-016 | Add water treatment graphic | conceptual treatment flow; no real facility specs |

## Issue template

```markdown
## Summary
Add/modify graphic: `<graphic-id>`.

## Target path
`SimAssets/reactor/svg/<area>/<file>.svg`

## Data bindings
- `bindingName`: normalized state only

## Bilingual labels
- EN:
- 粵語:

## Safety boundary
- [ ] No real setpoints
- [ ] No plant-specific identifiers
- [ ] No physical-security details
- [ ] No exact emergency operating steps
- [ ] Simulation-only label visible

## Acceptance criteria
- [ ] Renders in `index.html`
- [ ] Works in light/dark mode
- [ ] Accessible title/desc included
- [ ] Keyboard/screen-reader equivalent where interactive
- [ ] Screenshot export tested
```

## Pull request checklist

```markdown
- [ ] SVG added under `SimAssets/reactor/svg/`
- [ ] Metadata JSON updated
- [ ] Bilingual labels added
- [ ] CSS states implemented
- [ ] JS binding does not hardcode real-world limits
- [ ] Documentation screenshot updated
- [ ] Safety-boundary checklist complete
```

## Definition of done

A graphics issue is done when a user can understand the concept visually, see a bilingual label, interact with the element safely, and replay or screenshot it without exposing real-world operational details.
