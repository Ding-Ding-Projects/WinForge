# WinForge smoke coverage ledger

Create one ledger row for every manifest item and child behavior. Use JSON,
CSV, Markdown, or a database as long as each field below is preserved.

## Required fields

| Field | Meaning |
| --- | --- |
| id | Stable manifest route, page, feature, or source-method identifier. |
| kind | route, page, control, action, service, companion, test, or source-review. |
| parentId | Owning route/page, if any. |
| source | Relative source path with relevant line or method. |
| risk | safe, stateful, external, privileged, or destructive. |
| expected | Concise expected behavior. |
| status | One of the precise states below. |
| evidence | Commands, test names, screenshot path/hash, observations, and date. |
| notes | Reproduction, exclusion reason, remediation, or follow-up. |

## Evidence dimensions

Track these independently; a route is not fully verified because one dimension
is green.

| Dimension | Sufficient evidence |
| --- | --- |
| static | Registry, navigation, type, alias, handler, and source-review relationships checked. |
| build | Current source compiles with the required command. |
| test | Named automated test/harness and result, or a justified no-harness gap. |
| launch | Page reached through its actual route without crash/blank fallback. |
| visual | Current, inspected screenshot proves the expected visual state. |
| behavior | Representative valid, invalid, and lifecycle interaction evidence. |
| sideEffect | Live operation evidence, or a documented safe non-execution disposition. |
| docs | Current docs/screenshot references were updated and inspected. |

## Allowed statuses

| Status | Use when |
| --- | --- |
| not-started | Discovered but not investigated. |
| in-progress | Work actively underway. |
| static-pass | Static route/source relationship checked only. |
| build-pass | Compiled only. |
| test-pass | Named automated test passed. |
| launch-pass | Actual route launched correctly. |
| visual-pass | Current screenshot was captured and inspected. |
| behavior-pass | Required safe interactions passed. |
| pass | All applicable dimensions are complete. |
| failed | Reproducible defect remains. |
| blocked | Environment/tooling prevents progress; include exact blocker. |
| capture-blocked | Screenshot capture specifically failed; never treat as visual-pass. |
| unsafe-without-authorization | Live execution would change user/system/external state beyond authorization. |
| not-applicable | Dimension genuinely does not apply; explain why. |

## Sample row

~~~json
{
  "id": "module.packages",
  "kind": "route",
  "source": [
    "Services/ModuleRegistry.cs",
    "MainWindow.xaml.cs",
    "Pages/PackageManagerModule.xaml"
  ],
  "risk": "stateful",
  "expected": "Package Manager resolves from navigation and renders installed/discover/operations surfaces.",
  "status": "launch-pass",
  "evidence": {
    "static": "manifest.json generated 2026-07-11",
    "launch": "driver.ps1 -Page packages; exit 0",
    "screenshot": "screenshots/packages-default.png"
  },
  "notes": "Install/uninstall commands are not executed without a disposable target and explicit scope."
}
~~~

## Screenshot naming

Use stable, descriptive names:

- <route>-default.png
- <route>-valid-result.png
- <route>-validation.png
- <route>-operations.png
- <route>-dialog-<name>.png

Store the source command/log next to each image. When replacing a canonical
documentation image, update its Markdown references and remove the superseded
asset only after the replacement is confirmed and published.
