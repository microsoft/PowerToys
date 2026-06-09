// Renderer.h
//
// Per-window Direct2D + DXGI renderer attached to a DirectComposition target.
// Owns the swap chain, the D2D device context bound to it, and the per-window
// Sim. Renders the procedural grass once per frame.

#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <wrl/client.h>
#include <d3d11.h>
#include <dxgi1_3.h>
#include <d2d1_3.h>
#include <dcomp.h>
#include <dwrite.h>

#include <unordered_map>

#include "Sim.h"

namespace desktopgrass {

class Renderer {
public:
    Renderer() = default;
    ~Renderer();

    // Sets up D3D / D2D / DComp on `hwnd` of the given width × height in DIPs,
    // and generates the initial blade list with `seed`. Returns false on
    // failure (logged via OutputDebugString).
    bool Initialize(HWND hwnd, int widthPx, int heightPx,
                    UINT dpi, uint64_t seed, double density,
                    double swaySpeed = 1.0, double swayAmplitude = 1.0);

    // Resize the swap chain & D2D target. Call when the monitor changes size
    // (DPI change, mode change). Leaves Sim intact; caller may regenerate it.
    bool Resize(int widthPx, int heightPx, UINT dpi);

    // Regenerate the blade layout for the current (post-Resize) DIP width after
    // a DPI change, reseeding with the same deterministic per-monitor seed and
    // preserving scene/critter/cut state. Mirrors the Win2D rebuild path. Must
    // NOT be called on device-loss recovery (which leaves the Sim untouched).
    void RegenerateForDpi(uint64_t seed, double density);

    // Advance the simulation by `dt` seconds, then draw a frame.
    void RenderFrame(double dt,
                     const InputEvent* events,
                     std::size_t numEvents);

    // For windows that have been minimized / occluded: skip rendering but keep
    // the simulation alive.
    void Tick(double dt,
              const InputEvent* events,
              std::size_t numEvents);

    Sim&        GetSim()        { return sim_; }
    const Sim&  GetSim() const  { return sim_; }
    HWND        GetHwnd() const { return hwnd_; }

    void SetWindowOriginScreen(int x, int y) { windowOriginScreenX_ = x; windowOriginScreenY_ = y; }
    int  GetWindowOriginScreenX() const { return windowOriginScreenX_; }
    int  GetWindowOriginScreenY() const { return windowOriginScreenY_; }
    int  GetWidthPx() const  { return widthPx_; }
    int  GetHeightPx() const { return heightPx_; }
    UINT GetDpi() const      { return dpi_; }

private:
    template<class T> using ComPtr = Microsoft::WRL::ComPtr<T>;

    void Cleanup();
    bool CreateDeviceResources();
    bool CreateSwapChainResources(int widthPx, int heightPx);
    void DiscardDeviceResources();
    void DrawGrass(bool treesOnly, bool backgroundTrees);
    void DrawEntities(const D2D1_POINT_2F* cursorPosition);
    void DrawButterfly(const Entity& e);
    void DrawFirefly(const Entity& e);
    void DrawBird(const Entity& e);
    void DrawCoral(const Blade& b, float groundY);
    void DrawFish(const Entity& e);
    void DrawCat(const Entity& e, const D2D1_POINT_2F* cursorPosition);
    void DrawBunny(const Entity& e);
    void DrawHedgehog(const Entity& e);
    void DrawPetName(const Entity& e, const D2D1_POINT_2F* cursorPosition);
    bool TryGetCursorPositionDip(D2D1_POINT_2F& cursorPosition) const;

    HWND                                   hwnd_ = nullptr;
    int                                    widthPx_   = 0;
    int                                    heightPx_  = 0;
    UINT                                   dpi_       = 96;
    int                                    windowOriginScreenX_ = 0;
    int                                    windowOriginScreenY_ = 0;

