<!--
WinForge Reactor Realism Gap Fill Plan
Scope: educational / fictionalized reactor simulator planning.
Safety boundary: do not include real plant-specific setpoints, emergency operating procedures,
security layouts, cyber implementation details, cable routes, or real plant identifiers. Use
fictional values, normalized states, and clearly marked simulation-only labels.
-->
# Plan 13 — Reactor Realism Gap Fill

## Goal

Turn the graphics plans and reference reports into a concrete realism backlog for a fictional,
educational PWR simulator. This file fills the gap between "what the control room should show" and
"what the simulator must model for those displays to be believable."

This repo currently does not contain a reactor runtime, so this plan starts from a new module
baseline. If a reactor branch is later merged in, use this file as a gap checklist against that
implementation instead.

## Realism Targets

| Area | Current planning gap | Fill-in requirement |
|---|---|---|
| Plant identity | Graphics assume a PWR but do not define the simulation baseline | Use a fictional generic four-loop PWR-inspired training plant; never clone a real station |
| Engine shape | Graphics consume state but do not define state ownership | Create a headless deterministic plant kernel with typed snapshots and command inputs |
| Physics credibility | Existing visuals mention power, feedback, xenon, boron, and heat transfer | Implement stable hybrid 0D/1D simulator behavior before adding detailed control-room polish |
| Safety systems | Safety graphics are conceptual only | Add fictional RPS/ESFAS-style state machines, latches, channel quality, and first-out events |
| HMI realism | Control-room screens need richer state than normalized tiles alone | Project plant snapshots into task-oriented overview, safety, electrical, chemistry, alarm, and trend views |
| Scenarios | Scenario cards do not define model faults or success criteria | Create deterministic scenario injectors, debrief metrics, and replay traces |
| Validation | Acceptance criteria are visual but not physics-oriented | Add headless tests, scenario golden traces, and UI alarm/trend checks |

## Engine Gap Fill

### Plant kernel

Implement the reactor module around a dependency-light core that can run without WinUI:

```text
WinForge.Reactor.Core
  PlantModel            deterministic stepper
  PlantCommand          operator/training commands
  PlantSnapshot         immutable state projection
  SensorBus             simulated instrument outputs and quality
  EventBus              alarms, trips, scenario markers, historian events
```

Minimum behavior:

- Fixed-step solver with replayable deterministic output.
- Separate solver cadence, UI projection cadence, trend logging cadence, and external API cadence.
- Snapshot save/load for scenarios and autosave.
- No direct UI dependency inside physics, protection, or scenario logic.
- All public values marked as fictional simulator values or normalized display states.

### Physics model

Use the hybrid fidelity recommended by the research docs: six-group style point kinetics concepts plus
lumped or loop-wise thermal-hydraulics. Do not attempt full-core 3D physics or CFD.

Required model gaps:

| Subsystem | Required simulator behavior | Acceptance test |
|---|---|---|
| Neutronics | Implicit/stable point-kinetics step, startup source concept, rods, boron, xenon/iodine, Doppler and moderator feedback | Power remains finite and deterministic under startup, shutdown, and scenario replay |
| Reactivity calibration | Fictional rod worth curves, boron worth, xenon equilibrium, and excess reactivity budget | Stable cold shutdown, controlled startup, stable at-power training state |
| Decay heat | Post-trip heat source that decays over time and requires heat removal | After trip, power falls but heat-removal displays remain meaningful |
| Primary thermal-hydraulics | Loop-wise cold/hot leg temperatures, average coolant temperature, flow, pressurizer pressure/level concept, subcooling margin category | Heat balance closes over steady operation and transients remain bounded |
| Secondary side | Steam generator inventory, steam pressure category, feedwater flow, turbine load, condenser/heat sink abstraction | Load changes affect heat removal with visible thermal lag |
| Fuel/hot channel | Average fuel temperature plus hot-channel margin surrogate | Margin trends degrade under heat-removal challenges without exposing real limits |
| Containment | One or two conceptual nodes for pressure, temperature, isolation, spray/fan cooling state | Containment challenge scenarios produce alarms, trends, and recovery/failed outcome states |
| Chemistry/radiation | Boron/chemistry state, leakage indicators, secondary activity concept, water quality concept | Chemistry and leakage panels have live state and scenario hooks |
| Electrical | Generator, grid support, safety buses, emergency diesel concept, DC/battery endurance category | LOOP/SBO-style training scenarios can degrade support power and recover or escalate |

