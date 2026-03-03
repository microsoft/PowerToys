# Ceremonies — Command Palette Team

> Team meetings that happen before or after work.

## Design Review

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | before |
| **Condition** | multi-agent task involving 2+ agents modifying shared systems |
| **Facilitator** | Ripley |
| **Participants** | Dallas, Parker |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. Review the task and requirements
2. Agree on interfaces and contracts between components
3. Identify risks and edge cases
4. Assign action items

---

## Retrospective

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | after |
| **Condition** | build failure, test failure, or reviewer rejection |
| **Facilitator** | Ripley |
| **Participants** | all-involved |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. What happened? (facts only)
2. Root cause analysis
3. What should change?
4. Action items for next iteration

---

## Pre-Ship Check

| Field | Value |
|-------|-------|
| **Trigger** | manual |
| **When** | before merge |
| **Condition** | user says "pre-ship" or "ready to ship?" |
| **Facilitator** | Ripley |
| **Participants** | Dallas, Parker, Lambert |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. AOT compatibility check
2. Test coverage review
3. Boundary compliance (nothing outside CommandPalette.slnf)
4. Final sign-off
