# UI-test workflows

Choose one workflow per task. Both use the shared [framework](framework-guide.md) and the
[local VM](../../ui-tests-local-vm/SKILL.md) and [CI](../../ui-tests-pipeline-ci/SKILL.md) handoff.

## Scenario 1: Create new tests

1. **Identify the expected behavior.** Start from the feature spec, regression report, or manual
   sign-off checklist. Look under the module's developer docs and test folders (e.g.
   `<Module>UITest.md`). Ask for acceptance criteria if the behavior isn't specified.
2. **Choose the project.** Add the tests to the module's existing suite when available, whether the
   feature is new or existing. Otherwise, follow [project setup](project-setup.md) for the project
   layout, DPI manifest, artifact naming, and `PowerToys.slnx` registration.
3. **Map each case to a trigger, observable signal, and assertion.** Use one `[TestMethod]` per
   coherent scenario and follow the suite's `[TestCategory]` conventions. List the expected
   readiness signal for each boundary the test crosses: Runner, Explorer, a separate window,
   renderer, compositor, or child process.
4. **Implement and verify.** Start with a focused end-to-end case, then cover the remaining
   acceptance criteria. Report any cases that still need manual verification. Do not invent an
   assertion for a signal the product doesn't expose.

| Behavior | Trigger | Assertion |
|---|---|---|
| Enabling a module starts it | Toggle it in Settings | Its process or service becomes ready without restarting Runner. |
| The activation shortcut opens an overlay | Read the Settings shortcut and send it with `KeyboardHelper` | `WindowsFinder.WaitForWindowByApp` finds the expected window. |
| A format setting changes copy output | Set the option, then activate and copy | `ClipboardHelper.WaitForText` observes the expected content. |
| Selecting or removing an item changes a list | Find and invoke the target control | The expected item becomes selected or disappears. |

Prefer stable automation IDs and existing readouts such as UIA values, clipboard data, process
state, or composed screenshots. For an otherwise unaddressable control, add
`AutomationProperties.AutomationId` as described in [patterns and pitfalls](patterns-and-pitfalls.md).
Ask before adding any other product-side test hook. Drive overlay and hotkey scenarios through
`PowerToysSettings` so Runner owns module activation.

## Scenario 2: Update or fix existing tests

1. **Establish the baseline.** Locate the failing or outdated test and its fixture or shared helper.
   Run the narrowest applicable filter on a supported local VM. For CI-only failures, compare the
   failing TRX, screenshots or video, logs, configuration, and test environment to a passing run.
2. **Find the first divergence.** Check the inputs, prerequisites, window/process ownership, action,
   and expected state in order. Decide whether the failure is a test defect, an intentional behavior
   change, a product regression, or missing infrastructure. Report a product regression rather than
   weakening its assertion or treating failed setup as a pass.
3. **Fix the responsible boundary.** Use a stable selector, authoritative readiness wait, or scoped
   fixture cleanup when the evidence points there. Change an expected outcome only if the product
   contract changed. Do not add blanket retries, arbitrary delays, or new baselines to conceal a
   failure; use the [CI stability checklist](ci-stability.md).
4. **Verify the fix and its neighbors.** Rerun the same filter against the fixed code, repeat it when
   diagnosing a flake, then widen to the full affected suite. Check other callers when changing a
   shared helper. Record the original symptom and the evidence that the fix addresses it.

## Validation for both scenarios

- Build the affected project before running tests. Confirm any new project references
  `UITestAutomation.csproj`, uses the PerMonitorV2 manifest, and is registered in `PowerToys.slnx`.
- Verify that the tests restore settings, clipboard state, and owned windows, even on failure.
- Run the focused case and then the full default and constrained suites in the
  [local VM matrix](../../ui-tests-local-vm/SKILL.md). Include applicable architectures. Preserve
  TRX, logs and failure media; use a restored baseline when clean-profile behavior matters.
- Unless explicitly limited to local work, continue through [scoped CI](../../ui-tests-pipeline-ci/SKILL.md)
  after local validation. Report a concrete infrastructure or access blocker if any gate cannot run.
