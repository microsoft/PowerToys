// Renderer.cpp

#include "Renderer.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cwchar>
#include <vector>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "d2d1.lib")
#pragma comment(lib, "dcomp.lib")
#pragma comment(lib, "dwrite.lib")

namespace desktopgrass {

namespace {

inline D2D1::ColorF FromArgb(uint32_t argb) {
    const float a = ((argb >> 24) & 0xFF) / 255.0f;
    const float r = ((argb >> 16) & 0xFF) / 255.0f;
    const float g = ((argb >>  8) & 0xFF) / 255.0f;
    const float b = ( argb        & 0xFF) / 255.0f;
    return D2D1::ColorF(r, g, b, a);
}

void LogHR(const char* tag, HRESULT hr) {
    char buf[128];
    std::snprintf(buf, sizeof(buf), "[DesktopGrass] %s failed: 0x%08lX\n",
                  tag, static_cast<unsigned long>(hr));
    OutputDebugStringA(buf);
}

constexpr float SHEEP_CURIOUS_VERTICAL_RADIUS_DIP = 120.0f;


// Render-only shear that leans a tree about its trunk base (pivotGy) by a
// damped, clamped fraction of the blade's effectiveLean. Returns identity for
// degenerate heights so the matrix is always well-formed.
D2D1_MATRIX_3X2_F TreeSwayTransform(const Blade& b, double totalH, double pivotGy) noexcept {
    if (!(totalH > 0.0)) return D2D1::Matrix3x2F::Identity();
    double apexLean = b.effectiveLean * TREE_SWAY_LEAN_FACTOR;
    const double maxApex = TREE_SWAY_MAX_HEIGHT_FRACTION * totalH;
    if (apexLean >  maxApex) apexLean =  maxApex;
    if (apexLean < -maxApex) apexLean = -maxApex;
    const float k = static_cast<float>(apexLean / totalH);
    return D2D1::Matrix3x2F(1.0f, 0.0f, -k, 1.0f, k * static_cast<float>(pivotGy), 0.0f);
}

} // anonymous

Renderer::~Renderer() {
    Cleanup();
}

void Renderer::Cleanup() {
    DiscardDeviceResources();
    dcompVisual_.Reset();
    dcompTarget_.Reset();
    dcompDevice_.Reset();
    initialized_ = false;
}

bool Renderer::CreateDeviceResources() {
    HRESULT hr = S_OK;

    UINT d3dFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
#ifdef _DEBUG
    // d3dFlags |= D3D11_CREATE_DEVICE_DEBUG; // skip — requires SDK debug layer
#endif

    static const D3D_FEATURE_LEVEL kFeatures[] = {
        D3D_FEATURE_LEVEL_11_1,
        D3D_FEATURE_LEVEL_11_0,
        D3D_FEATURE_LEVEL_10_1,
        D3D_FEATURE_LEVEL_10_0,
    };

    hr = D3D11CreateDevice(
        nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, d3dFlags,
        kFeatures, ARRAYSIZE(kFeatures), D3D11_SDK_VERSION,
        d3dDevice_.ReleaseAndGetAddressOf(), nullptr,
        d3dContext_.ReleaseAndGetAddressOf());

    if (FAILED(hr)) {
        // Fall back to WARP (software).
        hr = D3D11CreateDevice(
            nullptr, D3D_DRIVER_TYPE_WARP, nullptr, d3dFlags,
            kFeatures, ARRAYSIZE(kFeatures), D3D11_SDK_VERSION,
            d3dDevice_.ReleaseAndGetAddressOf(), nullptr,
            d3dContext_.ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("D3D11CreateDevice", hr); return false; }
    }

    hr = d3dDevice_.As(&dxgiDevice_);
    if (FAILED(hr)) { LogHR("d3dDevice.As<IDXGIDevice1>", hr); return false; }
    dxgiDevice_->SetMaximumFrameLatency(1);

    ComPtr<IDXGIAdapter> adapter;
    hr = dxgiDevice_->GetAdapter(&adapter);
    if (FAILED(hr)) { LogHR("GetAdapter", hr); return false; }
    hr = adapter->GetParent(IID_PPV_ARGS(dxgiFactory_.ReleaseAndGetAddressOf()));
    if (FAILED(hr)) { LogHR("adapter.GetParent<IDXGIFactory2>", hr); return false; }

    D2D1_FACTORY_OPTIONS opts{};
    hr = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED,
                           __uuidof(ID2D1Factory1), &opts,
                           reinterpret_cast<void**>(d2dFactory_.ReleaseAndGetAddressOf()));
    if (FAILED(hr)) { LogHR("D2D1CreateFactory", hr); return false; }

