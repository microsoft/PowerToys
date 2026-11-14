// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Renders the calibration overlay for made-up monitor layouts into PNG files, to check it on layouts that are hard to
// set up. For each layout it writes an overview of all monitors at their places in the virtual desktop and the primary
// monitor, where the panel is, large enough to read.

#include "../OverlaySnapshot.h"

#include <d2d1.h>
#include <wincodec.h>
#include <winrt/base.h>

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <filesystem>
#include <functional>
#include <string>
#include <vector>

using namespace seamless_cursor;

namespace
{
    struct FakeMonitor
    {
        std::wstring name;
        RECT rect{};
        double diagonalInches = 24.0; // As EDID would report it, for the physical size.
        float scale = 1.0f;
        bool primary = false;
    };

    struct Layout
    {
        std::wstring file;
        std::vector<FakeMonitor> monitors;
        std::vector<BorderCalibration> saved;
        bool identified = true;
        bool trail = false; // Shows the pointer trail instead of the edge map.
        std::function<void(SnapshotPointer&)> movePointer;
    };

    constexpr ULONGLONG moveStartTime = 100000;
    constexpr ULONGLONG moveIntervalMs = 6;
    constexpr float overviewWidth = 3000.0f;
    constexpr float primaryWidth = 1800.0f;

    SnapshotMonitor ToSnapshot(const FakeMonitor& monitor, bool identified)
    {
        const double width = static_cast<double>(monitor.rect.right) - monitor.rect.left;
        const double height = static_cast<double>(monitor.rect.bottom) - monitor.rect.top;
        SnapshotMonitor snapshot;
        snapshot.details.monitor = { monitor.name, monitor.rect };
        snapshot.details.name = monitor.name;
        snapshot.details.primary = monitor.primary;
        snapshot.details.mmPerPixel = monitor.diagonalInches * 25.4 / std::sqrt(width * width + height * height);
        snapshot.details.identified = identified;
        snapshot.scale = monitor.scale;
        return snapshot;
    }

    void SavePng(IWICImagingFactory* wic, IWICBitmapSource* source, const std::filesystem::path& path)
    {
        winrt::com_ptr<IWICStream> stream;
        winrt::com_ptr<IWICBitmapEncoder> encoder;
        winrt::com_ptr<IWICBitmapFrameEncode> frame;
        winrt::check_hresult(wic->CreateStream(stream.put()));
        winrt::check_hresult(stream->InitializeFromFilename(path.c_str(), GENERIC_WRITE));
        winrt::check_hresult(wic->CreateEncoder(GUID_ContainerFormatPng, nullptr, encoder.put()));
        winrt::check_hresult(encoder->Initialize(stream.get(), WICBitmapEncoderNoCache));
        winrt::check_hresult(encoder->CreateNewFrame(frame.put(), nullptr));
        winrt::check_hresult(frame->Initialize(nullptr));
        UINT width = 0;
        UINT height = 0;
        winrt::check_hresult(source->GetSize(&width, &height));
        winrt::check_hresult(frame->SetSize(width, height));
        WICPixelFormatGUID format = GUID_WICPixelFormat32bppPBGRA;
        winrt::check_hresult(frame->SetPixelFormat(&format));
        winrt::check_hresult(frame->WriteSource(source, nullptr));
        winrt::check_hresult(frame->Commit());
        winrt::check_hresult(encoder->Commit());
    }

    winrt::com_ptr<IWICBitmapSource> Scaled(IWICImagingFactory* wic, IWICBitmapSource* source, UINT width, UINT height)
    {
        winrt::com_ptr<IWICBitmapScaler> scaler;
        winrt::check_hresult(wic->CreateBitmapScaler(scaler.put()));
        winrt::check_hresult(scaler->Initialize(source, std::max(1u, width), std::max(1u, height), WICBitmapInterpolationModeHighQualityCubic));
        return scaler;
    }

