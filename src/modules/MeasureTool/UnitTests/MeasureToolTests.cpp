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

    // ── Unit conversion helpers ─────────────────────────────────────────────
    // Replicates the anonymous-namespace Convert() in Measurement.cpp so we
    // can verify the mathematical formulas independently.

    enum MeasureUnit
    {
        Pixel = 1,
        Inch = 2,
        Centimetre = 4,
        Millimetre = 8,
    };

    inline float ConvertPixels(float pixels, MeasureUnit units, float px2mmRatio)
    {
        if (px2mmRatio > 0)
        {
            switch (units)
            {
            case Pixel: return pixels;
            case Inch: return pixels * px2mmRatio / 10.0f / 2.54f;
            case Centimetre: return pixels * px2mmRatio / 10.0f;
            case Millimetre: return pixels * px2mmRatio;
            default: return pixels;
            }
        }
        else
        {
            switch (units)
            {
            case Pixel: return pixels;
            case Inch: return pixels / 96.0f;
            case Centimetre: return pixels / 96.0f * 2.54f;
            case Millimetre: return pixels / 96.0f / 10.0f * 2.54f;
            default: return pixels;
            }
        }
    }

    inline int GetUnitFromIndex(int index)
    {
        switch (index)
        {
        case 0: return Pixel;
        case 1: return Inch;
        case 2: return Centimetre;
        case 3: return Millimetre;
        default: return Pixel;
        }
    }

    // ── BGRATextureView tests ───────────────────────────────────────────────

    TEST_CLASS(BGRATextureViewTests)
    {
    public:
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

        TEST_METHOD(PixelsClose_PerChannel_IdenticalPixels)
        {
            Assert::IsTrue(BGRATextureView::PixelsClose<true>(RED, RED, 0));
            Assert::IsTrue(BGRATextureView::PixelsClose<true>(WHITE, WHITE, 0));
            Assert::IsTrue(BGRATextureView::PixelsClose<true>(BLACK, BLACK, 0));
        }

        TEST_METHOD(PixelsClose_Total_IdenticalPixels)
        {
            Assert::IsTrue(BGRATextureView::PixelsClose<false>(RED, RED, 0));
            Assert::IsTrue(BGRATextureView::PixelsClose<false>(WHITE, WHITE, 0));
            Assert::IsTrue(BGRATextureView::PixelsClose<false>(BLACK, BLACK, 0));
        }

        TEST_METHOD(PixelsClose_PerChannel_WithinTolerance)
        {
            // Each channel differs by exactly 1
            uint32_t p1 = 0xFF804020; // A=FF R=80 G=40 B=20
            uint32_t p2 = 0xFF814121; // A=FF R=81 G=41 B=21
            Assert::IsTrue(BGRATextureView::PixelsClose<true>(p1, p2, 1),
                           L"Each channel differs by 1, tolerance=1 should pass");
        }

        TEST_METHOD(PixelsClose_PerChannel_ExceedsTolerance)
        {
            // Red channel differs by 17 (0x91 - 0x80 = 0x11)
            uint32_t p1 = 0xFF800000;
            uint32_t p2 = 0xFF910000;
            Assert::IsFalse(BGRATextureView::PixelsClose<true>(p1, p2, 10),
                            L"R differs by 17, tolerance=10 should fail");
        }

        TEST_METHOD(PixelsClose_PerChannel_ExactBoundary)
        {
            uint32_t p1 = 0xFF000000;
            uint32_t p2 = 0xFF1E0000; // R differs by 30 (0x1E)
            Assert::IsTrue(BGRATextureView::PixelsClose<true>(p1, p2, 30),
                           L"Diff equals tolerance → pass");
            Assert::IsFalse(BGRATextureView::PixelsClose<true>(p1, p2, 29),
                            L"Diff exceeds tolerance by 1 → fail");
        }

        TEST_METHOD(PixelsClose_Total_WithinTolerance)
        {
            // Total diff = 5+5+5+0 = 15
            uint32_t p1 = 0xFF000000;
            uint32_t p2 = 0xFF050505;
            Assert::IsTrue(BGRATextureView::PixelsClose<false>(p1, p2, 15),
                           L"Total diff=15, tolerance=15 should pass");
        }

        TEST_METHOD(PixelsClose_Total_ExceedsTolerance)
        {
            uint32_t p1 = 0xFF000000;
            uint32_t p2 = 0xFF050505; // total diff = 15
            Assert::IsFalse(BGRATextureView::PixelsClose<false>(p1, p2, 14),
                            L"Total diff=15, tolerance=14 should fail");
        }

        TEST_METHOD(PixelsClose_CompletelyDifferent)
        {
            // Black (0xFF000000) vs White (0xFFFFFFFF): each RGB channel differs by 255
            Assert::IsFalse(BGRATextureView::PixelsClose<true>(BLACK, WHITE, 254),
                            L"Per-channel: 255 > 254 tolerance");
        }
    };

    // ── Edge detection tests ────────────────────────────────────────────────

    TEST_CLASS(EdgeDetectionTests)
    {
    public:
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

    // ── Unit conversion math tests ──────────────────────────────────────────

    TEST_CLASS(UnitConversionTests)
    {
    public:
        TEST_METHOD(PixelMode_Identity)
        {
            Assert::AreEqual(100.f, ConvertPixels(100.f, Pixel, 0.f));
            Assert::AreEqual(100.f, ConvertPixels(100.f, Pixel, 0.5f));
        }

        TEST_METHOD(Inch_FallbackDPI)
        {
            // 96 pixels @ 96 DPI = 1 inch
            float result = ConvertPixels(96.f, Inch, 0.f);
            Assert::AreEqual(1.f, result, 0.001f, L"96 pixels should be 1 inch at 96 DPI fallback");
        }

        TEST_METHOD(Centimetre_FallbackDPI)
        {
            // 96 pixels @ 96 DPI = 2.54 cm
            float result = ConvertPixels(96.f, Centimetre, 0.f);
            Assert::AreEqual(2.54f, result, 0.001f, L"96 pixels should be 2.54 cm at 96 DPI");
        }

        TEST_METHOD(Millimetre_FallbackDPI)
        {
            // 96 / 96 / 10 * 2.54 = 0.254 mm
            float result = ConvertPixels(96.f, Millimetre, 0.f);
            Assert::AreEqual(0.254f, result, 0.001f);
        }

        TEST_METHOD(Inch_WithPx2mmRatio)
        {
            // 100 pixels * 0.5 / 10 / 2.54 ≈ 1.9685 inches
            float result = ConvertPixels(100.f, Inch, 0.5f);
            float expected = 100.f * 0.5f / 10.f / 2.54f;
            Assert::AreEqual(expected, result, 0.001f);
        }

        TEST_METHOD(Centimetre_WithPx2mmRatio)
        {
            // 100 pixels * 0.5 / 10 = 5.0 cm
            float result = ConvertPixels(100.f, Centimetre, 0.5f);
            Assert::AreEqual(5.f, result, 0.001f);
        }

        TEST_METHOD(Millimetre_WithPx2mmRatio)
        {
            // 100 pixels * 0.5 = 50 mm
            float result = ConvertPixels(100.f, Millimetre, 0.5f);
            Assert::AreEqual(50.f, result, 0.001f);
        }

        TEST_METHOD(GetUnitFromIndex_ValidIndices)
        {
            Assert::AreEqual(static_cast<int>(Pixel), GetUnitFromIndex(0));
            Assert::AreEqual(static_cast<int>(Inch), GetUnitFromIndex(1));
            Assert::AreEqual(static_cast<int>(Centimetre), GetUnitFromIndex(2));
            Assert::AreEqual(static_cast<int>(Millimetre), GetUnitFromIndex(3));
        }

        TEST_METHOD(GetUnitFromIndex_InvalidDefaultsToPixel)
        {
            Assert::AreEqual(static_cast<int>(Pixel), GetUnitFromIndex(-1));
            Assert::AreEqual(static_cast<int>(Pixel), GetUnitFromIndex(99));
        }

        TEST_METHOD(MeasurementWidth_InclusiveRange)
        {
            // Measurement uses inclusive range: width = right - left + 1
            float left = 10.f, right = 109.f;
            float width = right - left + 1.f;
            Assert::AreEqual(100.f, width);
        }

        TEST_METHOD(MeasurementHeight_InclusiveRange)
        {
            float top = 20.f, bottom = 69.f;
            float height = bottom - top + 1.f;
            Assert::AreEqual(50.f, height);
        }

        TEST_METHOD(UnitConversion_96DPI_1Inch)
        {
            // The classic: 96 pixels at default DPI equals exactly 1 inch
            float pixels = 96.f;
            float inches = ConvertPixels(pixels, Inch, 0.f);
            Assert::AreEqual(1.0f, inches, 0.0001f);
        }
    };

    // ── Constants verification ───────────────────────────────────────────────

    TEST_CLASS(ConstantsTests)
    {
    public:
        TEST_METHOD(TargetFrameRate_Is90)
        {
            Assert::AreEqual(static_cast<size_t>(90), consts::TARGET_FRAME_RATE);
        }

        TEST_METHOD(FontSize_Is14)
        {
            Assert::AreEqual(14.f, consts::FONT_SIZE);
        }

        TEST_METHOD(TextBoxCornerRadius_Is4)
        {
            Assert::AreEqual(4.f, consts::TEXT_BOX_CORNER_RADIUS);
        }

        TEST_METHOD(FeetHalfLength_Is2)
        {
            Assert::AreEqual(2.f, consts::FEET_HALF_LENGTH);
        }

        TEST_METHOD(MouseWheelToleranceStep_Is15)
        {
            Assert::AreEqual(static_cast<int8_t>(15), consts::MOUSE_WHEEL_TOLERANCE_STEP);
        }

        TEST_METHOD(CursorOffset_Is4)
        {
            Assert::AreEqual(4L, consts::CURSOR_OFFSET_AMOUNT_X);
            Assert::AreEqual(4L, consts::CURSOR_OFFSET_AMOUNT_Y);
        }

        TEST_METHOD(ShadowOpacity_Is04)
        {
            Assert::AreEqual(.4f, consts::SHADOW_OPACITY);
        }

        TEST_METHOD(CrossOpacity_Is025)
        {
            Assert::AreEqual(.25f, consts::CROSS_OPACITY);
        }
    };
}
