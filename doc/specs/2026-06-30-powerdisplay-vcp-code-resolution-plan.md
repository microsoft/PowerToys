# PowerDisplay Per-Feature VCP Code Resolution — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve each PowerDisplay feature to one of an ordered list of candidate VCP codes per monitor (cap-string-first, probe-as-fallback), persist the decision until the user refreshes, and show the resolved code in diagnostics.

**Architecture:** A pure, data-driven resolver (`VcpFeatureRegistry` + `VcpFeatureResolver` → `VcpFeatureCodeMap`) runs inside DDC/CI discovery. The persisted per-monitor map and the max-compat flag are pushed into `DdcCiController` before discovery (mirroring `SetMaxCompatibilityMode`). The app persists resolved maps to `monitor_state.json` and forwards them to `MonitorInfo` for the diagnostics text.

**Tech Stack:** C# (.NET 9, `net9.0-windows`), System.Text.Json source-gen (AOT), MSTest + Moq, WinUI 3 (PowerDisplay app), Settings.UI.Library.

## Global Constraints

- C# style: `src/.editorconfig` + StyleCop.Analyzers. All new files start with the MIT license header (3 comment lines) used across the repo.
- No new external/NuGet dependencies; no native P/Invoke signature changes; no ABI changes.
- JSON/IPC schema changes must be **additive only** (old files missing the new fields = "never resolved"; unknown fields ignored).
- VCP code literals live in `NativeConstants` (single source of truth). The registry composes them.
- Persistence "not supported" sentinel = `-1` (the state file serializes with `WhenWritingNull`, which would drop a `null`).
- Settings.UI.Library MUST NOT take a binary dependency on `PowerDisplay.Lib` (diagnostics text renders codes hex-only; no `VcpNames`).
- Namespaces: Lib models = `PowerDisplay.Common.Models`; Lib utils = `PowerDisplay.Common.Utils`; tests = `PowerDisplay.UnitTests`.
- **Toolchain (verified): use MSBuild + vstest, NOT `dotnet`.** PowerToys has transitive C++ `.vcxproj` deps that the .NET CLI cannot build. The solution (`PowerToys.slnx`) has already been restored (`/t:restore /p:RestorePackagesConfig=true`) and the C++ deps are warm, so incremental managed builds work in a plain shell without entering the VS dev shell. Use these absolute paths:
  - `MSBUILD = C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe`
  - `VSTEST  = C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe`
  - **Build a project:** `"$MSBUILD" <csproj> -t:build -p:Configuration=Debug -p:Platform=x64 -m -nologo -v:minimal`
  - **Run Lib unit tests** (build the test project first, then): `"$VSTEST" "src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/x64/Debug/tests/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.dll" /Platform:x64 /TestCaseFilter:"FullyQualifiedName~<ClassOrName>"` (omit `/TestCaseFilter` to run all).
  - Wherever a task step below says `dotnet build`/`dotnet test`, run the MSBuild/vstest equivalent above instead. Baseline before changes: 138 tests, 138 passed.
- Commit after every task (and after each green test in TDD tasks). Commit messages start with `[PowerDisplay]`.

---

### Task 1: Feature enum + candidate registry

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/NativeConstants.cs` (add two named codes)
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Models/VcpFeature.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Utils/VcpFeatureRegistry.cs`
- Test: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/VcpFeatureRegistryTests.cs`

**Interfaces:**
- Produces: `enum VcpFeature { Brightness, Contrast, Volume, ColorTemperature, InputSource, PowerState }`;
  `static class VcpFeatureRegistry` with `IReadOnlyList<VcpFeature> AllFeatures`,
  `IReadOnlyList<byte> Candidates(VcpFeature)`, `byte Primary(VcpFeature)`,
  `string Key(VcpFeature)`, `bool TryParseKey(string, out VcpFeature)`.

- [ ] **Step 1: Write the failing test**

Create `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/VcpFeatureRegistryTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.UnitTests;

[TestClass]
public class VcpFeatureRegistryTests
{
    [TestMethod]
    public void Brightness_HasThreeCandidatesInPriorityOrder()
    {
        CollectionAssert.AreEqual(
            new byte[] { 0x10, 0x13, 0x6B },
            new List<byte>(VcpFeatureRegistry.Candidates(VcpFeature.Brightness)));
    }

    [TestMethod]
    public void SingleCandidateFeatures_MatchTheirStandardCode()
    {
        Assert.AreEqual((byte)0x12, VcpFeatureRegistry.Primary(VcpFeature.Contrast));
        Assert.AreEqual((byte)0x62, VcpFeatureRegistry.Primary(VcpFeature.Volume));
        Assert.AreEqual((byte)0x14, VcpFeatureRegistry.Primary(VcpFeature.ColorTemperature));
        Assert.AreEqual((byte)0x60, VcpFeatureRegistry.Primary(VcpFeature.InputSource));
        Assert.AreEqual((byte)0xD6, VcpFeatureRegistry.Primary(VcpFeature.PowerState));
    }

    [TestMethod]
    public void Primary_IsFirstCandidate()
    {
        Assert.AreEqual((byte)0x10, VcpFeatureRegistry.Primary(VcpFeature.Brightness));
    }

    [TestMethod]
    public void AllFeatures_ContainsEverySixFeatures()
    {
        Assert.AreEqual(6, VcpFeatureRegistry.AllFeatures.Count);
    }

    [TestMethod]
    public void Key_RoundTripsThroughTryParseKey()
    {
        foreach (var feature in VcpFeatureRegistry.AllFeatures)
        {
            Assert.IsTrue(VcpFeatureRegistry.TryParseKey(VcpFeatureRegistry.Key(feature), out var parsed));
            Assert.AreEqual(feature, parsed);
        }
    }