    ComPtr<ID3D11Device>                   d3dDevice_;
    ComPtr<ID3D11DeviceContext>            d3dContext_;
    ComPtr<IDXGIDevice1>                   dxgiDevice_;
    ComPtr<IDXGIFactory2>                  dxgiFactory_;
    ComPtr<IDXGISwapChain1>                swapChain_;
    ComPtr<ID2D1Factory1>                  d2dFactory_;
    ComPtr<ID2D1Device>                    d2dDevice_;
    ComPtr<ID2D1DeviceContext>             d2dContext_;
    ComPtr<ID2D1Bitmap1>                   d2dTarget_;
    ComPtr<ID2D1SolidColorBrush>           brushes_[SCENE_COUNT][PALETTE_SIZE];
    ComPtr<ID2D1SolidColorBrush>           flowerHeadBrushes_[FLOWER_PALETTE_SIZE];
    ComPtr<ID2D1SolidColorBrush>           mushroomCapBrushes_[MUSHROOM_PALETTE_SIZE];
    ComPtr<ID2D1SolidColorBrush>           mushroomStemBrush_;
    ComPtr<ID2D1SolidColorBrush>           cactusBrush_;
    ComPtr<ID2D1StrokeStyle>               roundStrokeStyle_;
    ComPtr<ID2D1SolidColorBrush>           tumbleweedBrush_;
    ComPtr<ID2D1SolidColorBrush>           snowflakeBrush_;
    ComPtr<ID2D1SolidColorBrush>           leafBrushes_[LEAF_COLOR_COUNT];
    ComPtr<ID2D1SolidColorBrush>           snowTipBrush_;
    ComPtr<ID2D1SolidColorBrush>           snowBankShadowBrush_;
    ComPtr<ID2D1SolidColorBrush>           pineBrush_;
    ComPtr<ID2D1SolidColorBrush>           pineShadowBrush_;
    ComPtr<ID2D1SolidColorBrush>           pineHighlightBrush_;
    ComPtr<ID2D1SolidColorBrush>           birchBarkBrush_;
    ComPtr<ID2D1SolidColorBrush>           birchMarkBrush_;
    ComPtr<ID2D1SolidColorBrush>           mapleTrunkBrush_;
    ComPtr<ID2D1SolidColorBrush>           mapleTrunkDarkBrush_;
    ComPtr<ID2D1SolidColorBrush>           mapleCanopyBrushes_[MAPLE_CANOPY_COLOR_COUNT];
    ComPtr<ID2D1SolidColorBrush>           coralBrushes_[CORAL_COLOR_COUNT];
    ComPtr<ID2D1SolidColorBrush>           bubbleStrokeBrush_;
    ComPtr<ID2D1SolidColorBrush>           bubbleHighlightBrush_;
    ComPtr<ID2D1SolidColorBrush>           fishBrushes_[FISH_COLOR_COUNT];
    ComPtr<ID2D1SolidColorBrush>           fishFinBrush_;
    ComPtr<ID2D1SolidColorBrush>           sheepBodyBrush_;
    ComPtr<ID2D1SolidColorBrush>           sheepLegBrush_;
    ComPtr<ID2D1SolidColorBrush>           sheepFaceBrush_;
    ComPtr<ID2D1SolidColorBrush>           sheepEarBrush_;
    ComPtr<ID2D1SolidColorBrush>           sheepInkBrush_;
    struct CatCoatBrushSet {
        ComPtr<ID2D1SolidColorBrush> body;
        ComPtr<ID2D1SolidColorBrush> leg;
        ComPtr<ID2D1SolidColorBrush> face;
        ComPtr<ID2D1SolidColorBrush> ear;
        ComPtr<ID2D1SolidColorBrush> ink;
    };
    CatCoatBrushSet catCoatBrushes_[CAT_COAT_VARIANT_COUNT];
    ComPtr<ID2D1SolidColorBrush>           bunnyBodyBrush_;
    ComPtr<ID2D1SolidColorBrush>           bunnyBellyBrush_;
    ComPtr<ID2D1SolidColorBrush>           bunnyEarBrush_;
    ComPtr<ID2D1SolidColorBrush>           bunnyEarInnerBrush_;
    ComPtr<ID2D1SolidColorBrush>           bunnyTailBrush_;
    ComPtr<ID2D1SolidColorBrush>           bunnyEyeBrush_;
    ComPtr<ID2D1SolidColorBrush>           bunnyNoseBrush_;
    ComPtr<ID2D1SolidColorBrush>           hedgehogBodyBrush_;
    ComPtr<ID2D1SolidColorBrush>           hedgehogSpikeBrush_;
    ComPtr<ID2D1SolidColorBrush>           hedgehogSpikeTipBrush_;
    ComPtr<ID2D1SolidColorBrush>           hedgehogNoseBrush_;
    ComPtr<ID2D1SolidColorBrush>           hedgehogEyeBrush_;
    ComPtr<ID2D1SolidColorBrush>           butterflyBodyBrush_;
    ComPtr<ID2D1SolidColorBrush>           butterflyWingBrushes_[BUTTERFLY_COLOR_COUNT];
    ComPtr<ID2D1SolidColorBrush>           butterflyAccentBrushes_[BUTTERFLY_COLOR_COUNT];
    ComPtr<ID2D1SolidColorBrush>           fireflyBodyBrush_;
    ComPtr<ID2D1SolidColorBrush>           fireflyGlowBrush_;
    ComPtr<ID2D1SolidColorBrush>           birdBrush_;
    ComPtr<ID2D1SolidColorBrush>           petNameBrush_;
    ComPtr<ID2D1SolidColorBrush>           petNameShadowBrush_;
    ComPtr<IDWriteFactory>                 dwriteFactory_;
    ComPtr<IDWriteTextFormat>              petNameTextFormat_;

    ComPtr<IDCompositionDevice>            dcompDevice_;
    ComPtr<IDCompositionTarget>            dcompTarget_;
    ComPtr<IDCompositionVisual>            dcompVisual_;

    Sim                                    sim_{};
    std::unordered_map<uint64_t, double>   petNameLastHover_;
    bool                                   initialized_ = false;
};

} // namespace desktopgrass
