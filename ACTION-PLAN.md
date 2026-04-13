# PR #46933 — MeasureTool Tests: Consolidated Architect Action Plan

**PR**: https://github.com/microsoft/PowerToys/pull/46933
**Verdict**: REVISE — keep 19 solid tests, delete 20 hollow/brittle tests, add ~10 new real tests, file 3 product bugs

---

## 1. Tests to KEEP (19 tests — add deep comments)

### BGRATextureView (10 tests) ✅ SOLID
These call real `BGRATextureView::GetPixel` and `BGRATextureView::PixelsClose<>` — genuine product code with SSE/NEON intrinsics.

| # | Test | Why keep |
|---|------|----------|
| 1 | `GetPixel_BasicAccess` | Exercises real `GetPixel` with `pixels[x + pitch * y]` indexing |
| 2 | `GetPixel_WithPitchGreaterThanWidth` | Validates pitch≠width stride — catches DX11 row-pitch bugs |
| 3 | `PixelsClose_PerChannel_IdenticalPixels` | Zero-distance baseline for SIMD path |
| 4 | `PixelsClose_Total_IdenticalPixels` | Zero-distance baseline for SAD path |
| 5 | `PixelsClose_PerChannel_WithinTolerance` | Multi-channel within-tolerance through real SIMD compare |
| 6 | `PixelsClose_PerChannel_ExceedsTolerance` | Single-channel exceed through real SIMD compare |
| 7 | `PixelsClose_PerChannel_ExactBoundary` | Boundary=30 and 29 tests exact SIMD threshold |
| 8 | `PixelsClose_Total_WithinTolerance` | Sum-of-abs through real `_mm_sad_epu8` |
| 9 | `PixelsClose_Total_ExceedsTolerance` | Just-over threshold for SAD path |
| 10 | `PixelsClose_CompletelyDifferent` | BLACK vs WHITE per-channel (255 diff) |

**Required comment additions for each:**
```cpp
// REAL-CODE TEST: calls BGRATextureView::PixelsClose<true> from BGRATextureView.h
// which uses SSE2/NEON SIMD intrinsics (_mm_cmpgt_epi16 / _mm_sad_epu8).
```

### EdgeDetection (9 tests) ✅ SOLID
These call real `DetectEdges()` which internally invokes `FindEdge<>` templates.

| # | Test | Why keep |
|---|------|----------|
| 1 | `UniformTexture_EdgesReachBorders` | Uniform color → edges reach all four walls |
| 2 | `CenteredBox_EdgesMatchBoxBounds` | Centered rect detection accuracy |
| 3 | `CornerBox_EdgesAtOrigin` | Origin-anchored rect (0,0)-(19,19) |
| 4 | `CursorClamped_NoOutOfBounds` | Cursor at (0,0) clamped to (1,1) — documents known limitation |
| 5 | `PerChannelMode_DetectsEdges` | `perChannel=true` template path |
| 6 | `SingleDifferentPixel` | Minimal 1px region detection |
| 7 | `ColorBands_HorizontalEdges` | Band detection across horizontal stripes |
| 8 | `ToleranceAffectsResult` | Low vs high tolerance behavior |
| 9 | `AsymmetricBox` | Non-square region detection |

**Required comment addition for `CursorClamped_NoOutOfBounds`:**
```cpp
// NOTE: This documents a known product limitation — FindEdge clamps the start
// to (1,1), so pixel 0 along any axis is never compared against the seed pixel.
// See PRODUCT-BUG-3 in ACTION-PLAN.md.
```

---

## 2. Tests to REMOVE (20 tests)

### UnitConversion — DELETE ALL 12 ❌ HOLLOW
Every test calls the local `ConvertPixels()` replica (lines 69-96 of test file), NOT `Measurement::Width/Height/Convert`. They test a copy-paste of the formula, not the product.

