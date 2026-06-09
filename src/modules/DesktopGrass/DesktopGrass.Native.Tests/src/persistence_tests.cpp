#include "../third_party/catch2/catch.hpp"
#include "Persistence.h"
#include "Sim.h"

#include <algorithm>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <string>

using namespace desktopgrass;

namespace {

std::filesystem::path test_state_path(const char* name) {
    std::filesystem::path dir = std::filesystem::current_path()
        / ".copilot-scratch"
        / "native-persistence-tests"
        / name;
    std::error_code ec;
    std::filesystem::remove_all(dir, ec);
    std::filesystem::create_directories(dir);
    return dir / "state.json";
}

void use_state_path(const std::filesystem::path& path) {
    persistence::SetStateFilePathForTest(path.wstring());
}

void write_text(const std::filesystem::path& path, const std::string& text) {
    std::filesystem::create_directories(path.parent_path());
    std::ofstream file(path, std::ios::binary | std::ios::trunc);
    file << text;
}

std::string read_text(const std::filesystem::path& path) {
    std::ifstream file(path, std::ios::binary);
    std::ostringstream buffer;
    buffer << file.rdbuf();
    return buffer.str();
}

persistence::AppState make_state_with_cuts() {
    persistence::AppState state;
    state.scene = Scene::Winter;
    state.critter = CritterKind::Cat;
    state.critterCountOverride = 4;
    state.autoStart = true;

    for (int i = 0; i < 3; ++i) {
        persistence::MonitorState monitor;
        monitor.width = 1920 + i * 320;
        monitor.height = 1080 + i * 120;
        monitor.left = i * 1920;
        monitor.top = i == 2 ? -120 : 0;
        const int cutCount = 2 + i;
        for (int j = 0; j < cutCount; ++j) {
            monitor.cuts.push_back(persistence::CutRecord{ i * 100 + j, -5.0 - i - j * 0.5 });
        }
        state.monitors.push_back(monitor);
    }

    return state;
}

void assert_state_equal(const persistence::AppState& expected, const persistence::AppState& actual) {
    REQUIRE(actual.version == 2);
    REQUIRE(actual.scene == expected.scene);
    REQUIRE(actual.critter == expected.critter);
    REQUIRE(actual.critterCountOverride == expected.critterCountOverride);
    REQUIRE(actual.autoStart == expected.autoStart);
    REQUIRE(actual.monitors.size() == expected.monitors.size());

    for (std::size_t i = 0; i < expected.monitors.size(); ++i) {
        const auto& e = expected.monitors[i];
        const auto& a = actual.monitors[i];
        REQUIRE(a.width == e.width);
        REQUIRE(a.height == e.height);
        REQUIRE(a.left == e.left);
        REQUIRE(a.top == e.top);
        REQUIRE(a.cuts.size() == e.cuts.size());
        for (std::size_t j = 0; j < e.cuts.size(); ++j) {
            REQUIRE(a.cuts[j].bladeIndex == e.cuts[j].bladeIndex);
            REQUIRE(a.cuts[j].cutTime == Approx(e.cuts[j].cutTime).margin(1e-9));
        }
    }
}

Blade make_blade(double regrowDelay, double regrowDuration) {
    Blade b{};
    b.baseX = 100.0;
    b.height = 20.0;
    b.thickness = 1.0;
    b.cutHeight = 1.0;
    b.cutAnimStart = -1.0;
    b.cutInitialHeight = 1.0;
    b.regrowDelay = regrowDelay;
    b.regrowDuration = regrowDuration;
    b.regrowStart = -1.0;
    return b;
}

} // namespace

TEST_CASE("persistence round-trips empty state", "[persistence]") {
    const auto path = test_state_path("round-trip-empty");
    use_state_path(path);

    persistence::AppState expected;
    REQUIRE(persistence::SaveAppState(expected));

    persistence::AppState actual;
    REQUIRE(persistence::LoadAppState(actual));
    assert_state_equal(expected, actual);
}

TEST_CASE("persistence round-trips state with cuts", "[persistence]") {
    const auto path = test_state_path("round-trip-cuts");
    use_state_path(path);

    const persistence::AppState expected = make_state_with_cuts();
    REQUIRE(persistence::SaveAppState(expected));

    persistence::AppState actual;
    REQUIRE(persistence::LoadAppState(actual));
    assert_state_equal(expected, actual);
}

TEST_CASE("persistence round-trips every scene", "[persistence]") {
    const Scene scenes[] = {
        Scene::Grass, Scene::Desert, Scene::Winter, Scene::Autumn, Scene::Ocean
    };
    for (Scene scene : scenes) {
        const auto path = test_state_path("round-trip-scene");
        use_state_path(path);

        persistence::AppState expected;
        expected.scene = scene;
        REQUIRE(persistence::SaveAppState(expected));

        persistence::AppState actual;
        REQUIRE(persistence::LoadAppState(actual));
        REQUIRE(actual.scene == scene);
    }
}

TEST_CASE("persistence version mismatch returns false", "[persistence]") {
    const auto path = test_state_path("version-mismatch");
    use_state_path(path);
    write_text(path, "{ \"version\": 999, \"monitors\": {} }");

    persistence::AppState actual;
    REQUIRE_FALSE(persistence::LoadAppState(actual));
}

