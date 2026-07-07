# Reactor Graphics Plans — Repo Status

These documents are imported planning material only. In this checkout, WinForge does not currently
include a `ReactorModule`, `ReactorSimService`, or `SimAssets/reactor/` runtime asset surface.

The paths named inside the planning pack, especially `SimAssets/reactor/`, describe a future
implementation target. Before building those graphics, create and wire the reactor module and asset
surface explicitly through the normal WinForge module pattern.

`13_REACTOR_REALISM_GAP_FILL_PLAN.md` is the bridge from the graphics pack to a future implementation.
Use it to define the first reactor runtime backlog or to audit a later merged reactor branch.

Safety boundary for any future implementation:

- Keep the feature fictional, educational, and clearly marked simulation-only.
- Use normalized states and invented simulator ranges rather than real plant setpoints.
- Do not include real emergency operating procedures, plant identifiers, security layouts, guard
  paths, cyber network details, cable routes, or real facility drawings.
- Keep bilingual English + 粵語 labels where the app surface has room.
