// App.h
//
// Application lifecycle. Owns the tray icon, the mouse hook, the per-monitor
// GrassWindow list, and the message loop.

#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>

#include <memory>
#include <vector>

#include "GrassWindow.h"
#include "MouseHook.h"
#include "Pacing.h"
#include "Persistence.h"
#include "Config.h"

namespace desktopgrass {

class App {
public:
    static constexpr UINT  kTrayMessage     = WM_APP + 100;
    static constexpr UINT  kTrayIconId      = 1;
    static constexpr int   kMenuQuit          = 1001;
    static constexpr int   kMenuSceneGrass    = 1010;
    static constexpr int   kMenuSceneDesert   = 1011;
    static constexpr int   kMenuSceneWinter   = 1012;
    static constexpr int   kMenuSceneAutumn   = 1013;
    static constexpr int   kMenuSceneOcean    = 1014;
    static constexpr int   kMenuCritterNone     = 1020;
    static constexpr int   kMenuCritterSheep    = 1021;
    static constexpr int   kMenuCritterCat      = 1022;
    static constexpr int   kMenuCritterAll      = 1023;
    static constexpr int   kMenuPetCountRandom  = 1030;
    static constexpr int   kMenuPetCount1       = 1031;
    static constexpr int   kMenuPetCount6       = 1036;
    static constexpr int   kMenuAutoStart       = 1040;

    App() = default;
    ~App();

    bool Initialize(HINSTANCE hInst);
    int  Run();
    void RequestQuit();
    void SetScene(Scene s);
    Scene GetScene() const { return currentScene_; }
    void SetCritter(CritterKind c);
    CritterKind GetCritter() const { return currentCritter_; }
    void SetCritterCount(int n);
    int  GetCritterCount() const { return currentCritterCount_; }

private:
    bool CreateMessageWindow();
    bool CreateTrayIcon();
    void RemoveTrayIcon();
    void DestroyMessageWindow();
    bool EnumerateMonitorsAndCreateWindows();
    void DestroyAllGrassWindows();
    void OnDisplayChanged();
    void DispatchMouseEvents();
    void RenderAllWindows(double dt);
    void ApplyPersistedStateToWindow(GrassWindow& window, const RECT& monitorBounds);
    persistence::AppState BuildAppState();
    void SaveCurrentState();
    void SetAutoStart(bool enabled);
    void UpdateSceneMenuCheck();
    void UpdateCritterMenuCheck();
    void UpdatePetCountMenuCheck();
    void UpdateAutoStartMenuCheck();

    static LRESULT CALLBACK MessageWindowProc(HWND hwnd, UINT msg,
                                              WPARAM wp, LPARAM lp);
    LRESULT HandleMessageWindowMessage(UINT msg, WPARAM wp, LPARAM lp);

    HINSTANCE                                   hInst_   = nullptr;
    HWND                                        msgHwnd_ = nullptr;
    HMENU                                       trayMenu_ = nullptr;
    HMENU                                       sceneSubmenu_ = nullptr;
    HMENU                                       critterSubmenu_ = nullptr;
    HMENU                                       petCountSubmenu_ = nullptr;
    NOTIFYICONDATAW                             nid_{};
    bool                                        trayAdded_ = false;
    MouseEventQueue                             queue_{};
    std::vector<std::unique_ptr<GrassWindow>>   windows_;
    config::Config                              config_{};
    Scene                                       currentScene_ = SCENE_DEFAULT;
    CritterKind                                 currentCritter_ = CRITTER_DEFAULT;
    int                                         currentCritterCount_ = 0;
    bool                                        autoStart_ = false;
    bool                                        hasPersistedState_ = false;
    persistence::AppState                       persistedState_{};
    ULONGLONG                                   lastPersistenceSaveMs_ = 0;
    LARGE_INTEGER                               qpcFreq_{};
    LARGE_INTEGER                               qpcLast_{};
    FramePacer                                  pacer_{};
    bool                                        quitRequested_ = false;
};

} // namespace desktopgrass
