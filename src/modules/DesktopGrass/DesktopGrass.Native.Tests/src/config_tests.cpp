#include "../third_party/catch2/catch.hpp"
#include "Config.h"

#include <filesystem>
#include <fstream>
#include <sstream>
#include <string>

using namespace desktopgrass;

namespace {

std::filesystem::path test_config_path(const char* name) {
    std::filesystem::path dir = std::filesystem::current_path()
        / ".copilot-scratch"
        / "native-config-tests"
        / name;
    std::error_code ec;
    std::filesystem::remove_all(dir, ec);
    std::filesystem::create_directories(dir);
    return dir / "config.json";
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

} // namespace

TEST_CASE("Config: missing file yields defaults and writes a template", "[config]") {
    const std::filesystem::path path = test_config_path("missing");
    REQUIRE_FALSE(std::filesystem::exists(path));

    const config::Config cfg = config::LoadConfig(path.wstring());

    CHECK(cfg.targetFps == config::kTargetFpsDefault);
    CHECK(cfg.bladeDensity == Approx(config::kBladeDensityDefault));

    // A default file should have been created and be re-readable (it is JSONC).
    REQUIRE(std::filesystem::exists(path));
    const config::Config reread = config::LoadConfig(path.wstring());
    CHECK(reread.targetFps == config::kTargetFpsDefault);
    CHECK(reread.bladeDensity == Approx(config::kBladeDensityDefault));
}

TEST_CASE("Config: valid values are parsed", "[config]") {
    const std::filesystem::path path = test_config_path("valid");
    write_text(path, "{ \"version\": 1, \"targetFps\": 60, \"bladeDensity\": 1.5 }");

    const config::Config cfg = config::LoadConfig(path.wstring());
    CHECK(cfg.targetFps == 60);
    CHECK(cfg.bladeDensity == Approx(1.5));
}

TEST_CASE("Config: out-of-range values are clamped", "[config]") {
    const std::filesystem::path path = test_config_path("clamp");
    write_text(path, "{ \"targetFps\": 1000, \"bladeDensity\": 99.0 }");

    config::Config cfg = config::LoadConfig(path.wstring());
    CHECK(cfg.targetFps == config::kTargetFpsMax);
    CHECK(cfg.bladeDensity == Approx(config::kBladeDensityMax));

    write_text(path, "{ \"targetFps\": 0, \"bladeDensity\": 0.0 }");
    cfg = config::LoadConfig(path.wstring());
    CHECK(cfg.targetFps == config::kTargetFpsMin);
    CHECK(cfg.bladeDensity == Approx(config::kBladeDensityMin));
}

TEST_CASE("Config: JSONC comments and trailing commas are tolerated", "[config]") {
    const std::filesystem::path path = test_config_path("jsonc");
    write_text(path,
        "{\n"
        "  // a comment\n"
        "  \"targetFps\": 24, /* inline */\n"
        "  \"bladeDensity\": 2.0,\n"  // trailing comma below
        "}\n");

    const config::Config cfg = config::LoadConfig(path.wstring());
    CHECK(cfg.targetFps == 24);
    CHECK(cfg.bladeDensity == Approx(2.0));
}

TEST_CASE("Config: malformed file falls back to defaults and is preserved", "[config]") {
    const std::filesystem::path path = test_config_path("malformed");
    write_text(path, "{ not valid json ");

    const config::Config cfg = config::LoadConfig(path.wstring());
    CHECK(cfg.targetFps == config::kTargetFpsDefault);
    CHECK(cfg.bladeDensity == Approx(config::kBladeDensityDefault));

    // The user's (broken) file must be left untouched for them to fix.
    CHECK(read_text(path) == "{ not valid json ");
}

TEST_CASE("Config: missing keys fall back to per-key defaults", "[config]") {
    const std::filesystem::path path = test_config_path("partial");
    write_text(path, "{ \"targetFps\": 45 }");

    const config::Config cfg = config::LoadConfig(path.wstring());
    CHECK(cfg.targetFps == 45);
    CHECK(cfg.bladeDensity == Approx(config::kBladeDensityDefault));
    CHECK(cfg.swaySpeed == Approx(config::kSwaySpeedDefault));
    CHECK(cfg.swayAmplitude == Approx(config::kSwayAmplitudeDefault));
}

TEST_CASE("Config: keys are matched case-insensitively", "[config]") {
    const std::filesystem::path path = test_config_path("case-insensitive");
    write_text(path,
        "{ \"TargetFps\": 60, \"BLADEDENSITY\": 1.5, "
        "\"SwaySpeed\": 0.5, \"swayamplitude\": 2.0 }");

    const config::Config cfg = config::LoadConfig(path.wstring());
    CHECK(cfg.targetFps == 60);
    CHECK(cfg.bladeDensity == Approx(1.5));
    CHECK(cfg.swaySpeed == Approx(0.5));
    CHECK(cfg.swayAmplitude == Approx(2.0));
}

TEST_CASE("Config: sway knobs parse, clamp, and reject non-finite", "[config]") {
    const std::filesystem::path path = test_config_path("sway");

    // Defaults when absent.
    write_text(path, "{ }");
    config::Config cfg = config::LoadConfig(path.wstring());
    CHECK(cfg.swaySpeed == Approx(config::kSwaySpeedDefault));
    CHECK(cfg.swayAmplitude == Approx(config::kSwayAmplitudeDefault));

    // Valid values parsed.
    write_text(path, "{ \"swaySpeed\": 0.5, \"swayAmplitude\": 2.0 }");
    cfg = config::LoadConfig(path.wstring());
    CHECK(cfg.swaySpeed == Approx(0.5));
    CHECK(cfg.swayAmplitude == Approx(2.0));

    // Out-of-range clamped to bounds.
    write_text(path, "{ \"swaySpeed\": 99.0, \"swayAmplitude\": -5.0 }");
    cfg = config::LoadConfig(path.wstring());
    CHECK(cfg.swaySpeed == Approx(config::kSwaySpeedMax));
    CHECK(cfg.swayAmplitude == Approx(config::kSwayAmplitudeMin));

    // Non-finite (inf from overflow) falls back to default, never poisons the sim.
    write_text(path, "{ \"swaySpeed\": 1e999, \"swayAmplitude\": 1e999 }");
    cfg = config::LoadConfig(path.wstring());
    CHECK(cfg.swaySpeed == Approx(config::kSwaySpeedDefault));
    CHECK(cfg.swayAmplitude == Approx(config::kSwayAmplitudeDefault));
}