    // All monitors at their places in the virtual desktop, scaled down, on a lighter background, so gaps show.
    void SaveOverview(IWICImagingFactory* wic, const std::vector<SnapshotMonitor>& monitors, const std::vector<winrt::com_ptr<IWICBitmap>>& bitmaps, const std::filesystem::path& path)
    {
        RECT bounds = monitors.front().details.monitor.rect;
        for (const auto& monitor : monitors)
        {
            const RECT& r = monitor.details.monitor.rect;
            bounds = { std::min(bounds.left, r.left), std::min(bounds.top, r.top), std::max(bounds.right, r.right), std::max(bounds.bottom, r.bottom) };
        }

        constexpr float margin = 160.0f;
        const float width = static_cast<float>(bounds.right - bounds.left) + 2.0f * margin;
        const float height = static_cast<float>(bounds.bottom - bounds.top) + 2.0f * margin;
        const float factor = std::min(1.0f, overviewWidth / width);

        winrt::com_ptr<ID2D1Factory> d2d;
        winrt::com_ptr<IWICBitmap> canvas;
        winrt::com_ptr<ID2D1RenderTarget> target;
        winrt::com_ptr<ID2D1SolidColorBrush> brush;
        winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, d2d.put()));
        winrt::check_hresult(wic->CreateBitmap(static_cast<UINT>(width * factor), static_cast<UINT>(height * factor), GUID_WICPixelFormat32bppPBGRA, WICBitmapCacheOnLoad, canvas.put()));
        const auto properties = D2D1::RenderTargetProperties(D2D1_RENDER_TARGET_TYPE_DEFAULT, D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED), 96.0f, 96.0f);
        winrt::check_hresult(d2d->CreateWicBitmapRenderTarget(canvas.get(), properties, target.put()));
        winrt::check_hresult(target->CreateSolidColorBrush(D2D1::ColorF(0x000000), brush.put()));

        target->BeginDraw();
        target->Clear(D2D1::ColorF(0x5A5A5A));
        for (size_t i = 0; i < monitors.size(); ++i)
        {
            const RECT& r = monitors[i].details.monitor.rect;
            const auto place = [&](LONG value, LONG origin) { return (static_cast<float>(value - origin) + margin) * factor; };
            const D2D1_RECT_F dest{ place(r.left, bounds.left), place(r.top, bounds.top), place(r.right, bounds.left), place(r.bottom, bounds.top) };
            const auto scaled = Scaled(wic, bitmaps[i].get(), static_cast<UINT>(dest.right - dest.left), static_cast<UINT>(dest.bottom - dest.top));
            winrt::com_ptr<ID2D1Bitmap> bitmap;
            winrt::check_hresult(target->CreateBitmapFromWicBitmap(scaled.get(), bitmap.put()));
            target->DrawBitmap(bitmap.get(), dest);
            target->DrawRectangle(dest, brush.get(), 2.0f);
        }

        winrt::check_hresult(target->EndDraw());
        SavePng(wic, canvas.get(), path);
    }

    void Render(IWICImagingFactory* wic, const Layout& layout, const std::filesystem::path& folder)
    {
        SnapshotRequest request;
        for (const auto& monitor : layout.monitors)
        {
            request.monitors.push_back(ToSnapshot(monitor, layout.identified));
        }

        request.saved = layout.saved;
        request.preferences = { true, !layout.trail, layout.trail };
        request.movePointer = layout.movePointer;
        const auto bitmaps = RenderSnapshot(wic, request);
        if (bitmaps.size() != request.monitors.size())
        {
            wprintf(L"%s: the overlay could not be drawn\n", layout.file.c_str());
            return;
        }

        const auto overview = folder / (layout.file + L"-overview.png");
        SaveOverview(wic, request.monitors, bitmaps, overview);
        for (size_t i = 0; i < bitmaps.size(); ++i)
        {
            if (request.monitors[i].details.primary)
            {
                const RECT& r = request.monitors[i].details.monitor.rect;
                const float width = static_cast<float>(r.right - r.left);
                const float factor = std::min(1.0f, primaryWidth / width);
                const auto scaled = Scaled(wic, bitmaps[i].get(), static_cast<UINT>(width * factor), static_cast<UINT>(static_cast<float>(r.bottom - r.top) * factor));
                SavePng(wic, scaled.get(), folder / (layout.file + L"-primary.png"));
            }
        }

        wprintf(L"%s\n", overview.c_str());
    }

    std::vector<Layout> Layouts()
    {
        std::vector<Layout> layouts;

        // Two monitors standing on a very wide one, with a gap between them that Windows cannot close; calibrated,
        // and the two on top glued.
        layouts.push_back({ L"1-two-on-a-wide-monitor",
                            { { L"Odyssey G95NC", { 0, 0, 7680, 2160 }, 57.0, 1.5f, true },
                              { L"BenQ PD3220U", { 1204, -2160, 5044, 0 }, 31.5, 1.5f },
                              { L"Dell U2724D", { 5858, -1440, 8418, 0 }, 27.0, 1.25f } },
                            { BorderCalibration{ L"BenQ PD3220U", L"Odyssey G95NC", Edge::Bottom, true, { MarkerPair{ 0.25, 0.2602 }, MarkerPair{ 0.75, 0.4634 } } },
                              BorderCalibration{ L"Odyssey G95NC", L"Dell U2724D", Edge::Top, true, { MarkerPair{ 0.7105, 0.1585 }, MarkerPair{ 0.92, 0.6825 } } },
                              BorderCalibration{ L"BenQ PD3220U", L"Dell U2724D", Edge::Right, true, { MarkerPair{ 0.3028, 0.2083 }, MarkerPair{ 0.7662, 0.6972 } }, true, true, true } } });

        // A portrait monitor, a 4K primary, a laptop half under it, a TV below the portrait one and a small touch panel.
        layouts.push_back({ L"2-mixed-orientation-and-scale",
                            { { L"Portrait 1440p", { -1440, -400, 0, 2160 }, 27.0, 1.0f },
                              { L"Main 4K", { 0, 0, 3840, 2160 }, 31.5, 1.5f, true },
                              { L"Laptop", { 2000, 2160, 4880, 3960 }, 14.0, 2.0f },
                              { L"TV", { -3360, 2160, 0, 3240 }, 55.0, 1.0f },
                              { L"Touch panel", { 3840, 300, 4864, 900 }, 7.0, 1.0f } } });

        // A staircase: each monitor shares half of a side with the next; the last touches only at a corner.
        layouts.push_back({ L"3-staircase",
                            { { L"Step 1", { 0, 0, 1920, 1080 }, 24.0, 1.0f, true },
                              { L"Step 2", { 1920, 540, 3840, 1620 } },
                              { L"Step 3", { 3840, 1080, 5760, 2160 } },
                              { L"Step 4", { 5760, 1620, 7680, 2700 } },
                              { L"Corner only", { 7680, 2700, 9600, 3780 } } } });

        // A monitor on every side of the primary, so the panel and the switches must make room.
        layouts.push_back({ L"4-surround",
                            { { L"Center", { 0, 0, 2560, 1440 }, 27.0, 1.0f, true },
                              { L"Top", { 320, -1080, 2240, 0 } },
                              { L"Bottom", { 320, 1440, 2240, 2520 } },
                              { L"Left portrait", { -1080, -240, 0, 1680 } },
                              { L"Right portrait", { 2560, -240, 3640, 1680 } } } });

        // A tiny primary display next to an 8K monitor: the compact panel.
        layouts.push_back({ L"5-tiny-primary",
                            { { L"Small display", { 0, 0, 800, 480 }, 7.0, 1.0f, true },
                              { L"8K", { 800, -1920, 8480, 2400 }, 65.0, 3.0f } } });

        // Monitors that face each other across gaps Windows cannot close, on several sides.
        layouts.push_back({ L"6-gaps",
                            { { L"Main", { 0, 0, 2560, 1440 }, 27.0, 1.0f, true },
                              { L"Right, across a gap", { 2760, 200, 4680, 1280 } },
                              { L"Above, across a gap", { 300, -1280, 2220, -200 } },
                              { L"Below the right one", { 2760, 1480, 4680, 2560 } } } });

        // A remote session, where Windows does not report the monitors' device paths.
        Layout remote{ L"7-remote-session", { { L"Remote 1", { 0, 0, 1920, 1080 }, 24.0, 1.0f, true }, { L"Remote 2", { 1920, 0, 3840, 1080 } } } };
        remote.identified = false;
        layouts.push_back(remote);

        // Three monitors side by side with the pointer trail: the pointer crosses from the middle monitor to the right one,
        // remapped as Try while adjusting does, and at the right edge CursorWrap wraps it to the left monitor at the
        // physically matching height, where the wrap line is.
        Layout trail{ L"8-trail-crossing-and-wrap",
                      { { L"Left 1440p", { -2560, 300, 0, 1740 }, 27.0, 1.0f },
                        { L"Middle 4K", { 0, 0, 3840, 2160 }, 31.5, 1.5f, true },
                        { L"Right 1080p", { 3840, 600, 5760, 1680 }, 24.0, 1.0f } } };
        trail.trail = true;
        trail.movePointer = [](SnapshotPointer& pointer) {
            auto engine = pointer.CurrentEngine();
            ULONGLONG time = moveStartTime;
            POINT cursor{ 2300, 1500 };
            const auto move = [&](POINT requested) {
                if (const auto remap = engine.Resolve(cursor, requested))
                {
                    requested = remap->point;
                }

                cursor = requested;
                time += moveIntervalMs;
                pointer.Move(cursor, time);
            };

            pointer.Move(cursor, time);
            constexpr double pi = 3.14159265358979;
            for (int i = 1; i <= 160; ++i)
            {
                // An arc up and back down, in relative steps as a mouse makes them.
                const double rise = 380.0 * (std::sin(i / 160.0 * pi) - std::sin((static_cast<double>(i) - 1.0) / 160.0 * pi));
                move({ cursor.x + 14, cursor.y - static_cast<LONG>(std::lround(rise)) });
            }

            const RECT left{ -2560, 300, 0, 1740 };
            const RECT right{ 3840, 600, 5760, 1680 };
            while (cursor.x < right.right - 1)
            {
                move({ std::min<LONG>(cursor.x + 14, right.right - 1), cursor.y });
            }

            // CursorWrap with "Keep the physical position" asks Seamless where to land.
            cursor = { left.left + 1, engine.MapJump(right, Edge::Right, cursor.y, left).value_or(cursor.y) };
            time += moveIntervalMs;
            pointer.Move(cursor, time);
            for (int i = 1; i <= 40; ++i)
            {
                move({ cursor.x + 14, cursor.y + 3 });
            }
        };
        layouts.push_back(trail);
        return layouts;
    }
}