    [TestMethod]
    public void TryParseKey_UnknownKey_ReturnsFalse()
    {
        Assert.IsFalse(VcpFeatureRegistry.TryParseKey("nonsense", out _));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj -c Debug --filter "FullyQualifiedName~VcpFeatureRegistryTests"`
Expected: FAIL to compile — `VcpFeature` / `VcpFeatureRegistry` do not exist.

- [ ] **Step 3: Add the two named codes to `NativeConstants.cs`**

In `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/NativeConstants.cs`, immediately after the `VcpCodeBrightness` block (after line 17), add:

```csharp
        /// <summary>
        /// VCP code: Backlight Control (0x13).
        /// Alternate brightness control used by some panels that do not implement 0x10.
        /// </summary>
        public const byte VcpCodeBacklightControl = 0x13;

        /// <summary>
        /// VCP code: Backlight Level: White (0x6B).
        /// Alternate brightness control used by some panels that expose the backlight directly.
        /// </summary>
        public const byte VcpCodeBacklightLevelWhite = 0x6B;
```

- [ ] **Step 4: Create `VcpFeature.cs`**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Logical monitor features that may map to one of several candidate VCP codes.
    /// </summary>
    public enum VcpFeature
    {
        Brightness,
        Contrast,
        Volume,
        ColorTemperature,
        InputSource,
        PowerState,
    }
}
```

- [ ] **Step 5: Create `VcpFeatureRegistry.cs`**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Common.Models;
using static PowerDisplay.Common.Drivers.NativeConstants;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Maps each <see cref="VcpFeature"/> to its ordered list of candidate VCP codes
    /// (highest priority first) and a stable persistence/diagnostic key. Candidate
    /// values come from <see cref="PowerDisplay.Common.Drivers.NativeConstants"/>.
    /// Only brightness has multiple candidates today; the rest are single-candidate.
    /// </summary>
    public static class VcpFeatureRegistry
    {
        private static readonly VcpFeature[] AllFeaturesArray =
        {
            VcpFeature.Brightness,
            VcpFeature.Contrast,
            VcpFeature.Volume,
            VcpFeature.ColorTemperature,
            VcpFeature.InputSource,
            VcpFeature.PowerState,
        };

        private static readonly Dictionary<VcpFeature, byte[]> CandidatesByFeature = new()
        {
            [VcpFeature.Brightness] = new[] { VcpCodeBrightness, VcpCodeBacklightControl, VcpCodeBacklightLevelWhite },
            [VcpFeature.Contrast] = new[] { VcpCodeContrast },
            [VcpFeature.Volume] = new[] { VcpCodeVolume },
            [VcpFeature.ColorTemperature] = new[] { VcpCodeSelectColorPreset },
            [VcpFeature.InputSource] = new[] { VcpCodeInputSource },
            [VcpFeature.PowerState] = new[] { VcpCodePowerMode },
        };

        private static readonly Dictionary<VcpFeature, string> KeysByFeature = new()
        {
            [VcpFeature.Brightness] = "brightness",
            [VcpFeature.Contrast] = "contrast",
            [VcpFeature.Volume] = "volume",
            [VcpFeature.ColorTemperature] = "colorTemperature",
            [VcpFeature.InputSource] = "inputSource",
            [VcpFeature.PowerState] = "powerState",
        };

        public static IReadOnlyList<VcpFeature> AllFeatures => AllFeaturesArray;

        public static IReadOnlyList<byte> Candidates(VcpFeature feature) => CandidatesByFeature[feature];

        public static byte Primary(VcpFeature feature) => CandidatesByFeature[feature][0];

        public static string Key(VcpFeature feature) => KeysByFeature[feature];

        public static bool TryParseKey(string key, out VcpFeature feature)
        {
            foreach (var kvp in KeysByFeature)
            {
                if (string.Equals(kvp.Value, key, StringComparison.Ordinal))
                {
                    feature = kvp.Key;
                    return true;
                }
            }

            feature = default;
            return false;
        }
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj -c Debug --filter "FullyQualifiedName~VcpFeatureRegistryTests"`
Expected: PASS (6 tests).

- [ ] **Step 7: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/Drivers/NativeConstants.cs \
        src/modules/powerdisplay/PowerDisplay.Lib/Models/VcpFeature.cs \
        src/modules/powerdisplay/PowerDisplay.Lib/Utils/VcpFeatureRegistry.cs \
        src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/VcpFeatureRegistryTests.cs
git commit -m "[PowerDisplay] Add VcpFeature enum and candidate-code registry"
```

---

### Task 2: Per-monitor resolved-code map model

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Models/VcpFeatureCodeMap.cs`
- Test: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/VcpFeatureCodeMapTests.cs`

**Interfaces:**
- Consumes: `VcpFeature`, `VcpFeatureRegistry` (Task 1).
- Produces: `sealed class VcpFeatureCodeMap` with `const int NotSupportedSentinel = -1`,
  `bool IsResolved(VcpFeature)`, `bool IsSupported(VcpFeature)`, `byte GetCode(VcpFeature)`,
  `void SetCode(VcpFeature, byte)`, `void SetNotSupported(VcpFeature)`,
  `Dictionary<string,int> ToPersisted()`, `static VcpFeatureCodeMap FromPersisted(Dictionary<string,int>?)`.

- [ ] **Step 1: Write the failing test**

Create `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/VcpFeatureCodeMapTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class VcpFeatureCodeMapTests
{
    [TestMethod]
    public void SetCode_MarksSupported_AndReturnsCode()
    {
        var map = new VcpFeatureCodeMap();
        map.SetCode(VcpFeature.Brightness, 0x6B);

        Assert.IsTrue(map.IsResolved(VcpFeature.Brightness));
        Assert.IsTrue(map.IsSupported(VcpFeature.Brightness));
        Assert.AreEqual((byte)0x6B, map.GetCode(VcpFeature.Brightness));
    }

    [TestMethod]
    public void SetNotSupported_IsResolvedButNotSupported()
    {
        var map = new VcpFeatureCodeMap();
        map.SetNotSupported(VcpFeature.Volume);

        Assert.IsTrue(map.IsResolved(VcpFeature.Volume));
        Assert.IsFalse(map.IsSupported(VcpFeature.Volume));
    }

    [TestMethod]
    public void GetCode_WhenUnresolved_FallsBackToRegistryPrimary()
    {
        var map = new VcpFeatureCodeMap();
        Assert.AreEqual((byte)0x10, map.GetCode(VcpFeature.Brightness));
    }

    [TestMethod]
    public void GetCode_WhenNotSupported_FallsBackToRegistryPrimary()
    {
        var map = new VcpFeatureCodeMap();
        map.SetNotSupported(VcpFeature.Brightness);
        Assert.AreEqual((byte)0x10, map.GetCode(VcpFeature.Brightness));
    }

    [TestMethod]
    public void ToPersisted_UsesStringKeysAndSentinel()
    {
        var map = new VcpFeatureCodeMap();
        map.SetCode(VcpFeature.Brightness, 0x6B);
        map.SetNotSupported(VcpFeature.Volume);

        var persisted = map.ToPersisted();

        Assert.AreEqual(0x6B, persisted["brightness"]);
        Assert.AreEqual(-1, persisted["volume"]);
    }

    [TestMethod]
    public void FromPersisted_RoundTripsValuesAndSentinel()
    {
        var source = new Dictionary<string, int> { ["brightness"] = 0x13, ["contrast"] = -1 };
        var map = VcpFeatureCodeMap.FromPersisted(source);

        Assert.IsTrue(map.IsSupported(VcpFeature.Brightness));
        Assert.AreEqual((byte)0x13, map.GetCode(VcpFeature.Brightness));
        Assert.IsTrue(map.IsResolved(VcpFeature.Contrast));
        Assert.IsFalse(map.IsSupported(VcpFeature.Contrast));
    }

    [TestMethod]
    public void FromPersisted_Null_ReturnsEmptyMap()
    {
        var map = VcpFeatureCodeMap.FromPersisted(null);
        Assert.IsFalse(map.IsResolved(VcpFeature.Brightness));
    }

    [TestMethod]
    public void FromPersisted_IgnoresUnknownKeys()
    {
        var map = VcpFeatureCodeMap.FromPersisted(new Dictionary<string, int> { ["future"] = 5 });
        Assert.IsFalse(map.IsResolved(VcpFeature.Brightness));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj -c Debug --filter "FullyQualifiedName~VcpFeatureCodeMapTests"`
Expected: FAIL to compile — `VcpFeatureCodeMap` does not exist.

- [ ] **Step 3: Create `VcpFeatureCodeMap.cs`**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Per-monitor resolved VCP code for each <see cref="VcpFeature"/>:
    /// a real code (0x00-0xFF), the <see cref="NotSupportedSentinel"/> (checked, no
    /// candidate worked), or absent (not yet resolved). Persisted as
    /// <c>Dictionary&lt;string,int&gt;</c> keyed by <see cref="VcpFeatureRegistry.Key"/>.
    /// </summary>
    public sealed class VcpFeatureCodeMap
    {
        /// <summary>
        /// Value stored for a feature that was checked but has no usable candidate code.
        /// A non-null sentinel is required because the monitor-state file serializes with
        /// <c>WhenWritingNull</c>, which would drop a null and lose the "checked" fact.
        /// </summary>
        public const int NotSupportedSentinel = -1;

        private readonly Dictionary<VcpFeature, int> _codes = new();

        public bool IsResolved(VcpFeature feature) => _codes.ContainsKey(feature);

        public bool IsSupported(VcpFeature feature) =>
            _codes.TryGetValue(feature, out var code) && code != NotSupportedSentinel;

        /// <summary>
        /// Returns the resolved code when supported; otherwise the registry's primary
        /// candidate as a safe default (callers gate writes on <see cref="IsSupported"/>).
        /// </summary>
        public byte GetCode(VcpFeature feature) =>
            IsSupported(feature) ? (byte)_codes[feature] : VcpFeatureRegistry.Primary(feature);

        public void SetCode(VcpFeature feature, byte code) => _codes[feature] = code;

        public void SetNotSupported(VcpFeature feature) => _codes[feature] = NotSupportedSentinel;

        public Dictionary<string, int> ToPersisted()
        {
            var result = new Dictionary<string, int>(_codes.Count);
            foreach (var kvp in _codes)
            {
                result[VcpFeatureRegistry.Key(kvp.Key)] = kvp.Value;
            }

            return result;
        }

        public static VcpFeatureCodeMap FromPersisted(Dictionary<string, int>? persisted)
        {
            var map = new VcpFeatureCodeMap();
            if (persisted == null)
            {
                return map;
            }

            foreach (var kvp in persisted)
            {
                if (VcpFeatureRegistry.TryParseKey(kvp.Key, out var feature))
                {
                    map._codes[feature] = kvp.Value;
                }
            }

            return map;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj -c Debug --filter "FullyQualifiedName~VcpFeatureCodeMapTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/Models/VcpFeatureCodeMap.cs \
        src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/VcpFeatureCodeMapTests.cs
git commit -m "[PowerDisplay] Add VcpFeatureCodeMap with persistence round-trip"
```

---

### Task 3: Pure resolver (cap-string-first, probe-as-fallback)

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Utils/VcpFeatureResolver.cs`
- Test: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/VcpFeatureResolverTests.cs`

**Interfaces:**
- Consumes: `VcpFeature`, `VcpFeatureRegistry`, `VcpFeatureCodeMap`, `VcpCapabilities`/`VcpCodeInfo`.
- Produces: `static VcpFeatureCodeMap VcpFeatureResolver.Resolve(VcpCapabilities caps, bool maxCompatibilityMode, VcpFeatureCodeMap? persisted, Func<byte,bool> probe)`.

- [ ] **Step 1: Write the failing test**

Create `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/VcpFeatureResolverTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.UnitTests;

[TestClass]
public class VcpFeatureResolverTests
{
    private static VcpCapabilities CapsWith(params byte[] codes)
    {
        var caps = new VcpCapabilities();
        foreach (var code in codes)
        {
            caps.SupportedVcpCodes[code] = new VcpCodeInfo(code, "test");
        }

        return caps;
    }

    private static Func<byte, bool> NeverProbe(List<byte> log) => code =>
    {
        log.Add(code);
        return false;
    };

    [TestMethod]
    public void Phase1_PicksFirstCandidatePresentInCaps()
    {
        var caps = CapsWith(0x10, 0x12, 0x62);
        var probeLog = new List<byte>();

        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: false, persisted: null, probe: NeverProbe(probeLog));

        Assert.AreEqual((byte)0x10, map.GetCode(VcpFeature.Brightness));
        Assert.AreEqual(0, probeLog.Count, "Normal mode must never probe.");
    }

    [TestMethod]
    public void Phase1_FallsToLowerPriorityCandidate()
    {
        // 0x10 absent, 0x6B present -> brightness resolves to 0x6B.
        var caps = CapsWith(0x6B);

        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: false, persisted: null, probe: _ => false);

        Assert.AreEqual((byte)0x6B, map.GetCode(VcpFeature.Brightness));
        Assert.IsTrue(map.IsSupported(VcpFeature.Brightness));
    }

    [TestMethod]
    public void Phase1_HonorsPriorityWhenMultiplePresent()
    {
        var caps = CapsWith(0x13, 0x6B); // both alternates present, no 0x10
        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: false, persisted: null, probe: _ => false);
        Assert.AreEqual((byte)0x13, map.GetCode(VcpFeature.Brightness));
    }

    [TestMethod]
    public void NormalMode_NoCandidate_ResolvesNotSupported_WithoutProbing()
    {
        var caps = CapsWith(0x12); // contrast only; brightness candidates absent
        var probeLog = new List<byte>();

        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: false, persisted: null, probe: NeverProbe(probeLog));

        Assert.IsFalse(map.IsSupported(VcpFeature.Brightness));
        Assert.IsTrue(map.IsResolved(VcpFeature.Brightness));
        Assert.AreEqual(0, probeLog.Count);
    }

    [TestMethod]
    public void MaxCompat_ProbesInPriorityOrder_AndStopsAtFirstSuccess()
    {
        var caps = CapsWith(); // empty: no candidate for any feature
        var probeLog = new List<byte>();
        Func<byte, bool> probe = code =>
        {
            probeLog.Add(code);
            return code == 0x6B; // only the third brightness candidate responds
        };

        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: true, persisted: null, probe: probe);

        Assert.AreEqual((byte)0x6B, map.GetCode(VcpFeature.Brightness));
        // Brightness probes happen in order 0x10, 0x13, 0x6B (stops at 0x6B).
        CollectionAssert.AreEqual(new byte[] { 0x10, 0x13, 0x6B }, probeLog.GetRange(0, 3));
    }