### Protection and safety systems

Keep protective behavior fictional and educational. Model functions and state transitions, not real
plant actuation thresholds.

Required gaps:

- Channelized simulated sensors with quality states: `GOOD`, `STALE`, `FAILED_HIGH`, `FAILED_LOW`, `BYPASSED_TEST`.
- Training protection logic with N-of-M style voting, hysteresis, latching, reset permissives, and first-out cause capture.
- Separate protection layer from normal control layer.
- Explicit simulated safety function state for:
  - reactor trip / SCRAM state;
  - safety injection concept;
  - accumulators concept;
  - auxiliary feedwater concept;
  - residual heat removal/shutdown cooling concept;
  - containment isolation and spray/fan cooling concept;
  - emergency power and DC support concept;
  - diverse backup actuation concept.
- Visible test/bypass state in the HMI, with strong simulation-only labeling.
- No real setpoints, exact emergency steps, or plant-specific system names.

## HMI and Data Gap Fill

### Snapshot contract

The graphics plans need a stable contract. Use a schema-versioned snapshot with normalized states and
fictional values:

```json
{
  "schemaVersion": 1,
  "sequence": "monotonic simulator sequence",
  "timestampSim": "scenario clock",
  "plantMode": "COLD_SHUTDOWN | STARTUP | POWER | HOT_STANDBY | SCENARIO | TRIPPED",
  "overview": {
    "coreHeatState": "LOW | NOMINAL | RISING | HIGH",
    "primaryState": "NORMAL | WATCH | CHALLENGED | ISOLATED",
    "heatRemovalState": "AVAILABLE | REDUCED | LOST",
    "containmentState": "NORMAL | ISOLATED | CHALLENGED",
    "electricalState": "AVAILABLE | DEGRADED | LOST"
  },
  "safetyFunctions": {
    "reactivityControl": "STABLE | WATCH | CHALLENGED | SIM_TRIP",
    "coreCooling": "AVAILABLE | REDUCED | CHALLENGED",
    "heatRemoval": "AVAILABLE | REDUCED | LOST",
    "barrierIntegrity": "NORMAL | WATCH | CHALLENGED",
    "electricalSupport": "AVAILABLE | DEGRADED | LOST",
    "habitabilityConcept": "NORMAL | ISOLATED | TRAINING_EVENT"
  },
  "alarms": [],
  "events": []
}
```

Numeric internal model values may exist in the engine, but the public graphics layer should default to
normalized states, trend direction, and fictional simulator units.

### Display gaps

| Screen | Fill-in requirement |
|---|---|
| Overview wall | Plant mode, heat path, safety function strip, top alarms, key trend ribbons always visible |
| Plant mimic | Primary loop, secondary loop, containment, cooling path, electrical support, and system state overlays |
| Reactor panel | Power/rate, rod/boron/xenon contribution cards, feedback loop explanation, startup/shutdown state |
| Primary panel | Loop health, flow category, temperature categories, pressurizer pressure/level concept, subcooling category |
| Secondary/BOP panel | SG inventory, steam/feedwater mismatch, turbine/generator load, condenser/heat sink state |
| Safety panel | Protection channel health, first-out trip, latches, safety function state, test/bypass indicators |
| Electrical panel | Generator, grid support, safety bus status, EDG/DC support, load sequencing concept |
| Chemistry/radiation panel | Boron/chemistry state, leak indicators, secondary activity concept, water treatment links |
| Alarm/historian | Alarm lifecycle, first-out, acknowledge, standing/cleared states, event markers, replay scrubber |
| Scenario/debrief | Briefing, live objectives, optional coach, outcome, trend replay, concept score |

### Alarm discipline

The alarm system should be an engineered simulator feature, not a list of text strings.

Required gaps:

- Alarm lifecycle: normal -> advisory -> alarm -> acknowledged -> standing -> cleared.
- Priority classes for training display only: `INFO`, `WATCH`, `CAUTION`, `TRIP`.
- First-out cause tracking for trip/safety-function changes.
- Flood grouping by system and scenario.
- Deep links from alarm to related system view, trend, and debrief note.
- Alarm messages with English + 粵語 text and one plain-language explanation.
- No real-world alarm setpoints or operator response procedures.

## Scenario Gap Fill

Create scenarios as deterministic injectors over the plant model, not as scripted UI-only stories.

