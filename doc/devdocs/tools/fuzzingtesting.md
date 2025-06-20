# Fuzzing Testing in PowerToys

## Overview

Fuzzing is an automated testing technique that helps identify vulnerabilities and bugs by feeding random, invalid, or unexpected data into the application. This is especially important for PowerToys modules that handle file input/output or user input, such as Hosts File Editor, Registry Preview, and others.

PowerToys integrates Microsoft's OneFuzz service to systematically discover edge cases and unexpected behaviors that could lead to crashes or security vulnerabilities. Fuzzing testing is a requirement from the security team to ensure robust and secure modules.

## Why Fuzzing Matters

- **Security Enhancement**: Identifies potential security vulnerabilities before they reach production
- **Stability Improvement**: Discovers edge cases that might cause crashes
- **Automated Bug Discovery**: Finds bugs that traditional testing might miss
- **Reduced Manual Testing**: Automates the process of testing with unusual inputs

## Types of Fuzzing in PowerToys

PowerToys supports two types of fuzzing depending on the module's implementation language:

1. **.NET Fuzzing** - For C# modules (using [OneFuzz](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/howto/fuzzing-dotnet-code))
2. **C++ Fuzzing** - For native C++ modules using [libFuzzer](https://llvm.org/docs/LibFuzzer.html)

## Setting Up .NET Fuzzing Tests

### Step 1: Add a Fuzzing Test Project

Create a new test project within your module folder. Ensure the project name follows the format `*.FuzzTests`.

### Step 2: Configure the Project

1. Set up a `.NET 8 (Windows)` project
   - Note: OneFuzz currently supports only .NET 8 projects. The Fuzz team is working on .NET 9 support.

2. Add the required files to your fuzzing test project:
   - Create fuzzing test code
   - Add `OneFuzzConfig.json` configuration file

### Step 3: Configure OneFuzzConfig.json

The `OneFuzzConfig.json` file provides critical information for deploying fuzzing jobs. For detailed guidance, see the [OneFuzzConfig V3 Documentation](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/onefuzzconfig/onefuzzconfigv3).

```json
{
  "fuzzers": [
    {
      "name": "YourModuleFuzzer",
      "fuzzerLibrary": "libfuzzer-dotnet",
      "targetAssembly": "YourModule.FuzzTests.dll",
      "targetClass": "YourModule.FuzzTests.FuzzTestClass",
      "targetMethod": "FuzzTest",
      "FuzzingTargetBinaries": [
        "YourModule.FuzzTests.dll"
      ]
    }
  ],
  "adoTemplate": [
    {
      "AssignedTo": "PowerToys@microsoft.com",
      "jobNotificationEmail": "PowerToys@microsoft.com"
    }
  ],
  "oneFuzzJobs": [
    {
      "projectName": "PowerToys",
      "targetName": "YourModule",
      "jobDependencies": {
        "binaries": [
          "PowerToys\\x64\\Debug\\tests\\YourModule.FuzzTests\\net8.0-windows10.0.19041.0\\**"
        ]
      }
    }
  ],
  "configVersion": "3.0.0"
}
```

Key fields to update:
1. Update the `targetAssembly`, `targetClass`, `targetMethod`, and `FuzzingTargetBinaries` fields
2. Set the `AssignedTo` and `jobNotificationEmail` to your Microsoft email
3. Update the `projectName` and `targetName` fields
4. Define job dependencies pointing to your compiled fuzzing tests

### Step 4: Configure the OneFuzz Pipeline

Modify the patterns in the job steps within [job-fuzz.yml](https://github.com/microsoft/PowerToys/blob/main/.pipelines/v2/templates/job-fuzz.yml) to match your fuzzing project name:

```yaml
- download: current
  displayName: Download artifacts
  artifact: $(ArtifactName)
  patterns: |-
    **/tests/*.FuzzTests/**
```

## Setting Up C++ Fuzzing Tests

### Step 1: Create a New C++ Project

- Use the **Empty Project** template
- Name it `<ModuleName>.FuzzingTest`

### Step 2: Update Build Configuration

- In **Configuration Manager**, uncheck Build for both Release|ARM64, Debug|ARM64 and Debug|x64 configurations
- ARM64 is not supported for fuzzing tests

### Step 3: Enable ASan and libFuzzer in .vcxproj

Edit the project file to enable fuzzing:

```xml
<PropertyGroup>
  <EnableASAN>true</EnableASAN>
  <EnableFuzzer>true</EnableFuzzer>
</PropertyGroup>
```

### Step 4: Add Fuzzing Compiler Flags

Add these to `AdditionalOptions` under the `Fuzzing` configuration:

```xml
/fsanitize=address
/fsanitize-coverage=inline-8bit-counters
/fsanitize-coverage=edge
/fsanitize-coverage=trace-cmp
/fsanitize-coverage=trace-div
%(AdditionalOptions)
```

### Step 5: Link the Sanitizer Coverage Runtime

In `Linker → Input → Additional Dependencies`, add:

```text
$(VCToolsInstallDir)lib\$(Platform)\libsancov.lib
```

### Step 6: Copy Required Runtime DLL

Add a `PostBuildEvent` to copy the ASAN DLL:

```xml
<Command>
  xcopy /y "$(VCToolsInstallDir)bin\Hostx64\x64\clang_rt.asan_dynamic-x86_64.dll" "$(OutDir)"
</Command>
```

### Step 7: Add Preprocessor Definitions

To avoid annotation issues, add these to the `Preprocessor Definitions`:

```text
_DISABLE_VECTOR_ANNOTATION;_DISABLE_STRING_ANNOTATION
```

### Step 8: Implement the Entry Point

Every C++ fuzzing project must expose this function:

```cpp
extern "C" int LLVMFuzzerTestOneInput(const uint8_t* data, size_t size)
{
    std::string input(reinterpret_cast<const char*>(data), size);

    try
    {
        // Call your module with the input here
    }
    catch (...) {}

    return 0;
}
```

## Running Fuzzing Tests

### Running Locally (.NET)

To run .NET fuzzing tests locally, follow the [Running a .NET Fuzz Target Locally](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/howto/fuzzing-dotnet-code#extra-running-a-net-fuzz-target-locally) guide:

```powershell
# Instrument the assembly
.\dotnet-fuzzing-windows\sharpfuzz\SharpFuzz.CommandLine.exe path\to\YourModule.FuzzTests.dll

# Set environment variables
$env:LIBFUZZER_DOTNET_TARGET_ASSEMBLY="path\to\YourModule.FuzzTests.dll"
$env:LIBFUZZER_DOTNET_TARGET_CLASS="YourModule.FuzzTests.FuzzTestClass"
$env:LIBFUZZER_DOTNET_TARGET_METHOD="FuzzTest"

# Run the fuzzer
.\dotnet-fuzzing-windows\libfuzzer-dotnet\libfuzzer-dotnet.exe --target_path=dotnet-fuzzing-windows\LibFuzzerDotnetLoader\LibFuzzerDotnetLoader.exe
```

### Running in the Cloud

To submit a job to the OneFuzz cloud service, follow the [OneFuzz Cloud Testing Walkthrough](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/faq/notwindows/walkthrough):

1. Run the pipeline:
   - Navigate to the [fuzzing pipeline](https://microsoft.visualstudio.com/Dart/_build?definitionId=152899&view=runs)
   - Click "Run pipeline"
   - Choose your branch and start the run

2. Alternative: Use [OIP (OneFuzz Ingestion Preparation) tool](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/oip/onefuzzingestionpreparationtool):
   ```
   oip submit --config .\OneFuzzConfig.json --drop-path <your_submission_directory> --platform windows --do-not-file-bugs --duration 1
   ```
   - Use `--do-not-file-bugs` to prevent automatic bug creation during initial testing
   - `--duration` specifies the number of hours (default is 48 if not specified)

3. OneFuzz will send you an email when the job has started with a link to view results

## Reviewing Results

1. You'll receive an email notification when your fuzzing job starts
2. Click the link in the email to view the job status on the [OneFuzz Web UI](https://onefuzz-ui.microsoft.com/)
3. The OneFuzz platform will show statistics like inputs processed, coverage, and any crashes found
4. If the final status is "success," your fuzzing test is working correctly

## Current Status

PowerToys has implemented fuzzing for several modules:
- Hosts File Editor
- Registry Preview
- Fancy Zones

Modules that still need fuzzing implementation:
- Environmental Variables
- Keyboard Manager

## Requesting Access to OneFuzz

To log into the production instance of OneFuzz with the [CLI](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/howto/downloading-cli), you must request access. Visit the [OneFuzz Access Request Page](https://myaccess.microsoft.com/@microsoft.onmicrosoft.com#/access-packages/6df691eb-e3d1-444b-b4b2-9e944dc794be).

## Resources

- [OneFuzz Documentation](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/howto/fuzzing-dotnet-code)
- [OneFuzzConfig V3 Documentation](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/onefuzzconfig/onefuzzconfigv3)
- [OneFuzz Ingestion Preparation Tool](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/oip/onefuzzingestionpreparationtool)
- [OneFuzz CLI Setup Guide](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/howto/downloading-cli)
- [OneFuzz Web UI](https://onefuzz-ui.microsoft.com/)
- [libFuzzer Documentation](https://llvm.org/docs/LibFuzzer.html)
- [OneFuzz Cloud Testing Walkthrough](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/faq/notwindows/walkthrough)
