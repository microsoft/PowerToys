// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Unit tests for MeasureToolCore, mirroring the Rust test suite in
// src/rust/libs/measuretool-core/src/.
//
// These are pure-logic tests that construct BGRATextureView instances and
// exercise pixel comparison, edge detection, and measurement conversion
// math without requiring real screen captures or D2D resources.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "BGRATextureView.h"
#include "EdgeDetection.h"
#include "../MeasureToolCore/Measurement.h"
#include "constants.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace MeasureToolUnitTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    static BGRATextureView MakeTexture(const uint32_t* data, size_t width, size_t height, size_t pitch = 0)
    {
        BGRATextureView view;
        view.pixels = data;
        view.width = width;
        view.height = height;
        view.pitch = pitch ? pitch : width;
        return view;
    }

    static std::vector<uint32_t> MakeSolidBuffer(size_t width, size_t height, uint32_t color)
    {
        return std::vector<uint32_t>(width * height, color);
    }

    // Create a buffer with a colored rectangle on a background (inclusive bounds).
    static std::vector<uint32_t> MakeRectBuffer(size_t width, size_t height,
                                                 uint32_t bgColor, uint32_t fgColor,
                                                 size_t rectLeft, size_t rectTop,
                                                 size_t rectRight, size_t rectBottom)
    {
        std::vector<uint32_t> buffer(width * height, bgColor);
        for (size_t y = rectTop; y <= rectBottom && y < height; ++y)
            for (size_t x = rectLeft; x <= rectRight && x < width; ++x)
                buffer[y * width + x] = fgColor;
        return buffer;
    }

    // BGRA pixel constants (uint32_t format: 0xAARRGGBB on little-endian)
    constexpr uint32_t RED = 0xFFFF0000;
    constexpr uint32_t GREEN = 0xFF00FF00;
    constexpr uint32_t BLUE = 0xFF0000FF;
    constexpr uint32_t WHITE = 0xFFFFFFFF;
    constexpr uint32_t BLACK = 0xFF000000;

    // ── BGRATextureView tests ───────────────────────────────────────────────

    TEST_CLASS(BGRATextureViewTests)
    {
    public:
        // Product code: BGRATextureView::GetPixel (BGRATextureView.h)
        // What: Verifies basic pixel indexing using x + pitch * y formula
        // Why: Incorrect indexing would corrupt all edge detection results
        TEST_METHOD(GetPixel_BasicAccess)
        {
            uint32_t data[] = {
                0x01, 0x02, 0x03,
                0x04, 0x05, 0x06,
                0x07, 0x08, 0x09
            };
            auto view = MakeTexture(data, 3, 3);

            Assert::AreEqual(0x01u, view.GetPixel(0, 0));
            Assert::AreEqual(0x05u, view.GetPixel(1, 1));
            Assert::AreEqual(0x09u, view.GetPixel(2, 2));
            Assert::AreEqual(0x06u, view.GetPixel(2, 1));
        }

        // Product code: BGRATextureView::GetPixel (BGRATextureView.h)
        // What: Verifies pitch-based row stride when pitch > width (padding)
        // Why: D3D11 textures often have pitch != width; wrong pitch = garbled pixels
        TEST_METHOD(GetPixel_WithPitchGreaterThanWidth)
        {
            // Width=2, pitch=4 (row stride with padding)
            uint32_t data[] = {
                0xAA, 0xBB, 0x00, 0x00,
                0xCC, 0xDD, 0x00, 0x00
            };
            auto view = MakeTexture(data, 2, 2, 4);

            Assert::AreEqual(0xAAu, view.GetPixel(0, 0));
            Assert::AreEqual(0xBBu, view.GetPixel(1, 0));
            Assert::AreEqual(0xCCu, view.GetPixel(0, 1));
            Assert::AreEqual(0xDDu, view.GetPixel(1, 1));
        }

        // Product code: BGRATextureView::PixelsClose<true> (BGRATextureView.h)
        // What: Verifies identical pixels pass per-channel mode with zero tolerance
        // Why: Base case — if this fails, PixelsClose is fundamentally broken
        TEST_METHOD(PixelsClose_PerChannel_IdenticalPixels)
        {
            Assert::IsTrue(BGRATextureView::PixelsClose<true>(RED, RED, 0));
            Assert::IsTrue(BGRATextureView::PixelsClose<true>(WHITE, WHITE, 0));
            Assert::IsTrue(BGRATextureView::PixelsClose<true>(BLACK, BLACK, 0));
        }

        // Product code: BGRATextureView::PixelsClose<false> (BGRATextureView.h)
        // What: Verifies identical pixels pass total-difference mode with zero tolerance
        // Why: Base case for total mode — validates _mm_sad_epu8 returns 0 for identical inputs
        TEST_METHOD(PixelsClose_Total_IdenticalPixels)
        {
            Assert::IsTrue(BGRATextureView::PixelsClose<false>(RED, RED, 0));
            Assert::IsTrue(BGRATextureView::PixelsClose<false>(WHITE, WHITE, 0));
            Assert::IsTrue(BGRATextureView::PixelsClose<false>(BLACK, BLACK, 0));
        }

        // Product code: BGRATextureView::PixelsClose<true> (BGRATextureView.h)
        // What: Verifies small per-channel diffs (1 each) pass with tolerance=1
        // Why: Exercises the _mm_cmpgt_epi16 comparison — off-by-one here breaks anti-aliased edge detection
        TEST_METHOD(PixelsClose_PerChannel_WithinTolerance)
        {
            // Each channel differs by exactly 1
            uint32_t p1 = 0xFF804020; // A=FF R=80 G=40 B=20
            uint32_t p2 = 0xFF814121; // A=FF R=81 G=41 B=21
            Assert::IsTrue(BGRATextureView::PixelsClose<true>(p1, p2, 1),
                           L"Each channel differs by 1, tolerance=1 should pass");
        }

        // Product code: BGRATextureView::PixelsClose<true> (BGRATextureView.h)
        // What: Verifies per-channel mode catches a single channel exceeding tolerance
        // Why: Ensures the SIMD comparison checks EACH channel, not just the sum
        TEST_METHOD(PixelsClose_PerChannel_ExceedsTolerance)
        {
            // Red channel differs by 17 (0x91 - 0x80 = 0x11)
            uint32_t p1 = 0xFF800000;
            uint32_t p2 = 0xFF910000;
            Assert::IsFalse(BGRATextureView::PixelsClose<true>(p1, p2, 10),
                            L"R differs by 17, tolerance=10 should fail");
        }

        // Product code: BGRATextureView::PixelsClose<true> (BGRATextureView.h)
        // What: Verifies exact boundary: diff == tolerance passes, diff == tolerance+1 fails
        // Why: Documents the <= vs < semantics of per-channel comparison
        TEST_METHOD(PixelsClose_PerChannel_ExactBoundary)
        {
            uint32_t p1 = 0xFF000000;
            uint32_t p2 = 0xFF1E0000; // R differs by 30 (0x1E)
            Assert::IsTrue(BGRATextureView::PixelsClose<true>(p1, p2, 30),
                           L"Diff equals tolerance -> pass");
            Assert::IsFalse(BGRATextureView::PixelsClose<true>(p1, p2, 29),
                            L"Diff exceeds tolerance by 1 -> fail");
        }

        // Product code: BGRATextureView::PixelsClose<false> (BGRATextureView.h)
        // What: Verifies total-diff mode sums all channel diffs (5+5+5=15 vs tolerance 15)
        // Why: Exercises the _mm_sad_epu8 sum-of-absolute-differences path
        TEST_METHOD(PixelsClose_Total_WithinTolerance)
        {
            // Total diff = 5+5+5+0 = 15
            uint32_t p1 = 0xFF000000;
            uint32_t p2 = 0xFF050505;
            Assert::IsTrue(BGRATextureView::PixelsClose<false>(p1, p2, 15),
                           L"Total diff=15, tolerance=15 should pass");
        }

        // Product code: BGRATextureView::PixelsClose<false> (BGRATextureView.h)
        // What: Verifies total-diff mode rejects when sum exceeds tolerance by 1
        // Why: Boundary check for <= comparison in total mode
        TEST_METHOD(PixelsClose_Total_ExceedsTolerance)
        {
            uint32_t p1 = 0xFF000000;
            uint32_t p2 = 0xFF050505; // total diff = 15
            Assert::IsFalse(BGRATextureView::PixelsClose<false>(p1, p2, 14),
                            L"Total diff=15, tolerance=14 should fail");
        }

        // Product code: BGRATextureView::PixelsClose<true> (BGRATextureView.h)
        // What: Verifies BLACK vs WHITE (max channel diff 255) fails at tolerance 254
        // Why: Ensures per-channel mode works at the maximum possible difference
        TEST_METHOD(PixelsClose_CompletelyDifferent)
        {
            // Black (0xFF000000) vs White (0xFFFFFFFF): each RGB channel differs by 255
            Assert::IsFalse(BGRATextureView::PixelsClose<true>(BLACK, WHITE, 254),
                            L"Per-channel: 255 > 254 tolerance");
        }

        // Product code: BGRATextureView::PixelsClose<false> (BGRATextureView.h)
        // What: Tests total mode with BLACK vs WHITE — total diff is 765 (3x255)
        // Why: Documents & 0xFF truncation bug — total diff 765 wraps to 253,
        //      so BLACK vs WHITE INCORRECTLY appears "close" at tolerance 254
        // NOTE: This test asserts the BUGGY behavior. When the bug is fixed,
        //       this should return false (the pixels are not close).
        TEST_METHOD(PixelsClose_Total_LargeDiff_BlackVsWhite)
        {
            // BUG: score = _mm_sad_epu8(...) & 0xFF truncates 765 to 253
            // So 253 <= 254 returns true, even though true total diff is 765
            bool result = BGRATextureView::PixelsClose<false>(BLACK, WHITE, 254);
            Assert::IsTrue(result,
                           L"BUG: & 0xFF truncation makes 765 wrap to 253, passing tolerance 254");
        }
    };

    // ── Edge detection tests ────────────────────────────────────────────────

    TEST_CLASS(EdgeDetectionTests)
    {
    public:
        // Product code: DetectEdges -> FindEdge (EdgeDetection.cpp)
        // What: Uniform RED texture — all edges should reach the texture borders
        // Why: Base case that FindEdge's while-loop terminates correctly at boundaries
        // NOTE: FindEdge has a boundary bug — when decrementing, it breaks at x==0/y==0
        //       without checking that pixel's color. For uniform textures this is
        //       harmless (returns 0 anyway), but for mixed textures at row/col 0 it
        //       would incorrectly report the edge extends to the border.
        TEST_METHOD(UniformTexture_EdgesReachBorders)
        {
            const size_t W = 100, H = 100;
            auto pixels = MakeSolidBuffer(W, H, RED);
            auto view = MakeTexture(pixels.data(), W, H);

            POINT center = { 50, 50 };
            RECT edges = DetectEdges(view, center, false, 30);

            Assert::AreEqual(0L, edges.left, L"Left should reach 0");
            Assert::AreEqual(0L, edges.top, L"Top should reach 0");
            Assert::AreEqual(static_cast<LONG>(W - 1), edges.right, L"Right should reach width-1");
            Assert::AreEqual(static_cast<LONG>(H - 1), edges.bottom, L"Bottom should reach height-1");
        }

        // Product code: DetectEdges -> FindEdge (EdgeDetection.cpp)
        // What: Red box on white background — edges should match the box bounds exactly
        // Why: Core use case — measures a colored region on screen
        TEST_METHOD(CenteredBox_EdgesMatchBoxBounds)
        {
            const size_t W = 100, H = 100;
            // Red box (20,30)-(79,69) on white background
            auto pixels = MakeRectBuffer(W, H, WHITE, RED, 20, 30, 79, 69);
            auto view = MakeTexture(pixels.data(), W, H);

            POINT center = { 50, 50 };
            RECT edges = DetectEdges(view, center, false, 0);

            Assert::AreEqual(20L, edges.left);
            Assert::AreEqual(30L, edges.top);
            Assert::AreEqual(79L, edges.right);
            Assert::AreEqual(69L, edges.bottom);
        }

        // Product code: DetectEdges -> FindEdge (EdgeDetection.cpp)
        // What: Red box at top-left corner (0,0)-(19,19)
        // Why: Exercises the FindEdge boundary where decrement hits x==0/y==0
        TEST_METHOD(CornerBox_EdgesAtOrigin)
        {
            const size_t W = 100, H = 100;
            auto pixels = MakeRectBuffer(W, H, WHITE, RED, 0, 0, 19, 19);
            auto view = MakeTexture(pixels.data(), W, H);

            POINT center = { 10, 10 };
            RECT edges = DetectEdges(view, center, false, 0);

            Assert::AreEqual(0L, edges.left);
            Assert::AreEqual(0L, edges.top);
            Assert::AreEqual(19L, edges.right);
            Assert::AreEqual(19L, edges.bottom);
        }

        // Product code: DetectEdges -> FindEdge (EdgeDetection.cpp)
        // What: Cursor at (0,0) — clamped to (1,1) by std::clamp inside FindEdge
        // Why: Verifies no out-of-bounds read when cursor is at the extreme corner
        TEST_METHOD(CursorClamped_NoOutOfBounds)
        {
            const size_t W = 50, H = 50;
            auto pixels = MakeSolidBuffer(W, H, RED);
            auto view = MakeTexture(pixels.data(), W, H);

            // Cursor at origin — clamped to (1,1) internally
            POINT corner = { 0, 0 };
            RECT edges = DetectEdges(view, corner, false, 30);

            Assert::AreEqual(0L, edges.left);
            Assert::AreEqual(0L, edges.top);
            Assert::AreEqual(static_cast<LONG>(W - 1), edges.right);
            Assert::AreEqual(static_cast<LONG>(H - 1), edges.bottom);
        }

        // Product code: DetectEdges -> DetectEdgesInternal<true> -> FindEdge<true,...> (EdgeDetection.cpp)
        // What: Red box on white background using per-channel comparison mode
        // Why: Validates the perChannel=true template path dispatches correctly
        TEST_METHOD(PerChannelMode_DetectsEdges)
        {
            const size_t W = 100, H = 100;
            auto pixels = MakeRectBuffer(W, H, WHITE, RED, 20, 20, 79, 79);
            auto view = MakeTexture(pixels.data(), W, H);

            POINT center = { 50, 50 };
            RECT edges = DetectEdges(view, center, true, 0);

            Assert::AreEqual(20L, edges.left);
            Assert::AreEqual(20L, edges.top);
            Assert::AreEqual(79L, edges.right);
            Assert::AreEqual(79L, edges.bottom);
        }

        // Product code: DetectEdges -> FindEdge (EdgeDetection.cpp)
        // What: A single different pixel at (5,5) in uniform RED — edges collapse to 1x1
        // Why: Tests the smallest possible detected region (single pixel island)
        TEST_METHOD(SingleDifferentPixel)
        {
            const size_t W = 11, H = 11;
            auto pixels = MakeSolidBuffer(W, H, RED);
            pixels[5 * W + 5] = GREEN; // single different pixel at (5,5)
            auto view = MakeTexture(pixels.data(), W, H);

            POINT center = { 5, 5 };
            RECT edges = DetectEdges(view, center, false, 0);

            Assert::AreEqual(5L, edges.left);
            Assert::AreEqual(5L, edges.top);
            Assert::AreEqual(5L, edges.right);
            Assert::AreEqual(5L, edges.bottom);
        }

        // Product code: DetectEdges -> FindEdge (EdgeDetection.cpp)
        // What: Three horizontal color bands — cursor in middle green band
        // Why: Verifies FindEdge stops at color transitions in Y axis (vertical edges)
        TEST_METHOD(ColorBands_HorizontalEdges)
        {
            // Three horizontal bands: RED (0-9), GREEN (10-19), BLUE (20-29)
            const size_t W = 10, H = 30;
            std::vector<uint32_t> pixels(W * H);
            for (size_t y = 0; y < 10; ++y)
                for (size_t x = 0; x < W; ++x)
                    pixels[y * W + x] = RED;
            for (size_t y = 10; y < 20; ++y)
                for (size_t x = 0; x < W; ++x)
                    pixels[y * W + x] = GREEN;
            for (size_t y = 20; y < 30; ++y)
                for (size_t x = 0; x < W; ++x)
                    pixels[y * W + x] = BLUE;

            auto view = MakeTexture(pixels.data(), W, H);

            POINT center = { 5, 15 };
            RECT edges = DetectEdges(view, center, false, 0);

            Assert::AreEqual(0L, edges.left);
            Assert::AreEqual(10L, edges.top, L"Top edge of green band");
            Assert::AreEqual(static_cast<LONG>(W - 1), edges.right);
            Assert::AreEqual(19L, edges.bottom, L"Bottom edge of green band");
        }

        // Product code: DetectEdges -> FindEdge -> PixelsClose (EdgeDetection.cpp, BGRATextureView.h)
        // What: Gray gradient with slight brightness step — low tolerance finds edge, high tolerance ignores it
        // Why: Demonstrates how the tolerance parameter controls edge sensitivity
        TEST_METHOD(ToleranceAffectsResult)
        {
            const size_t W = 100, H = 3;
            std::vector<uint32_t> pixels(W * H, 0xFF808080); // gray
            // Right half is slightly brighter (channel diff = 5, total diff = 15)
            for (size_t y = 0; y < H; ++y)
                for (size_t x = 50; x < W; ++x)
                    pixels[y * W + x] = 0xFF858585;

            auto view = MakeTexture(pixels.data(), W, H);
            POINT center = { 25, 1 };

            // Low tolerance: edge detected at color boundary
            RECT edgesLow = DetectEdges(view, center, false, 0);
            // High tolerance (>= 15): entire row treated as same color
            RECT edgesHigh = DetectEdges(view, center, false, 20);

            Assert::IsTrue(edgesLow.right < static_cast<LONG>(W - 1),
                           L"Low tolerance should detect edge before border");
            Assert::AreEqual(static_cast<LONG>(W - 1), edgesHigh.right,
                             L"High tolerance should extend to border");
        }

        // Product code: DetectEdges -> FindEdge (EdgeDetection.cpp)
        // What: Tall narrow box (40x90) — asymmetric width/height
        // Why: Ensures FindEdge handles non-square regions correctly (X and Y independent)
        TEST_METHOD(AsymmetricBox)
        {
            const size_t W = 100, H = 100;
            // Tall narrow box (10,5)-(49,94)
            auto pixels = MakeRectBuffer(W, H, WHITE, RED, 10, 5, 49, 94);
            auto view = MakeTexture(pixels.data(), W, H);

            POINT center = { 30, 50 };
            RECT edges = DetectEdges(view, center, false, 0);

            Assert::AreEqual(10L, edges.left);
            Assert::AreEqual(5L, edges.top);
            Assert::AreEqual(49L, edges.right);
            Assert::AreEqual(94L, edges.bottom);
        }
    };

    // ── Measurement class tests ─────────────────────────────────────────────
    // Tests exercise the real Measurement class (Measurement.cpp linked into test project).

    TEST_CLASS(MeasurementTests)
    {
    public:
        // Product code: Measurement::Width(Unit::Pixel) in Measurement.cpp
        // What: Verifies pixel-mode width returns inclusive range (right - left + 1)
        // Why: Off-by-one in measurement display is the #1 user complaint category
        TEST_METHOD(Measurement_Width_Pixel_InclusiveRange)
        {
            RECT r{10, 20, 105, 70};  // width = 105 - 10 + 1 = 96 inclusive
            Measurement m(r, 0.f);
            auto width = m.Width(Measurement::Unit::Pixel);
            Assert::AreEqual(96.f, width, 0.001f);
        }

        // Product code: Measurement::Height(Unit::Pixel) in Measurement.cpp
        // What: Verifies pixel-mode height returns inclusive range (bottom - top + 1)
        // Why: Same off-by-one risk as Width — height must also be inclusive
        TEST_METHOD(Measurement_Height_Pixel_InclusiveRange)
        {
            RECT r{10, 20, 105, 70};  // height = 70 - 20 + 1 = 51 inclusive
            Measurement m(r, 0.f);
            auto height = m.Height(Measurement::Unit::Pixel);
            Assert::AreEqual(51.f, height, 0.001f);
        }

        // Product code: Measurement::Width(Unit::Inch) via Convert() in Measurement.cpp
        // What: Verifies inch conversion at 96 DPI (px2mmRatio=0 fallback path)
        // Why: 96 px = 1 inch is the canonical relationship at default DPI
        TEST_METHOD(Measurement_Width_Inch_FallbackDPI)
        {
            RECT r{0, 0, 95, 0};  // width = 96 px
            Measurement m(r, 0.f);
            Assert::AreEqual(1.0f, m.Width(Measurement::Unit::Inch), 0.001f);
        }

        // Product code: Measurement::Width(Unit::Centimetre) via Convert() in Measurement.cpp
        // What: Verifies cm conversion at 96 DPI fallback (96 px = 2.54 cm)
        // Why: Validates the cm formula: pixels / 96 * 2.54
        TEST_METHOD(Measurement_Width_Centimetre_FallbackDPI)
        {
            RECT r{0, 0, 95, 0};  // width = 96 px
            Measurement m(r, 0.f);
            Assert::AreEqual(2.54f, m.Width(Measurement::Unit::Centimetre), 0.001f);
        }

        // Product code: Measurement::Width(Unit::Millimetre) via Convert() in Measurement.cpp
        // What: Tests mm conversion at 96 DPI fallback
        // Why: Documents a KNOWN PRODUCT BUG — the mm formula divides by 10 instead of
        //      multiplying, making the result 100x too small.
        //      Expected: 96px at 96 DPI = 1 inch = 25.4mm
        //      Actual:   pixels / 96 / 10 * 2.54 = 0.254mm
        // NOTE: When this bug is fixed, update expected value to 25.4f
        TEST_METHOD(Measurement_Width_Millimetre_KNOWN_BUG)
        {
            RECT r{0, 0, 95, 0};  // width = 96 px
            Measurement m(r, 0.f);
            float result = m.Width(Measurement::Unit::Millimetre);
            // BUG: formula is pixels / 96 / 10 * 2.54 = 0.254mm (should be 25.4mm)
            Assert::AreEqual(0.254f, result, 0.001f);
        }

        // Product code: Measurement::Width(Unit::Inch) via Convert() px2mmRatio>0 path
        // What: Verifies inch conversion with a known px2mmRatio value
        // Why: Tests the physical-DPI path (px2mmRatio > 0) that uses actual monitor data
        TEST_METHOD(Measurement_Width_Inch_WithPositiveRatio)
        {
            RECT r{0, 0, 99, 0};  // width = 100 px
            Measurement m(r, 0.254f);  // 100 * 0.254 / 10 / 2.54 = 1.0 inch
            Assert::AreEqual(1.0f, m.Width(Measurement::Unit::Inch), 0.001f);
        }

        // Product code: Measurement::GetUnitFromIndex() in Measurement.cpp
        // What: Verifies all valid indices (0-3) map to the correct Unit enum values
        // Why: UI dropdown index -> Unit mapping must be stable for settings persistence
        TEST_METHOD(Measurement_GetUnitFromIndex_ValidIndices)
        {
            Assert::IsTrue(Measurement::Unit::Pixel == Measurement::GetUnitFromIndex(0));
            Assert::IsTrue(Measurement::Unit::Inch == Measurement::GetUnitFromIndex(1));
            Assert::IsTrue(Measurement::Unit::Centimetre == Measurement::GetUnitFromIndex(2));
            Assert::IsTrue(Measurement::Unit::Millimetre == Measurement::GetUnitFromIndex(3));
        }

        // Product code: Measurement::GetUnitFromIndex() in Measurement.cpp
        // What: Verifies invalid indices default to Pixel
        // Why: Prevents crash/UB if settings file has corrupt index value
        TEST_METHOD(Measurement_GetUnitFromIndex_InvalidDefaultsToPixel)
        {
            Assert::IsTrue(Measurement::Unit::Pixel == Measurement::GetUnitFromIndex(-1));
            Assert::IsTrue(Measurement::Unit::Pixel == Measurement::GetUnitFromIndex(99));
        }
    };

    // ── Constants verification ───────────────────────────────────────────────

    TEST_CLASS(ConstantsTests)
    {
    public:
        // Product code: consts::TARGET_FRAME_RATE (constants.h)
        // What: Verifies target frame rate is 90 FPS
        // Why: Safety-critical — wrong frame rate causes UI jank or excessive CPU usage
        TEST_METHOD(TargetFrameRate_Is90)
        {
            Assert::AreEqual(static_cast<size_t>(90), consts::TARGET_FRAME_RATE);
        }

        // Product code: consts::MOUSE_WHEEL_TOLERANCE_STEP (constants.h)
        // What: Verifies mouse wheel tolerance step is 15
        // Why: Safety-critical — controls edge detection sensitivity per scroll notch;
        //      wrong value makes tolerance adjustment too coarse or too fine for users
        TEST_METHOD(MouseWheelToleranceStep_Is15)
        {
            Assert::AreEqual(static_cast<int8_t>(15), consts::MOUSE_WHEEL_TOLERANCE_STEP);
        }
    };
}
