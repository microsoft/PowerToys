#include "pch.h"

#include "PerGlyphOpacityTextRender.h"

PerGlyphOpacityTextRender::PerGlyphOpacityTextRender(
    winrt::com_ptr<ID2D1Factory> pD2DFactory,
    winrt::com_ptr<ID2D1RenderTarget> rt,
    winrt::com_ptr<ID2D1SolidColorBrush> baseBrush) :
    _pD2DFactory{ pD2DFactory.get() },
    _rt{ rt.get() },
    _baseBrush{ baseBrush.get() }
{
}

HRESULT __stdcall PerGlyphOpacityTextRender::DrawGlyphRun(void* /*clientDrawingContext*/,
                                                          FLOAT baselineOriginX,
                                                          FLOAT baselineOriginY,
                                                          DWRITE_MEASURING_MODE measuringMode,
                                                          _In_ const DWRITE_GLYPH_RUN* glyphRun,
                                                          _In_ const DWRITE_GLYPH_RUN_DESCRIPTION* /*glyphRunDescription*/,
                                                          IUnknown* clientDrawingEffect_) noexcept
{
    HRESULT hr = S_OK;
    if (!clientDrawingEffect_)
    {
        _rt->DrawGlyphRun(D2D1_POINT_2F{ .x = baselineOriginX, .y = baselineOriginY }, glyphRun, _baseBrush, measuringMode);
        return hr;
    }
    wil::com_ptr<IUnknown> clientDrawingEffect{ clientDrawingEffect_ };

    // Create the path geometry.
    wil::com_ptr<ID2D1PathGeometry> pathGeometry;
    hr = _pD2DFactory->CreatePathGeometry(&pathGeometry);

    // Write to the path geometry using the geometry sink.
    wil::com_ptr<ID2D1GeometrySink> pSink;
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
            pSink.get());
    }

    if (pSink)
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
    auto opacityEffect = clientDrawingEffect.try_query<IDrawingEffect>();

    if (opacityEffect)
    {
        const auto temp_opacity = dynamic_cast<OpacityEffect*>(opacityEffect.get());
        assert(nullptr != temp_opacity);
        _baseBrush->SetOpacity(temp_opacity->alpha);
    }
    

    if (SUCCEEDED(hr))
    {
        _rt->DrawGeometry(pTransformedGeometry.get(), _baseBrush);
        _rt->FillGeometry(pTransformedGeometry.get(), _baseBrush);
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
