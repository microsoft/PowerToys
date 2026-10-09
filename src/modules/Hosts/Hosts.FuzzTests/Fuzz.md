# Create Fuzzing Tests in your .NET Code Project

This document provides a step-by-step guide for integrating fuzzing tests into your .NET project.

### Step1: Add a Fuzzing Test Project
Create a new test project within your module folder. Ensure the project name follows the format *.FuzzTests*.

### step2:  Add FuzzTests and OneFuzzConfig.json to your fuzzing test project
Follow the instructions in [Fuzz.md](https://github.com/microsoft/PowerToys/blob/main/src/modules/AdvancedPaste/AdvancedPaste.FuzzTests/Fuzz.md) from AdvancedPaste.FuzzTests to properly integrate fuzzing tests into your project.

Configuring **OneFuzzConfig.json**:
1. Update the dll, class, method, and FuzzingTargetBinaries field in the fuzzers list.
2. Modify the AssignedTo field in the adoTemplate list.
3. Set the jobNotificationEmail to your Microsoft email account.
4. Update the projectName and targetName fields in the oneFuzzJobs list.
5. Define job dependencies in the following directory:
Example:
```PowerToys\x64\Debug\tests\Hosts.FuzzTests\net8.0-windows10.0.19041.0```


# step3: Configure the OneFuzz Pipeline
Modify the patterns in the job steps within [job-fuzz.yml](https://github.com/microsoft/PowerToys/blob/main/.pipelines/v2/templates/job-fuzz.yml) to match your fuzzing project name.

Example:
```
 - download: current
        displayName: Download artifacts
        artifact: $(ArtifactName)
        patterns: |-
          **/tests/Hosts.FuzzTests/**
```


# step4:  Submit OneFuzz Pipeline and Verify Results on the OneFuzz Platform 
After executing the tests, check your email for the job link. Click the link to review the fuzzing test results.