    [TestMethod]
    public void MaxCompat_NoCandidateResponds_ResolvesNotSupported()
    {
        var caps = CapsWith();
        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: true, persisted: null, probe: _ => false);
        Assert.IsFalse(map.IsSupported(VcpFeature.Brightness));
        Assert.IsTrue(map.IsResolved(VcpFeature.Brightness));
    }

    [TestMethod]
    public void Persisted_IsReusedVerbatim_WithoutCapsOrProbe()
    {
        var persisted = new VcpFeatureCodeMap();
        persisted.SetCode(VcpFeature.Brightness, 0x6B);
        persisted.SetNotSupported(VcpFeature.Volume);

        var probeLog = new List<byte>();
        // caps says brightness is on 0x10, but persisted (0x6B) must win.
        var map = VcpFeatureResolver.Resolve(CapsWith(0x10, 0x62), maxCompatibilityMode: true, persisted: persisted, probe: NeverProbe(probeLog));

        Assert.AreEqual((byte)0x6B, map.GetCode(VcpFeature.Brightness));
        Assert.IsFalse(map.IsSupported(VcpFeature.Volume));
        Assert.AreEqual(0, probeLog.Count, "Reused features must not be probed.");
    }

    [TestMethod]
    public void Persisted_PartialMap_ResolvesOnlyMissingFeatures()
    {
        var persisted = new VcpFeatureCodeMap();
        persisted.SetCode(VcpFeature.Brightness, 0x6B); // only brightness persisted

        // contrast resolved fresh from caps; brightness reused.
        var map = VcpFeatureResolver.Resolve(CapsWith(0x12), maxCompatibilityMode: false, persisted: persisted, probe: _ => false);

        Assert.AreEqual((byte)0x6B, map.GetCode(VcpFeature.Brightness));
        Assert.IsTrue(map.IsSupported(VcpFeature.Contrast));
        Assert.AreEqual((byte)0x12, map.GetCode(VcpFeature.Contrast));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj -c Debug --filter "FullyQualifiedName~VcpFeatureResolverTests"`
Expected: FAIL to compile — `VcpFeatureResolver` does not exist.

- [ ] **Step 3: Create `VcpFeatureResolver.cs`**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Resolves each <see cref="VcpFeature"/> to a concrete VCP code for one monitor:
    /// cap-string-first, probe-as-fallback. Pure and side-effect free — the only I/O is
    /// the caller-supplied <paramref name="probe"/> delegate, which must be a read-only
    /// VCP GET that returns true only when the code yields a usable value.
    /// </summary>
    public static class VcpFeatureResolver
    {
        public static VcpFeatureCodeMap Resolve(
            VcpCapabilities caps,
            bool maxCompatibilityMode,
            VcpFeatureCodeMap? persisted,
            Func<byte, bool> probe)
        {
            var map = new VcpFeatureCodeMap();

            foreach (var feature in VcpFeatureRegistry.AllFeatures)
            {
                // Per-feature reuse: a persisted decision (code or sentinel) wins verbatim,
                // and is neither re-derived from caps nor re-probed.
                if (persisted != null && persisted.IsResolved(feature))
                {
                    if (persisted.IsSupported(feature))
                    {
                        map.SetCode(feature, persisted.GetCode(feature));
                    }
                    else
                    {
                        map.SetNotSupported(feature);
                    }

                    continue;
                }

                var candidates = VcpFeatureRegistry.Candidates(feature);
                byte? resolved = null;

                // Phase 1 (both modes): first candidate the cap string reports as supported.
                foreach (var code in candidates)
                {
                    if (caps != null && caps.SupportsVcpCode(code))
                    {
                        resolved = code;
                        break;
                    }
                }

                // Phase 2 (max-compat only, when Phase 1 found nothing): probe in priority order.
                if (resolved == null && maxCompatibilityMode)
                {
                    foreach (var code in candidates)
                    {
                        if (probe(code))
                        {
                            resolved = code;
                            break;
                        }
                    }
                }

                if (resolved.HasValue)
                {
                    map.SetCode(feature, resolved.Value);
                }
                else
                {
                    map.SetNotSupported(feature);
                }
            }

            return map;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj -c Debug --filter "FullyQualifiedName~VcpFeatureResolverTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/Utils/VcpFeatureResolver.cs \
        src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/VcpFeatureResolverTests.cs
git commit -m "[PowerDisplay] Add VcpFeatureResolver (cap-string-first, probe fallback)"
```

---

### Task 4: Model + persistence fields and serialization

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Models/Monitor.cs` (add `ResolvedVcpCodes`)
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Models/MonitorStateEntry.cs` (add `VcpFeatureCodes`)
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Serialization/MonitorStateSerializationContext.cs` (register `Dictionary<string,int>`)
- Test: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/MonitorStateEntrySerializationTests.cs`

**Interfaces:**
- Consumes: `VcpFeatureCodeMap` (Task 2).
- Produces: `Monitor.ResolvedVcpCodes` (`VcpFeatureCodeMap`, defaults to empty);
  `MonitorStateEntry.VcpFeatureCodes` (`Dictionary<string,int>?`, JSON `vcpFeatureCodes`).

- [ ] **Step 1: Write the failing test**

Create `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/MonitorStateEntrySerializationTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Serialization;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MonitorStateEntrySerializationTests
{
    [TestMethod]
    public void VcpFeatureCodes_RoundTripsThroughStateContext()
    {
        var entry = new MonitorStateEntry
        {
            Brightness = 75,
            VcpFeatureCodes = new Dictionary<string, int> { ["brightness"] = 0x6B, ["volume"] = -1 },
        };

        var json = JsonSerializer.Serialize(entry, MonitorStateSerializationContext.Default.MonitorStateEntry);
        var roundTripped = JsonSerializer.Deserialize(json, MonitorStateSerializationContext.Default.MonitorStateEntry);

        Assert.IsNotNull(roundTripped);
        Assert.IsNotNull(roundTripped!.VcpFeatureCodes);
        Assert.AreEqual(0x6B, roundTripped.VcpFeatureCodes!["brightness"]);
        Assert.AreEqual(-1, roundTripped.VcpFeatureCodes["volume"]);
        StringAssert.Contains(json, "vcpFeatureCodes");
    }

    [TestMethod]
    public void NullVcpFeatureCodes_OmittedFromJson()
    {
        var entry = new MonitorStateEntry { Brightness = 50 };
        var json = JsonSerializer.Serialize(entry, MonitorStateSerializationContext.Default.MonitorStateEntry);
        Assert.IsFalse(json.Contains("vcpFeatureCodes"), "Null map must be omitted (WhenWritingNull).");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj -c Debug --filter "FullyQualifiedName~MonitorStateEntrySerializationTests"`
Expected: FAIL to compile — `MonitorStateEntry.VcpFeatureCodes` does not exist.

- [ ] **Step 3: Add `VcpFeatureCodes` to `MonitorStateEntry.cs`**

In `src/modules/powerdisplay/PowerDisplay.Lib/Models/MonitorStateEntry.cs`, after the `CapabilitiesRaw` property (after line 44), add:

```csharp
        /// <summary>
        /// Gets or sets the resolved feature-to-VCP-code map (JSON key = feature name,
        /// value = VCP code, or <c>-1</c> = checked-but-unsupported). <c>null</c> when the
        /// monitor's features have never been resolved.
        /// </summary>
        [JsonPropertyName("vcpFeatureCodes")]
        public Dictionary<string, int>? VcpFeatureCodes { get; set; }
```

Add `using System.Collections.Generic;` to the file's usings (after `using System;`).

- [ ] **Step 4: Register `Dictionary<string,int>` in `MonitorStateSerializationContext.cs`**

In `src/modules/powerdisplay/PowerDisplay.Lib/Serialization/MonitorStateSerializationContext.cs`, add after line 22 (`[JsonSerializable(typeof(Dictionary<string, MonitorStateEntry>))]`):

```csharp
    [JsonSerializable(typeof(Dictionary<string, int>))]
```

- [ ] **Step 5: Add `ResolvedVcpCodes` to `Monitor.cs`**

In `src/modules/powerdisplay/PowerDisplay.Lib/Models/Monitor.cs`, after the `VcpCapabilitiesInfo` property (after line 265), add:

```csharp
        /// <summary>
        /// Gets or sets the per-feature resolved VCP code map for this monitor, produced by
        /// <see cref="PowerDisplay.Common.Utils.VcpFeatureResolver"/> during discovery and
        /// persisted via <c>MonitorStateManager</c>.
        /// </summary>
        public VcpFeatureCodeMap ResolvedVcpCodes { get; set; } = new();
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj -c Debug --filter "FullyQualifiedName~MonitorStateEntrySerializationTests"`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/Models/Monitor.cs \
        src/modules/powerdisplay/PowerDisplay.Lib/Models/MonitorStateEntry.cs \
        src/modules/powerdisplay/PowerDisplay.Lib/Serialization/MonitorStateSerializationContext.cs \
        src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/MonitorStateEntrySerializationTests.cs
git commit -m "[PowerDisplay] Persist resolved VCP codes in monitor state"
```

---

### Task 5: MonitorStateManager get/update/clear APIs

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Services/MonitorStateManager.cs`

**Interfaces:**
- Consumes: `VcpFeatureCodeMap`, `MonitorStateEntry.VcpFeatureCodes` (Tasks 2, 4).
- Produces: `VcpFeatureCodeMap? GetVcpCodeMap(string monitorId)`,
  `void UpdateVcpCodeMap(string monitorId, VcpFeatureCodeMap map)`,
  `void ClearAllVcpCodeMaps()`.

> No unit test: the manager's constructor reads/writes the real `monitor_state.json`
> under `%LOCALAPPDATA%` and the existing `Get/UpdateMonitorParameter` methods are
> likewise untested. The serialization is covered by Task 4; correctness here is
> verified by the Task 9 build + manual run. (House pattern: no MonitorStateManager tests.)

- [ ] **Step 1: Add `VcpFeatureCodes` to the internal `MonitorState` class**

In `MonitorState` (around line 39-50), add after `CapabilitiesRaw`:

```csharp
            public Dictionary<string, int>? VcpFeatureCodes { get; set; }
```

- [ ] **Step 2: Carry the field through `CloneState`**

In `CloneState` (around line 217-224), add after `CapabilitiesRaw = s.CapabilitiesRaw,`:

```csharp
            VcpFeatureCodes = s.VcpFeatureCodes,
```

- [ ] **Step 3: Load the field in `LoadStateFromDisk`**

In `LoadStateFromDisk` (around line 248-255), add to the `new MonitorState { ... }` initializer after `CapabilitiesRaw = entry.CapabilitiesRaw,`:

```csharp
                            VcpFeatureCodes = entry.VcpFeatureCodes,
```

- [ ] **Step 4: Serialize the field in `BuildStateJson`**

In `BuildStateJson` (around line 329-337), add to the `new MonitorStateEntry { ... }` initializer after `CapabilitiesRaw = state.CapabilitiesRaw,`:

```csharp
                    VcpFeatureCodes = state.VcpFeatureCodes,
```

- [ ] **Step 5: Add the three public methods**

Add after `GetMonitorParameters` (after line 147):

```csharp
        /// <summary>
        /// Gets the persisted resolved feature-to-VCP-code map for a monitor, or <c>null</c>
        /// if it has never been resolved.
        /// </summary>
        public VcpFeatureCodeMap? GetVcpCodeMap(string monitorId)
        {
            if (string.IsNullOrEmpty(monitorId))
            {
                return null;
            }

            if (_states.TryGetValue(monitorId, out var state) && state.VcpFeatureCodes != null)
            {
                return VcpFeatureCodeMap.FromPersisted(state.VcpFeatureCodes);
            }

            return null;
        }

        /// <summary>
        /// Persists the resolved feature-to-VCP-code map for a monitor. Idempotent: skips the
        /// write when the serialized map is unchanged, so re-running discovery does not churn disk.
        /// </summary>
        public void UpdateVcpCodeMap(string monitorId, VcpFeatureCodeMap map)
        {
            if (string.IsNullOrEmpty(monitorId) || map == null)
            {
                return;
            }

            try
            {
                var persisted = map.ToPersisted();
                var state = _states.GetOrAdd(monitorId, _ => new MonitorState());
                if (DictionariesEqual(state.VcpFeatureCodes, persisted))
                {
                    return;
                }

                state.VcpFeatureCodes = persisted;
                _isDirty = true;
                _saveDebouncer.Debounce(SaveStateToDiskAsync);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update VCP code map for monitorId '{monitorId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Clears every monitor's resolved feature-to-VCP-code map (leaving brightness/contrast/
        /// volume/color values intact), forcing a fresh resolution on the next discovery. Called
        /// by the user-initiated Refresh path.
        /// </summary>
        public void ClearAllVcpCodeMaps()
        {
            bool changed = false;
            foreach (var state in _states.Values)
            {
                if (state.VcpFeatureCodes != null)
                {
                    state.VcpFeatureCodes = null;
                    changed = true;
                }
            }

            if (changed)
            {
                _isDirty = true;
                _saveDebouncer.Debounce(SaveStateToDiskAsync);
            }
        }

        private static bool DictionariesEqual(Dictionary<string, int>? a, Dictionary<string, int> b)
        {
            if (a == null || a.Count != b.Count)
            {
                return false;
            }

            foreach (var kvp in b)
            {
                if (!a.TryGetValue(kvp.Key, out var v) || v != kvp.Value)
                {
                    return false;
                }
            }

            return true;
        }
```

Ensure `using PowerDisplay.Common.Utils;` is present (for `VcpFeatureCodeMap` — it is in `PowerDisplay.Common.Models`, already imported via `PowerDisplay.Common.Models`; no new using needed). Add `using PowerDisplay.Common.Models;` if not already present (it is).

- [ ] **Step 6: Build to verify**

Run: `dotnet build src/modules/powerdisplay/PowerDisplay.Lib/PowerDisplay.Lib.csproj -c Debug`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/Services/MonitorStateManager.cs
git commit -m "[PowerDisplay] Add get/update/clear APIs for resolved VCP code maps"
```

---

### Task 6: DdcCiController resolves and uses per-feature codes

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`

**Interfaces:**
- Consumes: `VcpFeatureResolver`, `VcpFeatureCodeMap`, `VcpFeature`, `Monitor.ResolvedVcpCodes`.
- Produces: `DdcCiController.PersistedVcpCodeMaps` (`IReadOnlyDictionary<string, VcpFeatureCodeMap>`,
  settable, default empty). Sets `monitor.ResolvedVcpCodes` during discovery; reads/writes each
  feature through the resolved code.

- [ ] **Step 1: Add the persisted-map property**

After the `MaxCompatibilityMode` property (after line 45), add:

```csharp
        /// <summary>
        /// Gets or sets the per-monitor persisted resolved-code maps (keyed by <c>Monitor.Id</c>),
        /// pushed by <see cref="MonitorManager"/> before each discovery. A monitor present here
        /// reuses its persisted decision instead of re-resolving/probing. Default: empty.
        /// </summary>
        public IReadOnlyDictionary<string, VcpFeatureCodeMap> PersistedVcpCodeMaps { get; set; }
            = new Dictionary<string, VcpFeatureCodeMap>(MonitorIdComparer.Instance);
```

Add `using PowerDisplay.Common.Utils;` if not already imported (it is, per existing usings).

- [ ] **Step 2: Resolve codes in `BuildMonitorFromPhysical` (ordering is load-bearing)**

In `BuildMonitorFromPhysical`, replace the block:

```csharp
                monitor.VcpCapabilitiesInfo = caps;
                UpdateMonitorCapabilitiesFromVcp(monitor, caps);
```

with:

```csharp
                monitor.VcpCapabilitiesInfo = caps;

                // Resolve each feature to a concrete VCP code BEFORE capability flags and
                // value initialization, both of which now read monitor.ResolvedVcpCodes.
                PersistedVcpCodeMaps.TryGetValue(monitor.Id, out var persistedCodeMap);
                monitor.ResolvedVcpCodes = VcpFeatureResolver.Resolve(
                    caps,
                    MaxCompatibilityMode,
                    persistedCodeMap,
                    code => TryGetVcpFeature(physical.HPhysicalMonitor, code, monitor.Id, out _, out var max) && max > 0);

                UpdateMonitorCapabilitiesFromVcp(monitor, caps);
```

- [ ] **Step 3: Derive brightness support from the resolved map**

In `UpdateMonitorCapabilitiesFromVcp`, replace the brightness block:

```csharp
            // Check for Brightness support (VCP 0x10)
            if (vcpCaps.SupportsVcpCode(VcpCodeBrightness))
            {
                monitor.Capabilities |= MonitorCapabilities.Brightness;
            }
```

with:

```csharp
            // Brightness support derives from the resolved map so a monitor that exposes
            // brightness only via an alternate code (0x13/0x6B) is still recognized.
            if (monitor.ResolvedVcpCodes.IsSupported(VcpFeature.Brightness))
            {
                monitor.Capabilities |= MonitorCapabilities.Brightness;
            }
```

(Contrast/Volume/ColorTemperature blocks in this method stay unchanged — single-candidate, cap-string based.)

- [ ] **Step 4: Use the resolved code in brightness init**

In `InitializeBrightness`, replace `TryGetVcpFeature(handle, VcpCodeBrightness, monitor.Id, ...)` with the resolved code. Change the method to read the code from the monitor:

```csharp
        private static void InitializeBrightness(Monitor monitor, IntPtr handle)
        {
            var code = monitor.ResolvedVcpCodes.GetCode(VcpFeature.Brightness);
            if (TryGetVcpFeature(handle, code, monitor.Id, out uint current, out uint max))
            {
                var brightnessInfo = new VcpFeatureValue((int)current, 0, (int)max);
                if (!brightnessInfo.IsValid)
                {
                    Logger.LogWarning(
                        $"DDC: [{monitor.Id}] Ignoring invalid brightness range current={current}, max={max}");
                    return;
                }

                monitor.BrightnessVcpMax = (int)max;
                monitor.CurrentBrightness = brightnessInfo.ToPercentage();
            }
        }
```

- [ ] **Step 5: Use resolved codes in the get/set/init for every feature**

Replace each hardcoded `NativeConstants` code with the resolved code:

In `GetBrightnessAsync`: `return await GetVcpFeatureAsync(monitor, monitor.ResolvedVcpCodes.GetCode(VcpFeature.Brightness), cancellationToken);`

In `SetBrightnessAsync`: change to
```csharp
            var raw = VcpFeatureValue.FromPercentage(brightness, monitor.BrightnessVcpMax);
            return SetVcpFeatureAsync(monitor, monitor.ResolvedVcpCodes.GetCode(VcpFeature.Brightness), raw, cancellationToken);
```

In `GetContrastAsync` / `SetContrastAsync`: use `monitor.ResolvedVcpCodes.GetCode(VcpFeature.Contrast)`.
In `GetVolumeAsync` / `SetVolumeAsync`: use `monitor.ResolvedVcpCodes.GetCode(VcpFeature.Volume)`.
In `GetColorTemperatureAsync` / `SetColorTemperatureAsync`: use `monitor.ResolvedVcpCodes.GetCode(VcpFeature.ColorTemperature)`.
In `GetInputSourceAsync` / `SetInputSourceAsync`: use `monitor.ResolvedVcpCodes.GetCode(VcpFeature.InputSource)`.
In `GetPowerStateAsync` / `SetPowerStateAsync`: use `monitor.ResolvedVcpCodes.GetCode(VcpFeature.PowerState)`.

In `InitializeContrast`: `var code = monitor.ResolvedVcpCodes.GetCode(VcpFeature.Contrast);` then `TryGetVcpFeature(handle, code, ...)`.
In `InitializeVolume`: `var code = monitor.ResolvedVcpCodes.GetCode(VcpFeature.Volume);` then `TryGetVcpFeature(handle, code, ...)`.
In `InitializeColorTemperature`: `var code = monitor.ResolvedVcpCodes.GetCode(VcpFeature.ColorTemperature);` then `TryGetVcpFeature(handle, code, ...)`.
In `InitializeInputSource`: `var code = monitor.ResolvedVcpCodes.GetCode(VcpFeature.InputSource);` then `TryGetVcpFeature(handle, code, ...)`.
In `InitializePowerState`: `var code = monitor.ResolvedVcpCodes.GetCode(VcpFeature.PowerState);` then `TryGetVcpFeature(handle, code, ...)`.

> For single-candidate features the resolved code equals the former constant, so behavior is identical;
> the substitution keeps one uniform code path and makes the diagnostics/persistence honest.

- [ ] **Step 6: Build to verify**

Run: `dotnet build src/modules/powerdisplay/PowerDisplay.Lib/PowerDisplay.Lib.csproj -c Debug`
Expected: Build succeeded, 0 errors. (Unused `NativeConstants.VcpCode*` usings/`static` may remain — that is fine.)

- [ ] **Step 7: Run the full Lib test suite (regression guard)**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj -c Debug`
Expected: PASS (all existing + new tests).

- [ ] **Step 8: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs
git commit -m "[PowerDisplay] Resolve and use per-feature VCP codes in DDC controller"
```

---

### Task 7: App orchestration — push before, persist after, clear on refresh

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/Helpers/MonitorManager.cs` (pass-through)
- Modify: `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.Monitors.cs` (push + persist + clear)
- Modify: `src/modules/powerdisplay/PowerDisplay/ViewModels/MonitorViewModel.cs` (expose `ResolvedVcpCodes`)

**Interfaces:**
- Consumes: `MonitorStateManager.GetVcpCodeMap/UpdateVcpCodeMap/ClearAllVcpCodeMaps`,
  `DdcCiController.PersistedVcpCodeMaps`, `Monitor.ResolvedVcpCodes`.
- Produces: `MonitorManager.SetPersistedVcpCodeMaps(IReadOnlyDictionary<string, VcpFeatureCodeMap>)`;
  `MonitorViewModel.ResolvedVcpCodes` (`VcpFeatureCodeMap`).

- [ ] **Step 1: Add the pass-through to `MonitorManager.cs`**

After `SetMaxCompatibilityMode` (after line 88), add:

```csharp
        /// <summary>
        /// Pushes the per-monitor persisted resolved-code maps onto the DDC/CI controller before
        /// discovery so already-resolved monitors are reused without re-probing. No-op if the DDC
        /// controller failed to initialize.
        /// </summary>
        public void SetPersistedVcpCodeMaps(IReadOnlyDictionary<string, VcpFeatureCodeMap> maps)
        {
            if (_ddcController != null)
            {
                _ddcController.PersistedVcpCodeMaps = maps;
            }
        }
```

Add `using PowerDisplay.Common.Utils;` to the file if not present (for `VcpFeatureCodeMap` — it lives in `PowerDisplay.Common.Models`, already imported). No new using needed.

- [ ] **Step 2: Expose `ResolvedVcpCodes` on `MonitorViewModel.cs`**

Next to the existing `CapabilitiesRaw => _monitor.CapabilitiesRaw;` accessor (around line 283), add:

```csharp
    public VcpFeatureCodeMap ResolvedVcpCodes => _monitor.ResolvedVcpCodes;
```

Ensure `using PowerDisplay.Common.Models;` is present (it is — `Monitor` is referenced).

- [ ] **Step 3: Build a helper to push persisted maps before discovery**

In `MainViewModel.Monitors.cs`, add a private helper (after `InitializeAsync`, before `CompleteInitializationAsync`):

```csharp
    /// <summary>
    /// Builds the per-monitor persisted resolved-code maps from the state manager and pushes
    /// them onto the controller stack ahead of discovery. Empty maps force fresh resolution.
    /// </summary>
    private void PushPersistedVcpCodeMaps()
    {
        var maps = new Dictionary<string, VcpFeatureCodeMap>(MonitorIdComparer.Instance);
        foreach (var existing in _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName).Properties.Monitors)
        {
            if (string.IsNullOrEmpty(existing.Id))
            {
                continue;
            }

            var map = _stateManager.GetVcpCodeMap(existing.Id);
            if (map != null)
            {
                maps[existing.Id] = map;
            }
        }

        _monitorManager.SetPersistedVcpCodeMaps(maps);
    }