    hr = d2dFactory_->CreateDevice(dxgiDevice_.Get(),
                                   d2dDevice_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateDevice(D2D)", hr); return false; }

    hr = d2dDevice_->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE,
                                         d2dContext_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateDeviceContext", hr); return false; }

    d2dContext_->SetAntialiasMode(D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);
    d2dContext_->SetDpi(static_cast<float>(dpi_), static_cast<float>(dpi_));

    roundStrokeStyle_.Reset();
    {
        D2D1_STROKE_STYLE_PROPERTIES ssp = D2D1::StrokeStyleProperties(
            D2D1_CAP_STYLE_ROUND, D2D1_CAP_STYLE_ROUND, D2D1_CAP_STYLE_ROUND,
            D2D1_LINE_JOIN_ROUND, 1.0f, D2D1_DASH_STYLE_SOLID, 0.0f);
        hr = d2dFactory_->CreateStrokeStyle(ssp, nullptr, 0,
                                            roundStrokeStyle_.ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateStrokeStyle", hr); return false; }
    }


    hr = DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED,
                             __uuidof(IDWriteFactory),
                             reinterpret_cast<IUnknown**>(dwriteFactory_.ReleaseAndGetAddressOf()));
    if (FAILED(hr)) { LogHR("DWriteCreateFactory", hr); return false; }

    petNameTextFormat_.Reset();
    hr = dwriteFactory_->CreateTextFormat(
        L"Segoe UI", nullptr,
        DWRITE_FONT_WEIGHT_REGULAR,
        DWRITE_FONT_STYLE_NORMAL,
        DWRITE_FONT_STRETCH_NORMAL,
        static_cast<FLOAT>(PET_NAME_FONT_SIZE),
        L"",
        petNameTextFormat_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateTextFormat", hr); return false; }
    petNameTextFormat_->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
    petNameTextFormat_->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR);

    // DComp device tied to the same DXGI device.
    hr = DCompositionCreateDevice(dxgiDevice_.Get(),
                                  __uuidof(IDCompositionDevice),
                                  reinterpret_cast<void**>(dcompDevice_.ReleaseAndGetAddressOf()));
    if (FAILED(hr)) { LogHR("DCompositionCreateDevice", hr); return false; }

    hr = dcompDevice_->CreateTargetForHwnd(hwnd_, TRUE, dcompTarget_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateTargetForHwnd", hr); return false; }

    hr = dcompDevice_->CreateVisual(dcompVisual_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateVisual", hr); return false; }

    // Pre-create palette brushes for every scene (§13). Brushes are tiny
    // (a few floats each) and only ever read at draw time, so we cache
    // SCENE_COUNT × PALETTE_SIZE instead of recreating on scene change.
    for (int s = 0; s < SCENE_COUNT; ++s) {
        for (int i = 0; i < PALETTE_SIZE; ++i) {
            brushes_[s][i].Reset();
            hr = d2dContext_->CreateSolidColorBrush(FromArgb(SCENE_PALETTES[s][i]),
                                                    brushes_[s][i].ReleaseAndGetAddressOf());
            if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
        }
    }

    for (int i = 0; i < FLOWER_PALETTE_SIZE; ++i) {
        flowerHeadBrushes_[i].Reset();
        hr = d2dContext_->CreateSolidColorBrush(FromArgb(FLOWER_PALETTE[i]),
                                                flowerHeadBrushes_[i].ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    }

    for (int i = 0; i < MUSHROOM_PALETTE_SIZE; ++i) {
        mushroomCapBrushes_[i].Reset();
        hr = d2dContext_->CreateSolidColorBrush(FromArgb(MUSHROOM_PALETTE[i]),
                                                mushroomCapBrushes_[i].ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    }

    mushroomStemBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(MUSHROOM_STEM_COLOR),
                                            mushroomStemBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    cactusBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(CACTUS_COLOR),
                                            cactusBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    tumbleweedBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(TUMBLEWEED_COLOR),
                                            tumbleweedBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    snowflakeBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(SNOWFLAKE_COLOR),
                                            snowflakeBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    for (int i = 0; i < LEAF_COLOR_COUNT; ++i) {
        leafBrushes_[i].Reset();
        hr = d2dContext_->CreateSolidColorBrush(FromArgb(LEAF_COLORS[i]),
                                                leafBrushes_[i].ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    }

    snowTipBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(SNOW_TIP_COLOR),
                                            snowTipBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    snowBankShadowBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(SNOW_BANK_SHADOW_COLOR),
                                            snowBankShadowBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    pineBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(PINE_COLOR),
                                            pineBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    pineShadowBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(PINE_SHADOW_COLOR),
                                            pineShadowBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    pineHighlightBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(PINE_HIGHLIGHT_COLOR),
                                            pineHighlightBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    birchBarkBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BIRCH_BARK_COLOR),
                                            birchBarkBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    birchMarkBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BIRCH_MARK_COLOR),
                                            birchMarkBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    mapleTrunkBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(MAPLE_TRUNK_COLOR),
                                            mapleTrunkBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    mapleTrunkDarkBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(MAPLE_TRUNK_DARK),
                                            mapleTrunkDarkBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    for (int i = 0; i < MAPLE_CANOPY_COLOR_COUNT; ++i) {
        mapleCanopyBrushes_[i].Reset();
        hr = d2dContext_->CreateSolidColorBrush(FromArgb(MAPLE_CANOPY_COLORS[i]),
                                                mapleCanopyBrushes_[i].ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    }

    for (int i = 0; i < CORAL_COLOR_COUNT; ++i) {
        coralBrushes_[i].Reset();
        hr = d2dContext_->CreateSolidColorBrush(FromArgb(CORAL_COLORS[i]),
                                                coralBrushes_[i].ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    }
    bubbleStrokeBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUBBLE_STROKE_COLOR),
                                            bubbleStrokeBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    bubbleHighlightBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUBBLE_HIGHLIGHT_COLOR),
                                            bubbleHighlightBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    for (int i = 0; i < FISH_COLOR_COUNT; ++i) {
        fishBrushes_[i].Reset();
        hr = d2dContext_->CreateSolidColorBrush(FromArgb(FISH_COLORS[i]),
                                                fishBrushes_[i].ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    }
    fishFinBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(FISH_FIN_COLOR),
                                            fishFinBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    sheepBodyBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(SHEEP_BODY_COLOR),
                                            sheepBodyBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    sheepLegBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(SHEEP_LEG_COLOR),
                                            sheepLegBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    sheepFaceBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(SHEEP_FACE_COLOR),
                                            sheepFaceBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    sheepEarBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(SHEEP_EAR_COLOR),
                                            sheepEarBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    sheepInkBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(SHEEP_INK_COLOR),
                                            sheepInkBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    for (auto& brushes : catCoatBrushes_) {
        brushes.body.Reset();
        brushes.leg.Reset();
        brushes.face.Reset();
        brushes.ear.Reset();
        brushes.ink.Reset();
    }
    for (int i = 0; i < CAT_COAT_VARIANT_COUNT; ++i) {
        const CatCoatPalette& palette = CAT_COAT_PALETTES[i];
        CatCoatBrushSet& brushes = catCoatBrushes_[i];

        hr = d2dContext_->CreateSolidColorBrush(FromArgb(palette.body),
                                                brushes.body.ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

        hr = d2dContext_->CreateSolidColorBrush(FromArgb(palette.leg),
                                                brushes.leg.ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

        hr = d2dContext_->CreateSolidColorBrush(FromArgb(palette.face),
                                                brushes.face.ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

        hr = d2dContext_->CreateSolidColorBrush(FromArgb(palette.ear),
                                                brushes.ear.ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

        hr = d2dContext_->CreateSolidColorBrush(FromArgb(palette.ink),
                                                brushes.ink.ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    }

    bunnyBodyBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUNNY_BODY_COLOR),
                                            bunnyBodyBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    bunnyBellyBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUNNY_BELLY_COLOR),
                                            bunnyBellyBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    bunnyEarBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUNNY_EAR_COLOR),
                                            bunnyEarBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    bunnyEarInnerBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUNNY_EAR_INNER_COLOR),
                                            bunnyEarInnerBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    bunnyTailBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUNNY_TAIL_COLOR),
                                            bunnyTailBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    bunnyEyeBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUNNY_EYE_COLOR),
                                            bunnyEyeBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    bunnyNoseBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUNNY_NOSE_COLOR),
                                            bunnyNoseBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    hedgehogBodyBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(HEDGEHOG_BODY_COLOR),
                                            hedgehogBodyBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    hedgehogSpikeBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(HEDGEHOG_SPIKE_COLOR),
                                            hedgehogSpikeBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    hedgehogSpikeTipBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(HEDGEHOG_SPIKE_TIP_COLOR),
                                            hedgehogSpikeTipBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    hedgehogNoseBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(HEDGEHOG_NOSE_COLOR),
                                            hedgehogNoseBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    hedgehogEyeBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(HEDGEHOG_EYE_COLOR),
                                            hedgehogEyeBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    butterflyBodyBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUTTERFLY_BODY_COLOR),
                                            butterflyBodyBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    for (int i = 0; i < BUTTERFLY_COLOR_COUNT; ++i) {
        butterflyWingBrushes_[i].Reset();
        butterflyAccentBrushes_[i].Reset();
        hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUTTERFLY_PALETTES[i].wingColor),
                                                butterflyWingBrushes_[i].ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
        hr = d2dContext_->CreateSolidColorBrush(FromArgb(BUTTERFLY_PALETTES[i].accentColor),
                                                butterflyAccentBrushes_[i].ReleaseAndGetAddressOf());
        if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    }

    fireflyBodyBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(FIREFLY_BODY_COLOR),
                                            fireflyBodyBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }
    fireflyGlowBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(D2D1::ColorF(
        static_cast<float>((FIREFLY_GLOW_COLOR_RGB >> 16) & 0xFF) / 255.0f,
        static_cast<float>((FIREFLY_GLOW_COLOR_RGB >>  8) & 0xFF) / 255.0f,
        static_cast<float>( FIREFLY_GLOW_COLOR_RGB        & 0xFF) / 255.0f,
        1.0f),
        fireflyGlowBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    birdBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(BIRD_BODY_COLOR),
                                            birdBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    petNameBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(PET_NAME_COLOR),
                                            petNameBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    petNameShadowBrush_.Reset();
    hr = d2dContext_->CreateSolidColorBrush(FromArgb(PET_NAME_SHADOW_COLOR),
                                            petNameShadowBrush_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSolidColorBrush", hr); return false; }

    return true;
}

bool Renderer::CreateSwapChainResources(int widthPx, int heightPx) {
    if (widthPx <= 0 || heightPx <= 0) return false;

    DXGI_SWAP_CHAIN_DESC1 desc{};
    desc.Width            = static_cast<UINT>(widthPx);
    desc.Height           = static_cast<UINT>(heightPx);
    desc.Format           = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.Stereo           = FALSE;
    desc.SampleDesc.Count = 1;
    desc.BufferUsage      = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    desc.BufferCount      = 2;
    desc.Scaling          = DXGI_SCALING_STRETCH;
    desc.SwapEffect       = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
    desc.AlphaMode        = DXGI_ALPHA_MODE_PREMULTIPLIED;
    desc.Flags            = 0;

    HRESULT hr = dxgiFactory_->CreateSwapChainForComposition(
        d3dDevice_.Get(), &desc, nullptr,
        swapChain_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateSwapChainForComposition", hr); return false; }

    ComPtr<IDXGISurface2> surface;
    hr = swapChain_->GetBuffer(0, IID_PPV_ARGS(&surface));
    if (FAILED(hr)) { LogHR("swapChain.GetBuffer", hr); return false; }

    D2D1_BITMAP_PROPERTIES1 bp = D2D1::BitmapProperties1(
        D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED),
        static_cast<float>(dpi_), static_cast<float>(dpi_));

    hr = d2dContext_->CreateBitmapFromDxgiSurface(surface.Get(), &bp,
                                                  d2dTarget_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateBitmapFromDxgiSurface", hr); return false; }

    d2dContext_->SetTarget(d2dTarget_.Get());

    hr = dcompVisual_->SetContent(swapChain_.Get());
    if (FAILED(hr)) { LogHR("Visual.SetContent", hr); return false; }

    hr = dcompTarget_->SetRoot(dcompVisual_.Get());
    if (FAILED(hr)) { LogHR("Target.SetRoot", hr); return false; }

    hr = dcompDevice_->Commit();
    if (FAILED(hr)) { LogHR("DComp Commit", hr); return false; }

    return true;
}

void Renderer::DiscardDeviceResources() {
    for (auto& row : brushes_) for (auto& b : row) b.Reset();
    for (auto& b : flowerHeadBrushes_) b.Reset();
    for (auto& b : mushroomCapBrushes_) b.Reset();
    mushroomStemBrush_.Reset();
    cactusBrush_.Reset();
    tumbleweedBrush_.Reset();
    snowflakeBrush_.Reset();
    for (auto& b : leafBrushes_) b.Reset();
    snowTipBrush_.Reset();
    snowBankShadowBrush_.Reset();
    pineBrush_.Reset();
    pineShadowBrush_.Reset();
    pineHighlightBrush_.Reset();
    birchBarkBrush_.Reset();
    birchMarkBrush_.Reset();
    mapleTrunkBrush_.Reset();
    mapleTrunkDarkBrush_.Reset();
    for (auto& b : mapleCanopyBrushes_) b.Reset();
    for (auto& b : coralBrushes_) b.Reset();
    bubbleStrokeBrush_.Reset();
    bubbleHighlightBrush_.Reset();
    for (auto& b : fishBrushes_) b.Reset();
    fishFinBrush_.Reset();
    sheepBodyBrush_.Reset();
    sheepLegBrush_.Reset();
    sheepFaceBrush_.Reset();
    sheepEarBrush_.Reset();
    sheepInkBrush_.Reset();
    for (auto& brushes : catCoatBrushes_) {
        brushes.body.Reset();
        brushes.leg.Reset();
        brushes.face.Reset();
        brushes.ear.Reset();
        brushes.ink.Reset();
    }
    bunnyBodyBrush_.Reset();
    bunnyBellyBrush_.Reset();
    bunnyEarBrush_.Reset();
    bunnyEarInnerBrush_.Reset();
    bunnyTailBrush_.Reset();
    bunnyEyeBrush_.Reset();
    bunnyNoseBrush_.Reset();
    butterflyBodyBrush_.Reset();
    for (auto& b : butterflyWingBrushes_) b.Reset();
    for (auto& b : butterflyAccentBrushes_) b.Reset();
    fireflyBodyBrush_.Reset();
    fireflyGlowBrush_.Reset();
    petNameBrush_.Reset();
    petNameShadowBrush_.Reset();
    petNameTextFormat_.Reset();
    roundStrokeStyle_.Reset();
    dwriteFactory_.Reset();
    d2dTarget_.Reset();
    if (d2dContext_) d2dContext_->SetTarget(nullptr);
    d2dContext_.Reset();
    d2dDevice_.Reset();
    d2dFactory_.Reset();
    swapChain_.Reset();
    dxgiFactory_.Reset();
    dxgiDevice_.Reset();
    d3dContext_.Reset();
    d3dDevice_.Reset();
}

bool Renderer::Initialize(HWND hwnd, int widthPx, int heightPx,
                          UINT dpi, uint64_t seed, double density,
                          double swaySpeed, double swayAmplitude)
{
    hwnd_     = hwnd;
    widthPx_  = widthPx;
    heightPx_ = heightPx;
    dpi_      = dpi == 0 ? 96 : dpi;

    if (!CreateDeviceResources())   return false;
    if (!CreateSwapChainResources(widthPx, heightPx)) return false;

    const double widthDip  = static_cast<double>(widthPx)  * 96.0 / static_cast<double>(dpi_);
    const double heightDip = static_cast<double>(heightPx) * 96.0 / static_cast<double>(dpi_);
    sim_ = sim_init(seed, widthDip, density);
    sim_.windowHeight = heightDip;
    sim_.swaySpeedScale = swaySpeed;
    sim_.swayAmpScale   = swayAmplitude;
    initialized_ = true;
    return true;
}

bool Renderer::Resize(int widthPx, int heightPx, UINT dpi) {
    if (!initialized_) return false;
    if (widthPx <= 0 || heightPx <= 0) return false;

    widthPx_  = widthPx;
    heightPx_ = heightPx;
    dpi_      = dpi == 0 ? 96 : dpi;

    // Discard render-target view.
    d2dTarget_.Reset();
    if (d2dContext_) d2dContext_->SetTarget(nullptr);

    HRESULT hr = swapChain_->ResizeBuffers(
        0, static_cast<UINT>(widthPx), static_cast<UINT>(heightPx),
        DXGI_FORMAT_UNKNOWN, 0);
    if (FAILED(hr)) {
        LogHR("ResizeBuffers", hr);
        return false;
    }

    ComPtr<IDXGISurface2> surface;
    hr = swapChain_->GetBuffer(0, IID_PPV_ARGS(&surface));
    if (FAILED(hr)) { LogHR("swapChain.GetBuffer(resize)", hr); return false; }

    D2D1_BITMAP_PROPERTIES1 bp = D2D1::BitmapProperties1(
        D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED),
        static_cast<float>(dpi_), static_cast<float>(dpi_));

    hr = d2dContext_->CreateBitmapFromDxgiSurface(surface.Get(), &bp,
                                                  d2dTarget_.ReleaseAndGetAddressOf());
    if (FAILED(hr)) { LogHR("CreateBitmapFromDxgiSurface(resize)", hr); return false; }

    d2dContext_->SetTarget(d2dTarget_.Get());
    d2dContext_->SetDpi(static_cast<float>(dpi_), static_cast<float>(dpi_));

    const double heightDip = static_cast<double>(heightPx) * 96.0 / static_cast<double>(dpi_);
    sim_.windowHeight = heightDip;
    return true;
}

void Renderer::RegenerateForDpi(uint64_t seed, double density) {
    // Reflow the blade layout for the new DIP width after a DPI change,
    // mirroring the Win2D rebuild path (RebuildWindows -> CreatePerMonitorWindows),
    // which recreates the window and re-runs sim init for the new width. We
    // reseed with the SAME deterministic per-monitor seed so the regenerated
    // layout is identical to a fresh launch at the new DPI.
    //
    // IMPORTANT: this is intentionally separate from the device-loss recovery
    // path (D2DERR_RECREATE_TARGET / DXGI_ERROR_DEVICE_*), which only rebuilds
    // GPU resources and must NOT regenerate the sim. Only a DPI change calls
    // here.
    if (!initialized_) return;

    // Width/height in pixels and the DPI were already updated by Resize(); derive
    // the DIP extents the same way Initialize() does.
    const double widthDip  = static_cast<double>(widthPx_)  * 96.0 / static_cast<double>(dpi_);
    const double heightDip = static_cast<double>(heightPx_) * 96.0 / static_cast<double>(dpi_);

    // Preserve live runtime state across the reflow, exactly as the Win2D
    // rebuild does: it persists cuts and re-applies scene / critter selection to
    // the freshly created window.
    const Scene       scene        = sim_.currentScene;
    const CritterKind critter      = sim_.currentCritter;
    const int         critterCount = sim_.critterCountOverride;
    const double      swaySpeed    = sim_.swaySpeedScale;
    const double      swayAmp      = sim_.swayAmpScale;
    const std::vector<persistence::CutRecord> cuts = sim_get_cuts(sim_);

    sim_regenerate(sim_, seed, widthDip, density);
    sim_.windowHeight   = heightDip;
    sim_.swaySpeedScale = swaySpeed;
    sim_.swayAmpScale   = swayAmp;

    // Re-apply persisted/live state in the same order as App::ApplyPersistedStateToWindow.
    sim_set_scene(sim_, scene);
    sim_set_critter_count(sim_, critterCount);
    sim_set_critter(sim_, critter);
    sim_apply_cuts(sim_, cuts);
}

bool Renderer::TryGetCursorPositionDip(D2D1_POINT_2F& cursorPosition) const {
    POINT pt{};
    if (!GetCursorPos(&pt)) return false;

    const double scale = 96.0 / static_cast<double>(dpi_ == 0 ? 96 : dpi_);
    cursorPosition.x = static_cast<float>((pt.x - windowOriginScreenX_) * scale);
    cursorPosition.y = static_cast<float>((pt.y - windowOriginScreenY_) * scale);
    return true;
}

void Renderer::Tick(double dt,
                    const InputEvent* events,
                    std::size_t numEvents)
{
    sim_tick(sim_, clamp_dt(dt), events, numEvents);
}

void Renderer::RenderFrame(double dt,
                           const InputEvent* events,
                           std::size_t numEvents)
{
    if (!initialized_) return;

    Tick(dt, events, numEvents);

    d2dContext_->BeginDraw();
    // Fully transparent background so the layered window stays click-through.
    d2dContext_->Clear(D2D1::ColorF(0.0f, 0.0f, 0.0f, 0.0f));

    D2D1_POINT_2F cursorPosition{};
    const D2D1_POINT_2F* cursorForRender = TryGetCursorPositionDip(cursorPosition)
        ? &cursorPosition
        : nullptr;

    DrawGrass(true, true);   // background treeline (smaller, hazier)
    DrawGrass(false, false); // ground cover incl. Winter snow-tipped grass
    DrawGrass(true, false);  // foreground treeline
    DrawEntities(cursorForRender);

    HRESULT hr = d2dContext_->EndDraw();
    if (hr == D2DERR_RECREATE_TARGET) {
        DiscardDeviceResources();
        dcompVisual_.Reset();
        dcompTarget_.Reset();
        dcompDevice_.Reset();
        initialized_ = false;
        if (CreateDeviceResources() && CreateSwapChainResources(widthPx_, heightPx_)) {
            initialized_ = true;
        }
        return;
    }
    if (FAILED(hr)) { LogHR("EndDraw", hr); }

    DXGI_PRESENT_PARAMETERS pp{};
    hr = swapChain_->Present1(1, 0, &pp);
    if (hr == DXGI_ERROR_DEVICE_REMOVED || hr == DXGI_ERROR_DEVICE_RESET) {
        DiscardDeviceResources();
        dcompVisual_.Reset();
        dcompTarget_.Reset();
        dcompDevice_.Reset();
        initialized_ = false;
        if (CreateDeviceResources() && CreateSwapChainResources(widthPx_, heightPx_)) {
            initialized_ = true;
        }
    } else if (FAILED(hr)) {
        LogHR("Present1", hr);
    }
}

void Renderer::DrawGrass(bool treesOnly, bool backgroundTrees) {
    if (treesOnly && sim_.currentScene != Scene::Winter && sim_.currentScene != Scene::Autumn) return;
    const double groundY = sim_.windowHeight;
    const int sceneIdx = static_cast<int>(sim_.currentScene);
    ComPtr<ID2D1Factory> factoryGeneric;
    d2dFactory_.As(&factoryGeneric);

    // §15.4 depth layer: background trees draw smaller and hazier behind the
    // snowbank. Apply a uniform haze to every tree brush for the current tree.
    auto applyTreeAlpha = [&](float a) {
        if (pineBrush_)      pineBrush_->SetOpacity(a);
        if (pineShadowBrush_) pineShadowBrush_->SetOpacity(a);
        if (snowTipBrush_)   snowTipBrush_->SetOpacity(a);
        if (birchBarkBrush_) birchBarkBrush_->SetOpacity(a);
        if (birchMarkBrush_) birchMarkBrush_->SetOpacity(a);
    };

    auto drawCactusArm = [&](float baseX, float gy, float h, float width, int side) {
        const float sx = baseX;
        const float sy = gy - h * 0.4f;
        const float ex = baseX + static_cast<float>(side) * width * 1.2f;
        const float ey = gy - h * 0.7f;
        const float cx = ex;
        const float cy = sy;
        const float armWidth = width * 0.7f;

        ComPtr<ID2D1PathGeometry> path;
        if (FAILED(d2dFactory_->CreatePathGeometry(&path))) return;
        ComPtr<ID2D1GeometrySink> sink;
        if (FAILED(path->Open(&sink))) return;
        sink->BeginFigure(D2D1::Point2F(sx, sy), D2D1_FIGURE_BEGIN_HOLLOW);
        sink->AddQuadraticBezier(D2D1::QuadraticBezierSegment(
            D2D1::Point2F(cx, cy), D2D1::Point2F(ex, ey)));
        sink->AddLine(D2D1::Point2F(ex, ey - h * 0.10f));
        sink->EndFigure(D2D1_FIGURE_END_OPEN);
        if (FAILED(sink->Close())) return;
        d2dContext_->DrawGeometry(path.Get(), cactusBrush_.Get(), armWidth,
                                  roundStrokeStyle_.Get());
    };

    // §CPU Batch A: instead of issuing ~4 DrawLine calls per plain grass blade
    // (~2,800/frame, each with a per-blade brush that defeats D2D's internal
    // batching), accumulate every blade's tessellated stroke into a small set of
    // grouped path geometries keyed by (hue, quantized thickness). Each group is
    // then stroked with a SINGLE DrawGeometry call sharing one brush + thickness,
    // collapsing thousands of draw calls into ~36. Tip decorations (flower heads,
    // snow caps) are deferred and drawn on top after all strokes so they read
    // crisply and are never clipped by a later blade.
    constexpr int kBucketCount = 32;
    constexpr float kBladeThicknessBucket = 0.25f;
    struct BladeGroup {
        ComPtr<ID2D1PathGeometry> geom;
        ComPtr<ID2D1GeometrySink> sink;
        float thickness = 0.0f;
    };
    BladeGroup bladeGroups[PALETTE_SIZE][kBucketCount];
    struct DeferredEllipse {
        D2D1_ELLIPSE ellipse;
        ID2D1SolidColorBrush* brush;
    };
    std::vector<DeferredEllipse> deferredCaps;

    for (const Blade& b : sim_.blades) {
        if (treesOnly) {
            if (!b.isPine && !b.isMaple) continue;
            // bg pass draws only background pines; fg pass draws the rest
            // (foreground pines + all maples, which are never background).
            const bool isBg = b.isPine && b.treeBackground;
            if (backgroundTrees != isBg) continue;
        } else if (b.isPine || b.isMaple) {
            continue;
        }

        if (b.isCoral) {
            DrawCoral(b, static_cast<float>(groundY));
            continue;
        }

        if (b.isCactus) {
            const float baseX = static_cast<float>(b.baseX);
            const float gy = static_cast<float>(groundY);
            const float width = static_cast<float>(b.cactusWidth);

            if (b.cutHeight < CUT_STUMP_THRESHOLD) {
                d2dContext_->DrawLine(
                    D2D1::Point2F(baseX, gy),
                    D2D1::Point2F(baseX, gy - static_cast<float>(STUMP_HEIGHT)),
                    cactusBrush_.Get(), width);
                continue;
            }

            const float h = static_cast<float>(b.cactusHeight * b.cutHeight);
            const float topY = gy - h;
            const float capR = width * 0.5f * static_cast<float>(b.cutHeight);
            d2dContext_->DrawLine(D2D1::Point2F(baseX, gy), D2D1::Point2F(baseX, topY),
                                  cactusBrush_.Get(), width);
            d2dContext_->FillEllipse(
                D2D1::Ellipse(D2D1::Point2F(baseX, topY), capR, capR),
                cactusBrush_.Get());

            if (b.cutHeight >= CACTUS_ARM_MIN_CUT_HEIGHT) {
                if (b.cactusType == 1) {
                    drawCactusArm(baseX, gy, h, width, b.cactusArmSide < 0 ? -1 : 1);
                } else if (b.cactusType == 2) {
                    drawCactusArm(baseX, gy, h, width, -1);
                    drawCactusArm(baseX, gy, h, width, +1);
                }
            }
            continue;
        }

        // Tree (§15.1). Slot-bound Winter variant. Two styles selected
        // by treeVariant: 0 = classic tiered pine, 1 = bare birch with
        // dark bark marks and short branch stubs. Below CUT_STUMP_THRESHOLD
        // both styles reduce to a short brown stump.
        if (b.isPine) {
            const float baseX = static_cast<float>(b.baseX);
            const float gy    = static_cast<float>(groundY);

            // §15.4 depth: background trees shrink toward their base and fade.
            const bool  bgTree    = b.treeBackground;
            const float treeScale = bgTree ? static_cast<float>(TREE_BG_SCALE) : 1.0f;
            const float treeAlpha = bgTree ? TREE_BG_OPACITY : 1.0f;
            const auto  depthXform = [&](const D2D1_MATRIX_3X2_F& sway) {
                return bgTree
                    ? D2D1::Matrix3x2F::Scale(treeScale, treeScale, D2D1::Point2F(baseX, gy)) * sway
                    : sway;
            };
            applyTreeAlpha(treeAlpha);

            if (b.cutHeight < CUT_STUMP_THRESHOLD) {
                d2dContext_->DrawLine(
                    D2D1::Point2F(baseX, gy),
                    D2D1::Point2F(baseX, gy - static_cast<float>(STUMP_HEIGHT) * treeScale),
                    pineBrush_.Get(),
                    static_cast<float>(std::max(2.0, b.pineWidth * 0.25)) * treeScale);
                applyTreeAlpha(1.0f);
                continue;
            }

            auto drawFilledTri = [&](float cx, float baseY, float topY, float halfW,
                                     ID2D1SolidColorBrush* brush) {
                const float h = baseY - topY;
                if (h <= 0.0f || halfW <= 0.0f) return;
                constexpr float kStep = 0.5f;
                for (float y = baseY; y >= topY; y -= kStep) {
                    const float t  = (baseY - y) / h;
                    const float hw = halfW * (1.0f - t);
                    if (hw <= 0.0f) continue;
                    d2dContext_->DrawLine(
                        D2D1::Point2F(cx - hw, y),
                        D2D1::Point2F(cx + hw, y),
                        brush, kStep * 1.5f);
                }
            };

            if (b.treeVariant == 1) {
                // ---- Birch: vertical trunk + short bark dashes + upward branch fan ----
                const float totalH    = static_cast<float>(b.pineHeight * b.cutHeight);
                const float trunkW    = static_cast<float>(b.pineWidth);
                const float trunkTopY = gy - totalH;

                d2dContext_->SetTransform(depthXform(TreeSwayTransform(b, totalH, gy)));

                d2dContext_->DrawLine(
                    D2D1::Point2F(baseX, gy),
                    D2D1::Point2F(baseX, trunkTopY),
                    birchBarkBrush_.Get(),
                    trunkW);

                // Short bark dashes — centered on trunk, varied lengths so the
                // pattern reads as broken bark "eyes" instead of full ribs.
                static const float kDashLenFrac[BIRCH_BARK_MARK_COUNT] = {
                    0.50f, 0.30f, 0.45f, 0.25f, 0.40f
                };
                for (int m = 0; m < BIRCH_BARK_MARK_COUNT; ++m) {
                    const float tM = (m + 1.0f) / (BIRCH_BARK_MARK_COUNT + 1.0f);
                    const float yM = gy - totalH * tM;
                    const float dashLen = trunkW * kDashLenFrac[m];
                    d2dContext_->DrawLine(
                        D2D1::Point2F(baseX - dashLen * 0.5f, yM),
                        D2D1::Point2F(baseX + dashLen * 0.5f, yM),
                        birchMarkBrush_.Get(),
                        std::max(1.0f, trunkW * 0.22f));
                }

                // Branch fan — hand-tuned for deciduous tree silhouette.
                // Each branch is angled UPWARD (never horizontal) and ends in
                // a small white snow puff, so the shape never reads as a cross.
                struct Branch { float trunkFrac; float angleDeg; float side; float lenMul; };
                static const Branch kBranches[BIRCH_BRANCH_COUNT] = {
                    {0.45f, 35.0f, +1.0f, 1.20f},
                    {0.55f, 50.0f, -1.0f, 1.40f},
                    {0.65f, 25.0f, +1.0f, 1.60f},
                    {0.72f, 60.0f, -1.0f, 1.00f},
                    {0.80f, 20.0f, +1.0f, 1.10f},
                    {0.85f, 45.0f, -1.0f, 0.80f},
                };
                const float branchBaseLen = trunkW * 3.0f;
                const float branchW = std::max(1.0f, trunkW * 0.35f);
                const float snowR   = std::max(1.5f, trunkW * 0.65f);
                for (const auto& br : kBranches) {
                    const float sy   = gy - totalH * br.trunkFrac;
                    const float blen = branchBaseLen * br.lenMul;
                    const float ang  = br.angleDeg * 3.14159265f / 180.0f;
                    const float ex   = baseX + br.side * blen * std::sin(ang);
                    const float ey   = sy - blen * std::cos(ang);
                    d2dContext_->DrawLine(
                        D2D1::Point2F(baseX, sy), D2D1::Point2F(ex, ey),
                        birchBarkBrush_.Get(), branchW);
                    d2dContext_->FillEllipse(
                        D2D1::Ellipse(D2D1::Point2F(ex, ey), snowR, snowR),
                        snowTipBrush_.Get());
                }

                // Small snow puff right at the top of the trunk.
                const float capR = std::max(2.0f, trunkW * 0.9f);
                d2dContext_->FillEllipse(
                    D2D1::Ellipse(D2D1::Point2F(baseX, trunkTopY), capR, capR * 0.6f),
                    snowTipBrush_.Get());
                d2dContext_->SetTransform(D2D1::Matrix3x2F::Identity());
                applyTreeAlpha(1.0f);
                continue;
            }

            // ---- Pine: stacked snow-capped triangle tiers ----
            const int    tierCount  = b.pineTierCount > 0 ? b.pineTierCount : PINE_TIER_COUNT_MIN;
            const double totalH     = b.pineHeight * b.cutHeight;
            const double tierH      = totalH / tierCount;

            d2dContext_->SetTransform(depthXform(TreeSwayTransform(b, totalH, gy)));

            for (int i = 0; i < tierCount; ++i) {
                const double tFrac    = (tierCount == 1) ? 0.0
                                       : static_cast<double>(i) / static_cast<double>(tierCount - 1);
                const double widthAt  = b.pineWidth * (1.0 - tFrac * (1.0 - PINE_TIP_TAPER));
                const double baseY    = gy - i * tierH * (1.0 - PINE_TIER_OVERLAP);
                const double topY     = baseY - tierH;
                const float  halfW    = static_cast<float>(widthAt * 0.5);

                // Dimensional bough: a self-shadow dropped down-right, the body
                // on top, then a lighter lit face dabbed on the upper-left, so
                // the tier reads as rounded volume instead of a flat triangle.
                const float shadowDX = static_cast<float>(halfW * PINE_SHADOW_OFFSET_X_FRAC);
                const float shadowDY = static_cast<float>(tierH * PINE_SHADOW_OFFSET_Y_FRAC);
                drawFilledTri(baseX + shadowDX,
                              static_cast<float>(baseY) + shadowDY,
                              static_cast<float>(topY) + shadowDY,
                              halfW,
                              pineShadowBrush_.Get());
                drawFilledTri(baseX,
                              static_cast<float>(baseY),
                              static_cast<float>(topY),
                              halfW,
                              pineBrush_.Get());
                pineHighlightBrush_->SetOpacity(PINE_HIGHLIGHT_OPACITY * treeAlpha);
                drawFilledTri(baseX - static_cast<float>(halfW * PINE_HIGHLIGHT_OFFSET_X_FRAC),
                              static_cast<float>(baseY),
                              static_cast<float>(topY),
                              static_cast<float>(halfW * PINE_HIGHLIGHT_WIDTH_FRAC),
                              pineHighlightBrush_.Get());
                pineHighlightBrush_->SetOpacity(1.0f);

                // Snow cap: smaller triangle covering top PINE_SNOW_CAP_FRACTION
                // of the tier. Inherits the tier's apex; base is at the cap's
                // bottom (PINE_SNOW_CAP_FRACTION up from the tier apex).
                const double capH      = tierH * PINE_SNOW_CAP_FRACTION;
                const double capBaseY  = topY + capH;
                const double capHalfW  = widthAt * 0.5 * PINE_SNOW_CAP_FRACTION * 1.4;
                drawFilledTri(baseX,
                              static_cast<float>(capBaseY),
                              static_cast<float>(topY),
                              static_cast<float>(capHalfW),
                              snowTipBrush_.Get());
            }
            d2dContext_->SetTransform(D2D1::Matrix3x2F::Identity());
            applyTreeAlpha(1.0f);
            continue;
        }

        if (b.isMaple) {
            const float baseX = static_cast<float>(b.baseX);
            const float gy = static_cast<float>(groundY);
            const float trunkW = static_cast<float>(b.mapleTrunkWidth);

            if (b.cutHeight < CUT_STUMP_THRESHOLD) {
                d2dContext_->DrawLine(
                    D2D1::Point2F(baseX, gy),
                    D2D1::Point2F(baseX, gy - static_cast<float>(STUMP_HEIGHT)),
                    mapleTrunkBrush_.Get(), std::max(2.0f, trunkW * 0.65f));
                continue;
            }

            const float totalH = static_cast<float>(b.mapleHeight * b.cutHeight);
            const float topY = gy - totalH;
            const float canopyR = static_cast<float>(b.mapleCanopyRadius * b.cutHeight);
            d2dContext_->SetTransform(TreeSwayTransform(b, totalH, gy));
            d2dContext_->DrawLine(D2D1::Point2F(baseX, gy), D2D1::Point2F(baseX, topY),
                                  mapleTrunkBrush_.Get(), trunkW);
            d2dContext_->DrawLine(D2D1::Point2F(baseX + trunkW * 0.18f, gy - totalH * 0.08f),
                                  D2D1::Point2F(baseX + trunkW * 0.12f, topY + totalH * 0.15f),
                                  mapleTrunkDarkBrush_.Get(), std::max(1.0f, trunkW * 0.18f));

            struct MapleBranch { float trunkFrac; float angleDeg; float side; float lenMul; };
            static const MapleBranch kBranches[] = {
                {0.58f, 55.0f, -1.0f, 0.95f},
                {0.70f, 38.0f, +1.0f, 1.05f},
                {0.82f, 28.0f, -1.0f, 0.70f},
            };
            D2D1_POINT_2F tips[3]{};
            const float branchBaseLen = std::max(trunkW * 2.6f, canopyR * 0.55f);
            const float branchW = std::max(1.0f, trunkW * 0.32f);
            for (int i = 0; i < 3; ++i) {
                const auto& br = kBranches[i];
                const float sy = gy - totalH * br.trunkFrac;
                const float len = branchBaseLen * br.lenMul;
                const float angle = br.angleDeg * 3.14159265f / 180.0f;
                const float ex = baseX + br.side * len * std::sin(angle);
                const float ey = sy - len * std::cos(angle);
                tips[i] = D2D1::Point2F(ex, ey);
                d2dContext_->DrawLine(D2D1::Point2F(baseX, sy), tips[i],
                                      mapleTrunkDarkBrush_.Get(), branchW);
            }

            if (!b.mapleIsBare) {
                uint8_t idx = b.mapleCanopyColorIdx;
                if (idx >= MAPLE_CANOPY_COLOR_COUNT) idx = 0;
                const float cx = baseX;
                const float cy = topY;
                // Layered crown (§16.5): a broad base disc plus several
                // overlapping leaf clumps in staggered autumn tones, giving a
                // full, organic canopy instead of a single flat oval. dx/dy are
                // fractions of canopyR; colorOff cycles the warm palette.
                struct MapleClump { float dx; float dy; float r; int colorOff; };
                static const MapleClump kClumps[] = {
                    { 0.00f, -0.15f, 1.05f, 0 },  // back base
                    {-0.50f, -0.05f, 0.60f, 1 },
                    { 0.50f, -0.10f, 0.58f, 2 },
                    {-0.28f, -0.48f, 0.54f, 1 },
                    { 0.30f, -0.45f, 0.52f, 2 },
                    { 0.00f, -0.62f, 0.48f, 1 },  // top
                    {-0.15f,  0.30f, 0.55f, 0 },  // lower-left fill
                    { 0.22f,  0.28f, 0.50f, 2 },  // lower-right fill
                };
                for (const auto& c : kClumps) {
                    ID2D1SolidColorBrush* brush =
                        mapleCanopyBrushes_[(idx + c.colorOff) % MAPLE_CANOPY_COLOR_COUNT].Get();
                    d2dContext_->FillEllipse(
                        D2D1::Ellipse(D2D1::Point2F(cx + canopyR * c.dx, cy + canopyR * c.dy),
                                      canopyR * c.r, canopyR * c.r * 0.95f),
                        brush);
                }
                // Two light dabs near the upper-left for a soft sense of light.
                ID2D1SolidColorBrush* hi = mapleCanopyBrushes_[(idx + 3) % MAPLE_CANOPY_COLOR_COUNT].Get();
                d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx - canopyR * 0.34f, cy - canopyR * 0.34f), canopyR * 0.24f, canopyR * 0.20f), hi);
                d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx - canopyR * 0.05f, cy - canopyR * 0.58f), canopyR * 0.18f, canopyR * 0.16f), hi);
            } else {
                for (int i = 0; i < 3; ++i) {
                    ID2D1SolidColorBrush* leafBrush = leafBrushes_[i % LEAF_COLOR_COUNT].Get();
                    d2dContext_->FillEllipse(D2D1::Ellipse(tips[i], 1.8f, 1.8f), leafBrush);
                }
            }
            d2dContext_->SetTransform(D2D1::Matrix3x2F::Identity());
            continue;
        }

        // Mushroom slots preempt grass + flower rendering at this position.
        // Cap + stem scale linearly with cutHeight so the cut animation
        // visibly shrinks them; below CUT_STUMP_THRESHOLD the mushroom is
        // invisible (matches the grass-stump short-circuit in spirit).
        if (b.isMushroom) {
            const float baseX  = static_cast<float>(b.baseX);
            const float gy     = static_cast<float>(groundY);
            const float stemT  = static_cast<float>(b.mushroomStemThickness);

            // Stump-stub short-circuit: when a mushroom is cut below the
            // CUT_STUMP_THRESHOLD, draw a short ivory stem stub. We use
            // MUSHROOM_STUMP_HEIGHT (slightly taller than STUMP_HEIGHT)
            // so the mushroom nub reads as distinct from cut grass.
            if (b.cutHeight < CUT_STUMP_THRESHOLD) {
                d2dContext_->DrawLine(
                    D2D1::Point2F(baseX, gy),
                    D2D1::Point2F(baseX, gy - static_cast<float>(MUSHROOM_STUMP_HEIGHT)),
                    mushroomStemBrush_.Get(),
                    stemT);
                continue;
            }

            const float scale  = static_cast<float>(b.cutHeight);
            const float stemH  = static_cast<float>(b.mushroomStemHeight) * scale;
            const float capRX  = static_cast<float>(b.mushroomCapWidth)  * scale;
            const float capRY  = static_cast<float>(b.mushroomCapHeight) * scale;
            const float capCY  = gy - stemH;

            // Stem: short vertical line, ivory.
            d2dContext_->DrawLine(
                D2D1::Point2F(baseX, gy),
                D2D1::Point2F(baseX, capCY),
                mushroomStemBrush_.Get(),
                stemT);

            // Cap: filled ellipse sitting on top of the stem.
            uint8_t cIdx = b.mushroomCapColorIdx;
            if (cIdx >= MUSHROOM_PALETTE_SIZE) cIdx = 0;
            const D2D1_ELLIPSE cap = D2D1::Ellipse(
                D2D1::Point2F(baseX, capCY), capRX, capRY);
            d2dContext_->FillEllipse(cap, mushroomCapBrushes_[cIdx].Get());
            continue;
        }

        // Winter renders ordinary (non-tree) ground cover as snow-tipped grass
        // blades (handled by the shared blade path below + the snow-tip cap), so
        // no early-out here.

        // §CPU: in Winter, deterministically cull ~25% of plain blades (and their
        // snow caps) to cut the scene's dominant per-frame cost. Keyed on the
        // blade's stable array index so the thinning is steady, not shimmering.
        if (sim_.currentScene == Scene::Winter &&
            winter_blade_culled(static_cast<uint32_t>(&b - sim_.blades.data()))) {
            continue;
        }

        const Stroke s = compute_blade_stroke(b, groundY, sim_.currentScene);

        const float thickness = static_cast<float>(s.thickness + BLADE_THICKNESS_RENDER_BONUS);

        // Tessellate the quadratic Bezier into N line segments and append them as
        // one open figure (round joins/caps) into the geometry group that shares
        // this blade's hue + quantized thickness. The whole group is stroked with
        // a single DrawGeometry below, so this is far cheaper than per-blade
        // DrawLine while looking identical.
        const float bx = static_cast<float>(s.base.x);
        const float by = static_cast<float>(s.base.y);
        const float cx = static_cast<float>(s.control.x);
        const float cy = static_cast<float>(s.control.y);
        const float tx = static_cast<float>(s.tip.x);
        const float ty = static_cast<float>(s.tip.y);

        int hue = b.hue;
        if (hue < 0 || hue >= PALETTE_SIZE) hue = 0;
        int bucket = static_cast<int>(std::floor(thickness / kBladeThicknessBucket + 0.5f));
        if (bucket < 0) bucket = 0;
        if (bucket >= kBucketCount) bucket = kBucketCount - 1;

        BladeGroup& g = bladeGroups[hue][bucket];
        if (!g.sink) {
            if (SUCCEEDED(d2dFactory_->CreatePathGeometry(&g.geom))) {
                if (SUCCEEDED(g.geom->Open(&g.sink))) {
                    g.thickness = static_cast<float>(bucket) * kBladeThicknessBucket;
                } else {
                    g.geom.Reset();
                }
            }
        }
        if (g.sink) {
            constexpr int kBladeSegments = 4;
            g.sink->BeginFigure(D2D1::Point2F(bx, by), D2D1_FIGURE_BEGIN_HOLLOW);
            for (int i = 1; i <= kBladeSegments; ++i) {
                const float t   = static_cast<float>(i) / static_cast<float>(kBladeSegments);
                const float u   = 1.0f - t;
                const float u2  = u * u;
                const float t2  = t * t;
                const float ut2 = 2.0f * u * t;
                const float px  = u2 * bx + ut2 * cx + t2 * tx;
                const float py  = u2 * by + ut2 * cy + t2 * ty;
                g.sink->AddLine(D2D1::Point2F(px, py));
            }
            g.sink->EndFigure(D2D1_FIGURE_END_OPEN);
        }

        if (b.isFlower && b.cutHeight >= CUT_STUMP_THRESHOLD) {
            uint8_t idx = b.flowerHeadColorIdx;
            if (idx >= FLOWER_PALETTE_SIZE) idx = 0;
            deferredCaps.push_back({
                D2D1::Ellipse(D2D1::Point2F(static_cast<float>(s.tip.x),
                                            static_cast<float>(s.tip.y)),
                              static_cast<float>(b.flowerHeadRadius),
                              static_cast<float>(b.flowerHeadRadius)),
                flowerHeadBrushes_[idx].Get()});
        }

        if (sim_.currentScene == Scene::Winter && !b.isCactus && !b.isPine && b.cutHeight >= CUT_STUMP_THRESHOLD) {
            const float r = static_cast<float>(b.thickness * SNOW_TIP_RADIUS_FACTOR);
            deferredCaps.push_back({
                D2D1::Ellipse(D2D1::Point2F(static_cast<float>(s.tip.x),
                                            static_cast<float>(s.tip.y)),
                              r, r),
                snowTipBrush_.Get()});
        }
    }

    // Phase 2: stroke each non-empty blade group with a single DrawGeometry.
    for (int h = 0; h < PALETTE_SIZE; ++h) {
        for (int k = 0; k < kBucketCount; ++k) {
            BladeGroup& g = bladeGroups[h][k];
            if (!g.sink) continue;
            if (SUCCEEDED(g.sink->Close())) {
                d2dContext_->DrawGeometry(g.geom.Get(), brushes_[sceneIdx][h].Get(),
                                          g.thickness, roundStrokeStyle_.Get());
            }
        }
    }

    // Phase 3: draw deferred tip caps (flower heads, snow caps) on top.
    for (const DeferredEllipse& d : deferredCaps) {
        d2dContext_->FillEllipse(d.ellipse, d.brush);
    }
}

