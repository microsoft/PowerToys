// capcheck.cpp — proves a window is shareable the way Teams does it.
//
// Teams' "Share -> Window" uses Windows.Graphics.Capture CreateForWindow on the
// chosen HWND. This tool does exactly that from a SEPARATE process: find a
// window by title substring, WGC-capture one frame, and save it to a BMP.
//
// If this captures live content from our OFF-SCREEN mirror window, then Teams
// (same API) will too — proving we can hide the mirror completely.
//
//   capcheck.exe "Shared Region" out.bmp

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_6.h>
#include <inspectable.h>
#include <cstdio>
#include <cstdint>
#include <string>

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "windowsapp.lib")
#pragma comment(lib, "user32.lib")

namespace wgc  = winrt::Windows::Graphics::Capture;
namespace wgdx = winrt::Windows::Graphics::DirectX;
using winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice;
using winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface;

static void logf(const wchar_t* fmt, ...)
{
    wchar_t buf[1024]; va_list a; va_start(a, fmt);
    _vsnwprintf_s(buf, _countof(buf), _TRUNCATE, fmt, a); va_end(a);
    wprintf(L"%s\n", buf); fflush(stdout);
}

struct FindCtx { std::wstring needle; HWND hwnd; std::wstring title; };
static BOOL CALLBACK FindProc(HWND h, LPARAM lp)
{
    auto* c = reinterpret_cast<FindCtx*>(lp);
    if (!IsWindowVisible(h)) return TRUE;
    wchar_t t[512]{};
    GetWindowTextW(h, t, _countof(t));
    std::wstring title = t;
    if (title.empty()) return TRUE;
    // case-insensitive substring match
    std::wstring hay = title, ndl = c->needle;
    for (auto& ch : hay) ch = towlower(ch);
    for (auto& ch : ndl) ch = towlower(ch);
    if (hay.find(ndl) != std::wstring::npos)
    {
        c->hwnd = h; c->title = title;
        return FALSE; // stop
    }
    return TRUE;
}

static winrt::com_ptr<ID3D11Texture2D> TextureFromSurface(IDirect3DSurface const& surface)
{
    auto access = surface.as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
    winrt::com_ptr<ID3D11Texture2D> tex;
    winrt::check_hresult(access->GetInterface(winrt::guid_of<ID3D11Texture2D>(), tex.put_void()));
    return tex;
}

#pragma pack(push, 1)
struct BmpFileHeader { uint16_t type; uint32_t size; uint16_t r1, r2; uint32_t offBits; };
struct BmpInfoHeader { uint32_t size; int32_t width, height; uint16_t planes, bitCount;
                       uint32_t compression, sizeImage; int32_t xppm, yppm; uint32_t clrUsed, clrImp; };
#pragma pack(pop)

static bool SaveBmp(const wchar_t* path, const uint8_t* bgra, int w, int h, UINT rowPitch)
{
    FILE* f = nullptr;
    if (_wfopen_s(&f, path, L"wb") != 0 || !f) return false;
    BmpFileHeader fh{}; BmpInfoHeader ih{};
    fh.type = 0x4D42;
    fh.offBits = sizeof(fh) + sizeof(ih);
    fh.size = fh.offBits + static_cast<uint32_t>(w) * h * 4;
    ih.size = sizeof(ih);
    ih.width = w; ih.height = -h; // top-down
    ih.planes = 1; ih.bitCount = 32; ih.compression = 0;
    ih.sizeImage = static_cast<uint32_t>(w) * h * 4;
    fwrite(&fh, sizeof(fh), 1, f);
    fwrite(&ih, sizeof(ih), 1, f);
    for (int y = 0; y < h; ++y)
        fwrite(bgra + static_cast<size_t>(y) * rowPitch, 4, w, f);
    fclose(f);
    return true;
}