```

Add `using PowerDisplay.Common.Utils;` and `using PowerDisplay.Common.Models;` if missing (the file already imports `PowerDisplay.Common.Models`; add `PowerDisplay.Common.Utils`). `MonitorIdComparer` lives in `PowerDisplay.Models` — already imported via `using PowerDisplay.Models;`.

> Note: `GetVcpCodeMap` keys are the monitor `Id`s. Iterating known settings monitors is sufficient
> because `monitor_state.json` and `settings.json` share the same `Id` keys; any monitor not yet in
> settings simply has no persisted map and resolves fresh.

- [ ] **Step 4: Call the push in both discovery entry points**

In `InitializeAsync`, immediately after `_monitorManager.SetMaxCompatibilityMode(settings.Properties.MaxCompatibilityMode);` (line 33), add:

```csharp
            PushPersistedVcpCodeMaps();
```

In `RefreshMonitorsAsync`, replace:

```csharp
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            _monitorManager.SetMaxCompatibilityMode(settings.Properties.MaxCompatibilityMode);
```

with:

```csharp
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            _monitorManager.SetMaxCompatibilityMode(settings.Properties.MaxCompatibilityMode);

            // User-initiated Refresh: drop persisted resolved-code maps so every feature is
            // re-resolved from scratch (the only invalidation path for the resolved codes).
            _stateManager.ClearAllVcpCodeMaps();
            PushPersistedVcpCodeMaps();