void Renderer::DrawButterfly(const Entity& e) {
    if (!butterflyBodyBrush_) return;

    uint8_t idx = e.colorVariant;
    if (idx >= BUTTERFLY_COLOR_COUNT) idx = 0;
    ID2D1SolidColorBrush* wingBrush = butterflyWingBrushes_[idx].Get();
    ID2D1SolidColorBrush* accentBrush = butterflyAccentBrushes_[idx].Get();
    if (!wingBrush || !accentBrush) return;

    const float cx = static_cast<float>(e.x);
    const float cy = static_cast<float>(e.y);
    const float wingScale = static_cast<float>(butterfly_wing_scale(e.age, e.phaseY));
    const float wingRx = static_cast<float>(BUTTERFLY_WING_RADIUS) * wingScale;
    const float wingRy = static_cast<float>(BUTTERFLY_WING_RADIUS) * 0.78f;
    const float wingOffset = static_cast<float>(BUTTERFLY_WING_OFFSET);

    const D2D1_POINT_2F left = D2D1::Point2F(cx - wingOffset, cy);
    const D2D1_POINT_2F right = D2D1::Point2F(cx + wingOffset, cy);
    d2dContext_->FillEllipse(D2D1::Ellipse(left, wingRx, wingRy), wingBrush);
    d2dContext_->FillEllipse(D2D1::Ellipse(right, wingRx, wingRy), wingBrush);

    const float accentR = std::max(0.6f, wingRy * 0.22f);
    d2dContext_->FillEllipse(
        D2D1::Ellipse(D2D1::Point2F(left.x - wingRx * 0.35f, left.y - wingRy * 0.25f), accentR, accentR),
        accentBrush);
    d2dContext_->FillEllipse(
        D2D1::Ellipse(D2D1::Point2F(right.x + wingRx * 0.35f, right.y - wingRy * 0.25f), accentR, accentR),
        accentBrush);

    d2dContext_->FillEllipse(
        D2D1::Ellipse(D2D1::Point2F(cx, cy), 0.3f, static_cast<float>(BUTTERFLY_BODY_LENGTH * 0.5)),
        butterflyBodyBrush_.Get());
}

