#include "pch.h"

#include "PerGlyphOpacityTextRender.h"

PerGlyphOpacityTextRender::PerGlyphOpacityTextRender(
    wil::com_ptr<ID2D1Factory> pD2DFactory,
    wil::com_ptr<ID2D1HwndRenderTarget> rt,
    wil::com_ptr<ID2D1SolidColorBrush> baseBrush) :
    _pD2DFactory{ pD2DFactory },
    _rt{ rt },
    _baseBrush{ baseBrush }
{
}

HRESULT __stdcall PerGlyphOpacityTextRender::DrawGlyphRun(void* /*clientDrawingContext*/,
                                                          FLOAT baselineOriginX,
                                                          FLOAT baselineOriginY,
                                                          DWRITE_MEASURING_MODE measuringMode,
                                                          _In_ const DWRITE_GLYPH_RUN* glyphRun,
                                                          _In_ const DWRITE_GLYPH_RUN_DESCRIPTION* /*glyphRunDescription*/,
                                                          IUnknown* clientDrawingEffect) noexcept
{
    HRESULT hr = S_OK;
    if (!clientDrawingEffect)
    {
        _rt->DrawGlyphRun(D2D1_POINT_2F{ .x = baselineOriginX, .y = baselineOriginY }, glyphRun, _baseBrush.get(), measuringMode);
        return hr;
    }
    // Create the path geometry.
    wil::com_ptr<ID2D1PathGeometry> pathGeometry;
    hr = _pD2DFactory->CreatePathGeometry(&pathGeometry);

    // Write to the path geometry using the geometry sink.
    ID2D1GeometrySink* pSink = nullptr;
    if (SUCCEEDED(hr))
    {
        hr = pathGeometry->Open(&pSink);
    }

    // Get the glyph run outline geometries back from DirectWrite and place them within the
    // geometry sink.
    if (SUCCEEDED(hr))
    {
        hr = glyphRun->fontFace->GetGlyphRunOutline(
            glyphRun->fontEmSize,
            glyphRun->glyphIndices,
            glyphRun->glyphAdvances,
            glyphRun->glyphOffsets,
            glyphRun->glyphCount,
            glyphRun->isSideways,
            glyphRun->bidiLevel % 2,
            pSink);
    }

    // Close the geometry sink
    if (SUCCEEDED(hr))
    {
        hr = pSink->Close();
    }

    // Initialize a matrix to translate the origin of the glyph run.
    D2D1::Matrix3x2F const matrix = D2D1::Matrix3x2F(
        1.0f, 0.0f, 0.0f, 1.0f, baselineOriginX, baselineOriginY);

    // Create the transformed geometry
    wil::com_ptr<ID2D1TransformedGeometry> pTransformedGeometry;
    if (SUCCEEDED(hr))
    {
        hr = _pD2DFactory->CreateTransformedGeometry(pathGeometry.get(), &matrix, &pTransformedGeometry);
    }

    float prevOpacity = _baseBrush->GetOpacity();
    OpacityEffect* opacityEffect = nullptr;
    if (SUCCEEDED(hr))
    {
        hr = clientDrawingEffect->QueryInterface(__uuidof(IDrawingEffect), reinterpret_cast<void**>(&opacityEffect));
    }

    if (SUCCEEDED(hr))
    {
        _baseBrush->SetOpacity(opacityEffect->alpha);
    }

    if (SUCCEEDED(hr))
    {
        _rt->DrawGeometry(pTransformedGeometry.get(), _baseBrush.get());
        _rt->FillGeometry(pTransformedGeometry.get(), _baseBrush.get());
        _baseBrush->SetOpacity(prevOpacity);
    }

    return hr;
}

HRESULT __stdcall PerGlyphOpacityTextRender::DrawUnderline(void* /*clientDrawingContext*/,
                                                           FLOAT /*baselineOriginX*/,
                                                           FLOAT /*baselineOriginY*/,
                                                           _In_ const DWRITE_UNDERLINE* /*underline*/,
                                                           IUnknown* /*clientDrawingEffect*/) noexcept
{
    return E_NOTIMPL;
}

HRESULT __stdcall PerGlyphOpacityTextRender::DrawStrikethrough(void* /*clientDrawingContext*/,
                                                               FLOAT /*baselineOriginX*/,
                                                               FLOAT /*baselineOriginY*/,
                                                               _In_ const DWRITE_STRIKETHROUGH* /*strikethrough*/,
                                                               IUnknown* /*clientDrawingEffect*/) noexcept
{
    return E_NOTIMPL;
}

HRESULT __stdcall PerGlyphOpacityTextRender::DrawInlineObject(void* /*clientDrawingContext*/,
                                                              FLOAT /*originX*/,
                                                              FLOAT /*originY*/,
                                                              IDWriteInlineObject* /*inlineObject*/,
                                                              BOOL /*isSideways*/,
                                                              BOOL /*isRightToLeft*/,
                                                              IUnknown* /*clientDrawingEffect*/) noexcept
{
    return E_NOTIMPL;
}

HRESULT __stdcall PerGlyphOpacityTextRender::IsPixelSnappingDisabled(void* /*clientDrawingContext*/, BOOL* isDisabled) noexcept
{
    RETURN_HR_IF_NULL(E_INVALIDARG, isDisabled);

    *isDisabled = false;
    return S_OK;
}

HRESULT __stdcall PerGlyphOpacityTextRender::GetCurrentTransform(void* /*clientDrawingContext*/, DWRITE_MATRIX* transform) noexcept
{
    _rt->GetTransform(reinterpret_cast<D2D1_MATRIX_3X2_F*>(transform));
    return S_OK;
}

HRESULT __stdcall PerGlyphOpacityTextRender::GetPixelsPerDip(void* /*clientDrawingContext*/, FLOAT* pixelsPerDip) noexcept
{
    _rt->GetDpi(pixelsPerDip, pixelsPerDip);
    return S_OK;
}
