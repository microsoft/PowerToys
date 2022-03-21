## Summary of the Pull Request

**What is this about:**


**How does someone test / validate:** 

## Quality Checklist

- [ ] **Linked issue:** #xxx
- [ ] **Tests:** Added/updated and all pass
- [ ] **Localization:** All end user facing strings can be localized
- [ ] **Dev docs:** Added/updated
- [ ] **New binaries:** Added on the required places
   - [ ] No new binaries
   - [ ] [JSON for signing](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ESRPSigning_core.json) for new binaries
   - [ ] [WXS for installer](https://github.com/microsoft/PowerToys/blob/main/installer/PowerToysSetup/Product.wxs) for new binaries and localization folder
   - [ ] [YML for build pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ci/templates/build-powertoys-steps.yml) for new test projects

### End-user docs
Please ensure that the [end-user documentation](https://github.com/MicrosoftDocs/windows-uwp/tree/docs/hub/powertoys) is up to date. You can create a PR in the docs repository or you can create an issue here in the PowerToys repository with the remaining tasks.

## Contributor License Agreement (CLA)
A CLA must be signed. If not, go over [here](https://cla.opensource.microsoft.com/microsoft/PowerToys) and sign the CLA.