```

> The display-change watcher path (`OnDisplayChanging` → `RefreshMonitorsAsync(skipScanningCheck: true)`)
> also flows through here. If preserving persisted maps on *watcher-triggered* refresh is desired, gate
> the `ClearAllVcpCodeMaps()` call on `!skipScanningCheck`. Confirm intent during implementation; the
> spec's requirement is that the **user-initiated** Refresh clears. Default in this plan: gate on
> `!skipScanningCheck` so only the user button clears.

Apply the gate now — change the inserted lines to:

```csharp
            // User-initiated Refresh only (not the display-change watcher): drop persisted
            // resolved-code maps so every feature is re-resolved from scratch.
            if (!skipScanningCheck)
            {
                _stateManager.ClearAllVcpCodeMaps();
            }

            PushPersistedVcpCodeMaps();
```

- [ ] **Step 5: Persist resolved maps after discovery in `UpdateMonitorList`**

In `UpdateMonitorList`, after the `foreach (var monitor in monitors)` loop that builds VMs and before/after `SaveMonitorsToSettings();` (around line 171), add a persistence loop. Insert immediately before `SaveMonitorsToSettings();`:

```csharp
        // Persist the freshly resolved feature->code maps (idempotent; debounced).
        foreach (var monitor in monitors)
        {
            if (!string.IsNullOrEmpty(monitor.Id))
            {
                _stateManager.UpdateVcpCodeMap(monitor.Id, monitor.ResolvedVcpCodes);
            }
        }
