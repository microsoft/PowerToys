#include "pch.h"

#include "EdgeDetection.h"
#include "GuideOverlayUI.h"
#include "ScreenCaptureSession.h"

#include <common/logger/logger.h>
#include <common/utils/window.h>

#include <algorithm>

namespace
{
    constexpr wchar_t RenderWindowClassName[] = L"PowerToys.ScreenRuler.GuideRender";
    constexpr wchar_t InputWindowClassName[] = L"PowerToys.ScreenRuler.GuideInput";
    constexpr wchar_t LabelWindowClassName[] = L"PowerToys.ScreenRuler.GuideLabel";
    constexpr int MouseHitRadius = 6;
    constexpr int TouchHitRadius = 14;
    constexpr int DismissalThreshold = 18;
    constexpr auto SnapUpdateInterval = std::chrono::milliseconds{ 8 };

    GuideOverlayManager::WindowContext* GetContext(HWND window)
    {
        return reinterpret_cast<GuideOverlayManager::WindowContext*>(GetWindowLongPtrW(window, GWLP_USERDATA));
    }

}

GuideOverlayManager::GuideOverlayManager(
    DxgiAPI* dxgiAPI,
    D2D1::ColorF lineColor,
    uint8_t pixelTolerance,
    bool perColorChannelEdgeDetection,
    std::function<void(bool)> guidePresenceChanged) :
    _dxgiAPI{ dxgiAPI },
    _lineColor{ lineColor },
    _pixelTolerance{ pixelTolerance },
    _perColorChannelEdgeDetection{ perColorChannelEdgeDetection },
    _guidePresenceChanged{ std::move(guidePresenceChanged) }
{
    _thread = SpawnLoggedThread(L"Screen Ruler guide overlay", [this] {
        ThreadMain();
    });
    _readyEvent.wait();
    if (!_startupSucceeded.load())
    {
        if (_thread.joinable())
        {
            _thread.join();
        }
        winrt::throw_hresult(E_FAIL);
    }

    try
    {
        _snapCaptureThread = SpawnLoggedThread(L"Screen Ruler guide snap capture", [this] {
            SnapCaptureThreadMain();
        });
    }
    catch (...)
    {
        if (_dispatcherQueue)
        {
            if (!Enqueue([this] {
                    ShutdownOnThread();
                }))
            {
                PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
            }
        }
        else if (_threadId)
        {
            PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
        }
        if (_thread.joinable())
        {
            _thread.join();
        }
        throw;
    }
}

GuideOverlayManager::~GuideOverlayManager()
{
    StopSnapCaptureThread();

    if (_dispatcherQueue)
    {
        if (!Enqueue([this] {
                ShutdownOnThread();
            }))
        {
            PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
        }
    }
    else if (_threadId)
    {
        PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
    }

    if (_thread.joinable())
    {
        _thread.join();
    }
}

void GuideOverlayManager::BeginPlacement(
    GuideModel::Orientation orientation,
    std::vector<HWND> captureExclusionWindows)
{
    Enqueue([this, orientation, windows = std::move(captureExclusionWindows)]() mutable {
        BeginPlacementOnThread(orientation, std::move(windows));
    });
}

void GuideOverlayManager::ClearGuides()
{
    wil::shared_event completion{ wil::EventOptions::ManualReset };
    if (Enqueue([this, completion]() mutable {
            const auto signalCompletion = wil::scope_exit([&completion] {
                completion.SetEvent();
            });
            CancelInteractionOnThread();
            _guides.Clear();
            UpdateGuidePresence();
            _renderer->ClearGuides(true);
            UpdateInputRegions();
        }))
    {
        completion.wait();
    }
}

void GuideOverlayManager::CancelInteraction()
{
    Enqueue([this] {
        CancelInteractionOnThread();
    });
}

void GuideOverlayManager::SetEditMode(bool enabled)
{
    wil::shared_event completion{ wil::EventOptions::ManualReset };
    if (Enqueue([this, enabled, completion]() mutable {
            const auto signalCompletion = wil::scope_exit([&completion] {
                completion.SetEvent();
            });
            if (_editMode == enabled)
            {
                return;
            }

            CancelInteractionOnThread();
            _editMode = enabled;
            _renderer->SetEditMode(enabled);
            UpdateInputRegions();
        }))
    {
        completion.wait();
    }
}

void GuideOverlayManager::SetToolbarBoundingBox(const Box& toolbarBounds)
{
    Enqueue([this, toolbarBounds] {
        _toolbarBounds = toolbarBounds;
        _renderer->SetToolbarBoundingBox(RECT{
            .left = toolbarBounds.left(),
            .top = toolbarBounds.top(),
            .right = toolbarBounds.right(),
            .bottom = toolbarBounds.bottom(),
        });
        UpdateInputRegions();
    });
}

void GuideOverlayManager::SetCaptureExclusionWindows(std::vector<HWND> windows)
{
    Enqueue([this, windows = std::move(windows)]() mutable {
        _captureExclusionWindows = std::move(windows);
    });
}

void GuideOverlayManager::SetToolbarWindow(HWND window)
{
    Enqueue([this, window] {
        _toolbarWindow = window;
        BringToFrontOnThread();
    });
}