int wmain(int argc, wchar_t** argv)
{
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    std::wstring needle = (argc > 1) ? argv[1] : L"Shared Region";
    const wchar_t* outPath = (argc > 2) ? argv[2] : L"capcheck.bmp";

    FindCtx fc{ needle, nullptr, L"" };
    EnumWindows(FindProc, reinterpret_cast<LPARAM>(&fc));
    if (!fc.hwnd) { logf(L"[capcheck] no visible window matching \"%ls\".", needle.c_str()); return 1; }

    RECT wr{}; GetWindowRect(fc.hwnd, &wr);
    logf(L"[capcheck] target hwnd=%p title=\"%ls\" rect=%d,%d %dx%d",
         fc.hwnd, fc.title.c_str(), wr.left, wr.top, wr.right - wr.left, wr.bottom - wr.top);

    winrt::init_apartment(winrt::apartment_type::single_threaded);
    if (!wgc::GraphicsCaptureSession::IsSupported()) { logf(L"[capcheck] WGC unsupported."); return 1; }

    try
    {
        winrt::com_ptr<ID3D11Device> d3d;
        winrt::com_ptr<ID3D11DeviceContext> ctx;
        UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
        HRESULT hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags,
                                       nullptr, 0, D3D11_SDK_VERSION, d3d.put(), nullptr, ctx.put());
        if (FAILED(hr))
            winrt::check_hresult(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, flags,
                                                   nullptr, 0, D3D11_SDK_VERSION, d3d.put(), nullptr, ctx.put()));
        auto dxgi = d3d.as<IDXGIDevice>();
        winrt::com_ptr<IInspectable> insp;
        winrt::check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgi.get(), insp.put()));
        auto winrtDevice = insp.as<IDirect3DDevice>();

        auto interop = winrt::get_activation_factory<wgc::GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
        wgc::GraphicsCaptureItem item{ nullptr };
        winrt::check_hresult(interop->CreateForWindow(
            fc.hwnd, winrt::guid_of<wgc::GraphicsCaptureItem>(), winrt::put_abi(item)));
        auto sz = item.Size();
        logf(L"[capcheck] capture item size: %dx%d", sz.Width, sz.Height);

        auto pool = wgc::Direct3D11CaptureFramePool::CreateFreeThreaded(
            winrtDevice, wgdx::DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, sz);
        auto session = pool.CreateCaptureSession(item);
        winrt::handle ev{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
        HANDLE h = ev.get();
        pool.FrameArrived([h](auto&&, auto&&) { SetEvent(h); });
        session.StartCapture();

        // Grab a few frames so we get a fresh (not first blank) one.
        winrt::com_ptr<ID3D11Texture2D> lastTex;
        for (int i = 0; i < 5; ++i)
        {
            if (WaitForSingleObject(ev.get(), 2000) != WAIT_OBJECT_0) break;
            ResetEvent(ev.get());
            if (auto frame = pool.TryGetNextFrame())
            {
                lastTex = TextureFromSurface(frame.Surface());
                frame.Close();
            }
            Sleep(60);
        }
        session.Close(); pool.Close();

        if (!lastTex) { logf(L"[capcheck] FAIL: no frame captured."); return 2; }

        D3D11_TEXTURE2D_DESC desc{}; lastTex->GetDesc(&desc);
        D3D11_TEXTURE2D_DESC sd = desc;
        sd.Usage = D3D11_USAGE_STAGING;
        sd.BindFlags = 0; sd.CPUAccessFlags = D3D11_CPU_ACCESS_READ; sd.MiscFlags = 0;
        winrt::com_ptr<ID3D11Texture2D> staging;
        winrt::check_hresult(d3d->CreateTexture2D(&sd, nullptr, staging.put()));
        ctx->CopyResource(staging.get(), lastTex.get());

        D3D11_MAPPED_SUBRESOURCE map{};
        winrt::check_hresult(ctx->Map(staging.get(), 0, D3D11_MAP_READ, 0, &map));
        bool ok = SaveBmp(outPath, static_cast<const uint8_t*>(map.pData),
                          static_cast<int>(desc.Width), static_cast<int>(desc.Height), map.RowPitch);
        ctx->Unmap(staging.get(), 0);

        logf(L"[capcheck] %s -> %ls (%ux%u)", ok ? L"SAVED" : L"SAVE FAILED",
             outPath, desc.Width, desc.Height);
        return ok ? 0 : 3;
    }
    catch (winrt::hresult_error const& e)
    {
        logf(L"[capcheck] HRESULT 0x%08X: %ls", static_cast<unsigned>(e.code()), e.message().c_str());
        return 4;
    }
}
