#include "Persistence.h"

#include "Json.h"

#include <Windows.h>

#include <algorithm>
#include <chrono>
#include <cctype>
#include <cstdio>
#include <cstdlib>
#include <ctime>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <map>
#include <optional>
#include <sstream>
#include <string_view>
#include <utility>

namespace desktopgrass::persistence {
namespace {

using desktopgrass::json::FindMember;
using desktopgrass::json::ReadBool;
using desktopgrass::json::ReadDouble;
using desktopgrass::json::ReadInt;
using desktopgrass::json::ReadString;
using JsonValue = desktopgrass::json::Value;
using JsonParser = desktopgrass::json::Parser;

std::optional<std::wstring> g_stateFilePathForTest;
constexpr int kCurrentVersion = 2;

std::string JsonEscape(std::string_view text) {
    return desktopgrass::json::Escape(text);
}

std::string SceneToString(Scene scene) noexcept {
    switch (scene) {
    case Scene::Grass:  return "Grass";
    case Scene::Desert: return "Desert";
    case Scene::Winter: return "Winter";
    case Scene::Autumn: return "Autumn";
    case Scene::Ocean:  return "Ocean";
    }
    return "Grass";
}

Scene SceneFromString(const std::string& scene) noexcept {
    if (scene == "Desert") return Scene::Desert;
    if (scene == "Winter") return Scene::Winter;
    if (scene == "Autumn") return Scene::Autumn;
    if (scene == "Ocean") return Scene::Ocean;
    return Scene::Grass;
}

std::string CritterToString(CritterKind critter) noexcept {
    switch (critter) {
    case CritterKind::None:  return "None";
    case CritterKind::Sheep: return "Sheep";
    case CritterKind::Cat:   return "Cat";
    case CritterKind::Bunny: return "Bunny";
    }
    return "None";
}

CritterKind CritterFromString(const std::string& critter) noexcept {
    if (critter == "Sheep") return CritterKind::Sheep;
    if (critter == "Cat") return CritterKind::Cat;
    if (critter == "Bunny") return CritterKind::Bunny;
    return CritterKind::None;
}

std::string CurrentUtcTimestamp() {
    const auto now = std::chrono::system_clock::now();
    const std::time_t time = std::chrono::system_clock::to_time_t(now);
    std::tm utc{};
    gmtime_s(&utc, &time);

    std::ostringstream out;
    out << std::put_time(&utc, "%Y-%m-%dT%H:%M:%SZ");
    return out.str();
}

bool TryParseMonitorKey(const std::string& key, MonitorState& monitor) {
    int consumed = 0;
    const int matched = sscanf_s(key.c_str(), "%dx%d@%d,%d%n",
                                    &monitor.width,
                                    &monitor.height,
                                    &monitor.left,
                                    &monitor.top,
                                    &consumed);
    return matched == 4 && consumed == static_cast<int>(key.size());
}

std::string Serialize(const AppState& state) {
    std::ostringstream out;
    out << std::setprecision(17);
    out << "{\n";
    out << "  \"version\": " << kCurrentVersion << ",\n";
    out << "  \"savedAt\": \"" << CurrentUtcTimestamp() << "\",\n";
    out << "  \"scene\": \"" << SceneToString(state.scene) << "\",\n";
    out << "  \"critter\": \"" << CritterToString(state.critter) << "\",\n";
    out << "  \"critterCount\": " << state.critterCountOverride << ",\n";
    out << "  \"autoStart\": " << (state.autoStart ? "true" : "false") << ",\n";
    out << "  \"monitors\": {\n";

    for (std::size_t i = 0; i < state.monitors.size(); ++i) {
        const MonitorState& monitor = state.monitors[i];
        out << "    \"" << JsonEscape(MonitorKey(monitor)) << "\": {\n";
        out << "      \"cuts\": [";
        if (!monitor.cuts.empty()) {
            out << "\n";
            for (std::size_t j = 0; j < monitor.cuts.size(); ++j) {
                const CutRecord& cut = monitor.cuts[j];
                out << "        { \"bladeIndex\": " << cut.bladeIndex
                    << ", \"cutTime\": " << cut.cutTime << " }";
                if (j + 1 < monitor.cuts.size()) out << ",";
                out << "\n";
            }
            out << "      ";
        }
        out << "]\n";
        out << "    }";
        if (i + 1 < state.monitors.size()) out << ",";
        out << "\n";
    }

    out << "  }\n";
    out << "}\n";
    return out.str();
}

bool ParseAppState(const JsonValue& root, AppState& out) {
    if (root.type != JsonValue::Type::Object) return false;

    const int version = ReadInt(root, "version").value_or(0);
    if (version != 1 && version != kCurrentVersion) {
        OutputDebugStringA("DesktopGrass persistence: unsupported state.json version; starting fresh.\n");
        return false;
    }

    AppState parsed;
    parsed.version = kCurrentVersion;

    auto sceneName = ReadString(root, "scene");
    if (!sceneName) sceneName = ReadString(root, "currentScene");
    parsed.scene = SceneFromString(sceneName.value_or("Grass"));

    auto critterName = ReadString(root, "critter");
    if (!critterName) critterName = ReadString(root, "currentCritter");
    parsed.critter = CritterFromString(critterName.value_or("None"));

    auto critterCount = ReadInt(root, "critterCount");
    if (!critterCount) critterCount = ReadInt(root, "critterCountOverride");
    parsed.critterCountOverride = critterCount.value_or(0);
    if (parsed.critterCountOverride < 0 || parsed.critterCountOverride > PET_COUNT_MAX_PER_MONITOR) {
        parsed.critterCountOverride = 0;
    }
    parsed.autoStart = ReadBool(root, "autoStart").value_or(false);

    const JsonValue* monitors = FindMember(root, "monitors");
    if (monitors && monitors->type == JsonValue::Type::Object) {
        for (const auto& [key, value] : monitors->objectValue) {
            MonitorState monitor;
            if (!TryParseMonitorKey(key, monitor)) {
                continue;
            }

            const JsonValue* cuts = FindMember(value, "cuts");
            if (cuts && cuts->type == JsonValue::Type::Array) {
                for (const JsonValue& cutValue : cuts->arrayValue) {
                    if (cutValue.type != JsonValue::Type::Object) continue;
                    const auto bladeIndex = ReadInt(cutValue, "bladeIndex");
                    const auto cutTime = ReadDouble(cutValue, "cutTime");
                    if (!bladeIndex || !cutTime) continue;
                    monitor.cuts.push_back(CutRecord{ *bladeIndex, *cutTime });
                }
            }
            parsed.monitors.push_back(std::move(monitor));
        }
    }

    out = std::move(parsed);
    return true;
}

std::wstring DefaultStateFilePath() {
    wchar_t* localAppData = nullptr;
    std::size_t length = 0;
    _wdupenv_s(&localAppData, &length, L"LOCALAPPDATA");

    std::filesystem::path path = localAppData && length > 0
        ? std::filesystem::path(localAppData)
        : std::filesystem::current_path();

    if (localAppData) {
        std::free(localAppData);
    }

    path /= L"DesktopGrass";
    path /= L"state.json";
    return path.wstring();
}

} // namespace

std::string MonitorKey(int width, int height, int left, int top) {
    return std::to_string(width) + "x" + std::to_string(height) + "@"
         + std::to_string(left) + "," + std::to_string(top);
}

std::string MonitorKey(const MonitorState& monitor) {
    return MonitorKey(monitor.width, monitor.height, monitor.left, monitor.top);
}

std::wstring GetStateFilePath() {
    if (g_stateFilePathForTest) {
        return *g_stateFilePathForTest;
    }
    return DefaultStateFilePath();
}

void SetStateFilePathForTest(const std::wstring& path) {
    if (path.empty()) {
        g_stateFilePathForTest.reset();
    } else {
        g_stateFilePathForTest = path;
    }
}

bool LoadAppState(AppState& out) {
    const std::filesystem::path path(GetStateFilePath());
    std::ifstream file(path, std::ios::binary);
    if (!file) {
        return false;
    }

    std::ostringstream buffer;
    buffer << file.rdbuf();

    const std::string json = buffer.str();
    JsonValue root;
    JsonParser parser(json);
    if (!parser.Parse(root)) {
        OutputDebugStringA("DesktopGrass persistence: malformed state.json; starting fresh.\n");
        return false;
    }

    return ParseAppState(root, out);
}

bool SaveAppState(const AppState& state) {
    const std::filesystem::path path(GetStateFilePath());
    const std::filesystem::path directory = path.parent_path();
    if (!directory.empty()) {
        std::error_code ec;
        std::filesystem::create_directories(directory, ec);
        if (ec) return false;
    }

    const std::filesystem::path tempPath(path.wstring() + L".tmp");
    {
        std::ofstream file(tempPath, std::ios::binary | std::ios::trunc);
        if (!file) return false;
        const std::string json = Serialize(state);
        file.write(json.data(), static_cast<std::streamsize>(json.size()));
        if (!file) return false;
    }

    if (!MoveFileExW(tempPath.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) {
        std::error_code ec;
        std::filesystem::remove(tempPath, ec);
        return false;
    }

    return true;
}

} // namespace desktopgrass::persistence