void GuideOverlayManager::UpdateSettings(
    D2D1::ColorF lineColor,
    uint8_t pixelTolerance,
    bool perColorChannelEdgeDetection)
{
    Enqueue([this, lineColor, pixelTolerance, perColorChannelEdgeDetection] {
        _lineColor = lineColor;
        _pixelTolerance = pixelTolerance;
        _perColorChannelEdgeDetection = perColorChannelEdgeDetection;
        _renderer->SetLineColor(lineColor);
    });
}

void GuideOverlayManager::BringToFront()
{
    Enqueue([this] {
        BringToFrontOnThread();
    });
}

bool GuideOverlayManager::HasGuides() const
{
    return _hasGuides.load();
}

void GuideOverlayManager::UpdateGuidePresence()
{
    const bool hasGuides = !_guides.Empty();
    if (_hasGuides.exchange(hasGuides) != hasGuides && _guidePresenceChanged)
    {
        _guidePresenceChanged(hasGuides);
    }
}

void GuideOverlayManager::ThreadMain()
{
    _threadId = GetCurrentThreadId();
    winrt::init_apartment(winrt::apartment_type::single_threaded);

    try
    {
        DispatcherQueueOptions options{
            .dwSize = sizeof(DispatcherQueueOptions),
            .threadType = DQTYPE_THREAD_CURRENT,
            .apartmentType = DQTAT_COM_STA,
        };
        winrt::check_hresult(CreateDispatcherQueueController(
            options,
            reinterpret_cast<ABI::Windows::System::IDispatcherQueueController**>(
                winrt::put_abi(_dispatcherQueueController))));
        _dispatcherQueue = _dispatcherQueueController.DispatcherQueue();
        _snapUpdateTimer = _dispatcherQueue.CreateTimer();
        _snapUpdateTimer.Interval(SnapUpdateInterval);
        _snapUpdateTimer.IsRepeating(false);
        _snapUpdateTimerToken = _snapUpdateTimer.Tick([this](auto&&, auto&&) {
            ApplyPendingSnapUpdate();
        });
        _compositor = winrt::Windows::UI::Composition::Compositor{};
        _renderer = std::make_unique<GuideCompositionRenderer>(
            _dxgiAPI,
            _compositor,
            _dispatcherQueue,
            _lineColor);
        CreateWindows();
        _startupSucceeded = true;
    }
    catch (...)
    {
        _dispatcherQueue = nullptr;
        _dispatcherQueueController = nullptr;
        _compositor = nullptr;
        _readyEvent.SetEvent();
        return;
    }

    _readyEvent.SetEvent();

    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }

    if (_renderer)
    {
        ShutdownOnThread();
    }
    if (_snapUpdateTimer)
    {
        _snapUpdateTimer.Stop();
        _snapUpdateTimer.Tick(_snapUpdateTimerToken);
        _snapUpdateTimer = nullptr;
    }
    _dispatcherQueue = nullptr;
    _dispatcherQueueController = nullptr;
}

void GuideOverlayManager::SnapCaptureThreadMain()
{
    winrt::init_apartment(winrt::apartment_type::multi_threaded);

    while (true)
    {
        SnapCaptureRequest request;
        {
            std::unique_lock lock{ _snapCaptureMutex };
            _snapCaptureCondition.wait(lock, [this] {
                return _snapCaptureStopping || _pendingSnapCapture.has_value();
            });
            if (_snapCaptureStopping)
            {
                return;
            }

            request = *_pendingSnapCapture;
            _pendingSnapCapture.reset();
        }

        std::shared_ptr<const OwnedBGRATextureView> frame;
        try
        {
            auto captureSession = ScreenCaptureSession::Create(
                _dxgiAPI,
                MonitorInfo{ reinterpret_cast<HMONITOR>(request.monitorId) },
                winrt::DirectXPixelFormat::B8G8R8A8UIntNormalized,
                false);
            // The first WGC frame can predate recently composed content or affinity changes.
            auto mappedFrame = captureSession->CaptureSingleFrame(true);
            frame = std::make_shared<OwnedBGRATextureView>(mappedFrame.view);
        }
        catch (const winrt::hresult_error& error)
        {
            Logger::error(L"Failed to capture Screen Ruler guide snap frame: {}", error.message());
        }

        {
            std::lock_guard lock{ _snapCaptureMutex };
            if (_snapCaptureStopping)
            {
                return;
            }
            if (_pendingSnapCapture && _pendingSnapCapture->generation > request.generation)
            {
                continue;
            }
        }

        Enqueue([this, request, frame = std::move(frame)] {
            ApplySnapFrame(request, std::move(frame));
        });
    }
}

void GuideOverlayManager::StopSnapCaptureThread()
{
    {
        std::lock_guard lock{ _snapCaptureMutex };
        _snapCaptureStopping = true;
        _pendingSnapCapture.reset();
    }
    _snapCaptureCondition.notify_one();
    if (_snapCaptureThread.joinable())
    {
        _snapCaptureThread.join();
    }
}

