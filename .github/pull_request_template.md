<!-- Enter a brief description/summary of your PR here. What does it fix/what does it change/how was it tested (even manually, if necessary)? -->
## Summary of the Pull Request

<!-- Please review the items on the PR checklist before submitting-->
## PR Checklist

- [ ] Closes: #xxx
<!--  - [ ] Closes: #yyy (add separate lines for additional resolved issues) -->
- [ ] **Communication:** I've discussed this with core contributors already. If the work hasn't been agreed, this work might be rejected
- [ ] **Tests:** Added/updated and all pass
- [ ] **Localization:** All end-user-facing strings can be localized
- [ ] **Dev docs:** Added/updated
- [ ] **New binaries:** Added on the required places
   - [ ] [JSON for signing](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ESRPSigning_core.json) for new binaries
   - [ ] [WXS for installer](https://github.com/microsoft/PowerToys/blob/main/installer/PowerToysSetup/Product.wxs) for new binaries and localization folder
   - [ ] [YML for CI pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ci/templates/build-powertoys-steps.yml) for new test projects
   - [ ] [YML for signed pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/release.yml)
- [ ] **Documentation updated:** If checked, please file a pull request on [our docs repo](https://github.com/MicrosoftDocs/windows-uwp/tree/docs/hub/powertoys) and link it here: #xxx

<!-- Provide a more detailed description of the PR, other things fixed, or any additional comments/features here -->
## Detailed Description of the Pull Request / Additional comments

<!-- Describe how you validated the behavior. Add automated tests wherever possible, but list manual validation steps taken as well -->
## Validation Steps Performed