| Scenario family | Model gap covered | Safe training outcome |
|---|---|---|
| Heat path walkthrough | Primary-to-secondary-to-heat-sink energy flow | User identifies where heat is produced, transferred, and rejected |
| Controlled startup | Stable source range to low-power behavior, rod/boron/xenon effects | User observes delayed response and feedback, not real startup procedure |
| Load change | Turbine demand, steam/feedwater response, thermal lag | User links secondary changes to reactor heat balance |
| Pump degradation | Loop flow and heat-removal category changes | User diagnoses challenged cooling from trends and alarms |
| Feedwater mismatch | SG inventory and heat removal challenge | User tracks secondary-side symptoms without procedural steps |
| Turbine trip concept | Rapid load rejection and protection response | User sees trip-first-out, decay heat, and heat-removal demand |
| LOOP/SBO concept | Grid/support power degradation and emergency support state | User follows electrical support and safety function status |
| Small leak concept | Inventory/leak indicators and containment trend | User watches barrier/inventory trends and debriefs diagnosis |
| SGTR concept | Primary-to-secondary leak concept and radiation/secondary activity indicators | User identifies cross-system symptom pattern |
| Alarm flood drill | Alarm grouping, acknowledgement, and first-out logic | User practices prioritizing concepts, not real response steps |

Each scenario must define:

- Initial simulator snapshot.
- Deterministic fault injector.
- Learning objectives.
- Expected trend shapes in normalized terms.
- Alarm/event sequence.
- Success concept and debrief criteria.
- Forbidden content checklist: no real procedures, real thresholds, or plant-specific names.

## Validation Gap Fill

### Headless tests

| Test type | Required coverage |
|---|---|
| Unit tests | Reactivity terms, solver boundedness, latch/reset behavior, event ordering |
| Property tests | No negative impossible inventories, no NaN/Infinity, no contradictory state combinations |
| Steady-state tests | Cold shutdown remains stable; at-power training state stays bounded; decay heat declines after trip |
| Scenario tests | Each scenario reaches expected normalized outcome and event sequence |
| Golden traces | Curated scenario time series do not drift without an intentional baseline update |
| HMI projection tests | Snapshot-to-status mapping, alarm lifecycle display, channel quality badges |
| Replay tests | Event markers line up with trend samples and debrief summaries |

### Acceptance gates

Do not ship a "realistic" label until these pass:

1. Stable cold shutdown, startup, at-power, trip, and post-trip training traces.
2. Deterministic replay from the same seed and starting snapshot.
3. No UI-only alarm that lacks a corresponding model event.
4. No model event that lacks an HMI/debrief representation.
5. No public screen exposing real setpoints, real procedures, real plant IDs, or security details.
6. Build remains clean and reactor tests can run without launching WinUI.

## Implementation Backlog

| Issue | Title | Acceptance criteria |
|---|---|---|
| REAL-001 | Create reactor module shell and headless core boundary | Module compiles; core runs without WinUI; all screens show simulation-only labeling |
| REAL-002 | Add deterministic plant snapshot and command contract | Snapshot save/load, sequence, scenario clock, normalized overview states |
| REAL-003 | Implement stable neutronics and reactivity budget | Cold shutdown, startup, and at-power traces remain bounded and deterministic |
| REAL-004 | Close primary/secondary heat balance | Heat path trends behave consistently through load and trip scenarios |
| REAL-005 | Add decay heat and post-trip heat removal | Trip scenarios retain meaningful heat-removal challenge after power drops |
| REAL-006 | Add protection/safety state machines | Channel health, latches, first-out, reset, and simulated safety-function states visible |
| REAL-007 | Add electrical support model | Grid loss, emergency support, and DC endurance categories drive HMI and scenarios |
| REAL-008 | Add chemistry/radiation/leak model | Boron, water quality, leak, and secondary activity concepts feed panels and alarms |
| REAL-009 | Add scenario injector and replay buffer | Scenarios are deterministic, replayable, and debriefable |
| REAL-010 | Add validation suite and golden traces | Headless tests cover steady states, core scenarios, alarm lifecycle, and replay |

## Definition of Done

The reactor is "better and more realistic" when it behaves like a coherent fictional plant system:
physics, safety functions, alarms, trends, scenarios, and debrief all agree with each other. It should
feel operationally authentic while remaining explicitly educational, fictional, and non-operational.