void GuideOverlayManager::CreateWindows()
{
    WNDCLASSEXW renderClass{
        .cbSize = sizeof(WNDCLASSEXW),
        .lpfnWndProc = RenderWindowProc,
        .hInstance = GetModuleHandleW(nullptr),
        .lpszClassName = RenderWindowClassName,
    };
    RegisterClassExW(&renderClass);

    WNDCLASSEXW labelClass = renderClass;
    labelClass.lpszClassName = LabelWindowClassName;
    RegisterClassExW(&labelClass);

    WNDCLASSEXW inputClass{
        .cbSize = sizeof(WNDCLASSEXW),
        .lpfnWndProc = InputWindowProc,
        .hInstance = GetModuleHandleW(nullptr),
        .hCursor = LoadCursorW(nullptr, IDC_ARROW),
        .lpszClassName = InputWindowClassName,
    };
    RegisterClassExW(&inputClass);

    const auto monitors = MonitorInfo::GetMonitors(true);
    _monitors.reserve(monitors.size());
    for (const auto& monitorInfo : monitors)
    {
        MonitorWindows monitor;
        monitor.monitor = GuideModel::Monitor{
            .id = reinterpret_cast<GuideModel::MonitorId>(monitorInfo.GetHandle()),
            .bounds = monitorInfo.GetScreenSize(true).rect,
        };
        monitor.renderContext = std::make_unique<WindowContext>(WindowContext{
            .owner = this,
            .monitorId = monitor.monitor.id,
            .inputWindow = false,
        });
        monitor.inputContext = std::make_unique<WindowContext>(WindowContext{
            .owner = this,
            .monitorId = monitor.monitor.id,
            .inputWindow = true,
        });

        const DWORD renderStyles = WS_EX_LAYERED | WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE |
                                   WS_EX_TRANSPARENT | WS_EX_TOPMOST;
        monitor.renderWindow = CreateWindowExW(
            renderStyles,
            RenderWindowClassName,
            L"PowerToys Screen Ruler guides",
            WS_POPUP,
            monitor.monitor.bounds.left,
            monitor.monitor.bounds.top,
            monitor.monitor.bounds.right - monitor.monitor.bounds.left,
            monitor.monitor.bounds.bottom - monitor.monitor.bounds.top,
            nullptr,
            nullptr,
            GetModuleHandleW(nullptr),
            monitor.renderContext.get());
        winrt::check_bool(monitor.renderWindow);

        const DWORD inputStyles = WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
        monitor.inputWindow = CreateWindowExW(
            inputStyles,
            InputWindowClassName,
            L"PowerToys Screen Ruler guide input",
            WS_POPUP,
            monitor.monitor.bounds.left,
            monitor.monitor.bounds.top,
            monitor.monitor.bounds.right - monitor.monitor.bounds.left,
            monitor.monitor.bounds.bottom - monitor.monitor.bounds.top,
            nullptr,
            nullptr,
            GetModuleHandleW(nullptr),
            monitor.inputContext.get());
        winrt::check_bool(monitor.inputWindow);

        const DWORD labelStyles = WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOPMOST;
        monitor.labelWindow = CreateWindowExW(
            labelStyles,
            LabelWindowClassName,
            L"PowerToys Screen Ruler guide label",
            WS_POPUP,
            monitor.monitor.bounds.left,
            monitor.monitor.bounds.top,
            1,
            1,
            nullptr,
            nullptr,
            GetModuleHandleW(nullptr),
            nullptr);
        winrt::check_bool(monitor.labelWindow);

        BOOL excludedFromPeek = TRUE;
        DwmSetWindowAttribute(
            monitor.renderWindow,
            DWMWA_EXCLUDED_FROM_PEEK,
            &excludedFromPeek,
            sizeof(excludedFromPeek));
        DwmSetWindowAttribute(
            monitor.inputWindow,
            DWMWA_EXCLUDED_FROM_PEEK,
            &excludedFromPeek,
            sizeof(excludedFromPeek));
        DwmSetWindowAttribute(
            monitor.labelWindow,
            DWMWA_EXCLUDED_FROM_PEEK,
            &excludedFromPeek,
            sizeof(excludedFromPeek));
        SetWindowDisplayAffinity(monitor.renderWindow, WDA_EXCLUDEFROMCAPTURE);
        SetWindowDisplayAffinity(monitor.labelWindow, WDA_EXCLUDEFROMCAPTURE);

        ShowWindow(monitor.renderWindow, SW_SHOWNOACTIVATE);
        SetWindowPos(
            monitor.renderWindow,
            HWND_TOPMOST,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        _renderer->AddMonitor(monitor.monitor, monitor.renderWindow, monitor.labelWindow);
        _monitors.push_back(std::move(monitor));
    }

    UpdateInputRegions();
}

void GuideOverlayManager::DestroyWindows()
{
    _renderer->RemoveAllMonitors();
    for (auto& monitor : _monitors)
    {
        if (monitor.inputWindow)
        {
            DestroyWindow(monitor.inputWindow);
        }
        if (monitor.labelWindow)
        {
            DestroyWindow(monitor.labelWindow);
        }
        if (monitor.renderWindow)
        {
            DestroyWindow(monitor.renderWindow);
        }
    }
    _monitors.clear();
}

void GuideOverlayManager::ShutdownOnThread()
{
    CancelInteractionOnThread();
    if (_snapUpdateTimer)
    {
        _snapUpdateTimer.Stop();
        _snapUpdateTimer.Tick(_snapUpdateTimerToken);
        _snapUpdateTimer = nullptr;
    }
    DestroyWindows();
    _renderer.reset();
    _compositor = nullptr;
    PostQuitMessage(0);
}

void GuideOverlayManager::HandleDisplayChange()
{
    _displayChangePending = false;
    CancelInteractionOnThread();

    struct GuideLocation
    {
        GuideModel::Guide guide;
        POINT systemPoint;
    };

    std::vector<GuideLocation> guideLocations;
    guideLocations.reserve(_guides.Guides().size());
    for (const auto& guide : _guides.Guides())
    {
        if (const auto* monitor = FindMonitor(guide.monitorId))
        {
            guideLocations.push_back(GuideLocation{
                .guide = guide,
                .systemPoint = guide.orientation == GuideModel::Orientation::Horizontal ?
                                   POINT{
                                       (monitor->monitor.bounds.left + monitor->monitor.bounds.right) / 2,
                                       monitor->monitor.bounds.top + guide.coordinate,
                                   } :
                                   POINT{
                                       monitor->monitor.bounds.left + guide.coordinate,
                                       (monitor->monitor.bounds.top + monitor->monitor.bounds.bottom) / 2,
                                   },
            });
        }
    }

    DestroyWindows();
    CreateWindows();

    const auto guides = _guides.Guides();
    for (const auto& guide : guides)
    {
        const auto location = std::ranges::find_if(guideLocations, [id = guide.id](const auto& item) {
            return item.guide.id == id;
        });
        if (location == guideLocations.end())
        {
            _guides.Remove(guide.id);
            continue;
        }

        auto* monitor = FindMonitor(guide.monitorId);
        if (!monitor)
        {
            monitor = FindMonitorAtPoint(location->systemPoint);
        }
        if (!monitor)
        {
            _guides.Remove(guide.id);
            continue;
        }

        const int coordinate = monitor->monitor.id == guide.monitorId ?
                                   guide.coordinate :
                                   GuideModel::ToMonitorCoordinate(
                                       guide.orientation,
                                       monitor->monitor.bounds,
                                       location->systemPoint);
        _guides.Move(guide.id, monitor->monitor, coordinate);
    }

    UpdateGuidePresence();
    _renderer->SyncGuides(_guides.Guides(), false);
    UpdateInputRegions();
}

GuideOverlayManager::MonitorWindows* GuideOverlayManager::FindMonitor(GuideModel::MonitorId id)
{
    const auto iterator = std::find_if(_monitors.begin(), _monitors.end(), [id](const auto& monitor) {
        return monitor.monitor.id == id;
    });
    return iterator == _monitors.end() ? nullptr : &*iterator;
}

GuideOverlayManager::MonitorWindows* GuideOverlayManager::FindMonitorAtPoint(POINT systemPoint)
{
    const auto handle = MonitorFromPoint(systemPoint, MONITOR_DEFAULTTONEAREST);
    return FindMonitor(reinterpret_cast<GuideModel::MonitorId>(handle));
}

void GuideOverlayManager::BeginPlacementOnThread(
    GuideModel::Orientation orientation,
    std::vector<HWND> captureExclusionWindows)
{
    CancelInteractionOnThread();
    if (!captureExclusionWindows.empty())
    {
        _captureExclusionWindows = std::move(captureExclusionWindows);
    }

    POINT cursorPosition{};
    GetCursorPos(&cursorPosition);
    auto* monitor = FindMonitorAtPoint(cursorPosition);
    if (!monitor)
    {
        return;
    }

    const int coordinate = GuideModel::ToMonitorCoordinate(orientation, monitor->monitor.bounds, cursorPosition);
    _interaction.BeginPlacement(orientation, monitor->monitor, coordinate);
    _hasInteractionPoint = false;
    SetInteractionCaptureExclusion(true);
    RequestSnapFrame(*monitor);
    if (!_toolbarBounds.inside(cursorPosition))
    {
        UpdateInteraction(cursorPosition);
    }
    UpdateInputRegions();
}

void GuideOverlayManager::BeginDragOnThread(
    GuideModel::GuideId guideId,
    MonitorWindows& monitor,
    POINT systemPoint,
    HWND captureWindow)
{
    if (_interaction.Active())
    {
        return;
    }

    if (!_interaction.BeginDrag(guideId, monitor.monitor))
    {
        return;
    }

    _captureWindow = captureWindow;
    _hasInteractionPoint = false;
    SetCapture(captureWindow);
    SetInteractionCaptureExclusion(true);
    RequestSnapFrame(monitor);
    UpdateInteraction(systemPoint);
    UpdateInputRegions();
}

void GuideOverlayManager::UpdateInteraction(POINT systemPoint)
{
    if (!_interaction.Active())
    {
        return;
    }

    if (_interaction.Current().kind == GuideModel::InteractionKind::Placement &&
        !_hasInteractionPoint &&
        _toolbarBounds.inside(systemPoint))
    {
        return;
    }

    auto* monitor = FindMonitorAtPoint(systemPoint);
    if (!monitor)
    {
        return;
    }

    _latestInteractionPoint = systemPoint;
    _hasInteractionPoint = true;
    if (_interaction.Current().monitor.id != monitor->monitor.id)
    {
        RequestSnapFrame(*monitor);
    }

    const auto orientation = _interaction.Current().orientation;
    const int rawCoordinate = GuideModel::ToMonitorCoordinate(orientation, monitor->monitor.bounds, systemPoint);
    std::optional<int> snappedCoordinate;
    const bool bypassSnap = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
    if (bypassSnap)
    {
        CancelPendingSnapUpdate();
        _magneticSnap.Reset();
    }
    else if (_snapFrame && _snapMonitorId && *_snapMonitorId == monitor->monitor.id)
    {
        snappedCoordinate = _magneticSnap.Track(rawCoordinate, false);
        ScheduleSnapUpdate(systemPoint);
    }
    else
    {
        _magneticSnap.Reset();
    }

    const int coordinate = snappedCoordinate.value_or(rawCoordinate);
    ApplyInteractionState(*monitor, systemPoint, coordinate, snappedCoordinate.has_value());
}

void GuideOverlayManager::ApplyInteractionState(
    MonitorWindows& monitor,
    POINT systemPoint,
    int coordinate,
    bool snapped)
{
    const auto orientation = _interaction.Current().orientation;
    const auto monitors = [&] {
        std::vector<GuideModel::Monitor> result;
        result.reserve(_monitors.size());
        for (const auto& item : _monitors)
        {
            result.push_back(item.monitor);
        }
        return result;
    }();
    const auto dismissalEdge = GuideModel::GetDismissalEdge(
        orientation,
        systemPoint,
        monitor.monitor,
        monitors,
        DismissalThreshold);

    _interaction.Update(monitor.monitor, coordinate, dismissalEdge != GuideModel::DismissalEdge::None);
    _renderer->SetInteraction(GuideCompositionRenderer::InteractionVisualState{
        .interaction = _interaction.Current(),
        .systemPointer = systemPoint,
        .snapped = snapped,
        .dismissalEdge = dismissalEdge,
    });
    UpdateCursor();
}

void GuideOverlayManager::CommitInteraction()
{
    if (!_interaction.Active())
    {
        return;
    }

    if (_interaction.Current().kind == GuideModel::InteractionKind::Placement && !_hasInteractionPoint)
    {
        return;
    }

    if (_pendingSnapPoint)
    {
        if (_snapUpdateTimer && _snapUpdateScheduled)
        {
            _snapUpdateTimer.Stop();
        }
        _snapUpdateScheduled = false;
        ApplyPendingSnapUpdate();
    }

    _interaction.Commit();
    if (_captureWindow)
    {
        _captureWindow = nullptr;
        ReleaseCapture();
    }
    _activePointerId = 0;
    InvalidateSnapCapture();
    SetInteractionCaptureExclusion(false);
    UpdateGuidePresence();
    _renderer->SyncGuides(_guides.Guides(), true);
    _renderer->SetInteraction(std::nullopt);
    UpdateInputRegions();
}

void GuideOverlayManager::CancelInteractionOnThread()
{
    if (!_interaction.Active())
    {
        return;
    }

    _interaction.Cancel();
    if (_captureWindow)
    {
        _captureWindow = nullptr;
        ReleaseCapture();
    }
    if (_activePointerId)
    {
        _activePointerId = 0;
    }
    InvalidateSnapCapture();
    SetInteractionCaptureExclusion(false);
    _renderer->SyncGuides(_guides.Guides(), false);
    _renderer->SetInteraction(std::nullopt);
    UpdateInputRegions();
}

void GuideOverlayManager::UpdateHover(MonitorWindows& monitor, POINT systemPoint)
{
    if (_interaction.Active())
    {
        UpdateInteraction(systemPoint);
        return;
    }

    const auto hit = _guides.HitTest(systemPoint, monitor.monitor, MouseHitRadius);
    const auto guideId = hit ? std::optional<GuideModel::GuideId>{ hit->id } : std::nullopt;
    if (_hoveredGuide != guideId)
    {
        _hoveredGuide = guideId;
        _renderer->SetHoveredGuide(_hoveredGuide);
        UpdateCursor();
    }
}

void GuideOverlayManager::UpdateInputRegions()
{
    for (auto& monitor : _monitors)
    {
        if (!_editMode)
        {
            ShowWindow(monitor.inputWindow, SW_HIDE);
            continue;
        }

        wil::unique_hrgn region{ CreateRectRgn(0, 0, 0, 0) };
        bool hasRegion = false;
        const int width = monitor.monitor.bounds.right - monitor.monitor.bounds.left;
        const int height = monitor.monitor.bounds.bottom - monitor.monitor.bounds.top;

        if (_interaction.Active())
        {
            region.reset(CreateRectRgn(0, 0, width, height));
            hasRegion = true;
        }
        else
        {
            for (const auto& guide : _guides.Guides())
            {
                if (guide.monitorId != monitor.monitor.id)
                {
                    continue;
                }

                wil::unique_hrgn guideRegion;
                if (guide.orientation == GuideModel::Orientation::Horizontal)
                {
                    guideRegion.reset(CreateRectRgn(
                        0,
                        std::max(0, guide.coordinate - MouseHitRadius),
                        width,
                        std::min(height, guide.coordinate + MouseHitRadius + 1)));
                }
                else
                {
                    guideRegion.reset(CreateRectRgn(
                        std::max(0, guide.coordinate - MouseHitRadius),
                        0,
                        std::min(width, guide.coordinate + MouseHitRadius + 1),
                        height));
                }
                CombineRgn(region.get(), region.get(), guideRegion.get(), RGN_OR);
                hasRegion = true;
            }
        }

        if (hasRegion && _toolbarBounds.width() > 0 && _toolbarBounds.height() > 0)
        {
            RECT localToolbar{
                .left = _toolbarBounds.left() - monitor.monitor.bounds.left,
                .top = _toolbarBounds.top() - monitor.monitor.bounds.top,
                .right = _toolbarBounds.right() - monitor.monitor.bounds.left,
                .bottom = _toolbarBounds.bottom() - monitor.monitor.bounds.top,
            };
            wil::unique_hrgn toolbarRegion{ CreateRectRgn(
                localToolbar.left,
                localToolbar.top,
                localToolbar.right,
                localToolbar.bottom) };
            CombineRgn(region.get(), region.get(), toolbarRegion.get(), RGN_DIFF);
        }

        if (hasRegion)
        {
            ShowInputWindow(monitor, std::move(region));
        }
        else
        {
            ShowWindow(monitor.inputWindow, SW_HIDE);
        }
    }

    BringToFrontOnThread();
}

void GuideOverlayManager::RequestSnapFrame(MonitorWindows& monitor)
{
    CancelPendingSnapUpdate();
    _magneticSnap.Reset();
    _snapFrame.reset();
    _snapMonitorId.reset();

    const SnapCaptureRequest request{
        .generation = ++_snapCaptureGeneration,
        .monitorId = monitor.monitor.id,
    };
    {
        std::lock_guard lock{ _snapCaptureMutex };
        if (_snapCaptureStopping)
        {
            return;
        }
        _pendingSnapCapture = request;
    }
    _snapCaptureCondition.notify_one();
}

void GuideOverlayManager::ApplySnapFrame(
    const SnapCaptureRequest& request,
    std::shared_ptr<const OwnedBGRATextureView> frame)
{
    if (request.generation != _snapCaptureGeneration ||
        !_interaction.Active() ||
        _interaction.Current().monitor.id != request.monitorId)
    {
        return;
    }

    _snapFrame = std::move(frame);
    if (!_snapFrame)
    {
        return;
    }

    _snapMonitorId = request.monitorId;
    if (_hasInteractionPoint)
    {
        ScheduleSnapUpdate(_latestInteractionPoint);
    }
}

void GuideOverlayManager::InvalidateSnapCapture()
{
    ++_snapCaptureGeneration;
    {
        std::lock_guard lock{ _snapCaptureMutex };
        _pendingSnapCapture.reset();
    }
    CancelPendingSnapUpdate();
    _magneticSnap.Reset();
    _snapFrame.reset();
    _snapMonitorId.reset();
    _hasInteractionPoint = false;
}

void GuideOverlayManager::ScheduleSnapUpdate(POINT systemPoint)
{
    if (!_hasInteractionPoint)
    {
        return;
    }

    _pendingSnapPoint = systemPoint;
    if (_snapUpdateScheduled || !_snapUpdateTimer)
    {
        return;
    }

    _snapUpdateScheduled = true;
    _snapUpdateTimer.Start();
}

void GuideOverlayManager::ApplyPendingSnapUpdate()
{
    _snapUpdateScheduled = false;
    if (!_pendingSnapPoint || !_interaction.Active() || !_hasInteractionPoint)
    {
        _pendingSnapPoint.reset();
        return;
    }

    const POINT systemPoint = *_pendingSnapPoint;
    _pendingSnapPoint.reset();
    if (GetAsyncKeyState(VK_MENU) & 0x8000)
    {
        _magneticSnap.Reset();
        UpdateInteraction(systemPoint);
        return;
    }

    auto* monitor = FindMonitorAtPoint(systemPoint);
    if (!monitor ||
        monitor->monitor.id != _interaction.Current().monitor.id ||
        !_snapMonitorId ||
        *_snapMonitorId != monitor->monitor.id)
    {
        _magneticSnap.Reset();
        return;
    }

    const auto orientation = _interaction.Current().orientation;
    const int rawCoordinate = GuideModel::ToMonitorCoordinate(
        orientation,
        monitor->monitor.bounds,
        systemPoint);
    const auto snapCandidate = GetSnapCandidate(
        GuideModel::Interaction{
            .kind = _interaction.Current().kind,
            .orientation = orientation,
            .guideId = _interaction.Current().guideId,
            .monitor = monitor->monitor,
            .coordinate = rawCoordinate,
        },
        systemPoint);
    const auto snappedCoordinate = snapCandidate ?
                                       _magneticSnap.UpdateCandidate(rawCoordinate, *snapCandidate, false) :
                                       _magneticSnap.Track(rawCoordinate, false);
    ApplyInteractionState(
        *monitor,
        systemPoint,
        snappedCoordinate.value_or(rawCoordinate),
        snappedCoordinate.has_value());
}

void GuideOverlayManager::CancelPendingSnapUpdate()
{
    if (_snapUpdateTimer && _snapUpdateScheduled)
    {
        _snapUpdateTimer.Stop();
    }
    _snapUpdateScheduled = false;
    _pendingSnapPoint.reset();
}

std::optional<int> GuideOverlayManager::GetSnapCandidate(
    const GuideModel::Interaction& interaction,
    POINT systemPoint) const
{
    if (!_snapFrame || !_snapMonitorId || *_snapMonitorId != interaction.monitor.id)
    {
        return std::nullopt;
    }

    const POINT monitorPoint{
        systemPoint.x - interaction.monitor.bounds.left,
        systemPoint.y - interaction.monitor.bounds.top,
    };
    const RECT detectedEdges = DetectEdges(
        _snapFrame->view,
        monitorPoint,
        _perColorChannelEdgeDetection,
        _pixelTolerance);
    return GuideModel::ClampCoordinate(
        interaction.orientation,
        interaction.monitor.bounds,
        GuideModel::NearestAxisEdge(interaction.orientation, detectedEdges, interaction.coordinate));
}

void GuideOverlayManager::SetInteractionCaptureExclusion(bool exclude)
{
    if (!exclude)
    {
        SetCaptureExclusion(_captureExclusionWindows, false);
        return;
    }

    if (!_captureAffinities.empty())
    {
        return;
    }

    auto windows = _captureExclusionWindows;
    if (IsWindow(_toolbarWindow) &&
        std::ranges::find(windows, _toolbarWindow) == windows.end())
    {
        windows.push_back(_toolbarWindow);
    }
    SetCaptureExclusion(windows, true);
}

void GuideOverlayManager::SetCaptureExclusion(const std::vector<HWND>& windows, bool exclude)
{
    if (exclude)
    {
        _captureAffinities.clear();
        for (const auto window : windows)
        {
            DWORD affinity = WDA_NONE;
            if (IsWindow(window) && GetWindowDisplayAffinity(window, &affinity))
            {
                _captureAffinities.emplace_back(window, affinity);
                SetWindowDisplayAffinity(window, WDA_EXCLUDEFROMCAPTURE);
            }
        }
        return;
    }

    for (const auto& [window, affinity] : _captureAffinities)
    {
        if (IsWindow(window))
        {
            SetWindowDisplayAffinity(window, affinity);
        }
    }
    _captureAffinities.clear();
}

void GuideOverlayManager::ShowInputWindow(MonitorWindows& monitor, wil::unique_hrgn region)
{
    if (SetWindowRgn(monitor.inputWindow, region.get(), FALSE))
    {
        region.release();
    }
    ShowWindow(monitor.inputWindow, SW_SHOWNOACTIVATE);
    SetWindowPos(
        monitor.inputWindow,
        IsWindow(_toolbarWindow) ? _toolbarWindow : HWND_TOPMOST,
        0,
        0,
        0,
        0,
        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
}

void GuideOverlayManager::BringToFrontOnThread()
{
    for (auto& monitor : _monitors)
    {
        SetWindowPos(
            monitor.renderWindow,
            HWND_TOPMOST,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        if (IsWindowVisible(monitor.inputWindow))
        {
            SetWindowPos(
                monitor.inputWindow,
                HWND_TOPMOST,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        if (IsWindowVisible(monitor.labelWindow))
        {
            SetWindowPos(
                monitor.labelWindow,
                HWND_TOPMOST,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    if (IsWindow(_toolbarWindow))
    {
        SetWindowPos(
            _toolbarWindow,
            HWND_TOPMOST,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
    }
}

void GuideOverlayManager::UpdateCursor()
{
    GuideModel::Orientation orientation = GuideModel::Orientation::Horizontal;
    bool hasOrientation = false;
    if (_interaction.Active())
    {
        orientation = _interaction.Current().orientation;
        hasOrientation = true;
    }
    else if (_hoveredGuide)
    {
        if (const auto* guide = _guides.Find(*_hoveredGuide))
        {
            orientation = guide->orientation;
            hasOrientation = true;
        }
    }

    SetCursor(LoadCursorW(
        nullptr,
        hasOrientation && orientation == GuideModel::Orientation::Horizontal ? IDC_SIZENS :
        hasOrientation                                                       ? IDC_SIZEWE :
                                                                               IDC_ARROW));
}

LRESULT CALLBACK GuideOverlayManager::RenderWindowProc(
    HWND window,
    UINT message,
    WPARAM wParam,
    LPARAM lParam) noexcept
{
    if (message == WM_NCCREATE)
    {
        const auto create = reinterpret_cast<CREATESTRUCTW*>(lParam);
        SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(create->lpCreateParams));
    }

    switch (message)
    {
    case WM_NCHITTEST:
        return HTTRANSPARENT;
    case WM_ERASEBKGND:
        return 1;
    case WM_DISPLAYCHANGE:
        if (const auto context = GetContext(window))
        {
            if (!context->owner->_displayChangePending)
            {
                context->owner->_displayChangePending = true;
                context->owner->Enqueue([owner = context->owner] {
                    owner->HandleDisplayChange();
                });
            }
        }
        return 0;
    }
    return DefWindowProcW(window, message, wParam, lParam);
}

LRESULT CALLBACK GuideOverlayManager::InputWindowProc(
    HWND window,
    UINT message,
    WPARAM wParam,
    LPARAM lParam) noexcept
{
    if (message == WM_NCCREATE)
    {
        const auto create = reinterpret_cast<CREATESTRUCTW*>(lParam);
        SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(create->lpCreateParams));
    }

    auto* context = GetContext(window);
    auto* owner = context ? context->owner : nullptr;
    auto* monitor = owner && context ? owner->FindMonitor(context->monitorId) : nullptr;
    if (!owner || !monitor)
    {
        return DefWindowProcW(window, message, wParam, lParam);
    }

    switch (message)
    {
    case WM_NCHITTEST:
        return HTCLIENT;

    case WM_SETCURSOR:
        owner->UpdateCursor();
        return TRUE;

    case WM_MOUSEMOVE:
    {
        POINT point{ GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        ClientToScreen(window, &point);
        TRACKMOUSEEVENT track{ .cbSize = sizeof(TRACKMOUSEEVENT), .dwFlags = TME_LEAVE, .hwndTrack = window };
        TrackMouseEvent(&track);
        owner->UpdateHover(*monitor, point);
        return 0;
    }

    case WM_MOUSELEAVE:
        if (!owner->_interaction.Active())
        {
            owner->_hoveredGuide.reset();
            owner->_renderer->SetHoveredGuide(std::nullopt);
            owner->UpdateCursor();
        }
        return 0;

    case WM_LBUTTONDOWN:
    {
        POINT point{ GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        ClientToScreen(window, &point);
        if (owner->_interaction.Current().kind == GuideModel::InteractionKind::Placement)
        {
            owner->UpdateInteraction(point);
            owner->CommitInteraction();
            return 0;
        }

        const auto hit = owner->_guides.HitTest(point, monitor->monitor, MouseHitRadius);
        if (hit)
        {
            owner->BeginDragOnThread(hit->id, *monitor, point, window);
        }
        return 0;
    }

    case WM_LBUTTONUP:
    {
        if (owner->_interaction.Current().kind == GuideModel::InteractionKind::Drag)
        {
            POINT point{ GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
            ClientToScreen(window, &point);
            owner->UpdateInteraction(point);
            owner->CommitInteraction();
        }
        return 0;
    }

    case WM_RBUTTONUP:
        owner->CancelInteractionOnThread();
        return 0;

    case WM_CAPTURECHANGED:
        if (owner->_interaction.Current().kind == GuideModel::InteractionKind::Drag && owner->_captureWindow)
        {
            owner->_captureWindow = nullptr;
            owner->CancelInteractionOnThread();
        }
        return 0;

    case WM_POINTERCAPTURECHANGED:
        if (owner->_activePointerId != 0 &&
            owner->_activePointerId == GET_POINTERID_WPARAM(wParam) &&
            owner->_interaction.Current().kind == GuideModel::InteractionKind::Drag)
        {
            owner->_activePointerId = 0;
            owner->CancelInteractionOnThread();
        }
        return 0;

    case WM_POINTERDOWN:
    case WM_POINTERUPDATE:
    case WM_POINTERUP:
    {
        const auto pointerId = GET_POINTERID_WPARAM(wParam);
        POINTER_INPUT_TYPE pointerType{};
        if (!GetPointerType(pointerId, &pointerType) || pointerType == PT_MOUSE)
        {
            break;
        }

        POINTER_INFO pointerInfo{};
        if (!GetPointerInfo(pointerId, &pointerInfo))
        {
            break;
        }
        const POINT point = pointerInfo.ptPixelLocation;

        if (message == WM_POINTERDOWN)
        {
            if (owner->_interaction.Current().kind == GuideModel::InteractionKind::Placement)
            {
                owner->UpdateInteraction(point);
                owner->CommitInteraction();
                return 0;
            }
            if (owner->_interaction.Active() || owner->_activePointerId != 0)
            {
                return 0;
            }

            const auto hit = owner->_guides.HitTest(point, monitor->monitor, TouchHitRadius);
            if (hit)
            {
                owner->_activePointerId = pointerId;
                owner->BeginDragOnThread(hit->id, *monitor, point, window);
            }
        }
        else if (message == WM_POINTERUPDATE && owner->_activePointerId == pointerId)
        {
            owner->UpdateInteraction(point);
        }
        else if (message == WM_POINTERUP && owner->_activePointerId == pointerId)
        {
            owner->UpdateInteraction(point);
            owner->_activePointerId = 0;
            owner->CommitInteraction();
        }
        return 0;
    }

    case WM_DISPLAYCHANGE:
        if (!owner->_displayChangePending)
        {
            owner->_displayChangePending = true;
            owner->Enqueue([owner] {
                owner->HandleDisplayChange();
            });
        }
        return 0;
    }

    return DefWindowProcW(window, message, wParam, lParam);
}
