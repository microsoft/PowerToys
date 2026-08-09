#pragma once

#include "DxgiAPI.h"
#include "GuideCompositionRenderer.h"
#include "GuideModel.h"

#include <common/Display/monitors.h>

#include <atomic>
#include <condition_variable>
#include <cstdint>
#include <functional>
#include <memory>
#include <mutex>
#include <optional>
#include <thread>
#include <vector>

class GuideOverlayManager final
{
public:
    struct WindowContext
    {
        GuideOverlayManager* owner = nullptr;
        GuideModel::MonitorId monitorId = 0;
        bool inputWindow = false;
    };

    GuideOverlayManager(
        DxgiAPI* dxgiAPI,
        D2D1::ColorF lineColor,
        uint8_t pixelTolerance,
        bool perColorChannelEdgeDetection,
        std::function<void(bool)> guidePresenceChanged);
    ~GuideOverlayManager();

    void BeginPlacement(
        GuideModel::Orientation orientation,
        std::vector<HWND> captureExclusionWindows);
    void CancelInteraction();
    void ClearGuides();
    void SetEditMode(bool enabled);
    void SetToolbarBoundingBox(const Box& toolbarBounds);
    void SetToolbarWindow(HWND window);
    void SetCaptureExclusionWindows(std::vector<HWND> windows);
    void UpdateSettings(
        D2D1::ColorF lineColor,
        uint8_t pixelTolerance,
        bool perColorChannelEdgeDetection);
    void BringToFront();
    bool HasGuides() const;

private:
    struct MonitorWindows
    {
        GuideModel::Monitor monitor;
        HWND renderWindow = nullptr;
        HWND inputWindow = nullptr;
        HWND labelWindow = nullptr;
        std::unique_ptr<WindowContext> renderContext;
        std::unique_ptr<WindowContext> inputContext;
    };

    struct SnapCaptureRequest
    {
        uint64_t generation = 0;
        GuideModel::MonitorId monitorId = 0;
    };

    static LRESULT CALLBACK RenderWindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam) noexcept;
    static LRESULT CALLBACK InputWindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam) noexcept;

    void ThreadMain();
    void SnapCaptureThreadMain();
    void StopSnapCaptureThread();
    void CreateWindows();
    void DestroyWindows();
    void ShutdownOnThread();
    void HandleDisplayChange();
    MonitorWindows* FindMonitor(GuideModel::MonitorId id);
    MonitorWindows* FindMonitorAtPoint(POINT systemPoint);
    void BeginPlacementOnThread(
        GuideModel::Orientation orientation,
        std::vector<HWND> captureExclusionWindows);
    void BeginDragOnThread(GuideModel::GuideId guideId, MonitorWindows& monitor, POINT systemPoint, HWND captureWindow);
    void UpdateInteraction(POINT systemPoint);
    void ApplyInteractionState(
        MonitorWindows& monitor,
        POINT systemPoint,
        int coordinate,
        bool snapped);
    void CommitInteraction();
    void CancelInteractionOnThread();
    void UpdateHover(MonitorWindows& monitor, POINT systemPoint);
    void UpdateInputRegions();
    void RequestSnapFrame(MonitorWindows& monitor);
    void ApplySnapFrame(
        const SnapCaptureRequest& request,
        std::shared_ptr<const OwnedBGRATextureView> frame);
    void InvalidateSnapCapture();
    void ScheduleSnapUpdate(POINT systemPoint);
    void ApplyPendingSnapUpdate();
    void CancelPendingSnapUpdate();
    std::optional<int> GetSnapCandidate(
        const GuideModel::Interaction& interaction,
        POINT systemPoint) const;
    void SetInteractionCaptureExclusion(bool exclude);
    void SetCaptureExclusion(const std::vector<HWND>& windows, bool exclude);
    void ShowInputWindow(MonitorWindows& monitor, wil::unique_hrgn region);
    void BringToFrontOnThread();
    void UpdateCursor();
    void UpdateGuidePresence();

    template<typename Callback>
    bool Enqueue(Callback&& callback)
    {
        auto dispatcherQueue = _dispatcherQueue;
        if (dispatcherQueue)
        {
            return dispatcherQueue.TryEnqueue(std::forward<Callback>(callback));
        }
        return false;
    }

    DxgiAPI* _dxgiAPI = nullptr;
    D2D1::ColorF _lineColor;
    uint8_t _pixelTolerance = 30;
    bool _perColorChannelEdgeDetection = false;
    std::function<void(bool)> _guidePresenceChanged;
    Box _toolbarBounds;
    HWND _toolbarWindow = nullptr;
    bool _editMode = true;

    wil::shared_event _readyEvent{ wil::EventOptions::ManualReset };
    std::thread _thread;
    DWORD _threadId = 0;
    winrt::Windows::System::DispatcherQueueController _dispatcherQueueController{ nullptr };
    winrt::Windows::System::DispatcherQueue _dispatcherQueue{ nullptr };
    winrt::Windows::System::DispatcherQueueTimer _snapUpdateTimer{ nullptr };
    winrt::event_token _snapUpdateTimerToken{};
    winrt::Windows::UI::Composition::Compositor _compositor{ nullptr };
    std::unique_ptr<GuideCompositionRenderer> _renderer;
    std::thread _snapCaptureThread;

    GuideModel::Collection _guides;
    GuideModel::InteractionController _interaction{ _guides };
    GuideModel::MagneticSnapController _magneticSnap;
    std::vector<MonitorWindows> _monitors;
    std::optional<GuideModel::GuideId> _hoveredGuide;
    std::optional<GuideModel::MonitorId> _snapMonitorId;
    std::shared_ptr<const OwnedBGRATextureView> _snapFrame;
    std::optional<POINT> _pendingSnapPoint;
    POINT _latestInteractionPoint{};
    bool _hasInteractionPoint = false;
    bool _snapUpdateScheduled = false;
    uint64_t _snapCaptureGeneration = 0;
    std::mutex _snapCaptureMutex;
    std::condition_variable _snapCaptureCondition;
    std::optional<SnapCaptureRequest> _pendingSnapCapture;
    bool _snapCaptureStopping = false;
    std::vector<HWND> _captureExclusionWindows;
    std::vector<std::pair<HWND, DWORD>> _captureAffinities;
    std::atomic_bool _hasGuides = false;
    std::atomic_bool _startupSucceeded = false;
    bool _displayChangePending = false;
    DWORD _activePointerId = 0;
    HWND _captureWindow = nullptr;
};