TEST_CASE("persistence missing file returns false", "[persistence]") {
    const auto path = test_state_path("missing-file");
    use_state_path(path);

    persistence::AppState actual;
    REQUIRE_FALSE(persistence::LoadAppState(actual));
}

TEST_CASE("persistence malformed json returns false", "[persistence]") {
    const auto path = test_state_path("malformed-json");
    use_state_path(path);
    write_text(path, "not-json");

    persistence::AppState actual;
    REQUIRE_FALSE(persistence::LoadAppState(actual));
}

TEST_CASE("persistence atomic write leaves final file and removes tmp", "[persistence]") {
    const auto path = test_state_path("atomic-write");
    use_state_path(path);

    persistence::AppState state;
    REQUIRE(persistence::SaveAppState(state));

    REQUIRE(std::filesystem::exists(path));
    REQUIRE_FALSE(std::filesystem::exists(std::filesystem::path(path.wstring() + L".tmp")));
}

TEST_CASE("persistence monitor key format round-trips", "[persistence]") {
    const auto path = test_state_path("monitor-key");
    use_state_path(path);

    persistence::AppState state;
    persistence::MonitorState monitor;
    monitor.width = 1920;
    monitor.height = 1080;
    monitor.left = 0;
    monitor.top = 0;
    state.monitors.push_back(monitor);

    REQUIRE(persistence::SaveAppState(state));
    REQUIRE(read_text(path).find("\"1920x1080@0,0\"") != std::string::npos);

    persistence::AppState loaded;
    REQUIRE(persistence::LoadAppState(loaded));
    REQUIRE(loaded.monitors.size() == 1);
    REQUIRE(persistence::MonitorKey(loaded.monitors[0]) == "1920x1080@0,0");
}

TEST_CASE("persistence cut timestamps shift for fresh sim load", "[persistence]") {
    const auto path = test_state_path("time-shift");
    use_state_path(path);

    Sim running;
    running.globalTime = 100.0;
    running.blades.push_back(make_blade(30.0, 10.0));
    running.blades[0].cutHeight = 0.0;
    running.blades[0].regrowStart = 80.0 + CUT_DURATION_SEC + running.blades[0].regrowDelay;

    auto cuts = sim_get_cuts(running);
    REQUIRE(cuts.size() == 1);
    REQUIRE(cuts[0].cutTime == Approx(-20.0).margin(1e-9));

    persistence::AppState state;
    persistence::MonitorState monitor;
    monitor.width = 1920;
    monitor.height = 1080;
    monitor.left = 0;
    monitor.top = 0;
    monitor.cuts = cuts;
    state.monitors.push_back(monitor);
    REQUIRE(persistence::SaveAppState(state));

    persistence::AppState loaded;
    REQUIRE(persistence::LoadAppState(loaded));
    REQUIRE(loaded.monitors[0].cuts[0].cutTime < 0.0);

    Sim fresh;
    fresh.globalTime = 0.0;
    fresh.blades.push_back(make_blade(30.0, 10.0));
    sim_apply_cuts(fresh, loaded.monitors[0].cuts);
    REQUIRE(fresh.blades[0].cutHeight == Approx(0.0).margin(1e-9));
    REQUIRE(fresh.blades[0].regrowStart == Approx(10.0 + CUT_DURATION_SEC).margin(1e-9));
}

TEST_CASE("persistence unmatched monitor cuts are skipped", "[persistence]") {
    const auto path = test_state_path("unmatched-monitor");
    use_state_path(path);

    persistence::AppState state;
    persistence::MonitorState unmatched;
    unmatched.width = 9999;
    unmatched.height = 9999;
    unmatched.left = 99;
    unmatched.top = 99;
    unmatched.cuts.push_back(persistence::CutRecord{ 0, -20.0 });
    state.monitors.push_back(unmatched);
    REQUIRE(persistence::SaveAppState(state));

    persistence::AppState loaded;
    REQUIRE(persistence::LoadAppState(loaded));

    Sim sim;
    sim.blades.push_back(make_blade(30.0, 10.0));
    const int width = 1920;
    const int height = 1080;
    const int left = 0;
    const int top = 0;
    const auto match = std::find_if(loaded.monitors.begin(), loaded.monitors.end(),
        [&](const persistence::MonitorState& monitor) {
            return monitor.width == width && monitor.height == height
                && monitor.left == left && monitor.top == top;
        });
    if (match != loaded.monitors.end()) {
        sim_apply_cuts(sim, match->cuts);
    }

    REQUIRE(sim_get_cuts(sim).empty());
}

TEST_CASE("persistence json is human readable", "[persistence]") {
    const auto path = test_state_path("human-readable");
    use_state_path(path);

    REQUIRE(persistence::SaveAppState(make_state_with_cuts()));
    const std::string text = read_text(path);
    REQUIRE(text.find('\n') != std::string::npos);
    REQUIRE(text.find("  \"version\"") != std::string::npos);
    REQUIRE(text.find("    \"") != std::string::npos);
}