```

- [ ] **Step 6: Build to verify**

Run: `dotnet build src/modules/powerdisplay/PowerDisplay/PowerDisplay.csproj -c Debug -p:Platform=x64`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay/Helpers/MonitorManager.cs \
        src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.Monitors.cs \
        src/modules/powerdisplay/PowerDisplay/ViewModels/MonitorViewModel.cs
git commit -m "[PowerDisplay] Wire persisted VCP code maps through discovery/refresh"
```

---

### Task 8: Diagnostics — surface resolved codes

**Files:**
- Modify: `src/settings-ui/Settings.UI.Library/MonitorInfo.cs` (field + UpdateFrom + diagnostics section)
- Modify: `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.Settings.cs` (`CreateMonitorInfo` populates it)
- Modify: `src/modules/powerdisplay/PowerDisplay/Serialization/JsonSourceGenerationContext.cs` (register dict)
- Modify: `src/settings-ui/Settings.UI.Library/SettingsSerializationContext.cs` (register dict)
- Modify: `src/settings-ui/Settings.UI/SerializationContext/SourceGenerationContextContext.cs` (register dict)

**Interfaces:**
- Consumes: `Monitor.ResolvedVcpCodes`/`VcpFeatureCodeMap.ToPersisted()`, `MonitorViewModel.ResolvedVcpCodes`.
- Produces: `MonitorInfo.ResolvedVcpCodes` (`Dictionary<string,int>`, JSON `resolvedVcpCodes`), and a
  "Resolved feature -> VCP code" section in `GetDiagnosticsAsText()`.