void Renderer::DrawFirefly(const Entity& e) {
    if (!fireflyBodyBrush_ || !fireflyGlowBrush_) return;

    const double brightness = firefly_blink_brightness(e.age, e.blinkPeriod, e.blinkPhase);
    if (brightness <= 0.0) return;

    const float cx = static_cast<float>(e.x);
    const float cy = static_cast<float>(e.y);
    const float glowR = static_cast<float>(FIREFLY_GLOW_RADIUS * brightness);
    const float bodyR = static_cast<float>(FIREFLY_BODY_RADIUS);

    if (glowR > 0.0f) {
        fireflyGlowBrush_->SetOpacity(static_cast<float>((FIREFLY_GLOW_ALPHA_MAX / 255.0) * brightness));
        d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx, cy), glowR, glowR), fireflyGlowBrush_.Get());
    }

    fireflyBodyBrush_->SetOpacity(static_cast<float>((FIREFLY_BODY_ALPHA_MAX / 255.0) * brightness));
    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx, cy), bodyR, bodyR), fireflyBodyBrush_.Get());
    fireflyGlowBrush_->SetOpacity(1.0f);
    fireflyBodyBrush_->SetOpacity(1.0f);
}

void Renderer::DrawBird(const Entity& e) {
    if (!birdBrush_) return;

    const double alpha = bird_fade_alpha(e.x, e.vx, sim_.monitorWidth);
    if (alpha <= 0.0) return;

    const float cx = static_cast<float>(e.x);
    const float cy = static_cast<float>(e.y);
    const float wingScale = static_cast<float>(bird_wing_scale(e.age, e.phaseX));
    const float halfSpan = static_cast<float>(BIRD_WING_SPAN * 0.5) * wingScale;
    const float wingRise = std::max(0.8f, halfSpan * 0.55f);
    const float bodyRx = static_cast<float>(BIRD_BODY_LENGTH * 0.5);
    const float bodyRy = 0.75f;

    birdBrush_->SetOpacity(static_cast<float>(alpha));
    d2dContext_->DrawLine(D2D1::Point2F(cx, cy),
                          D2D1::Point2F(cx - halfSpan, cy - wingRise),
                          birdBrush_.Get(), 1.0f);
    d2dContext_->DrawLine(D2D1::Point2F(cx, cy),
                          D2D1::Point2F(cx + halfSpan, cy - wingRise),
                          birdBrush_.Get(), 1.0f);
    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx, cy), bodyRx, bodyRy), birdBrush_.Get());
    birdBrush_->SetOpacity(1.0f);
}