| # | Test | Why delete |
|---|------|------------|
| 1 | `PixelMode_Identity` | Calls local `ConvertPixels`, not `Measurement` |
| 2 | `Inch_FallbackDPI` | Calls local `ConvertPixels` |
| 3 | `Centimetre_FallbackDPI` | Calls local `ConvertPixels` |
| 4 | `Millimetre_FallbackDPI` | **Cements mm bug** — asserts 0.254 which is 100x wrong |
| 5 | `Inch_WithPx2mmRatio` | Calls local `ConvertPixels` |
| 6 | `Centimetre_WithPx2mmRatio` | Calls local `ConvertPixels` |
| 7 | `Millimetre_WithPx2mmRatio` | Calls local `ConvertPixels` |
| 8 | `GetUnitFromIndex_ValidIndices` | Calls local `GetUnitFromIndex`, not `Measurement::GetUnitFromIndex` |
| 9 | `GetUnitFromIndex_InvalidDefaultsToPixel` | Calls local `GetUnitFromIndex` |
| 10 | `MeasurementWidth_InclusiveRange` | Pure arithmetic — `right - left + 1` — no product code called |
| 11 | `MeasurementHeight_InclusiveRange` | Pure arithmetic — `bottom - top + 1` — no product code called |
| 12 | `UnitConversion_96DPI_1Inch` | Calls local `ConvertPixels` — duplicate of test #2 |

**Also delete**: the entire local helper block (lines 65-103):
- `enum MeasureUnit` (replica of `Measurement::Unit`)
- `ConvertPixels()` (replica of anonymous `Convert()` in Measurement.cpp)
- `GetUnitFromIndex()` (replica of `Measurement::GetUnitFromIndex`)

### Constants — DELETE 6 of 8, KEEP 2 ❌ BRITTLE
Snapshot tests that assert magic values. Most provide no behavioral coverage.

| # | Test | Action | Reason |
|---|------|--------|--------|
| 1 | `TargetFrameRate_Is90` | **KEEP** | Performance-critical constant worth guarding |
| 2 | `FontSize_Is14` | DELETE | Style constant, no behavioral impact |
| 3 | `TextBoxCornerRadius_Is4` | DELETE | Style constant |
| 4 | `FeetHalfLength_Is2` | DELETE | Style constant |
| 5 | `MouseWheelToleranceStep_Is15` | **KEEP** | Input handling constant, user-facing behavior |
| 6 | `CursorOffset_Is4` | DELETE | Style constant |
| 7 | `ShadowOpacity_Is04` | DELETE | Style constant |
| 8 | `CrossOpacity_Is025` | DELETE | Style constant |

**For the 2 kept**: add comment explaining why:
```cpp
// Guard performance-critical constant — accidental change would alter frame timing.
```

---

## 3. Tests to ADD (~10 new tests)

### 3A. Real Measurement class tests (replace the 12 deleted hollow tests)

These must `#include "Measurement.h"` and call the real `Measurement::Width()`, `Measurement::Height()`, and `Measurement::GetUnitFromIndex()`.