- [ ] **Step 1: Add the `ResolvedVcpCodes` property to `MonitorInfo.cs`**

Add a backing field near the other fields (after line 35, `_vcpCodesFormatted`):

```csharp
        private Dictionary<string, int> _resolvedVcpCodes = new Dictionary<string, int>();
```

Add the property after `VcpCodesFormatted` (after line 388):

```csharp
        /// <summary>
        /// Gets or sets the resolved feature-to-VCP-code map (feature name -> code, or -1 =
        /// checked-but-unsupported). Written by the PowerDisplay app; shown in diagnostics.
        /// </summary>
        [JsonPropertyName("resolvedVcpCodes")]
        public Dictionary<string, int> ResolvedVcpCodes
        {
            get => _resolvedVcpCodes;
            set
            {
                _resolvedVcpCodes = value ?? new Dictionary<string, int>();
                OnPropertyChanged();
            }
        }
```

- [ ] **Step 2: Copy it in `UpdateFrom`**

In `UpdateFrom` (after `VcpCodesFormatted = other.VcpCodesFormatted;`, line 751), add:

```csharp
            ResolvedVcpCodes = other.ResolvedVcpCodes;
```

- [ ] **Step 3: Render the resolved section in `GetDiagnosticsAsText()`**

In `GetDiagnosticsAsText()`, insert between the "Detected support" block and the "Raw capabilities"
block (after line 711, the `lines.Add(string.Empty);` that follows the support flags), add:

```csharp
            lines.Add("Resolved feature -> VCP code");
            lines.Add(new string('-', 50));
            foreach (var (key, label) in DiagnosticFeatureLabels)
            {
                if (_resolvedVcpCodes.TryGetValue(key, out var code))
                {
                    lines.Add(code < 0 ? $"{label}: not supported" : $"{label}: 0x{code:X2}");
                }
                else
                {
                    lines.Add($"{label}: not resolved");
                }
            }

            lines.Add(string.Empty);
```

