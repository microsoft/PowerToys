## Summary of the Pull Request

**What is this about:**


**How does someone test / validate:** 

## Quality Checklist

- [ ] **Linked issue:** #xxx
- [ ] **Tests:** Added/updated and all pass
- [ ] **Dev docs:** Added/updated
- [ ] **New binaries:** Added on the required places
   - [ ] No new binaries
   - [ ] [JSON for signing](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ESRPSigning_core.json) for new binaries
   - [ ] [WXS for installer](https://github.com/microsoft/PowerToys/blob/main/installer/PowerToysSetup/Product.wxs) for new binaries and localization folder
   - [ ] [YML for build pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ci/templates/build-powertoys-steps.yml) for new test projects

### Note
Please ensure that all strings can be localized and the [user docs](https://github.com/MicrosoftDocs/windows-uwp/tree/docs/hub/powertoys) are updated or you have created an issue for the user docs.

## Contributor License Agreement (CLA)
A CLA must be signed. If not, go over [here](https://cla.opensource.microsoft.com/microsoft/PowerToys) and sign the CLA.