```cpp
TEST_CLASS(MeasurementTests)
{
public:
    // ---- Width / Height basic ----

    TEST_METHOD(Width_Pixels_InclusiveRange)
    {
        // REAL-CODE TEST: calls Measurement::Width(Pixel) which computes
        // rect.right - rect.left + 1 then Convert() with Pixel (identity).
        RECT r{ .left = 10, .top = 20, .right = 109, .bottom = 69 };
        Measurement m{ r, /*px2mmRatio=*/0.f };
        Assert::AreEqual(100.f, m.Width(Measurement::Unit::Pixel));
    }

    TEST_METHOD(Height_Pixels_InclusiveRange)
    {
        RECT r{ .left = 10, .top = 20, .right = 109, .bottom = 69 };
        Measurement m{ r, 0.f };
        Assert::AreEqual(50.f, m.Height(Measurement::Unit::Pixel));
    }

    // ---- Fallback DPI path (px2mmRatio == 0) ----

    TEST_METHOD(Width_Inch_FallbackDPI)
    {
        // 96 pixels at default 96 DPI = 1 inch
        RECT r{ .left = 0, .top = 0, .right = 95, .bottom = 0 };
        Measurement m{ r, 0.f };
        Assert::AreEqual(1.f, m.Width(Measurement::Unit::Inch), 0.001f);
    }

    TEST_METHOD(Width_Centimetre_FallbackDPI)
    {
        // 96 pixels at 96 DPI = 2.54 cm
        RECT r{ .left = 0, .top = 0, .right = 95, .bottom = 0 };
        Measurement m{ r, 0.f };
        Assert::AreEqual(2.54f, m.Width(Measurement::Unit::Centimetre), 0.001f);
    }

    TEST_METHOD(Width_Millimetre_FallbackDPI)
    {
        // 96 pixels at 96 DPI = 25.4 mm (1 inch)
        // ⚠️ EXPECTED TO FAIL until PRODUCT-BUG-1 is fixed.
        // Product currently returns 0.254 mm (100x too small).
        // The formula is: pixels / 96.0f / 10.0f * 2.54f
        // Correct formula: pixels / 96.0f * 25.4f
        RECT r{ .left = 0, .top = 0, .right = 95, .bottom = 0 };
        Measurement m{ r, 0.f };
        Assert::AreEqual(25.4f, m.Width(Measurement::Unit::Millimetre), 0.01f,
                         L"BUG: product returns 0.254 — see PRODUCT-BUG-1");
    }

    // ---- Physical DPI path (px2mmRatio > 0) ----

    TEST_METHOD(Width_Millimetre_WithRatio)
    {
        // 100 pixels * px2mmRatio 0.5 = 50 mm
        RECT r{ .left = 0, .top = 0, .right = 99, .bottom = 0 };
        Measurement m{ r, 0.5f };
        Assert::AreEqual(50.f, m.Width(Measurement::Unit::Millimetre), 0.001f);
    }

    TEST_METHOD(Width_Inch_WithRatio)
    {
        // 100 pixels * 0.5 / 10 / 2.54 ≈ 1.9685 in
        RECT r{ .left = 0, .top = 0, .right = 99, .bottom = 0 };
        Measurement m{ r, 0.5f };
        float expected = 100.f * 0.5f / 10.f / 2.54f;
        Assert::AreEqual(expected, m.Width(Measurement::Unit::Inch), 0.001f);
    }

    // ---- GetUnitFromIndex ----

    TEST_METHOD(GetUnitFromIndex_ValidIndices)
    {
        Assert::IsTrue(Measurement::Unit::Pixel == Measurement::GetUnitFromIndex(0));
        Assert::IsTrue(Measurement::Unit::Inch == Measurement::GetUnitFromIndex(1));
        Assert::IsTrue(Measurement::Unit::Centimetre == Measurement::GetUnitFromIndex(2));
        Assert::IsTrue(Measurement::Unit::Millimetre == Measurement::GetUnitFromIndex(3));
    }

    TEST_METHOD(GetUnitFromIndex_InvalidDefaultsToPixel)
    {
        Assert::IsTrue(Measurement::Unit::Pixel == Measurement::GetUnitFromIndex(-1));
        Assert::IsTrue(Measurement::Unit::Pixel == Measurement::GetUnitFromIndex(99));
    }
};
```

**Build note**: The test project's vcxproj must add `Measurement.cpp` to its sources (or link against the MeasureToolCore lib) so it compiles the real `Convert()`. The current test only includes `BGRATextureView.h`, `EdgeDetection.h`, and `constants.h`.

### 3B. PixelsClose total-mode large-diff test (catches truncation bug)

```cpp
TEST_METHOD(PixelsClose_Total_LargeDiff_TruncationBug)
{
    // PRODUCT-BUG-2: PixelsClose<false> uses `& 0xFF` which truncates
    // the SAD sum to 8 bits. BLACK vs WHITE = 255+255+255+0 = 765,
    // but 765 & 0xFF = 253. With tolerance=254, the buggy code returns
    // true (253 <= 254) even though the actual diff is 765.
    //
    // ⚠️ EXPECTED TO FAIL until product truncation bug is fixed.
    Assert::IsFalse(BGRATextureView::PixelsClose<false>(BLACK, WHITE, 254),
                    L"BUG: total diff is 765 but & 0xFF truncates to 253, "
                    L"so 253<=254 returns true incorrectly");
}
```

### 3C. FindEdge at pixel 0 test (catches boundary bug)

```cpp
TEST_METHOD(FindEdge_DoesNotCheckPixelZero)
{
    // PRODUCT-BUG-3: FindEdge clamps start to (1, height-2) and the
    // decrement loop breaks at `--x == 0` without comparing pixel 0.
    // A different-colored pixel at column 0 is invisible to detection.
    //
    // This test documents the bug. It constructs a texture where column 0
    // is RED and everything else is GREEN. Starting from center, the left
    // edge should be 1 (start of GREEN region), not 0. But if the bug
    // is fixed to check pixel 0, left would need to be 1 since column 0
    // IS different.
    const size_t W = 20, H = 5;
    auto pixels = MakeSolidBuffer(W, H, GREEN);
    // Set column 0 to RED
    for (size_t y = 0; y < H; ++y)
        pixels[y * W + 0] = RED;
    auto view = MakeTexture(pixels.data(), W, H);

    POINT center = { 10, 2 };
    RECT edges = DetectEdges(view, center, false, 0);

    // With the bug: left=0 (never compared pixel 0, so it "extends" to 0)
    // Correct behavior: left=1 (pixel 0 IS different, so edge is at 1)
    //
    // The current product returns left=0, which is coincidentally correct
    // for a uniform texture but wrong in principle — it never actually
    // checked that pixel 0 matches. This test documents the current behavior.
    Assert::AreEqual(1L, edges.left,
                     L"BUG: FindEdge never compares pixel at index 0. "
                     L"Currently returns 0 because loop falls through, not because it checked.");
}
```

