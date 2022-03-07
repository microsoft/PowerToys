## Summary of the Pull Request

**What is this about:**

**What is included in the PR:** 

**How does someone test / validate:** 

## Quality Checklist

- [ ] **Linked issue:** #xxx
- [ ] **Communication:** I've discussed this with core contributors in the issue.
- [ ] **Code quality:**
   - [ ] **Tests:** Added/updated and all pass
   - [ ] **.Net Analyzer:** Enabled and warnings fixed for new projects (See [dev docs](/doc/devdocs/readme.md#rules) for more infos.)
- [ ] **Installer:** Added/updated and all pass
- [ ] **Localization:** All end user facing strings can be localized
- [ ] **Dev docs:** Added/updated
- [ ] **User docs**:
   - [ ] No updates required
   - [ ] New PR on [Microsoft Docs](https://github.com/MicrosoftDocs/windows-uwp/tree/docs/hub/powertoys) repository: #xxx
   - [ ] New issue to do work later: #xxx (Assignee: @ xxx)
- [ ] **Binaries:** Any new files are added to WXS / Signing pipeline / YML
   - [ ] No new binaries
   - [ ] [JSON for signing](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ESRPSigning_core.json) for new binaries
   - [ ] [WXS for installer](https://github.com/microsoft/PowerToys/blob/main/installer/PowerToysSetup/Product.wxs) for new binaries
   - [ ] [YML for build pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ci/templates/build-powertoys-steps.yml) for new test projects

## Contributor License Agreement (CLA)
A CLA must be signed. If not, go over [here](https://cla.opensource.microsoft.com/microsoft/PowerToys) and sign the CLA.
