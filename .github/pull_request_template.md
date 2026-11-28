<!--
Who fills what in this PR description:
- "Human only": you, the human author, write these sections in your own words and tick these boxes yourself. AI tools and agents must never write, rewrite, or tick them.
- "Agent may fill": an agent (or you) may add facts that the diff or a command proves, each with a one-line pointer to the evidence. No narrative.
- A longer AI-written explanation goes only in a collapsed "AI-generated notes" <details> block at the end.
Maintainers can close PRs whose human sections they think were not written by a human.
See https://github.com/microsoft/PowerToys/blob/main/CONTRIBUTING.md#pull-request-descriptions-must-be-written-by-a-human
-->

## Summary
<!-- Human only. 2–5 sentences in your own words: what changed, why, and what reviewers should look at. Short is fine. -->

## What I verified
<!-- Human only. What you tried yourself and what happened, for example manual steps, scenarios, or hardware. -->

## Author checklist
<!-- Human only. Agents never tick these boxes. -->
- [ ] **Communication:** I've discussed this with core contributors already. If the work hasn't been agreed, this work might be rejected
- [ ] **Ownership:** I wrote the summary and reviewed every change in this PR

## Linked issues
<!-- Agent may fill. Only issues this change really addresses: "Closes:" for issues this PR fixes, "Relates to:" for others. One issue per line. -->
- Closes: #xxx
<!-- - Relates to: #yyy -->

## PR checklist
<!-- Agent may fill. Tick a box only when the diff or a command proves it, and add a one-line pointer to the evidence after it, for example a test result, a .resw path, or a doc path. -->
- [ ] **Tests:** Added/updated and all pass
- [ ] **Localization:** All end-user-facing strings can be localized
- [ ] **Dev docs:** Added/updated
- [ ] **New binaries:** Added on the required places
   - [ ] [JSON for signing](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ESRPSigning_core.json) for new binaries
   - [ ] [WXS for installer](https://github.com/microsoft/PowerToys/blob/main/installer/PowerToysSetup/Product.wxs) for new binaries and localization folder
   - [ ] [YML for CI pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ci/templates/build-powertoys-steps.yml) for new test projects
   - [ ] [YML for signed pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/release.yml)
- [ ] **Documentation updated:** If checked, please file a pull request on [our docs repo](https://github.com/MicrosoftDocs/windows-uwp/tree/docs/hub/powertoys) and link it here: #xxx

## Automated checks and screenshots
<!-- Agent may fill. Exact commands with their results (passed or failed), and screenshots or videos with a one-line caption. Facts only. -->

<!--
Agent, collapsed (optional). Put a longer AI-written explanation only inside a block like this:
<details>
<summary>AI-generated notes</summary>

...

</details>
-->