void Renderer::DrawCoral(const Blade& b, float groundY) {
    int idx = b.coralColorIdx;
    if (static_cast<unsigned>(idx) >= static_cast<unsigned>(CORAL_COLOR_COUNT)) idx = 0;
    ID2D1SolidColorBrush* brush = coralBrushes_[idx].Get();
    if (!brush) return;
    ID2D1StrokeStyle* round = roundStrokeStyle_.Get();

    const float baseX = static_cast<float>(b.baseX);
    const float gy = groundY;
    const float h = static_cast<float>(b.coralHeight * b.cutHeight);
    const float w = static_cast<float>(b.coralWidth);

    if (b.cutHeight < CUT_STUMP_THRESHOLD) {
        const float stumpW = std::max(2.0f, w * 0.45f);
        d2dContext_->DrawLine(D2D1::Point2F(baseX, gy),
                              D2D1::Point2F(baseX, gy - static_cast<float>(STUMP_HEIGHT)),
                              brush, stumpW, round);
        return;
    }

    if (b.coralType == 0) {
        // Fan coral — splayed line bundle anchored at the base, tips spread
        // across a horizontal arc at the top.
        const int rays = 7;
        const float topY = gy - h;
        const float halfW = w * 0.5f;
        const float rayW = std::max(1.2f, w * 0.10f);
        for (int i = 0; i < rays; ++i) {
            const float t = (rays == 1) ? 0.5f : static_cast<float>(i) / (rays - 1);
            const float tipX = baseX - halfW + t * w;
            const float tipY = topY + std::fabs(t - 0.5f) * h * 0.18f;
            d2dContext_->DrawLine(D2D1::Point2F(baseX, gy), D2D1::Point2F(tipX, tipY),
                                  brush, rayW, round);
            d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(tipX, tipY), rayW * 0.9f, rayW * 0.9f), brush);
        }
        d2dContext_->DrawLine(D2D1::Point2F(baseX, gy), D2D1::Point2F(baseX, gy - h * 0.18f),
                              brush, std::max(2.0f, w * 0.30f), round);
    } else if (b.coralType == 1) {
        // Branching coral — central trunk with paired branches.
        const float trunkW = std::max(1.6f, w * 0.22f);
        const float topY = gy - h;
        d2dContext_->DrawLine(D2D1::Point2F(baseX, gy), D2D1::Point2F(baseX, topY),
                              brush, trunkW, round);
        d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(baseX, topY), trunkW * 0.9f, trunkW * 0.9f), brush);
        struct Branch { float frac; float side; float lenFrac; float angleDeg; };
        const Branch branches[] = {
            { 0.45f, -1.0f, 0.55f, 55.0f },
            { 0.55f, +1.0f, 0.50f, 50.0f },
            { 0.72f, -1.0f, 0.40f, 35.0f },
            { 0.78f, +1.0f, 0.45f, 40.0f },
        };
        const float branchW = std::max(1.2f, trunkW * 0.75f);
        for (const Branch& br : branches) {
            const float sx = baseX;
            const float sy = gy - h * br.frac;
            const float blen = h * br.lenFrac;
            const float ang = br.angleDeg * 3.14159265358979323846f / 180.0f;
            const float ex = sx + br.side * blen * std::sin(ang);
            const float ey = sy - blen * std::cos(ang);
            d2dContext_->DrawLine(D2D1::Point2F(sx, sy), D2D1::Point2F(ex, ey), brush, branchW, round);
            d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(ex, ey), branchW * 0.9f, branchW * 0.9f), brush);
        }
    } else {
        // Brain coral — bulbous mound with internal ridge lines.
        const float cx = baseX;
        const float cy = gy - h * 0.45f;
        const float rx = w * 0.55f;
        const float ry = h * 0.55f;
        d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx, cy), rx, ry), brush);
        if (!bubbleStrokeBrush_) return;
        const float ridgeW = std::max(0.8f, w * 0.06f);
        for (int i = -2; i <= 2; ++i) {
            const float t = i / 2.0f;
            const float ridgeY = cy + t * ry * 0.55f;
            const float halfRidge = rx * std::sqrt(std::max(0.0f, 1.0f - t * t * 0.85f)) * 0.85f;
            d2dContext_->DrawLine(D2D1::Point2F(cx - halfRidge, ridgeY),
                                  D2D1::Point2F(cx + halfRidge, ridgeY),
                                  bubbleStrokeBrush_.Get(), ridgeW, round);
        }
    }
}

void Renderer::DrawFish(const Entity& e) {
    int idx = e.colorVariant;
    if (static_cast<unsigned>(idx) >= static_cast<unsigned>(FISH_COLOR_COUNT)) idx = 0;
    ID2D1SolidColorBrush* body = fishBrushes_[idx].Get();
    if (!body) return;
    ID2D1StrokeStyle* round = roundStrokeStyle_.Get();

    const float cx = static_cast<float>(e.x);
    const float cy = static_cast<float>(e.y);
    const float halfLen = static_cast<float>(e.size);
    const float halfH = halfLen * 0.55f;
    const float facing = (e.vx >= 0.0) ? 1.0f : -1.0f;

    // Body
    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx, cy), halfLen, halfH), body);

    // Tail — wobbles with time; tail points away from heading direction.
    const double tailAngle = std::sin(e.age * FISH_TAIL_WOBBLE_FREQ + e.phaseX) * FISH_TAIL_WOBBLE_AMP;
    const float tailRootX = cx - facing * halfLen * 0.95f;
    const float tailLen = halfLen * 0.80f;
    const float tipX = tailRootX - facing * tailLen * static_cast<float>(std::cos(tailAngle));
    const float tipY = cy + tailLen * static_cast<float>(std::sin(tailAngle));
    const float spreadH = halfH * 0.85f;
    d2dContext_->DrawLine(D2D1::Point2F(tailRootX, cy - spreadH * 0.6f), D2D1::Point2F(tipX, tipY - spreadH * 0.5f), body, std::max(1.0f, halfH * 0.50f), round);
    d2dContext_->DrawLine(D2D1::Point2F(tailRootX, cy + spreadH * 0.6f), D2D1::Point2F(tipX, tipY + spreadH * 0.5f), body, std::max(1.0f, halfH * 0.50f), round);
    d2dContext_->DrawLine(D2D1::Point2F(tailRootX, cy), D2D1::Point2F(tipX, tipY), body, std::max(1.0f, halfH * 0.40f), round);

    // Top fin
    const float finBase1X = cx - facing * halfLen * 0.10f;
    const float finBase2X = cx + facing * halfLen * 0.15f;
    const float finBaseY = cy - halfH * 0.85f;
    const float finTipX = cx;
    const float finTipY = cy - halfH * 1.55f;
    d2dContext_->DrawLine(D2D1::Point2F(finBase1X, finBaseY), D2D1::Point2F(finTipX, finTipY), body, std::max(0.8f, halfH * 0.30f), round);
    d2dContext_->DrawLine(D2D1::Point2F(finBase2X, finBaseY), D2D1::Point2F(finTipX, finTipY), body, std::max(0.8f, halfH * 0.30f), round);

    // Eye — small dark dot near the head.
    if (fishFinBrush_) {
        const float eyeX = cx + facing * halfLen * 0.55f;
        const float eyeY = cy - halfH * 0.18f;
        const float eyeR = std::max(0.8f, halfH * 0.18f);
        d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(eyeX, eyeY), eyeR, eyeR), fishFinBrush_.Get());
    }
}

