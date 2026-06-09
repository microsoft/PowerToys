#pragma once

#include "Constants.h"

#include <string>
#include <vector>

namespace desktopgrass::persistence {

struct CutRecord {
    int bladeIndex = 0;
    double cutTime = 0.0;
};

struct MonitorState {
    int width = 0;
    int height = 0;
    int left = 0;
    int top = 0;
    std::vector<CutRecord> cuts;
};

struct AppState {
    int version = 2;
    Scene scene = Scene::Grass;
    CritterKind critter = CritterKind::None;
    int critterCountOverride = 0;
    bool autoStart = false;
    std::vector<MonitorState> monitors;
};

bool LoadAppState(AppState& out);
bool SaveAppState(const AppState& state);
std::wstring GetStateFilePath();
void SetStateFilePathForTest(const std::wstring& path);
std::string MonitorKey(int width, int height, int left, int top);
std::string MonitorKey(const MonitorState& monitor);

} // namespace desktopgrass::persistence