int wmain(int argc, wchar_t** argv)
{
    if (argc > 2 || (argc == 2 && (std::wstring{ argv[1] } == L"/?" || std::wstring{ argv[1] } == L"--help")))
    {
        wprintf(L"Usage: SeamlessCursor.OverlaySnapshots.exe [output folder]\n"
                L"Renders the Seamless Multi-Screen Cursor calibration overlay for made-up monitor layouts into PNG files.\n"
                L"The output folder defaults to OverlaySnapshots in the current folder.\n");
        return argc == 2 ? 0 : 1;
    }

    try
    {
        winrt::init_apartment(winrt::apartment_type::single_threaded);
        const std::filesystem::path folder = argc == 2 ? argv[1] : L"OverlaySnapshots";
        std::filesystem::create_directories(folder);

        winrt::com_ptr<IWICImagingFactory> wic;
        winrt::check_hresult(CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(wic.put())));
        for (const auto& layout : Layouts())
        {
            Render(wic.get(), layout, folder);
        }
    }
    catch (const winrt::hresult_error& error)
    {
        wprintf(L"Error 0x%08X: %s\n", static_cast<unsigned>(error.code()), error.message().c_str());
        return 1;
    }
    catch (const std::exception& error)
    {
        printf("Error: %s\n", error.what());
        return 1;
    }

    return 0;
}