void Renderer::DrawCat(const Entity& e, const D2D1_POINT_2F* cursorPosition) {
    const CatCoatBrushSet& coat = catCoatBrushes_[e.coatVariantIndex % CAT_COAT_VARIANT_COUNT];
    ID2D1SolidColorBrush* catBodyBrush = coat.body.Get();
    ID2D1SolidColorBrush* catLegBrush = coat.leg.Get();
    ID2D1SolidColorBrush* catFaceBrush = coat.face.Get();
    ID2D1SolidColorBrush* catEarBrush = coat.ear.Get();
    ID2D1SolidColorBrush* catInkBrush = coat.ink.Get();
    if (!catBodyBrush || !catLegBrush || !catFaceBrush || !catEarBrush || !catInkBrush) return;

    constexpr double TWO_PI_LOCAL = 6.28318530717958647692;
    const float cx = static_cast<float>(e.x);
    const float br = static_cast<float>(CAT_BODY_RADIUS);
    const float bh = static_cast<float>(CAT_BODY_HEIGHT);
    const float legLen = static_cast<float>(CAT_LEG_LENGTH);
    const float headR = static_cast<float>(CAT_HEAD_RADIUS);
    const float facing = (e.vx >= 0.0) ? 1.0f : -1.0f;

    const bool isWalking = (e.state == CAT_STATE_WALKING);
    const bool isIdle = (e.state == CAT_STATE_IDLE);
    const bool isSleeping = (e.state == CAT_STATE_SLEEPING);
    const bool isPouncing = (e.state == CAT_STATE_POUNCING);

    float pounceOffsetY = 0.0f;
    if (isPouncing) {
        const float t = std::max(0.0f,
            std::min(1.0f, static_cast<float>(e.age / CAT_POUNCE_DURATION)));
        pounceOffsetY = -4.0f * static_cast<float>(CAT_POUNCE_HEIGHT) * t * (1.0f - t);
    }
    const float sleepOffsetY = isSleeping ? legLen : 0.0f;
    const float cy = static_cast<float>(e.y) + pounceOffsetY + sleepOffsetY;

    const float walkPhase = static_cast<float>(e.age * (TWO_PI_LOCAL / CAT_WALK_PERIOD));
    const float legAmp = isWalking ? static_cast<float>(CAT_LEG_CYCLE_AMP) : 0.0f;
    const float headBob = isWalking
        ? std::sin(walkPhase * 2.0f) * static_cast<float>(CAT_HEAD_BOB_AMP)
        : 0.0f;
    const float tailSway = (isWalking || isIdle)
        ? std::sin(static_cast<float>(e.age * CAT_TAIL_SWAY_FREQ)) * static_cast<float>(CAT_TAIL_SWAY_AMP)
        : 0.0f;

    auto fillTriangle = [&](D2D1_POINT_2F a, D2D1_POINT_2F b, D2D1_POINT_2F c,
                            ID2D1SolidColorBrush* brush) {
        ComPtr<ID2D1PathGeometry> path;
        if (FAILED(d2dFactory_->CreatePathGeometry(&path))) return;
        ComPtr<ID2D1GeometrySink> sink;
        if (FAILED(path->Open(&sink))) return;
        sink->BeginFigure(a, D2D1_FIGURE_BEGIN_FILLED);
        sink->AddLine(b);
        sink->AddLine(c);
        sink->EndFigure(D2D1_FIGURE_END_CLOSED);
        if (FAILED(sink->Close())) return;
        d2dContext_->FillGeometry(path.Get(), brush);
    };

    auto drawBezier = [&](D2D1_POINT_2F p0, D2D1_POINT_2F c1, D2D1_POINT_2F c2,
                          D2D1_POINT_2F p1, ID2D1SolidColorBrush* brush, float thickness) {
        ComPtr<ID2D1PathGeometry> path;
        if (FAILED(d2dFactory_->CreatePathGeometry(&path))) return;
        ComPtr<ID2D1GeometrySink> sink;
        if (FAILED(path->Open(&sink))) return;
        sink->BeginFigure(p0, D2D1_FIGURE_BEGIN_HOLLOW);
        D2D1_BEZIER_SEGMENT seg{};
        seg.point1 = c1;
        seg.point2 = c2;
        seg.point3 = p1;
        sink->AddBezier(seg);
        sink->EndFigure(D2D1_FIGURE_END_OPEN);
        if (FAILED(sink->Close())) return;
        d2dContext_->DrawGeometry(path.Get(), brush, thickness);
    };

    auto drawZ = [&](float zX, float zY, float zSize, float alpha) {
        catInkBrush->SetOpacity(alpha);
        d2dContext_->DrawLine(D2D1::Point2F(zX, zY),
                              D2D1::Point2F(zX + zSize, zY),
                              catInkBrush, 0.9f);
        d2dContext_->DrawLine(D2D1::Point2F(zX + zSize, zY),
                              D2D1::Point2F(zX, zY + zSize),
                              catInkBrush, 0.9f);
        d2dContext_->DrawLine(D2D1::Point2F(zX, zY + zSize),
                              D2D1::Point2F(zX + zSize, zY + zSize),
                              catInkBrush, 0.9f);
        catInkBrush->SetOpacity(1.0f);
    };

    const float tailBaseX = cx - facing * br * 0.92f;
    const float tailBaseY = cy - bh * 0.10f;
    if (isSleeping) {
        drawBezier(
            D2D1::Point2F(tailBaseX, tailBaseY + bh * 0.15f),
            D2D1::Point2F(cx - facing * br * 0.55f, cy + bh * 0.95f),
            D2D1::Point2F(cx + facing * br * 0.10f, cy + bh * 0.95f),
            D2D1::Point2F(cx + facing * br * 0.72f, cy + bh * 0.45f),
            catLegBrush, static_cast<float>(CAT_TAIL_THICKNESS));
    } else {
        const float tailLen = static_cast<float>(CAT_TAIL_LENGTH);
        const float tipX = tailBaseX - facing * tailLen * (0.78f + 0.08f * std::sin(tailSway));
        const float tipY = tailBaseY - tailLen * (0.42f + 0.18f * std::cos(tailSway));
        drawBezier(
            D2D1::Point2F(tailBaseX, tailBaseY),
            D2D1::Point2F(tailBaseX - facing * tailLen * 0.18f, tailBaseY - tailLen * 0.08f),
            D2D1::Point2F(tailBaseX - facing * tailLen * 0.60f, tailBaseY - tailLen * (0.70f + 0.20f * std::sin(tailSway))),
            D2D1::Point2F(tipX, tipY),
            catLegBrush, static_cast<float>(CAT_TAIL_THICKNESS));
    }

    if (!isSleeping) {
        const float legY0 = cy + bh * 0.35f;
        const float legXs[4] = { -br * 0.58f, -br * 0.20f, br * 0.20f, br * 0.58f };
        const float swingA = std::sin(walkPhase) * legAmp;
        const float swingB = std::sin(walkPhase + 3.14159265f) * legAmp;
        const float legSwings[4] = { swingA, swingB, swingA, swingB };
        for (int li = 0; li < 4; ++li) {
            const float lx = cx + legXs[li];
            const float ly1 = cy + bh + legLen + legSwings[li];
            d2dContext_->DrawLine(D2D1::Point2F(lx, legY0),
                                  D2D1::Point2F(lx, ly1),
                                  catLegBrush, 1.2f);
        }
    }

    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx, cy), br, bh),
                             catBodyBrush);

    float headDirX = facing;
    float headDx = facing * br * 0.82f;
    float headDy = -bh * 0.78f + headBob;
    if (isIdle) {
        const float stripTop = static_cast<float>(sim_.windowHeight - STRIP_HEIGHT);
        const bool curious = cursorPosition != nullptr
            && std::fabs(cursorPosition->y - stripTop) <= SHEEP_CURIOUS_VERTICAL_RADIUS_DIP
            && std::fabs(cursorPosition->x - cx) <= static_cast<float>(CAT_CURIOUS_RADIUS);
        if (curious) {
            const float cursorDx = cursorPosition->x - cx;
            const float maxHeadDx = static_cast<float>(CAT_CURIOUS_HEAD_TURN_MAX * CAT_HEAD_RADIUS);
            headDirX = cursorDx >= 0.0f ? 1.0f : -1.0f;
            headDx = facing * br * 0.55f + std::clamp(cursorDx, -maxHeadDx, maxHeadDx);
        } else {
            const float sweep = std::sin(static_cast<float>(e.age * CAT_TAIL_SWAY_FREQ * 0.7));
            headDirX = sweep >= 0.0f ? 1.0f : -1.0f;
            headDx = facing * br * 0.60f + sweep * headR * 0.70f;
        }
        headDy = -bh * 0.82f;
    } else if (isSleeping) {
        headDx = facing * br * 0.62f;
        headDy = -bh * 0.20f;
    }

    const float headCx = cx + headDx;
    const float headCy = cy + headDy;
    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(headCx, headCy), headR, headR),
                             catFaceBrush);

    const float earBaseY = headCy - headR * 0.62f;
    const float earH = static_cast<float>(CAT_EAR_HEIGHT);
    fillTriangle(
        D2D1::Point2F(headCx - headR * 0.60f, earBaseY),
        D2D1::Point2F(headCx - headR * 0.18f, earBaseY),
        D2D1::Point2F(headCx - headR * 0.47f - headDirX * 0.65f, earBaseY - earH),
        catEarBrush);
    fillTriangle(
        D2D1::Point2F(headCx + headR * 0.18f, earBaseY),
        D2D1::Point2F(headCx + headR * 0.60f, earBaseY),
        D2D1::Point2F(headCx + headR * 0.47f + headDirX * 0.65f, earBaseY - earH),
        catEarBrush);

    if (isSleeping) {
        const float eyeY = headCy - headR * 0.05f;
        for (float ex : { -headR * 0.25f, headR * 0.32f }) {
            const float x0 = headCx + ex - 1.1f;
            const float x1 = headCx + ex + 1.1f;
            drawBezier(D2D1::Point2F(x0, eyeY),
                       D2D1::Point2F(x0 + 0.45f, eyeY + 0.8f),
                       D2D1::Point2F(x1 - 0.45f, eyeY + 0.8f),
                       D2D1::Point2F(x1, eyeY),
                       catInkBrush, 0.9f);
        }
    } else {
        const float eyeR = headR * 0.16f;
        d2dContext_->FillEllipse(
            D2D1::Ellipse(D2D1::Point2F(headCx + headDirX * headR * 0.22f,
                                        headCy - headR * 0.18f), eyeR, eyeR * 0.75f),
            catInkBrush);
        d2dContext_->FillEllipse(
            D2D1::Ellipse(D2D1::Point2F(headCx - headDirX * headR * 0.18f,
                                        headCy - headR * 0.18f), eyeR, eyeR * 0.75f),
            catInkBrush);
    }

    const float noseTipX = headCx + headDirX * headR * 0.63f;
    const float noseTipY = headCy + headR * 0.12f;
    fillTriangle(
        D2D1::Point2F(noseTipX, noseTipY),
        D2D1::Point2F(noseTipX - headDirX * 1.5f, noseTipY - 1.1f),
        D2D1::Point2F(noseTipX - headDirX * 1.5f, noseTipY + 1.1f),
        catInkBrush);

    if (isSleeping) {
        const float zBaseX = headCx + headDirX * headR * 0.55f;
        const float zBaseY = headCy - headR * 1.25f;
        for (int zi = 0; zi < 2; ++zi) {
            const float phaseOffset = 0.5f * static_cast<float>(zi);
            const float t = static_cast<float>(std::fmod(e.age / SHEEP_ZZZ_CYCLE_SEC + phaseOffset, 1.0));
            const float zSize = static_cast<float>((SHEEP_ZZZ_SIZE_START * 0.65) +
                t * ((SHEEP_ZZZ_SIZE_END * 0.70) - (SHEEP_ZZZ_SIZE_START * 0.65)));
            drawZ(zBaseX + t * 3.0f * headDirX, zBaseY - t * static_cast<float>(SHEEP_ZZZ_RISE * 0.75),
                  zSize, 1.0f - t);
        }
    }
}

void Renderer::DrawBunny(const Entity& e) {
    if (!bunnyBodyBrush_ || !bunnyBellyBrush_ || !bunnyEarBrush_ || !bunnyEarInnerBrush_
        || !bunnyTailBrush_ || !bunnyEyeBrush_ || !bunnyNoseBrush_) return;

    const bool isHopping = (e.state == BUNNY_STATE_HOPPING);
    const bool isGrazing = (e.state == BUNNY_STATE_GRAZING);
    const bool isIdle = (e.state == BUNNY_STATE_IDLE);
    const bool isSleeping = (e.state == BUNNY_STATE_SLEEPING);
    const bool isStartled = (e.state == BUNNY_STATE_STARTLED);
    const float facing = (e.vx >= 0.0) ? 1.0f : -1.0f;
    const float hopY = (isHopping || isStartled)
        ? static_cast<float>(bunny_hop_y_offset(e.age, isStartled))
        : 0.0f;
    const float poseLift = isIdle ? 1.5f : (isGrazing ? -1.5f : 0.0f);
    const float sleepDrop = isSleeping ? static_cast<float>(BUNNY_LEG_LENGTH + BUNNY_BODY_HEIGHT * 0.3) : 0.0f;
    const float cx = static_cast<float>(e.x);
    const float cy = static_cast<float>(e.y) - hopY - poseLift + sleepDrop;
    const float br = static_cast<float>(BUNNY_BODY_RADIUS);
    const float bh = static_cast<float>(BUNNY_BODY_HEIGHT) * (isSleeping ? 0.7f : 1.0f);
    const float headR = static_cast<float>(BUNNY_HEAD_RADIUS);
    const float tailR = static_cast<float>(BUNNY_TAIL_RADIUS);

    const float tailCx = cx - facing * (br + tailR * 0.35f);
    const float tailCy = cy + bh * 0.02f;
    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(tailCx, tailCy), tailR, tailR),
                              bunnyTailBrush_.Get());

    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx, cy), br, bh),
                              bunnyBodyBrush_.Get());
    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx + facing * br * 0.15f, cy + bh * 0.38f),
                                           br * 0.52f, bh * 0.34f),
                              bunnyBellyBrush_.Get());

    if (!isSleeping && !isHopping && !isStartled) {
        const float legY = cy + bh * 0.82f;
        const float legRx = 1.5f;
        const float legRy = static_cast<float>(BUNNY_LEG_LENGTH * 0.35);
        d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx - br * 0.35f, legY), legRx, legRy),
                                  bunnyBodyBrush_.Get());
        d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx + br * 0.35f, legY), legRx, legRy),
                                  bunnyBodyBrush_.Get());
    }

    float headCx = cx + facing * br * 0.78f;
    float headCy = cy - bh * 0.72f;
    if (isGrazing) headCy = cy + bh * 0.10f;
    if (isSleeping) headCy = cy - bh * 0.05f;
    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(headCx, headCy), headR, headR),
                              bunnyBodyBrush_.Get());

    if (isSleeping) {
        const float earY = headCy - headR * 0.55f;
        for (int i = 0; i < 2; ++i) {
            const float y = earY + static_cast<float>(i) * 1.8f;
            d2dContext_->DrawLine(D2D1::Point2F(headCx - facing * headR * 0.25f, y),
                                  D2D1::Point2F(headCx - facing * (headR + static_cast<float>(BUNNY_EAR_HEIGHT) * 0.45f), y + 0.7f),
                                  bunnyEarBrush_.Get(), static_cast<float>(BUNNY_EAR_WIDTH));
        }
        const float eyeY = headCy - headR * 0.05f;
        d2dContext_->DrawLine(D2D1::Point2F(headCx + facing * headR * 0.15f, eyeY),
                              D2D1::Point2F(headCx + facing * headR * 0.62f, eyeY),
                              bunnyEyeBrush_.Get(), 0.9f);
    } else {
        const float wiggle = isIdle
            ? static_cast<float>(BUNNY_EAR_WIGGLE_AMP * std::sin(e.age * BUNNY_EAR_WIGGLE_FREQ))
            : 0.0f;
        const float earTopY = headCy - headR - static_cast<float>(BUNNY_EAR_HEIGHT);
        const float earBaseY = headCy - headR * 0.45f;
        const float spacing = static_cast<float>(BUNNY_EAR_SPACING * 0.5);
        for (int i = 0; i < 2; ++i) {
            const float side = (i == 0) ? -1.0f : 1.0f;
            const float lean = side * wiggle;
            const float baseX = headCx + side * spacing;
            const float topX = baseX + lean * static_cast<float>(BUNNY_EAR_HEIGHT);
            d2dContext_->DrawLine(D2D1::Point2F(baseX, earBaseY),
                                  D2D1::Point2F(topX, earTopY),
                                  bunnyEarBrush_.Get(), static_cast<float>(BUNNY_EAR_WIDTH));
            d2dContext_->DrawLine(D2D1::Point2F(baseX, earBaseY - 0.8f),
                                  D2D1::Point2F(topX, earTopY + 1.8f),
                                  bunnyEarInnerBrush_.Get(), static_cast<float>(BUNNY_EAR_WIDTH * 0.45));
        }
        const float eyeR = 0.9f;
        d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(headCx + facing * headR * 0.35f,
                                                              headCy - headR * 0.12f), eyeR, eyeR),
                                  bunnyEyeBrush_.Get());
    }

    const float noseY = headCy + headR * 0.15f
        + (isIdle ? static_cast<float>(BUNNY_NOSE_TWITCH_AMP * std::sin(e.age * BUNNY_NOSE_TWITCH_FREQ)) : 0.0f);
    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(headCx + facing * headR * 0.72f, noseY), 1.0f, 0.85f),
                              bunnyNoseBrush_.Get());

    if (isSleeping) {
        const float zBaseX = headCx + facing * headR * 0.65f;
        const float zBaseY = headCy - headR * 1.3f;
        for (int zi = 0; zi < 2; ++zi) {
            const float t = static_cast<float>(std::fmod(e.age / BUNNY_ZZZ_CYCLE_SEC + 0.5 * zi, 1.0));
            const float zSize = static_cast<float>(BUNNY_ZZZ_SIZE_START + t * (BUNNY_ZZZ_SIZE_END - BUNNY_ZZZ_SIZE_START));
            const float zX = zBaseX + t * 3.0f * facing;
            const float zY = zBaseY - t * static_cast<float>(BUNNY_ZZZ_RISE);
            const float alpha = 1.0f - t;
            bunnyTailBrush_->SetOpacity(alpha);
            d2dContext_->DrawLine(D2D1::Point2F(zX, zY), D2D1::Point2F(zX + zSize, zY), bunnyTailBrush_.Get(), 0.9f);
            d2dContext_->DrawLine(D2D1::Point2F(zX + zSize, zY), D2D1::Point2F(zX, zY + zSize), bunnyTailBrush_.Get(), 0.9f);
            d2dContext_->DrawLine(D2D1::Point2F(zX, zY + zSize), D2D1::Point2F(zX + zSize, zY + zSize), bunnyTailBrush_.Get(), 0.9f);
        }
        bunnyTailBrush_->SetOpacity(1.0f);
    }
}

