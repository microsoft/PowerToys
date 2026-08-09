#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "BoundsSnapModel.h"

#include <vector>

using namespace BoundsSnapModel;
using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace MeasureToolCoreUnitTests
{
    namespace
    {
        class TestTexture
        {
        public:
            TestTexture(size_t width, size_t height, uint32_t color) :
                _pixels(width * height, color)
            {
                view.pixels = _pixels.data();
                view.pitch = width;
                view.width = width;
                view.height = height;
            }

            void Fill(RECT bounds, uint32_t color)
            {
                for (LONG y = bounds.top; y <= bounds.bottom; ++y)
                {
                    for (LONG x = bounds.left; x <= bounds.right; ++x)
                    {
                        _pixels[static_cast<size_t>(x) + view.pitch * static_cast<size_t>(y)] = color;
                    }
                }
            }

            void FillCheckerboard(RECT bounds, int cellSize, uint32_t first, uint32_t second)
            {
                for (LONG y = bounds.top; y <= bounds.bottom; ++y)
                {
                    for (LONG x = bounds.left; x <= bounds.right; ++x)
                    {
                        const bool alternate = ((x / cellSize) + (y / cellSize)) % 2 != 0;
                        _pixels[static_cast<size_t>(x) + view.pitch * static_cast<size_t>(y)] =
                            alternate ? second : first;
                    }
                }
            }

            BGRATextureView view;

        private:
            std::vector<uint32_t> _pixels;
        };

        void AssertRectEquals(const RECT& expected, const RECT& actual)
        {
            Assert::AreEqual(expected.left, actual.left);
            Assert::AreEqual(expected.top, actual.top);
            Assert::AreEqual(expected.right, actual.right);
            Assert::AreEqual(expected.bottom, actual.bottom);
        }
    }

    TEST_CLASS(BoundsSnapModelTests)
    {
    public:
        TEST_METHOD(NormalizeBoundsSupportsReverseDirectionDrags)
        {
            constexpr RECT expected{ 10, 20, 80, 90 };
            const RECT actual = NormalizeBounds(POINT{ 80, 90 }, POINT{ 10, 20 });

            AssertRectEquals(expected, actual);
        }

        TEST_METHOD(FitSelectionFindsEnclosedRectangle)
        {
            TestTexture texture{ 60, 50, 0xff202020 };
            constexpr RECT objectBounds{ 15, 12, 44, 37 };
            texture.Fill(objectBounds, 0xffe0e0e0);

            const auto fitted = FitSelectionToContent(
                texture.view,
                RECT{ 4, 4, 55, 45 },
                false,
                30);

            Assert::IsTrue(fitted.has_value());
            AssertRectEquals(objectBounds, *fitted);
        }

        TEST_METHOD(FitSelectionDoesNotRequireObjectAtSelectionCenter)
        {
            TestTexture texture{ 70, 50, 0xff202020 };
            constexpr RECT objectBounds{ 42, 10, 60, 38 };
            texture.Fill(objectBounds, 0xffe0e0e0);

            const auto fitted = FitSelectionToContent(
                texture.view,
                RECT{ 4, 4, 65, 45 },
                false,
                30);

            Assert::IsTrue(fitted.has_value());
            AssertRectEquals(objectBounds, *fitted);
        }

        TEST_METHOD(FitSelectionIgnoresLowContrastShadow)
        {
            TestTexture texture{ 60, 50, 0xff202020 };
            texture.Fill(RECT{ 10, 7, 49, 42 }, 0xff383838);
            constexpr RECT objectBounds{ 15, 12, 44, 37 };
            texture.Fill(objectBounds, 0xffe0e0e0);

            const auto fitted = FitSelectionToContent(
                texture.view,
                RECT{ 4, 4, 55, 45 },
                false,
                30);

            Assert::IsTrue(fitted.has_value());
            AssertRectEquals(objectBounds, *fitted);
        }

        TEST_METHOD(FitSelectionAdaptsToTexturedBackground)
        {
            TestTexture texture{ 80, 60, 0xff202020 };
            texture.FillCheckerboard(RECT{ 3, 3, 76, 56 }, 5, 0xff202020, 0xff2c2c2c);
            constexpr RECT objectBounds{ 25, 18, 54, 41 };
            texture.Fill(objectBounds, 0xffe0e0e0);

            const auto fitted = FitSelectionToContent(
                texture.view,
                RECT{ 3, 3, 76, 56 },
                false,
                30);

            Assert::IsTrue(fitted.has_value());
            AssertRectEquals(objectBounds, *fitted);
        }

        TEST_METHOD(FitSelectionAdaptsToSparseBackgroundTextureTransitions)
        {
            TestTexture texture{ 100, 80, 0xffe8e8e8 };
            texture.FillCheckerboard(RECT{ 3, 3, 96, 76 }, 12, 0xffe8e8e8, 0xfff6f6f6);
            constexpr RECT objectBounds{ 30, 22, 69, 57 };
            texture.Fill(objectBounds, 0xff2e75b6);

            const auto fitted = FitSelectionToContent(
                texture.view,
                RECT{ 3, 3, 96, 76 },
                false,
                30);

            Assert::IsTrue(fitted.has_value());
            AssertRectEquals(objectBounds, *fitted);
        }

        TEST_METHOD(FitSelectionFindsOuterEdgeAcrossGradualColorChanges)
        {
            TestTexture texture{ 60, 50, 0xff202020 };
            constexpr RECT objectBounds{ 15, 10, 44, 39 };
            for (int inset = 0; inset < 15; ++inset)
            {
                const uint32_t channel = static_cast<uint32_t>(0x28 + inset * 8);
                const uint32_t color = 0xff000000 | (channel << 16) | (channel << 8) | channel;
                texture.Fill(
                    RECT{
                        objectBounds.left + inset,
                        objectBounds.top + inset,
                        objectBounds.right - inset,
                        objectBounds.bottom - inset,
                    },
                    color);
            }

            const auto fitted = FitSelectionToContent(
                texture.view,
                RECT{ 4, 4, 55, 45 },
                false,
                30);

            Assert::IsTrue(fitted.has_value());
            AssertRectEquals(objectBounds, *fitted);
        }

        TEST_METHOD(EquivalentObjectsFitConsistentlyAcrossDifferentBackgrounds)
        {
            TestTexture texture{ 130, 60, 0xff202020 };
            texture.FillCheckerboard(RECT{ 3, 3, 60, 56 }, 4, 0xff202020, 0xff2c2c2c);
            texture.FillCheckerboard(RECT{ 68, 3, 125, 56 }, 4, 0xff505050, 0xff5c5c5c);
            constexpr RECT leftObject{ 18, 15, 45, 40 };
            constexpr RECT rightObject{ 83, 15, 110, 40 };
            texture.Fill(leftObject, 0xffe0e0e0);
            texture.Fill(rightObject, 0xff181818);

            const auto leftFit = FitSelectionToContent(
                texture.view,
                RECT{ 3, 3, 60, 56 },
                false,
                30);
            const auto rightFit = FitSelectionToContent(
                texture.view,
                RECT{ 68, 3, 125, 56 },
                false,
                30);

            Assert::IsTrue(leftFit.has_value());
            Assert::IsTrue(rightFit.has_value());
            AssertRectEquals(leftObject, *leftFit);
            AssertRectEquals(rightObject, *rightFit);
            Assert::AreEqual(leftFit->right - leftFit->left, rightFit->right - rightFit->left);
            Assert::AreEqual(leftFit->bottom - leftFit->top, rightFit->bottom - rightFit->top);
        }

        TEST_METHOD(ConfiguredToleranceStillControlsSubtleEdges)
        {
            TestTexture texture{ 40, 40, 0xff202020 };
            constexpr RECT objectBounds{ 10, 10, 29, 29 };
            texture.Fill(objectBounds, 0xff2c2c2c);

            const auto sensitiveFit = FitSelectionToContent(
                texture.view,
                RECT{ 4, 4, 35, 35 },
                false,
                30);
            const auto tolerantFit = FitSelectionToContent(
                texture.view,
                RECT{ 4, 4, 35, 35 },
                false,
                40);

            Assert::IsTrue(sensitiveFit.has_value());
            AssertRectEquals(objectBounds, *sensitiveFit);
            Assert::IsFalse(tolerantFit.has_value());
        }

        TEST_METHOD(FitSelectionChoosesLargestEnclosedObject)
        {
            TestTexture texture{ 80, 60, 0xff202020 };
            texture.Fill(RECT{ 8, 8, 15, 15 }, 0xffe0e0e0);
            constexpr RECT objectBounds{ 30, 15, 65, 45 };
            texture.Fill(objectBounds, 0xffd0d0d0);

            const auto fitted = FitSelectionToContent(
                texture.view,
                RECT{ 3, 3, 75, 55 },
                false,
                30);

            Assert::IsTrue(fitted.has_value());
            AssertRectEquals(objectBounds, *fitted);
        }

        TEST_METHOD(UniformSelectionProducesNoContentBounds)
        {
            TestTexture texture{ 40, 40, 0xff202020 };

            const auto fitted = FitSelectionToContent(
                texture.view,
                RECT{ 5, 5, 34, 34 },
                false,
                30);

            Assert::IsFalse(fitted.has_value());
        }

        TEST_METHOD(ObjectTouchingSelectionBoundaryDoesNotProduceInsetBounds)
        {
            TestTexture texture{ 40, 40, 0xff202020 };
            texture.Fill(RECT{ 5, 5, 34, 34 }, 0xffe0e0e0);

            const auto fitted = FitSelectionToContent(
                texture.view,
                RECT{ 5, 5, 34, 34 },
                false,
                30);

            Assert::IsFalse(fitted.has_value());
        }
    };
}