### 3D. Edge detection with D2D1_RECT_F constructor

```cpp
TEST_METHOD(Measurement_FromD2DRect)
{
    // Verify the D2D1_RECT_F constructor path works identically
    D2D1_RECT_F d2dRect{ .left = 10.f, .top = 20.f, .right = 109.f, .bottom = 69.f };
    Measurement m{ d2dRect, 0.f };
    Assert::AreEqual(100.f, m.Width(Measurement::Unit::Pixel));
    Assert::AreEqual(50.f, m.Height(Measurement::Unit::Pixel));
}
```

---

## 4. Product Bugs to Document

### PRODUCT-BUG-1: Millimetre fallback formula is 100x wrong (SEVERITY: HIGH)

**File**: `src/modules/MeasureTool/MeasureToolCore/Measurement.cpp`, line ~47
**Current code**:
```cpp
case Measurement::Unit::Millimetre:
    return pixels / 96.0f / 10.0f * 2.54f;  // = 0.254 mm for 96px
```
**Expected result**: 96 pixels at 96 DPI = 1 inch = **25.4 mm**
**Actual result**: 96 pixels → **0.254 mm** (100x too small)

**Root cause**: Division by 10 should be multiplication by 10, or equivalently:
```cpp
// Fix option 1 — reorder operations:
return pixels / 96.0f * 2.54f * 10.0f;

// Fix option 2 — use direct conversion:
return pixels / 96.0f * 25.4f;
```

**Verification**: The Centimetre path is correct (`pixels / 96.0f * 2.54f`). Millimetre should be exactly 10x that.

**Impact**: Any user in fallback-DPI mode (px2mmRatio=0, typical for most virtual/remote displays) sees millimetre measurements 100x too small. The existing test `Millimetre_FallbackDPI` asserts `0.254f` — cementing this bug as "correct."

**Action**: File GitHub issue. Fix is one-line. The test PR must NOT ship with the hollow test that asserts 0.254.

---

### PRODUCT-BUG-2: PixelsClose<false> truncates total diff to 8 bits (SEVERITY: MEDIUM)

**File**: `src/modules/MeasureTool/MeasureToolCore/BGRATextureView.h`, line ~99
**Current code**:
```cpp
const int32_t score = _mm_cvtsi128_si32(_mm_sad_epu8(distances, _mm_setzero_si128()))
                      & std::numeric_limits<uint8_t>::max();  // & 0xFF
return score <= tolerance;
```
**Problem**: `_mm_sad_epu8` returns a 16-bit sum per 64-bit lane. For high-contrast pixels (e.g., BLACK vs WHITE), the sum is 765 (255×3). The `& 0xFF` truncates 765 → 253, making `PixelsClose<false>(BLACK, WHITE, 254)` return `true`.

**Fix**:
```cpp
const int32_t score = _mm_cvtsi128_si32(_mm_sad_epu8(distances, _mm_setzero_si128()))
                      & std::numeric_limits<uint16_t>::max();  // & 0xFFFF — SAD result is 16-bit
```
Or simply remove the mask — `_mm_cvtsi128_si32` already returns the low 32 bits which is sufficient.

**Impact**: Edge detection in total-mode with high tolerance (>255) could treat vastly different pixels as "close." In practice, tolerance values are typically small (15–30 via mouse wheel), so this rarely triggers. But it's a latent correctness bug.

---

### PRODUCT-BUG-3: FindEdge never checks pixel at row/column 0 (SEVERITY: LOW)