void Renderer::DrawHedgehog(const Entity& e) {
    if (!hedgehogBodyBrush_ || !hedgehogSpikeBrush_ || !hedgehogSpikeTipBrush_
        || !hedgehogNoseBrush_ || !hedgehogEyeBrush_ || !d2dFactory_) return;

    auto fillTriangle = [&](D2D1_POINT_2F a, D2D1_POINT_2F b, D2D1_POINT_2F c,
                            ID2D1SolidColorBrush* brush) {
        ComPtr<ID2D1PathGeometry> geometry;
        if (FAILED(d2dFactory_->CreatePathGeometry(geometry.ReleaseAndGetAddressOf()))) return;
        ComPtr<ID2D1GeometrySink> sink;
        if (FAILED(geometry->Open(sink.ReleaseAndGetAddressOf()))) return;
        sink->BeginFigure(a, D2D1_FIGURE_BEGIN_FILLED);
        sink->AddLine(b);
        sink->AddLine(c);
        sink->EndFigure(D2D1_FIGURE_END_CLOSED);
        if (SUCCEEDED(sink->Close())) {
            d2dContext_->FillGeometry(geometry.Get(), brush);
        }
    };

    auto drawSpike = [&](float cx, float cy, float radiusX, float radiusY, float angle,
                         bool mirrorX, float spikeLength, float spikeWidth) {
        const float localUx = std::cos(angle);
        const float localUy = std::sin(angle);
        const float denom = std::sqrt((localUx * localUx) / (radiusX * radiusX)
                                    + (localUy * localUy) / (radiusY * radiusY));
        const float edgeRadius = denom > 0.0f ? 1.0f / denom : radiusX;
        const float mirror = mirrorX ? ((e.vx >= 0.0) ? 1.0f : -1.0f) : 1.0f;
        const float ux = mirror * localUx;
        const float uy = localUy;
        const D2D1_POINT_2F base = D2D1::Point2F(cx + ux * edgeRadius, cy + uy * edgeRadius);
        const D2D1_POINT_2F tip  = D2D1::Point2F(base.x + ux * spikeLength, base.y + uy * spikeLength);
        const float px = -uy;
        const float py = ux;
        const float half = spikeWidth * 0.5f;
        fillTriangle(tip,
                     D2D1::Point2F(base.x + px * half, base.y + py * half),
                     D2D1::Point2F(base.x - px * half, base.y - py * half),
                     hedgehogSpikeBrush_.Get());
        d2dContext_->DrawLine(base, tip, hedgehogSpikeTipBrush_.Get(), 0.45f);
    };

    const bool isSleeping = e.state == HEDGEHOG_STATE_SLEEPING;
    const bool isCurled = e.state == HEDGEHOG_STATE_CURLED;
    const bool isBall = isSleeping || isCurled;
    const float cx = static_cast<float>(e.x);
    float cy = static_cast<float>(e.y);

    if (isBall) {
        const float ballR = static_cast<float>(HEDGEHOG_BODY_RADIUS * 0.85);
        d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx, cy), ballR, ballR),
                                  hedgehogBodyBrush_.Get());
        const int ballSpikeCount = HEDGEHOG_SPIKE_COUNT * 3 / 2;
        for (int i = 0; i < ballSpikeCount; ++i) {
            const float angle = static_cast<float>(2.0 * 3.14159265358979323846 * i / ballSpikeCount);
            drawSpike(cx, cy, ballR, ballR, angle, false,
                      static_cast<float>(HEDGEHOG_SPIKE_LENGTH),
                      static_cast<float>(HEDGEHOG_SPIKE_WIDTH));
        }

        if (isSleeping) {
            for (int zi = 0; zi < 2; ++zi) {
                const float t = static_cast<float>(std::fmod(e.age / HEDGEHOG_ZZZ_CYCLE_SEC + 0.5 * zi, 1.0));
                const float zSize = static_cast<float>(HEDGEHOG_ZZZ_SIZE_START
                    + t * (HEDGEHOG_ZZZ_SIZE_END - HEDGEHOG_ZZZ_SIZE_START));
                const float zX = cx + t * 2.4f;
                const float zY = cy - ballR - 3.0f - t * static_cast<float>(HEDGEHOG_ZZZ_RISE);
                const float alpha = 1.0f - t;
                hedgehogSpikeTipBrush_->SetOpacity(alpha);
                d2dContext_->DrawLine(D2D1::Point2F(zX, zY), D2D1::Point2F(zX + zSize, zY), hedgehogSpikeTipBrush_.Get(), 0.75f);
                d2dContext_->DrawLine(D2D1::Point2F(zX + zSize, zY), D2D1::Point2F(zX, zY + zSize), hedgehogSpikeTipBrush_.Get(), 0.75f);
                d2dContext_->DrawLine(D2D1::Point2F(zX, zY + zSize), D2D1::Point2F(zX + zSize, zY + zSize), hedgehogSpikeTipBrush_.Get(), 0.75f);
            }
            hedgehogSpikeTipBrush_->SetOpacity(1.0f);
        }
        return;
    }

    if (e.state == HEDGEHOG_STATE_WALKING) {
        cy += static_cast<float>(HEDGEHOG_WADDLE_AMP * std::sin(e.age * HEDGEHOG_WADDLE_FREQ));
    } else if (e.state == HEDGEHOG_STATE_IDLE) {
        cy -= 1.0f;
    }

    const float facing = e.vx >= 0.0 ? 1.0f : -1.0f;
    const float br = static_cast<float>(HEDGEHOG_BODY_RADIUS);
    const float bh = static_cast<float>(HEDGEHOG_BODY_HEIGHT);
    const float headR = static_cast<float>(HEDGEHOG_HEAD_RADIUS);

    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx, cy), br, bh),
                              hedgehogBodyBrush_.Get());

    for (int i = 0; i < HEDGEHOG_SPIKE_COUNT; ++i) {
        const double t = HEDGEHOG_SPIKE_COUNT > 1
            ? static_cast<double>(i) / static_cast<double>(HEDGEHOG_SPIKE_COUNT - 1)
            : 0.0;
        const float degrees = static_cast<float>(HEDGEHOG_SPIKE_ARC_START_DEG
            + t * (HEDGEHOG_SPIKE_ARC_END_DEG - HEDGEHOG_SPIKE_ARC_START_DEG));
        const float angle = degrees * static_cast<float>(3.14159265358979323846 / 180.0);
        drawSpike(cx, cy, br, bh, angle, true,
                  static_cast<float>(HEDGEHOG_SPIKE_LENGTH),
                  static_cast<float>(HEDGEHOG_SPIKE_WIDTH));
    }

    const float legTopY = cy + bh * 0.72f;
    const float legBottomY = cy + bh + static_cast<float>(HEDGEHOG_LEG_LENGTH);
    for (int i = 0; i < 4; ++i) {
        const float offset = -br * 0.48f + static_cast<float>(i) * br * 0.32f;
        d2dContext_->DrawLine(D2D1::Point2F(cx + offset, legTopY),
                              D2D1::Point2F(cx + offset, legBottomY),
                              hedgehogSpikeBrush_.Get(), 1.0f);
    }

    const float snuffleOffset = e.state == HEDGEHOG_STATE_SNUFFLING
        ? static_cast<float>(HEDGEHOG_SNUFFLE_HEAD_AMP * std::sin(e.age * HEDGEHOG_SNUFFLE_HEAD_FREQ))
        : 0.0f;
    const float headCx = cx + facing * (br * 0.78f) + snuffleOffset;
    const float headCy = cy + bh * 0.22f;
    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(headCx, headCy), headR, headR),
                              hedgehogBodyBrush_.Get());

    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(headCx + facing * headR * 0.82f,
                                                          headCy + headR * 0.12f),
                                            static_cast<float>(HEDGEHOG_NOSE_RADIUS),
                                            static_cast<float>(HEDGEHOG_NOSE_RADIUS)),
                              hedgehogNoseBrush_.Get());
    d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(headCx + facing * headR * 0.30f,
                                                          headCy - headR * 0.35f),
                                            0.75f, 0.75f),
                              hedgehogEyeBrush_.Get());
}

void Renderer::DrawPetName(const Entity& e, const D2D1_POINT_2F* cursorPosition) {
    if (!petNameTextFormat_ || !petNameBrush_ || !petNameShadowBrush_) return;
    if (e.kind != EntityKind::Sheep && e.kind != EntityKind::Cat
        && e.kind != EntityKind::Bunny && e.kind != EntityKind::Hedgehog) return;

    const wchar_t* const* pool = SHEEP_NAME_POOL;
    std::size_t poolSize = sizeof(SHEEP_NAME_POOL) / sizeof(SHEEP_NAME_POOL[0]);
    if (e.kind == EntityKind::Cat) {
        pool = CAT_NAME_POOL;
        poolSize = sizeof(CAT_NAME_POOL) / sizeof(CAT_NAME_POOL[0]);
    } else if (e.kind == EntityKind::Bunny) {
        pool = BUNNY_NAME_POOL;
        poolSize = sizeof(BUNNY_NAME_POOL) / sizeof(BUNNY_NAME_POOL[0]);
    } else if (e.kind == EntityKind::Hedgehog) {
        pool = HEDGEHOG_NAME_POOL;
        poolSize = sizeof(HEDGEHOG_NAME_POOL) / sizeof(HEDGEHOG_NAME_POOL[0]);
    }
    if (poolSize == 0) return;

    const uint64_t key = (static_cast<uint64_t>(static_cast<uint8_t>(e.kind)) << 32)
                       ^ static_cast<uint64_t>(e.seed);
    bool hovering = false;
    if (cursorPosition != nullptr) {
        const double dx = static_cast<double>(cursorPosition->x) - e.x;
        const double dy = static_cast<double>(cursorPosition->y) - e.y;
        hovering = (dx * dx + dy * dy) <= (PET_NAME_HOVER_RADIUS * PET_NAME_HOVER_RADIUS);
    }

    float opacity = 0.0f;
    if (hovering) {
        petNameLastHover_[key] = sim_.globalTime;
        opacity = 1.0f;
    } else {
        auto it = petNameLastHover_.find(key);
        if (it == petNameLastHover_.end()) return;
        const double elapsed = sim_.globalTime - it->second;
        if (elapsed >= PET_NAME_FADE_DURATION) {
            petNameLastHover_.erase(it);
            return;
        }
        opacity = static_cast<float>(1.0 - (elapsed / PET_NAME_FADE_DURATION));
    }

    const wchar_t* name = pool[e.nameIndex % poolSize];
    const UINT32 length = static_cast<UINT32>(std::wcslen(name));
    const float centerX = static_cast<float>(e.x);
    const float top = static_cast<float>(e.y - e.size + PET_NAME_OFFSET_Y - PET_NAME_FONT_SIZE);
    const float halfWidth = 60.0f;
    const float height = static_cast<float>(PET_NAME_FONT_SIZE + 4.0);
    const D2D1_RECT_F rect = D2D1::RectF(centerX - halfWidth, top,
                                        centerX + halfWidth, top + height);
    const D2D1_RECT_F shadowRect = D2D1::RectF(rect.left + 1.0f, rect.top + 1.0f,
                                              rect.right + 1.0f, rect.bottom + 1.0f);

    petNameShadowBrush_->SetOpacity(opacity);
    petNameBrush_->SetOpacity(opacity);
    d2dContext_->DrawTextW(name, length, petNameTextFormat_.Get(), shadowRect,
                           petNameShadowBrush_.Get());
    d2dContext_->DrawTextW(name, length, petNameTextFormat_.Get(), rect,
                           petNameBrush_.Get());
    petNameShadowBrush_->SetOpacity(1.0f);
    petNameBrush_->SetOpacity(1.0f);
}

