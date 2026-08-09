#include "pch.h"

#include "BoundsToolOverlayUI.h"
#include "constants.h"
#include "CoordinateSystemConversion.h"
#include "EdgeDetection.h"
#include "ScreenCapturing.h"
#include "ScreenCaptureSession.h"

#include <condition_variable>

//#define DEBUG_EDGES

namespace
{
    struct BoundsCaptureRequests
    {
        void Request(uint64_t generation)
        {
            {
                std::lock_guard lock{ mutex };
                if (!pendingGeneration || generation > *pendingGeneration)
                {
                    pendingGeneration = generation;
                }
            }
            condition.notify_one();
        }

        std::mutex mutex;
        std::condition_variable condition;
        std::optional<uint64_t> pendingGeneration;
    };

    void UpdateCaptureState(
        const CommonState& commonState,
        Serialized<MeasureToolState>& state,
        HWND window,
        const MappedTextureView& textureView)
    {
        const auto cursorPosition = convert::FromSystemToWindow(window, commonState.cursorPosSystemSpace);
        const bool cursorInLeftScreenHalf = cursorPosition.x < textureView.view.width / 2;
        const bool cursorInTopScreenHalf = cursorPosition.y < textureView.view.height / 2;
        uint8_t pixelTolerance{};
        bool perColorChannelEdgeDetection{};
        state.Access([&](MeasureToolState& currentState) {
            currentState.perScreen[window].cursorInLeftScreenHalf = cursorInLeftScreenHalf;
            currentState.perScreen[window].cursorInTopScreenHalf = cursorInTopScreenHalf;
            pixelTolerance = currentState.global.pixelTolerance;
            perColorChannelEdgeDetection = currentState.global.perColorChannelEdgeDetection;
        });

        const RECT bounds = DetectEdges(
            textureView.view,
            cursorPosition,
            perColorChannelEdgeDetection,
            pixelTolerance);
        const auto physicalPixelToMillimeterRatio = commonState.GetPhysicalPx2MmRatio(window);

#if defined(DEBUG_EDGES)
        char buffer[256];
        sprintf_s(
            buffer,
            "Cursor: [%ld,%ld] Bounds: [%ld,%ld]-[%ld,%ld] Screen size: [%zu, %zu] Ratio: %g\n",
            cursorPosition.x,
            cursorPosition.y,
            bounds.left,
            bounds.top,
            bounds.right,
            bounds.bottom,
            textureView.view.width,
            textureView.view.height,
            physicalPixelToMillimeterRatio);
        OutputDebugStringA(buffer);
#endif

        state.Access([&](MeasureToolState& currentState) {
            currentState.perScreen[window].measuredEdges = Measurement{ bounds, physicalPixelToMillimeterRatio };
        });
    }
}

std::thread StartCapturingThread(
    DxgiAPI* dxgiAPI,
    const CommonState& commonState,
    Serialized<MeasureToolState>& state,
    HWND window,
    MonitorInfo monitor)
{
    return SpawnLoggedThread(L"Screen Capture thread", [&state, &commonState, monitor, window, dxgiAPI] {
        bool continuousCapture{};
        state.Read([&](const MeasureToolState& currentState) {
            continuousCapture = currentState.global.continuousCapture;
        });

        auto captureSession = ScreenCaptureSession::Create(
            dxgiAPI,
            monitor,
            winrt::DirectXPixelFormat::B8G8R8A8UIntNormalized,
            continuousCapture);
        const auto monitorArea = monitor.GetScreenSize(true);
        bool mouseOnMonitor = false;

        if (continuousCapture)
        {
            while (IsWindow(window) && !commonState.closeOnOtherMonitors)
            {
                if (mouseOnMonitor == monitorArea.inside(commonState.cursorPosSystemSpace))
                {
                    std::this_thread::sleep_for(consts::TARGET_FRAME_DURATION);
                    continue;
                }

                mouseOnMonitor = !mouseOnMonitor;
                if (mouseOnMonitor)
                {
                    captureSession->StartCapture([&, window](MappedTextureView textureView) {
                        UpdateCaptureState(commonState, state, window, textureView);
                    });
                }
                else
                {
                    captureSession->StopCapture();
                    state.Access([&](MeasureToolState& currentState) {
                        currentState.perScreen[window].measuredEdges = {};
                    });
                }
            }
        }
        else
        {
            const auto textureView = captureSession->CaptureSingleFrame();
            state.Access([&](MeasureToolState& currentState) {
                currentState.perScreen[window].capturedScreenTexture = &textureView;
            });

            while (IsWindow(window) && !commonState.closeOnOtherMonitors)
            {
                const auto now = std::chrono::high_resolution_clock::now();
                if (monitorArea.inside(commonState.cursorPosSystemSpace))
                {
#if defined(DEBUG_TEXTURE)
                    SYSTEMTIME localTime{};
                    GetLocalTime(&localTime);
                    char buffer[256];
                    sprintf_s(
                        buffer,
                        "frame-%02d-%02d-Monitor-%zu.bmp",
                        localTime.wHour,
                        localTime.wMinute,
                        reinterpret_cast<size_t>(window));
                    auto path = std::filesystem::temp_directory_path() / buffer;
                    textureView.view.SaveAsBitmap(path.string().c_str());
#endif
                    UpdateCaptureState(commonState, state, window, textureView);
                    mouseOnMonitor = true;
                }
                else if (mouseOnMonitor)
                {
                    state.Access([&](MeasureToolState& currentState) {
                        currentState.perScreen[window].measuredEdges = {};
                    });
                    mouseOnMonitor = false;
                }

                const auto frameTime = std::chrono::duration_cast<std::chrono::milliseconds>(
                    std::chrono::high_resolution_clock::now() - now);
                if (frameTime < consts::TARGET_FRAME_DURATION)
                {
                    std::this_thread::sleep_for(consts::TARGET_FRAME_DURATION - frameTime);
                }
            }
        }

        captureSession->StopCapture();
    });
}