**File**: `src/modules/MeasureTool/MeasureToolCore/EdgeDetection.cpp`, lines 13-16, 39-40
**Current code**:
```cpp
long x = std::clamp<long>(centerPoint.x, 1, ...);  // start clamped to 1
// ...
if (--x == 0)   // breaks WITHOUT comparing pixel at x=0
    break;
```
**Problem**: The decrement loop breaks when the coordinate reaches 0, but never compares the pixel AT 0 against the seed. This means:
- If pixel[0] is the same color, the edge correctly extends to 0 (via the fallback `return 0`)
- If pixel[0] is a different color, the edge STILL extends to 0 (because it was never checked)

**Fix**: Change the loop condition to check `< 0` instead of `== 0`, or compare pixel[0] before breaking.

**Impact**: Minor — only affects textures where the boundary pixel differs from the interior. On real screen captures, edges rarely touch pixel 0. But it's technically wrong.

---

## 5. Implementation Instructions

### File modifications in the PR branch:

#### `MeasureToolTests.cpp` — Major rewrite

1. **Delete lines 65-103** — Remove the entire local replica block:
   - `enum MeasureUnit` 
   - `ConvertPixels()` function
   - `GetUnitFromIndex()` function

2. **Delete the entire `UnitConversionTests` class** (~lines 300-380)

3. **Delete 6 of 8 `ConstantsTests`** — Keep only `TargetFrameRate_Is90` and `MouseWheelToleranceStep_Is15`

4. **Add `#include "Measurement.h"` to the includes** (after `#include "constants.h"`)

5. **Add new `MeasurementTests` class** with the ~9 tests from Section 3A

6. **Add `PixelsClose_Total_LargeDiff_TruncationBug` test** to `BGRATextureViewTests` (Section 3B)

7. **Add `FindEdge_DoesNotCheckPixelZero` test** to `EdgeDetectionTests` (Section 3C)

8. **Add deep comments** to all kept BGRATextureView and EdgeDetection tests explaining they call real product code

#### `pch.h` — Add Measurement.h dependency

Add `#include <dcommon.h>` if not already present (needed by `Measurement.h`'s `D2D1_RECT_F`). It's already there — confirmed.

#### `UnitTests-MeasureTool.vcxproj` — Link real Measurement code

The test project must compile or link against:
- `Measurement.cpp` (for `Measurement::Width/Height/Convert/GetUnitFromIndex`)
- Either add `Measurement.cpp` as a source file in the test project, or link against the MeasureToolCore static lib

Add to the vcxproj `<ItemGroup>`:
```xml
<ClCompile Include="..\MeasureToolCore\Measurement.cpp" />
```

Or add a project reference to MeasureToolCore if it builds as a lib.

### Tests expected to FAIL (document with `Assert::Fail` guard or `// TODO` comments):

| Test | Fails because | Resolution |
|------|--------------|------------|
| `Width_Millimetre_FallbackDPI` | PRODUCT-BUG-1 (0.254 vs 25.4) | Fix Measurement.cpp first, then test passes |
| `PixelsClose_Total_LargeDiff_TruncationBug` | PRODUCT-BUG-2 (& 0xFF) | Fix BGRATextureView.h first |
| `FindEdge_DoesNotCheckPixelZero` | PRODUCT-BUG-3 (clamp to 1) | Fix EdgeDetection.cpp first |

**Recommendation**: Mark these three with `BEGIN_TEST_METHOD_ATTRIBUTE` / `TEST_IGNORE()` or wrap with a descriptive `Logger::WriteMessage("KNOWN BUG: ...")` so CI is green. Unblock them as product fixes land.

### Final test count:

| Category | Before | After |
|----------|--------|-------|
| BGRATextureView | 10 | 11 (+1 truncation bug test) |
| EdgeDetection | 9 | 10 (+1 pixel-0 boundary test) |
| Measurement (real) | 0 | 10 (replaces 12 hollow) |
| Constants | 8 | 2 (keep critical only) |
| UnitConversion (hollow) | 12 | 0 (deleted) |
| **Total** | **39** | **33** |

Net: −6 tests numerically, but quality goes from 19/39 real (49%) → **31/33 real (94%)** with 2 known-bug tests and 2 snapshot guards.

---

## Summary for PR Author

> The BGRATextureView and EdgeDetection suites are excellent — real SIMD/template code getting real coverage. Ship those with pride.
>
> The 12 UnitConversion tests must go: they test a copy-pasted formula, not the product, and they cement a 100x mm bug. Replace them with tests that construct `Measurement` objects and call `Width()`/`Height()` directly.
>
> Three product bugs surfaced — the mm formula is the most user-visible and should be fixed before or alongside this PR.