void Renderer::DrawEntities(const D2D1_POINT_2F* cursorPosition) {
    if (sim_.entities.empty()) return;

    constexpr double TWO_PI_LOCAL = 6.28318530717958647692;

    for (const Entity& e : sim_.entities) {
        if (e.kind == EntityKind::Tumbleweed) {
            const float cx = static_cast<float>(e.x);
            const float cy = static_cast<float>(e.y);
            const float size = static_cast<float>(e.size);
            for (int k = 0; k < 5; ++k) {
                const double angle = e.rotation + static_cast<double>(k) * (TWO_PI_LOCAL / 5.0);
                const float dx = static_cast<float>(std::cos(angle));
                const float dy = static_cast<float>(std::sin(angle));
                const float px = -dy;
                const float py = dx;
                const D2D1_POINT_2F p0 = D2D1::Point2F(cx - dx * size * 0.95f + px * size * 0.18f,
                                                       cy - dy * size * 0.95f + py * size * 0.18f);
                const D2D1_POINT_2F p1 = D2D1::Point2F(cx - dx * size * 0.20f - px * size * 0.14f,
                                                       cy - dy * size * 0.20f - py * size * 0.14f);
                const D2D1_POINT_2F p2 = D2D1::Point2F(cx + dx * size * 0.95f + px * size * 0.18f,
                                                       cy + dy * size * 0.95f + py * size * 0.18f);
                d2dContext_->DrawLine(p0, p1, tumbleweedBrush_.Get(), 1.0f);
                d2dContext_->DrawLine(p1, p2, tumbleweedBrush_.Get(), 1.0f);
            }
            continue;
        }

        if (e.kind == EntityKind::Cat) {
            DrawCat(e, cursorPosition);
            DrawPetName(e, cursorPosition);
            continue;
        }

        if (e.kind == EntityKind::Bunny) {
            DrawBunny(e);
            DrawPetName(e, cursorPosition);
            continue;
        }

        if (e.kind == EntityKind::Hedgehog) {
            DrawHedgehog(e);
            DrawPetName(e, cursorPosition);
            continue;
        }

        if (e.kind == EntityKind::Butterfly) {
            DrawButterfly(e);
            continue;
        }

        if (e.kind == EntityKind::Firefly) {
            DrawFirefly(e);
            continue;
        }

        if (e.kind == EntityKind::Bubble) {
            const float cx = static_cast<float>(e.x);
            const float cy = static_cast<float>(e.y);
            const float br = static_cast<float>(e.size);
            if (bubbleStrokeBrush_) {
                d2dContext_->DrawEllipse(D2D1::Ellipse(D2D1::Point2F(cx, cy), br, br),
                                         bubbleStrokeBrush_.Get(), std::max(0.9f, br * 0.25f));
            }
            if (bubbleHighlightBrush_) {
                const float hr = std::max(0.6f, br * 0.30f);
                d2dContext_->FillEllipse(
                    D2D1::Ellipse(D2D1::Point2F(cx - br * 0.32f, cy - br * 0.32f), hr, hr),
                    bubbleHighlightBrush_.Get());
            }
            continue;
        }

        if (e.kind == EntityKind::Fish) {
            DrawFish(e);
            continue;
        }

        if (e.kind == EntityKind::Leaf) {
            uint8_t idx = e.colorVariant;
            if (idx >= LEAF_COLOR_COUNT) idx = 0;
            const float cx = static_cast<float>(e.x);
            const float cy = static_cast<float>(e.y);
            const float r = static_cast<float>(e.size);
            d2dContext_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(cx, cy), r, r * 0.78f), leafBrushes_[idx].Get());
            const float dx = std::cos(static_cast<float>(e.rotation));
            const float dy = std::sin(static_cast<float>(e.rotation));
            d2dContext_->DrawLine(D2D1::Point2F(cx, cy), D2D1::Point2F(cx + dx * r * 1.25f, cy + dy * r * 1.25f),
                                  mapleTrunkDarkBrush_.Get(), std::max(0.8f, r * 0.18f));
            continue;
        }

        if (e.kind == EntityKind::SnowPuff) {
            float alpha = 1.0f;
            if (e.lifetime > 0.0) alpha = static_cast<float>(1.0 - e.age / e.lifetime);
            if (alpha <= 0.0f) continue;
            if (alpha > 1.0f) alpha = 1.0f;
            const float r  = static_cast<float>(e.size);
            const float cx = static_cast<float>(e.x);
            const float cy = static_cast<float>(e.y);
            // Cool rim first so the white core reads against the white bank, then
            // the bright core on top.
            if (snowBankShadowBrush_) {
                const float sr = r * static_cast<float>(SNOW_PUFF_SHADOW_SCALE);
                snowBankShadowBrush_->SetOpacity(alpha * static_cast<float>(SNOW_PUFF_SHADOW_OPACITY));
                d2dContext_->FillEllipse(
                    D2D1::Ellipse(D2D1::Point2F(cx, cy + r * static_cast<float>(SNOW_PUFF_SHADOW_OFFSET)), sr, sr),
                    snowBankShadowBrush_.Get());
                snowBankShadowBrush_->SetOpacity(1.0f);
            }
            snowflakeBrush_->SetOpacity(alpha);
            d2dContext_->FillEllipse(
                D2D1::Ellipse(D2D1::Point2F(cx, cy), r, r),
                snowflakeBrush_.Get());
            snowflakeBrush_->SetOpacity(1.0f);
            continue;
        }

        if (e.kind != EntityKind::Snowflake) {
            if (e.kind == EntityKind::Sheep) {
                // Suffolk-style vector sheep: white wool cloud + dark head
                // and legs. State drives the pose:
                //   WALKING  : leg cycle + head bob + tail wiggle.
                //   GRAZING  : frozen, head pivoted down to the grass line.
                //   IDLE     : frozen, head turns side-to-side.
                //   GREETING : frozen, head gently bobs while facing partner.
                //   SLEEPING : tucked on the ground, legs hidden, eyes
                //              closed (horizontal slits), Z's drift up.
                //   HOPPING  : sheep arcs upward (parabola) — entire pose
                //              translated by a hopY offset; horizontal vx
                //              still applies so the sheep covers ground.
                const float cx = static_cast<float>(e.x);
                const float br = static_cast<float>(SHEEP_BODY_RADIUS);
                const float bh = static_cast<float>(SHEEP_BODY_HEIGHT);
                const float legLen = static_cast<float>(SHEEP_LEG_LENGTH);
                const float headR  = static_cast<float>(SHEEP_HEAD_RADIUS);
                const float tailR  = static_cast<float>(SHEEP_TAIL_RADIUS);
                const float facing = (e.vx >= 0.0) ? 1.0f : -1.0f;

                const bool isWalking  = (e.state == SHEEP_STATE_WALKING);
                const bool isGrazing  = (e.state == SHEEP_STATE_GRAZING);
                const bool isIdle     = (e.state == SHEEP_STATE_IDLE);
                const bool isGreeting = (e.state == SHEEP_STATE_GREETING);
                const bool isSleeping = (e.state == SHEEP_STATE_SLEEPING);
                const bool isHopping  = (e.state == SHEEP_STATE_HOPPING);

                // Hop parabola y-offset (negative = up). t = age / DURATION.
                float hopOffsetY = 0.0f;
                if (isHopping) {
                    const float t = std::max(0.0f,
                        std::min(1.0f, static_cast<float>(e.age / SHEEP_HOP_DURATION)));
                    hopOffsetY = -4.0f * static_cast<float>(SHEEP_HOP_HEIGHT) * t * (1.0f - t);
                }
                // Sleep pose: body drops by leg-length so it sits on the
                // ground; legs are hidden because they're tucked underneath.
                const float sleepOffsetY = isSleeping ? legLen : 0.0f;
                const float cy = static_cast<float>(e.y) + hopOffsetY + sleepOffsetY;

                const float walkPhase = static_cast<float>(e.age * (TWO_PI_LOCAL / SHEEP_WALK_PERIOD));
                const float legAmp   = isWalking ? static_cast<float>(SHEEP_LEG_CYCLE_AMP) : 0.0f;
                const float headBob  = isWalking
                    ? std::sin(walkPhase * 2.0f) * static_cast<float>(SHEEP_HEAD_BOB_AMP)
                    : 0.0f;
                const float tailWig  = isWalking
                    ? std::sin(walkPhase * 2.0f) * static_cast<float>(SHEEP_TAIL_WIGGLE_AMP)
                    : 0.0f;

                // Legs — hidden while sleeping (tucked). Hopping draws them
                // straight (no swing) so the sheep looks suspended.
                if (!isSleeping) {
                    const float legY0 = cy + bh * 0.30f;
                    const float legXs[4] = { -br * 0.62f, -br * 0.22f,
                                             +br * 0.22f, +br * 0.62f };
                    const float swingA = std::sin(walkPhase) * legAmp;
                    const float swingB = std::sin(walkPhase + 3.14159265f) * legAmp;
                    const float legSwings[4] = { swingA, swingB, swingA, swingB };
                    for (int li = 0; li < 4; ++li) {
                        const float lx = cx + legXs[li];
                        const float ly1 = cy + bh + legLen + legSwings[li];
                        d2dContext_->DrawLine(
                            D2D1::Point2F(lx, legY0),
                            D2D1::Point2F(lx, ly1),
                            sheepLegBrush_.Get(),
                            1.8f);
                    }
                }

                // Tail puff — rear of the body (opposite of facing).
                const float tailCx = cx - facing * br * 0.95f + tailWig;
                const float tailCy = cy - bh * 0.05f;
                d2dContext_->FillEllipse(
                    D2D1::Ellipse(D2D1::Point2F(tailCx, tailCy), tailR, tailR * 0.95f),
                    sheepBodyBrush_.Get());

                // Body — one large ellipse + 3 evenly-spaced top puffs.
                d2dContext_->FillEllipse(
                    D2D1::Ellipse(D2D1::Point2F(cx, cy), br, bh),
                    sheepBodyBrush_.Get());
                const float puffY = cy - bh * 0.55f;
                const float puffRx = br * 0.40f;
                const float puffRy = bh * 0.48f;
                const float puffXs[3] = { -br * 0.50f, 0.0f, +br * 0.50f };
                for (float pdx : puffXs) {
                    d2dContext_->FillEllipse(
                        D2D1::Ellipse(D2D1::Point2F(cx + pdx, puffY), puffRx, puffRy),
                        sheepBodyBrush_.Get());
                }

                // Head position. WALKING/HOPPING: forward + slight bob.
                // GRAZING: pivoted down to the grass. IDLE: sweeps L/R.
                // GREETING: faces partner via vx and gently nuzzles.
                // SLEEPING: rests low on the front edge of the body.
                float headDirX = facing;
                float headDx = headDirX * (br * 1.08f);
                float headDy = -bh * 0.05f + headBob;
                if (isGrazing) {
                    const float munch = std::sin(
                        static_cast<float>(e.age * SHEEP_GRAZE_MUNCH_FREQ))
                        * static_cast<float>(SHEEP_GRAZE_MUNCH_AMP);
                    headDx = headDirX * br * 0.85f;
                    headDy = bh * 0.85f + munch;
                } else if (isIdle) {
                    const float stripTop = static_cast<float>(sim_.windowHeight - STRIP_HEIGHT);
                    const bool curious = cursorPosition != nullptr
                        && std::fabs(cursorPosition->y - stripTop) <= SHEEP_CURIOUS_VERTICAL_RADIUS_DIP
                        && std::fabs(cursorPosition->x - cx) <= static_cast<float>(SHEEP_CURIOUS_RADIUS);
                    if (curious) {
                        const float cursorDx = cursorPosition->x - cx;
                        const float maxHeadDx = static_cast<float>(
                            SHEEP_CURIOUS_HEAD_TURN_MAX * SHEEP_HEAD_RADIUS);
                        headDirX = cursorDx >= 0.0f ? 1.0f : -1.0f;
                        headDx = std::clamp(cursorDx, -maxHeadDx, maxHeadDx);
                    } else {
                        const float sweep = std::sin(
                            static_cast<float>(e.age * SHEEP_IDLE_SWEEP_FREQ));
                        headDirX = sweep >= 0.0f ? 1.0f : -1.0f;
                        headDx = headDirX * (br * 1.08f) * (0.6f + 0.4f * std::fabs(sweep));
                    }
                    headDy = -bh * 0.05f;
                } else if (isGreeting) {
                    headDy -= std::sin(static_cast<float>(e.age * SHEEP_GREET_HEAD_BOB_FREQ))
                        * static_cast<float>(SHEEP_GREET_HEAD_BOB_AMP);
                } else if (isSleeping) {
                    headDx = headDirX * br * 0.95f;
                    headDy = bh * 0.10f;
                }
                const float headCx = cx + headDx;
                const float headCy = cy + headDy;

                d2dContext_->FillEllipse(
                    D2D1::Ellipse(D2D1::Point2F(headCx, headCy), headR, headR * 1.05f),
                    sheepFaceBrush_.Get());

                // Two ear blobs at the top of the head.
                const float earRx = headR * 0.32f;
                const float earRy = headR * 0.55f;
                d2dContext_->FillEllipse(
                    D2D1::Ellipse(D2D1::Point2F(headCx - headR * 0.55f,
                                                headCy - headR * 0.65f),
                                  earRx, earRy),
                    sheepEarBrush_.Get());
                d2dContext_->FillEllipse(
                    D2D1::Ellipse(D2D1::Point2F(headCx + headR * 0.55f,
                                                headCy - headR * 0.65f),
                                  earRx, earRy),
                    sheepEarBrush_.Get());

                // Eye — open dot in most states, closed slit while sleeping.
                if (isSleeping) {
                    const float slitY = headCy - headR * 0.05f;
                    const float slitX = headCx + headDirX * headR * 0.42f;
                    d2dContext_->DrawLine(
                        D2D1::Point2F(slitX - 1.4f, slitY),
                        D2D1::Point2F(slitX + 1.4f, slitY),
                        sheepInkBrush_.Get(),
                        1.0f);
                } else {
                    const float eyeR = headR * 0.22f;
                    d2dContext_->FillEllipse(
                        D2D1::Ellipse(D2D1::Point2F(headCx + headDirX * headR * 0.42f,
                                                    headCy - headR * 0.05f),
                                      eyeR, eyeR),
                        sheepInkBrush_.Get());
                }

                // Sleeping "Z" glyphs — two staggered Z's drifting up and
                // growing then fading, so the user reads the sleep state
                // instantly even from across the desktop. Drawn as 3-line
                // glyphs in body color (white) so they read on any biome.
                if (isSleeping) {
                    const float zBaseX = headCx + headDirX * headR * 0.7f;
                    const float zBaseY = headCy - headR * 1.4f;
                    for (int zi = 0; zi < 2; ++zi) {
                        const float phaseOffset = 0.5f * static_cast<float>(zi);
                        float t = static_cast<float>(
                            std::fmod(e.age / SHEEP_ZZZ_CYCLE_SEC + phaseOffset, 1.0));
                        // Skip the leading half-cycle of the offset Z so it
                        // doesn't pop in at full size.
                        const float zSize = static_cast<float>(
                            SHEEP_ZZZ_SIZE_START + t * (SHEEP_ZZZ_SIZE_END - SHEEP_ZZZ_SIZE_START));
                        const float zY = zBaseY - t * static_cast<float>(SHEEP_ZZZ_RISE);
                        const float zX = zBaseX + t * 4.0f * headDirX;
                        const float alpha = 1.0f - t;
                        sheepBodyBrush_->SetOpacity(alpha);
                        // Top horizontal
                        d2dContext_->DrawLine(
                            D2D1::Point2F(zX,         zY),
                            D2D1::Point2F(zX + zSize, zY),
                            sheepBodyBrush_.Get(),
                            1.1f);
                        // Diagonal
                        d2dContext_->DrawLine(
                            D2D1::Point2F(zX + zSize, zY),
                            D2D1::Point2F(zX,         zY + zSize),
                            sheepBodyBrush_.Get(),
                            1.1f);
                        // Bottom horizontal
                        d2dContext_->DrawLine(
                            D2D1::Point2F(zX,         zY + zSize),
                            D2D1::Point2F(zX + zSize, zY + zSize),
                            sheepBodyBrush_.Get(),
                            1.1f);
                    }
                    sheepBodyBrush_->SetOpacity(1.0f);
                }

                DrawPetName(e, cursorPosition);
                continue;
            }
            continue;
        }
        const float r = static_cast<float>(e.size);
        const D2D1_ELLIPSE flake = D2D1::Ellipse(
            D2D1::Point2F(static_cast<float>(e.x), static_cast<float>(e.y)),
            r,
            r);
        d2dContext_->FillEllipse(flake, snowflakeBrush_.Get());
    }

    for (const Entity& e : sim_.entities) {
        if (e.kind == EntityKind::Bird) {
            DrawBird(e);
        }
    }
}

} // namespace desktopgrass
