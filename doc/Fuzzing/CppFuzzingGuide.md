# 🧪 C++ Project Fuzzing Test Guide

This guide walks you through setting up a **fuzzing test** project for a C++ module using [libFuzzer](https://llvm.org/docs/LibFuzzer.html).
.

---

## 🏗️ Step-by-Step Setup

### 1. Create a New C++ Project

- Use **Empty Project** template.
- Name it `<ModuleName>.FuzzingTest`.

---

### 2. Update Build Configuration

- In **Configuration Manager**, Uncheck Build for both Release|ARM64, Debug|ARM64 and Debug|x64 configurations.
- Note: ARM64 is not supported in this case, so leave ARM64 configurations build disabled.
---

### 3. Enable ASan and libFuzzer in `.vcxproj`

Edit the project file to enable fuzzing:

```xml
<PropertyGroup>
  <EnableASAN>true</EnableASAN>
  <EnableFuzzer>true</EnableFuzzer>
</PropertyGroup>
```

---

### 4. Add Fuzzing Compiler Flags

Add this to `AdditionalOptions` under the `Fuzzing` configuration:

```xml
/fsanitize=address
/fsanitize-coverage=inline-8bit-counters
/fsanitize-coverage=edge
/fsanitize-coverage=trace-cmp
/fsanitize-coverage=trace-div
%(AdditionalOptions)
```

---

### 5. Link the Sanitizer Coverage Runtime

In `Linker → Input → Additional Dependencies`, add:

```text
$(VCToolsInstallDir)lib\$(Platform)\libsancov.lib
```

---

### 6. Copy Required Runtime DLL

Add a `PostBuildEvent` to copy the ASAN DLL:

```xml
<Command>
  xcopy /y "$(VCToolsInstallDir)bin\Hostx64\x64\clang_rt.asan_dynamic-x86_64.dll" "$(OutDir)"
</Command>
```

---

### 7. Add Preprocessor Definitions

To avoid annotation issues, add these to the `Preprocessor Definitions`:

```text
_DISABLE_VECTOR_ANNOTATION;_DISABLE_STRING_ANNOTATION
```

---

## 🧬 Required Code

### `LLVMFuzzerTestOneInput` Entry Point

Every fuzzing project must expose this function:

```cpp
extern "C" int LLVMFuzzerTestOneInput(const uint8_t* data, size_t size)
{
    std::string input(reinterpret_cast<const char*>(data), size);

    try
    {
        // Call your module with the input here.
    }
    catch (...) {}

    return 0;
}
```

---

## ⚙️ [Test run in the cloud](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/faq/notwindows/walkthrough)

To submit a job to the cloud you can run with this command:

```
oip submit --config .\OneFuzzConfig.json --drop-path <your_submission_directory> --platform windows --do-not-file-bugs --duration 1
```
You want to run with --do-not-file-bugs because if there is an issue with running the parser in the cloud (which is very possible), you don't want bugs to be created if there is an issue. The --duration task is the number of hours you want the task to run. I recommend just running for 1 hour to make sure things work initially. If you don't specify this parameter, it will default to 48 hours. You can find more about submitting a test job here. 

OneFuzz will send you an email when the job has started.

---
