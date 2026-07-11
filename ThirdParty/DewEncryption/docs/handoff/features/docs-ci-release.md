# Feature Handoff: Docs, CI, Release, and Pages

## Purpose

Docs and automation explain the product, validate core flows, build release artifacts, and deploy the GitHub Pages site.

## Documentation surfaces

- `README.md`: primary user and developer guide.
- `docs/index.html` and `docs/wiki.html`: GitHub Pages content.
- `docs/screenshots/*.svg`: static visual mockups.
- `docs/handoff/`: maintainer handoff documents.

## Automation surfaces

- `.github/workflows/ci.yml`: Windows and Linux tests, compile checks, installer parsing, and smoke tests.
- `.github/workflows/installer.yml`: per-push Windows installer builds with a SHA-256 checksum.
- `.github/workflows/release.yml`: release artifacts.
- `.github/workflows/pages.yml`: GitHub Pages deployment from `docs/`.

## Change checklist

- Update screenshots and docs when adding visible UI or shell actions.
- Keep smoke tests aligned with CLI output formats and paths.
- Release packaging should include icons, installers, CLI/GUI entry points, and portable marker behavior.