Add this static label list as a field on `MonitorInfo` (after the `_resolvedVcpCodes` field). It is a
self-contained display concern; the JSON keys MUST match `VcpFeatureRegistry.Key` in PowerDisplay.Lib
(convention coupling — Settings.UI.Library cannot reference .Lib):

```csharp
        // Display order + labels for the diagnostics "Resolved feature" section. Keys must match
        // PowerDisplay.Lib VcpFeatureRegistry.Key(...) (Settings.UI.Library must not depend on .Lib).
        private static readonly (string Key, string Label)[] DiagnosticFeatureLabels =
        {
            ("brightness", "Brightness"),
            ("contrast", "Contrast"),
            ("volume", "Volume"),
            ("colorTemperature", "Color temperature"),
            ("inputSource", "Input source"),
            ("powerState", "Power state"),
        };
```

- [ ] **Step 4: Populate it in `CreateMonitorInfo` (`MainViewModel.Settings.cs`)**

In `CreateMonitorInfo`, add to the `MonitorInfo` initializer (after `VcpCodesFormatted = ...`, around line 507):

```csharp
            ResolvedVcpCodes = vm.ResolvedVcpCodes.ToPersisted(),
```

- [ ] **Step 5: Register `Dictionary<string,int>` in the three serialization contexts**

In `src/modules/powerdisplay/PowerDisplay/Serialization/JsonSourceGenerationContext.cs`, add after
`[JsonSerializable(typeof(List<ProfileMonitorSetting>))]` (line 36):

```csharp
    [JsonSerializable(typeof(Dictionary<string, int>))]
```

In `src/settings-ui/Settings.UI.Library/SettingsSerializationContext.cs`, add near the `MonitorInfo`
registration (after line 146):

```csharp
    [JsonSerializable(typeof(Dictionary<string, int>))]
```

In `src/settings-ui/Settings.UI/SerializationContext/SourceGenerationContextContext.cs`, add near the
`PowerDisplaySettings` registration (after line 38):

```csharp
[JsonSerializable(typeof(Dictionary<string, int>))]
```

(If any context already declares `Dictionary<string,int>`, skip that one — duplicate `[JsonSerializable]`
of the same type is a compile error.)

- [ ] **Step 6: Build Settings UI + PowerDisplay to verify source-gen**

Run:
```
dotnet build src/settings-ui/Settings.UI.Library/Settings.UI.Library.csproj -c Debug -p:Platform=x64
dotnet build src/modules/powerdisplay/PowerDisplay/PowerDisplay.csproj -c Debug -p:Platform=x64
```
Expected: Build succeeded, 0 errors. If a source-gen analyzer warns that `Dictionary<string,int>`
isn't AOT-serializable, that indicates a missing registration in Step 5 — add it where flagged.

- [ ] **Step 7: Commit**

```bash
git add src/settings-ui/Settings.UI.Library/MonitorInfo.cs \
        src/settings-ui/Settings.UI.Library/SettingsSerializationContext.cs \
        src/settings-ui/Settings.UI/SerializationContext/SourceGenerationContextContext.cs \
        src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.Settings.cs \
        src/modules/powerdisplay/PowerDisplay/Serialization/JsonSourceGenerationContext.cs
git commit -m "[PowerDisplay] Show resolved feature->VCP code in diagnostics"
```

---

### Task 9: Full build + manual end-to-end verification

**Files:** none (verification only).

- [ ] **Step 1: Build the PowerDisplay projects + Settings UI**

Run:
```
dotnet build src/modules/powerdisplay/PowerDisplay.Lib/PowerDisplay.Lib.csproj -c Debug -p:Platform=x64
dotnet build src/modules/powerdisplay/PowerDisplay/PowerDisplay.csproj -c Debug -p:Platform=x64
dotnet build src/settings-ui/Settings.UI.Library/Settings.UI.Library.csproj -c Debug -p:Platform=x64
```
Expected: all succeed, 0 errors.

- [ ] **Step 2: Run the full Lib unit-test suite**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj -c Debug`
Expected: PASS — all prior tests plus `VcpFeatureRegistryTests` (6), `VcpFeatureCodeMapTests` (8),
`VcpFeatureResolverTests` (8), `MonitorStateEntrySerializationTests` (2).

- [ ] **Step 3: Manual smoke test (requires a DDC/CI external monitor)**

1. Build/run PowerToys with the new PowerDisplay, open the PowerDisplay flyout, confirm brightness/
   contrast/volume sliders still work on a normal monitor (regression).
2. Open Settings → PowerDisplay → a monitor's **Copy Diagnostics**, paste, and confirm the new
   "Resolved feature -> VCP code" section appears with `0xNN` / `not supported` lines.
3. Inspect `%LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\monitor_state.json` — confirm a
   `vcpFeatureCodes` object exists for the monitor.
4. Press the in-app **Refresh** button; confirm `vcpFeatureCodes` is rewritten (re-resolved) and the
   app still functions.

Record the pasted diagnostics text and the `monitor_state.json` snippet as evidence.

- [ ] **Step 4: Final spec/plan cross-check + commit any fixups**

Re-read the spec sections 4-10 and confirm each is implemented. Commit any small corrections found.

```bash
git add -A
git commit -m "[PowerDisplay] VCP code resolution: final verification fixups" || echo "nothing to commit"
```

---

## Self-Review

**Spec coverage:**
- §4 architecture / resolution-in-discovery → Task 6 Step 2. ✓
- §5.1 enum, §5.2 registry → Task 1. ✓
- §5.3 map model + sentinel → Task 2. ✓
- §5.4 resolver (Phase 1/2, per-feature reuse, usability probe) → Task 3. ✓
- §6 controller: persisted prop, ordering, brightness support, resolved codes in get/set/init → Task 6. ✓
- §7 persistence: entry field, serialization, manager get/update/clear → Tasks 4, 5. ✓
- §8 app orchestration: push before, persist after, clear on user refresh → Task 7. ✓
- §9 diagnostics: MonitorInfo field, UpdateFrom, hex-only section, source-gen registration, CreateMonitorInfo → Task 8. ✓
- §10 tests → Tasks 1-4 (resolver/map/registry/serialization). ✓
- §11 edge cases (probe usability `max>0`, forward-compat reuse, concurrency) → Tasks 3, 6. ✓

**Placeholder scan:** No TBD/TODO; every code step shows complete code. The one "confirm intent"
note (Task 7 Step 4) resolves itself with a concrete default (gate on `!skipScanningCheck`). ✓

**Type consistency:** `VcpFeatureCodeMap` API (`GetCode`/`IsSupported`/`IsResolved`/`SetCode`/
`SetNotSupported`/`ToPersisted`/`FromPersisted`) is used identically in Tasks 3, 5, 6, 7, 8.
`VcpFeature` enum members, `VcpFeatureRegistry.Key/Candidates/Primary/AllFeatures/TryParseKey`,
`MonitorStateEntry.VcpFeatureCodes`, `Monitor.ResolvedVcpCodes`, `MonitorInfo.ResolvedVcpCodes`,
`MonitorManager.SetPersistedVcpCodeMaps`, `DdcCiController.PersistedVcpCodeMaps`,
`MonitorViewModel.ResolvedVcpCodes`, `MonitorStateManager.GetVcpCodeMap/UpdateVcpCodeMap/ClearAllVcpCodeMaps`
are consistent across tasks. ✓
