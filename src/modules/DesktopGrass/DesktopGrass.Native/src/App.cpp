// App.cpp

#include "App.h"

#include "AutoStart.h"
#include "Constants.h"
#include "Sim.h"
#include "../resource.h"

#include <shellscalingapi.h>
#include <algorithm>
#include <cstdio>
#include <string>
#include <utility>

#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "Shcore.lib")
#pragma comment(lib, "User32.lib")

namespace desktopgrass {

namespace {

constexpr const wchar_t* kMsgWindowClass = L"DesktopGrass.Native.MessageWindow";

// Fixed launch seed shared with the Win2D implementation so both produce
// identical, deterministic per-monitor blade layouts.
constexpr uint64_t kAppSeed = 0xD3C7C0F30070D511ull;

// Per-monitor seed: combine the fixed app seed with the monitor's physical
// origin so different screens get different — but stable across launches —
// blade layouts. Mirrors Win2D App.cs exactly, including C# `(ulong)int`
// sign-extension and unchecked uint64 multiply/wraparound semantics.
uint64_t make_monitor_seed(const RECT& bounds) {
    const uint64_t left = static_cast<uint64_t>(static_cast<int64_t>(bounds.left));
    const uint64_t top  = static_cast<uint64_t>(static_cast<int64_t>(bounds.top));
    return kAppSeed
        ^ (left * 0xA0761D6478BD642Full)
        ^ (top  * 0xE7037ED1A0B428DBull);
}

// EnumDisplayMonitors callback context.
struct MonitorEnumCtx {
    App*                app;
    std::vector<RECT>   bounds;
    std::vector<UINT>   dpis;
};

BOOL CALLBACK MonitorEnumProc(HMONITOR hMon, HDC, LPRECT, LPARAM lParam) {
    auto* ctx = reinterpret_cast<MonitorEnumCtx*>(lParam);
    MONITORINFO mi{};
    mi.cbSize = sizeof(mi);
    if (GetMonitorInfoW(hMon, &mi)) {
        // Use the work area, not the full monitor rect, so the grass sits on
        // top of the taskbar instead of being drawn behind it. On monitors with
        // no taskbar (typical secondary displays), rcWork == rcMonitor.
        ctx->bounds.push_back(mi.rcWork);
        UINT xDpi = 96, yDpi = 96;
        if (FAILED(GetDpiForMonitor(hMon, MDT_EFFECTIVE_DPI, &xDpi, &yDpi))) {
            xDpi = 96;
        }
        ctx->dpis.push_back(xDpi);
    }
    return TRUE;
}

} // anonymous

App::~App() {
    DestroyAllGrassWindows();
    RemoveTrayIcon();
    if (trayMenu_) { DestroyMenu(trayMenu_); trayMenu_ = nullptr; }
    DestroyMessageWindow();
    uninstall_mouse_hook();
}

bool App::Initialize(HINSTANCE hInst) {
    hInst_   = hInst;
    config_  = config::LoadConfig();

    QueryPerformanceFrequency(&qpcFreq_);
    QueryPerformanceCounter(&qpcLast_);

    hasPersistedState_ = persistence::LoadAppState(persistedState_);
    if (hasPersistedState_) {
        currentScene_ = persistedState_.scene;
        currentCritter_ = persistedState_.critter;
        currentCritterCount_ = persistedState_.critterCountOverride;
        autoStart_ = persistedState_.autoStart;
    }
    if (!autostart::ReconcileWithState(autoStart_)) {
        OutputDebugStringA("[DesktopGrass] unable to reconcile Start with Windows registry state\n");
    }
    lastPersistenceSaveMs_ = GetTickCount64();

    if (!GrassWindow::RegisterWindowClass(hInst_)) return false;
    if (!CreateMessageWindow())                    return false;
    if (!CreateTrayIcon())                         return false;
    if (!EnumerateMonitorsAndCreateWindows())      return false;

    if (!install_mouse_hook(&queue_)) {
        OutputDebugStringA("[DesktopGrass] install_mouse_hook failed\n");
        // Non-fatal — the grass will still sway, just no gusts/cuts.
    }

    return true;
}

bool App::CreateMessageWindow() {
    WNDCLASSEXW wc{};
    wc.cbSize        = sizeof(wc);
    wc.lpfnWndProc   = App::MessageWindowProc;
    wc.hInstance     = hInst_;
    wc.lpszClassName = kMsgWindowClass;

    ATOM atom = RegisterClassExW(&wc);
    if (atom == 0 && GetLastError() != ERROR_CLASS_ALREADY_EXISTS) {
        return false;
    }

    msgHwnd_ = CreateWindowExW(
        0, kMsgWindowClass, L"DesktopGrass.Msg",
        0, 0, 0, 0, 0,
        HWND_MESSAGE, nullptr, hInst_, this);
    return msgHwnd_ != nullptr;
}

void App::DestroyMessageWindow() {
    if (msgHwnd_) {
        DestroyWindow(msgHwnd_);
        msgHwnd_ = nullptr;
    }
}

bool App::CreateTrayIcon() {
    // Build the menu: Scene ▸ (radio: Grass / Desert / Winter / Autumn) | Quit.
    // The Scene submenu is a child popup of trayMenu_; DestroyMenu is
    // recursive so destroying trayMenu_ cleans up the submenu too.
    trayMenu_ = CreatePopupMenu();
    if (!trayMenu_) return false;
    sceneSubmenu_ = CreatePopupMenu();
    if (!sceneSubmenu_) return false;
    AppendMenuW(sceneSubmenu_, MF_STRING, kMenuSceneGrass,  L"Grass");
    AppendMenuW(sceneSubmenu_, MF_STRING, kMenuSceneDesert, L"Desert");
    AppendMenuW(sceneSubmenu_, MF_STRING, kMenuSceneWinter, L"Winter");
    AppendMenuW(sceneSubmenu_, MF_STRING, kMenuSceneAutumn, L"Autumn");
    AppendMenuW(sceneSubmenu_, MF_STRING, kMenuSceneOcean,  L"Ocean");
    AppendMenuW(trayMenu_, MF_POPUP | MF_STRING,
                reinterpret_cast<UINT_PTR>(sceneSubmenu_), L"Scene");

    critterSubmenu_ = CreatePopupMenu();
    if (!critterSubmenu_) return false;
    AppendMenuW(critterSubmenu_, MF_STRING, kMenuCritterNone,  L"None");
    AppendMenuW(critterSubmenu_, MF_STRING, kMenuCritterSheep, L"Sheep");
    AppendMenuW(critterSubmenu_, MF_STRING, kMenuCritterCat,   L"Cat");
    AppendMenuW(critterSubmenu_, MF_STRING, kMenuCritterAll,   L"All");

    petCountSubmenu_ = CreatePopupMenu();
    if (!petCountSubmenu_) return false;
    AppendMenuW(petCountSubmenu_, MF_STRING, kMenuPetCountRandom, L"Random");
    for (int n : PET_COUNT_OPTIONS) {
        AppendMenuW(petCountSubmenu_, MF_STRING,
                    static_cast<UINT_PTR>(kMenuPetCount1 + (n - 1)),
                    std::to_wstring(n).c_str());
    }
    AppendMenuW(critterSubmenu_, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(critterSubmenu_, MF_POPUP | MF_STRING,
                reinterpret_cast<UINT_PTR>(petCountSubmenu_), L"Pet count");

    AppendMenuW(trayMenu_, MF_POPUP | MF_STRING,
                reinterpret_cast<UINT_PTR>(critterSubmenu_), L"Critter");

    AppendMenuW(trayMenu_, MF_STRING, kMenuAutoStart, L"Start with Windows");
    AppendMenuW(trayMenu_, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(trayMenu_, MF_STRING, kMenuQuit, L"Quit DesktopGrass");
    UpdateSceneMenuCheck();
    UpdateCritterMenuCheck();
    UpdatePetCountMenuCheck();
    UpdateAutoStartMenuCheck();

    nid_ = {};
    nid_.cbSize           = sizeof(nid_);
    nid_.hWnd             = msgHwnd_;
    nid_.uID              = kTrayIconId;
    nid_.uFlags           = NIF_ICON | NIF_MESSAGE | NIF_TIP;
    nid_.uCallbackMessage = kTrayMessage;

    HICON icon = LoadIconW(hInst_, MAKEINTRESOURCEW(IDI_TRAYICON));
    if (!icon) icon = LoadIconW(nullptr, IDI_APPLICATION);
    nid_.hIcon = icon;
    wcsncpy_s(nid_.szTip, L"Desktop Grass", _TRUNCATE);

    BOOL ok = Shell_NotifyIconW(NIM_ADD, &nid_);
    trayAdded_ = (ok == TRUE);
    if (!trayAdded_) {
        OutputDebugStringA("[DesktopGrass] Shell_NotifyIcon(NIM_ADD) failed\n");
    }
    return true; // non-fatal
}

void App::UpdateSceneMenuCheck() {
    if (!sceneSubmenu_) return;
    // Radio-style check: kMenuSceneGrass + Scene enum index.
    const int activeId = kMenuSceneGrass + static_cast<int>(currentScene_);
    CheckMenuRadioItem(sceneSubmenu_,
                       kMenuSceneGrass, kMenuSceneOcean,
                       activeId, MF_BYCOMMAND);
}

void App::SetScene(Scene s) {
    if (s == currentScene_) {
        UpdateSceneMenuCheck();
        return;
    }
    currentScene_ = s;
    for (auto& w : windows_) {
        sim_set_scene(w->GetRenderer().GetSim(), s);
    }
    UpdateSceneMenuCheck();
    SaveCurrentState();
}

void App::UpdateCritterMenuCheck() {
    if (!critterSubmenu_) return;
    const int activeId = kMenuCritterNone + static_cast<int>(currentCritter_);
    CheckMenuRadioItem(critterSubmenu_,
                       kMenuCritterNone, kMenuCritterAll,
                       activeId, MF_BYCOMMAND);
}

void App::UpdatePetCountMenuCheck() {
    if (!petCountSubmenu_) return;
    const int activeId = currentCritterCount_ > 0
        ? kMenuPetCount1 + (std::min(currentCritterCount_, PET_COUNT_MAX_PER_MONITOR) - 1)
        : kMenuPetCountRandom;
    CheckMenuRadioItem(petCountSubmenu_,
                       kMenuPetCountRandom, kMenuPetCount6,
                       activeId, MF_BYCOMMAND);
}

void App::UpdateAutoStartMenuCheck() {
    if (!trayMenu_) return;
    CheckMenuItem(trayMenu_, kMenuAutoStart,
                  MF_BYCOMMAND | (autoStart_ ? MF_CHECKED : MF_UNCHECKED));
}

void App::SetAutoStart(bool enabled) {
    if (enabled == autoStart_ && autostart::IsEnabled() == enabled) {
        UpdateAutoStartMenuCheck();
        return;
    }

    if (!autostart::SetEnabled(enabled)) {
        OutputDebugStringA("[DesktopGrass] unable to update Start with Windows registry state\n");
        UpdateAutoStartMenuCheck();
        return;
    }

    autoStart_ = enabled;
    UpdateAutoStartMenuCheck();
    SaveCurrentState();
}

void App::SetCritter(CritterKind c) {
    if (c == currentCritter_) {
        UpdateCritterMenuCheck();
        return;
    }
    currentCritter_ = c;
    for (auto& w : windows_) {
        sim_set_critter(w->GetRenderer().GetSim(), c);
    }
    UpdateCritterMenuCheck();
    SaveCurrentState();
}

void App::SetCritterCount(int n) {
    const int sanitized = n > 0 ? std::min(n, PET_COUNT_MAX_PER_MONITOR) : 0;
    if (sanitized == currentCritterCount_) {
        UpdatePetCountMenuCheck();
        return;
    }

    currentCritterCount_ = sanitized;
    for (auto& w : windows_) {
        sim_set_critter_count(w->GetRenderer().GetSim(), currentCritterCount_);
    }
    UpdatePetCountMenuCheck();
    SaveCurrentState();
}

void App::RemoveTrayIcon() {
    if (trayAdded_) {
        Shell_NotifyIconW(NIM_DELETE, &nid_);
        trayAdded_ = false;
    }
}

bool App::EnumerateMonitorsAndCreateWindows() {
    DestroyAllGrassWindows();

    MonitorEnumCtx ctx{ this, {}, {} };
    EnumDisplayMonitors(nullptr, nullptr, MonitorEnumProc,
                        reinterpret_cast<LPARAM>(&ctx));

    if (ctx.bounds.empty()) return false;

    for (size_t i = 0; i < ctx.bounds.size(); ++i) {
        auto w = std::make_unique<GrassWindow>();
        // Each monitor gets a deterministic seed derived from its physical
        // origin (shared formula with Win2D) so blade patterns differ across
        // monitors but stay stable across launches and line up with persisted
        // cut records.
        const uint64_t mseed = make_monitor_seed(ctx.bounds[i]);
        if (w->Create(hInst_, ctx.bounds[i], ctx.dpis[i], mseed, config_.bladeDensity,
                      config_.swaySpeed, config_.swayAmplitude)) {
            ApplyPersistedStateToWindow(*w, ctx.bounds[i]);
            w->Show();
            windows_.push_back(std::move(w));
        } else {
            OutputDebugStringA("[DesktopGrass] GrassWindow::Create failed\n");
        }
    }
    return !windows_.empty();
}

void App::DestroyAllGrassWindows() {
    windows_.clear();
}

void App::OnDisplayChanged() {
    SaveCurrentState();
    EnumerateMonitorsAndCreateWindows();
}

void App::DispatchMouseEvents() {
    // Drain the lock-free queue once. Each event is then routed to whichever
    // GrassWindow's screen rect contains it.
    RawMouseEvent raw[256];
    while (true) {
        std::size_t n = queue_.drain(raw, 256);
        if (n == 0) break;

        for (std::size_t i = 0; i < n; ++i) {
            const RawMouseEvent& e = raw[i];
            for (auto& w : windows_) {
                const RECT& r = w->GetScreenBounds();
                // Move events fire across the gust band; click events only
                // when in the strip. The Sim's band-check (apply_move / click)
                // re-filters in window-local coords. Here we route any event
                // whose x is in the window's horizontal range — Move events
                // need to update the prevCursor baseline even outside the
                // band so the baseline stays accurate, and the spec already
                // handles the band rejection.
                if (e.screenX < r.left || e.screenX > r.right) continue;

                // For move events we accept any y; for click events we only
                // accept y inside the band.
                if (e.type == EventType::Click) {
                    if (e.screenY < r.top || e.screenY > r.bottom) continue;
                }

                // Convert to window-local DIPs.
                const UINT dpi = w->GetRenderer().GetDpi();
                const double scale = 96.0 / static_cast<double>(dpi);
                InputEvent ie{};
                ie.type = e.type;
                ie.x    = (e.screenX - r.left) * scale;
                ie.y    = (e.screenY - r.top)  * scale;
                ie.time = e.timeSeconds;

                // Apply directly to the sim. Note that this happens BEFORE
                // sim_tick (which itself drains its events list), so we apply
                // events through the per-tick path — collect into a per-window
                // event vector instead.
                // To keep things simple, push to the Sim immediately:
                if (ie.type == EventType::Move) {
                    sim_apply_move(w->GetRenderer().GetSim(), ie);
                } else {
                    sim_apply_click(w->GetRenderer().GetSim(), ie);
                }
                break; // each event belongs to at most one window
            }
        }
        if (n < 256) break;
    }
}

void App::RenderAllWindows(double dt) {
    DispatchMouseEvents();
    for (auto& w : windows_) {
        w->RenderFrame(dt, nullptr, 0);
    }
}

void App::ApplyPersistedStateToWindow(GrassWindow& window, const RECT& monitorBounds) {
    Sim& sim = window.GetRenderer().GetSim();
    sim_set_scene(sim, currentScene_);
    sim_set_critter_count(sim, currentCritterCount_);
    sim_set_critter(sim, currentCritter_);

    if (!hasPersistedState_) return;

    const int width = monitorBounds.right - monitorBounds.left;
    const int height = monitorBounds.bottom - monitorBounds.top;
    for (const persistence::MonitorState& monitor : persistedState_.monitors) {
        if (monitor.width == width
            && monitor.height == height
            && monitor.left == monitorBounds.left
            && monitor.top == monitorBounds.top) {
            sim_apply_cuts(sim, monitor.cuts);
            return;
        }
    }
}

persistence::AppState App::BuildAppState() {
    persistence::AppState state;
    state.version = 2;
    state.scene = currentScene_;
    state.critter = currentCritter_;
    state.critterCountOverride = currentCritterCount_;
    state.autoStart = autoStart_;

    state.monitors.reserve(windows_.size());
    for (const auto& w : windows_) {
        const RECT& bounds = w->GetMonitorBounds();
        persistence::MonitorState monitor;
        monitor.width = bounds.right - bounds.left;
        monitor.height = bounds.bottom - bounds.top;
        monitor.left = bounds.left;
        monitor.top = bounds.top;
        const Sim& sim = w->GetRenderer().GetSim();
        monitor.cuts = sim_get_cuts(sim);
        state.monitors.push_back(std::move(monitor));
    }

    return state;
}

void App::SaveCurrentState() {
    persistedState_ = BuildAppState();
    hasPersistedState_ = true;
    persistence::SaveAppState(persistedState_);
    lastPersistenceSaveMs_ = GetTickCount64();
}

int App::Run() {
    MSG msg{};
    // Calm ambient content renders at the configured target fps (default 24
    // via Config.h kTargetFpsDefault) to keep per-frame CPU low; motion is
    // time-based (dt), so the rate only changes how often the same animation
    // is sampled. The user can override this in config.json (targetFps).
    const double kTargetFrameSec = 1.0 / static_cast<double>(config_.targetFps);

    while (!quitRequested_) {
        // Drain pending messages without blocking.
        while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) {
            if (msg.message == WM_QUIT) {
                quitRequested_ = true;
                break;
            }
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        if (quitRequested_) break;

        // Compute dt.
        LARGE_INTEGER now;
        QueryPerformanceCounter(&now);
        const double dt = static_cast<double>(now.QuadPart - qpcLast_.QuadPart) /
                          static_cast<double>(qpcFreq_.QuadPart);
        qpcLast_ = now;

        RenderAllWindows(dt);

        if (GetTickCount64() - lastPersistenceSaveMs_ >= 60000ull) {
            SaveCurrentState();
        }

        // Pace to the target frame interval, accounting for the time already
        // spent rendering/presenting this iteration so the cadence holds at
        // the target fps regardless of how long Present blocked. The pacer
        // uses a high-resolution waitable timer (Win 10 1803+) so the wait
        // honours sub-15.6 ms remainders instead of getting clamped to the
        // default system timer resolution. The wait returns early if input
        // arrives, keeping the app responsive.
        LARGE_INTEGER after;
        QueryPerformanceCounter(&after);
        const double elapsedSec = static_cast<double>(after.QuadPart - now.QuadPart) /
                                  static_cast<double>(qpcFreq_.QuadPart);
        const double remainingSec = kTargetFrameSec - elapsedSec;
        pacer_.WaitUntilNextFrame(remainingSec);
    }

    SaveCurrentState();
    return static_cast<int>(msg.wParam);
}

void App::RequestQuit() {
    quitRequested_ = true;
    PostQuitMessage(0);
}

LRESULT CALLBACK App::MessageWindowProc(HWND hwnd, UINT msg,
                                        WPARAM wp, LPARAM lp)
{
    App* self = nullptr;
    if (msg == WM_NCCREATE) {
        auto* cs = reinterpret_cast<CREATESTRUCTW*>(lp);
        self = reinterpret_cast<App*>(cs->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
        if (self) self->msgHwnd_ = hwnd;
    } else {
        self = reinterpret_cast<App*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    }
    if (self) return self->HandleMessageWindowMessage(msg, wp, lp);
    return DefWindowProcW(hwnd, msg, wp, lp);
}

LRESULT App::HandleMessageWindowMessage(UINT msg, WPARAM wp, LPARAM lp) {
    switch (msg) {
        case kTrayMessage:
            if (LOWORD(lp) == WM_RBUTTONUP || LOWORD(lp) == WM_CONTEXTMENU) {
                POINT pt;
                GetCursorPos(&pt);
                SetForegroundWindow(msgHwnd_);
                TrackPopupMenu(trayMenu_,
                               TPM_RIGHTBUTTON | TPM_BOTTOMALIGN,
                               pt.x, pt.y, 0, msgHwnd_, nullptr);
                PostMessageW(msgHwnd_, WM_NULL, 0, 0);
            }
            return 0;

        case WM_COMMAND: {
            const int id = LOWORD(wp);
            if (id == kMenuPetCountRandom) {
                SetCritterCount(0);
                return 0;
            }
            if (id >= kMenuPetCount1 && id <= kMenuPetCount6) {
                SetCritterCount(id - kMenuPetCount1 + 1);
                return 0;
            }
            switch (id) {
                case kMenuQuit:          RequestQuit();                       break;
                case kMenuAutoStart:     SetAutoStart(!autoStart_);           break;
                case kMenuSceneGrass:    SetScene(Scene::Grass);              break;
                case kMenuSceneDesert:   SetScene(Scene::Desert);             break;
                case kMenuSceneWinter:   SetScene(Scene::Winter);             break;
                case kMenuSceneAutumn:   SetScene(Scene::Autumn);             break;
    case kMenuSceneOcean:    SetScene(Scene::Ocean);              break;
                case kMenuCritterNone:   SetCritter(CritterKind::None);       break;
                case kMenuCritterSheep:  SetCritter(CritterKind::Sheep);      break;
                case kMenuCritterCat:    SetCritter(CritterKind::Cat);        break;
                case kMenuCritterAll:    SetCritter(CritterKind::Bunny);      break;
            }
            return 0;
        }

        case WM_DISPLAYCHANGE:
            OnDisplayChanged();
            return 0;

        case WM_CLOSE:
            // The smoke harness sends WM_CLOSE to the *grass* window, which
            // PostQuitMessages from its WndProc. Also handle it here for
            // robustness.
            RequestQuit();
            return 0;

        default:
            return DefWindowProcW(msgHwnd_, msg, wp, lp);
    }
}

} // namespace desktopgrass