BoundsCaptureThread StartBoundsCapturingThread(
    DxgiAPI* dxgiAPI,
    const CommonState& commonState,
    HWND window,
    MonitorInfo monitor)
{
    auto requests = std::make_shared<BoundsCaptureRequests>();

    BoundsCaptureThread result;
    result.requestFrame = [requests](uint64_t generation) {
        requests->Request(generation);
    };
    result.thread = SpawnLoggedThread(
        L"Bounds Screen Capture thread",
        [requests, &commonState, monitor, window, dxgiAPI] {
            while (IsWindow(window) && !commonState.closeOnOtherMonitors)
            {
                uint64_t generation = 0;
                {
                    std::unique_lock lock{ requests->mutex };
                    requests->condition.wait_for(
                        lock,
                        std::chrono::milliseconds{ 100 },
                        [&] {
                            return requests->pendingGeneration.has_value() ||
                                   commonState.closeOnOtherMonitors ||
                                   !IsWindow(window);
                        });
                    if (commonState.closeOnOtherMonitors || !IsWindow(window))
                    {
                        return;
                    }
                    if (!requests->pendingGeneration)
                    {
                        continue;
                    }

                    generation = *requests->pendingGeneration;
                    requests->pendingGeneration.reset();
                }

                SetWindowDisplayAffinity(window, WDA_EXCLUDEFROMCAPTURE);
                const auto restoreCaptureAffinity = wil::scope_exit([window] {
                    if (IsWindow(window))
                    {
                        SetWindowDisplayAffinity(window, WDA_NONE);
                    }
                });

                std::shared_ptr<const OwnedBGRATextureView> frame;
                try
                {
                    auto captureSession = ScreenCaptureSession::Create(
                        dxgiAPI,
                        monitor,
                        winrt::DirectXPixelFormat::B8G8R8A8UIntNormalized,
                        false);
                    // The first WGC frame can predate the overlay's capture-exclusion change.
                    const auto textureView = captureSession->CaptureSingleFrame(true);
                    frame = std::make_shared<OwnedBGRATextureView>(textureView.view);
                }
                catch (const winrt::hresult_error& error)
                {
                    Logger::error(L"Failed to capture Screen Ruler Bounds snap frame: {}", error.message());
                }

                {
                    std::lock_guard lock{ requests->mutex };
                    if (requests->pendingGeneration && *requests->pendingGeneration > generation)
                    {
                        continue;
                    }
                }

                if (IsWindow(window))
                {
                    const BoundsSnapFrameMessage message{
                        .generation = generation,
                        .frame = std::move(frame),
                    };
                    SendMessageW(
                        window,
                        WM_BOUNDS_SNAP_FRAME_READY,
                        {},
                        reinterpret_cast<LPARAM>(&message));
                }
            }
        });
    return result;
}
