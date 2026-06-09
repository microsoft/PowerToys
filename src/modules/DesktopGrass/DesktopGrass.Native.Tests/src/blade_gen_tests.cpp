// blade_gen_tests.cpp
//
// Snapshot + invariant tests for procedural blade generation (architecture.md §5).

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"
#include "snapshot_data.h"

#include <cmath>
#include <vector>

using namespace desktopgrass;
using namespace desktopgrass::test;

namespace {

void requireBladeEquals(const Blade& actual, const SnapshotBlade& expected, std::size_t index) {
    INFO("blade index = " << index);
    REQUIRE(actual.baseX           == Approx(expected.baseX          ).margin(1e-12));
    REQUIRE(actual.height          == Approx(expected.height         ).margin(1e-12));
    REQUIRE(actual.thickness       == Approx(expected.thickness      ).margin(1e-12));
    REQUIRE(actual.hue             == expected.hue);
    REQUIRE(actual.swayPhaseOffset == Approx(expected.sway          ).margin(1e-12));
    REQUIRE(actual.stiffness       == Approx(expected.stiffness     ).margin(1e-12));
    REQUIRE(actual.isFlower             == expected.isFlower);
    REQUIRE(actual.flowerHeadColorIdx   == expected.flowerHeadColorIdx);
    REQUIRE(actual.flowerHeadRadius     == Approx(expected.flowerHeadRadius).margin(1e-12));
    REQUIRE(actual.heightBonus          == Approx(expected.heightBonus    ).margin(1e-12));
}

} // anonymous

TEST_CASE("blade generation matches the canonical snapshot", "[blade-gen][snapshot]") {
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);

    REQUIRE(blades.size() == CANONICAL_BLADE_COUNT);

    SECTION("first 10 blades match") {
        for (std::size_t i = 0; i < 10; ++i) {
            requireBladeEquals(blades[i], CANONICAL_FIRST_10[i], i);
        }
    }

    SECTION("last 10 blades match") {
        const std::size_t start = blades.size() - 10;
        for (std::size_t i = 0; i < 10; ++i) {
            requireBladeEquals(blades[start + i], CANONICAL_LAST_10[i], start + i);
        }
    }
}

TEST_CASE("blade fields stay within spec ranges", "[blade-gen]") {
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);

    constexpr double TWO_PI = 6.283185307179586;
    for (std::size_t i = 0; i < blades.size(); ++i) {
        const Blade& b = blades[i];
        INFO("blade index = " << i);
        REQUIRE(b.baseX           >= 0.0);
        REQUIRE(b.baseX           <  1920.0);
        REQUIRE(b.height          >= BLADE_HEIGHT_MIN);
        REQUIRE(b.height          <  BLADE_HEIGHT_MAX);
        REQUIRE(b.thickness       >= BLADE_THICKNESS_MIN);
        REQUIRE(b.thickness       <  BLADE_THICKNESS_MAX);
        REQUIRE(b.hue             <  PALETTE_SIZE);
        REQUIRE(b.swayPhaseOffset >= 0.0);
        REQUIRE(b.swayPhaseOffset <  TWO_PI);
        REQUIRE(b.stiffness       >= STIFFNESS_MIN);
        REQUIRE(b.stiffness       <  STIFFNESS_MAX);
        REQUIRE(b.cutHeight        == Approx(1.0));
        REQUIRE(b.gustVelocity     == Approx(0.0));
        REQUIRE(b.cutAnimStart     == Approx(-1.0));
        REQUIRE(b.cutInitialHeight == Approx(1.0));
        if (b.isFlower) {
            REQUIRE(b.flowerHeadColorIdx < FLOWER_PALETTE_SIZE);
            REQUIRE(b.flowerHeadRadius >= FLOWER_HEAD_RADIUS_MIN);
            REQUIRE(b.flowerHeadRadius <  FLOWER_HEAD_RADIUS_MAX);
            REQUIRE(b.heightBonus      >= FLOWER_HEIGHT_BONUS_MIN);
            REQUIRE(b.heightBonus      <  FLOWER_HEIGHT_BONUS_MAX);
        } else {
            REQUIRE(b.flowerHeadColorIdx == 0);
            REQUIRE(b.flowerHeadRadius   == Approx(0.0));
            REQUIRE(b.heightBonus        == Approx(1.0));
        }
    }
}

TEST_CASE("flower count is near configured probability and ordinary blades use defaults", "[blade-gen][flowers]") {
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);
    REQUIRE(blades.size() > 100);

    std::size_t flowerCount = 0;
    for (const Blade& b : blades) {
        if (b.isFlower) {
            ++flowerCount;
        } else {
            REQUIRE(b.flowerHeadColorIdx == 0);
            REQUIRE(b.flowerHeadRadius   == Approx(0.0));
            REQUIRE(b.heightBonus        == Approx(1.0));
        }
    }

    const double n  = static_cast<double>(blades.size());
    const double p  = FLOWER_PROBABILITY;
    const double mu = n * p;
    const double sd = std::sqrt(n * p * (1.0 - p));
    REQUIRE(flowerCount >= static_cast<std::size_t>(std::floor(mu - 3.0 * sd)));
    REQUIRE(flowerCount <= static_cast<std::size_t>(std::ceil(mu + 3.0 * sd)));
}

TEST_CASE("blade baseX is strictly increasing", "[blade-gen]") {
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);
    REQUIRE(blades.size() > 0);
    for (std::size_t i = 1; i < blades.size(); ++i) {
        INFO("between " << (i-1) << " and " << i);
        REQUIRE(blades[i].baseX > blades[i-1].baseX);
    }
}

TEST_CASE("blade generation is deterministic across repeat runs", "[blade-gen]") {
    std::vector<Blade> a, b;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, a);
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, b);
    REQUIRE(a.size() == b.size());
    for (std::size_t i = 0; i < a.size(); ++i) {
        REQUIRE(a[i].baseX           == b[i].baseX);
        REQUIRE(a[i].height          == b[i].height);
        REQUIRE(a[i].thickness       == b[i].thickness);
        REQUIRE(a[i].hue             == b[i].hue);
        REQUIRE(a[i].swayPhaseOffset == b[i].swayPhaseOffset);
        REQUIRE(a[i].stiffness       == b[i].stiffness);
    }
}

TEST_CASE("density scales blade count roughly linearly", "[blade-gen]") {
    std::vector<Blade> low, high;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 0.5, low);
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 2.0, high);
    REQUIRE(low.size()  > 0);
    REQUIRE(high.size() > low.size() * 3);  // 4x density ≈ 4x blades, allow slack
}

TEST_CASE("blade count near plan default at density 1.25", "[blade-gen]") {
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.25, blades);
    // Plan target: ~400 blades per 1920 px at the v1 default density of 1.25.
    REQUIRE(blades.size() >= 350);
    REQUIRE(blades.size() <= 450);
}
