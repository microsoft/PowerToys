#pragma once

#include <winrt/base.h>
#include <wil/resource.h>
#include <Windows.h>
#include <dwrite.h>

struct __declspec(uuid("{01557C9F-E3DD-4C28-AE64-E731EAB479CC}")) IDrawingEffect : IUnknown
{
};

struct OpacityEffect : winrt::implements<OpacityEffect, IDrawingEffect>
{
    float alpha = 1.f;
};

struct PerGlyphOpacityTextRender : winrt::implements<PerGlyphOpacityTextRender, IDWriteTextRenderer>
{
    ID2D1Factory* _pD2DFactory = nullptr;
    ID2D1RenderTarget* _rt = nullptr;
    ID2D1SolidColorBrush* _baseBrush = nullptr;

    PerGlyphOpacityTextRender(
        winrt::com_ptr<ID2D1Factory> pD2DFactory,
        winrt::com_ptr<ID2D1RenderTarget> rt,
        winrt::com_ptr<ID2D1SolidColorBrush> baseBrush);

    HRESULT __stdcall DrawGlyphRun(void* clientDrawingContext,
                                   FLOAT baselineOriginX,
                                   FLOAT baselineOriginY,
                                   DWRITE_MEASURING_MODE measuringMode,
                                   _In_ const DWRITE_GLYPH_RUN* glyphRun,
                                   _In_ const DWRITE_GLYPH_RUN_DESCRIPTION* glyphRunDescription,
                                   IUnknown* clientDrawingEffect) noexcept override;
    HRESULT __stdcall DrawUnderline(void* clientDrawingContext,
                                    FLOAT baselineOriginX,
                                    FLOAT baselineOriginY,
                                    _In_ const DWRITE_UNDERLINE* underline,
                                    IUnknown* clientDrawingEffect) noexcept override;
    HRESULT __stdcall DrawStrikethrough(void* clientDrawingContext,
                                        FLOAT baselineOriginX,
                                        FLOAT baselineOriginY,
                                        _In_ const DWRITE_STRIKETHROUGH* strikethrough,
                                        IUnknown* clientDrawingEffect) noexcept override;
    HRESULT __stdcall DrawInlineObject(void* clientDrawingContext,
                                       FLOAT originX,
                                       FLOAT originY,
                                       IDWriteInlineObject* inlineObject,
                                       BOOL isSideways,
                                       BOOL isRightToLeft,
                                       IUnknown* clientDrawingEffect) noexcept override;
    HRESULT __stdcall IsPixelSnappingDisabled(void* clientDrawingContext, BOOL* isDisabled) noexcept override;
    HRESULT __stdcall GetCurrentTransform(void* clientDrawingContext, DWRITE_MATRIX* transform) noexcept override;
    HRESULT __stdcall GetPixelsPerDip(void* clientDrawingContext, FLOAT* pixelsPerDip) noexcept override;
};
