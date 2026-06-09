// pine_tests.cpp - §15.1 Winter pine trees (slot-bound, mirrors §14 cacti).

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"
#include "snapshot_data.h"

#include <cmath>
#include <cstddef>
#include <vector>

using namespace desktopgrass;

namespace {

constexpr double kMonitor1920 = 1920.0;

struct ExpectedTree {
    std::size_t slotIndex = 0;
    uint8_t variant = 0;
    double height = 0.0;
    double width = 0.0;
    int tierCount = 0;
};

ExpectedTree first_expected_tree(std::size_t bladeCount) {
    Prng p;
    prng_init(p, CANONICAL_TEST_SEED ^ PINE_PRNG_SALT);

    for (std::size_t i = 0; i < bladeCount; ++i) {
        const double r = prng_uniform(p, 0.0, 1.0);
        if (r >= PINE_PROBABILITY) continue;

        ExpectedTree expected{};
        expected.slotIndex = i;
        const double variantDraw = prng_uniform(p, 0.0, 1.0);
        expected.variant = variantDraw < BIRCH_VARIANT_PROBABILITY ? 1 : 0;
        expected.height = prng_uniform(p, PINE_HEIGHT_MIN, PINE_HEIGHT_MAX);
        if (expected.variant == 1) {
            expected.width = prng_uniform(p, BIRCH_TRUNK_WIDTH_MIN, BIRCH_TRUNK_WIDTH_MAX);
        } else {
            expected.width = prng_uniform(p, PINE_WIDTH_MIN, PINE_WIDTH_MAX);
        }
        const double tierDraw = prng_uniform(p,
            static_cast<double>(PINE_TIER_COUNT_MIN),
            static_cast<double>(PINE_TIER_COUNT_MAX + 1));
        int tiers = static_cast<int>(std::floor(tierDraw));
        if (tiers < PINE_TIER_COUNT_MIN) tiers = PINE_TIER_COUNT_MIN;
        if (tiers > PINE_TIER_COUNT_MAX) tiers = PINE_TIER_COUNT_MAX;
        expected.tierCount = tiers;
        return expected;
    }

    FAIL("canonical seed produced no tree slot");
    return {};
}

} // anonymous

TEST_CASE("Pine constants are pinned", "[pine][constants]") {
    REQUIRE(PINE_PROBABILITY == Approx(0.0075));
    REQUIRE(PINE_HEIGHT_MIN == Approx(45.0));
    REQUIRE(PINE_HEIGHT_MAX == Approx(90.0));
    REQUIRE(PINE_WIDTH_MIN  == Approx(28.0));
    REQUIRE(PINE_WIDTH_MAX  == Approx(48.0));
    REQUIRE(PINE_TIER_COUNT_MIN == 2);
    REQUIRE(PINE_TIER_COUNT_MAX == 4);
    REQUIRE(PINE_TIP_TAPER == Approx(0.25));
    REQUIRE(PINE_TIER_OVERLAP == Approx(0.15));
    REQUIRE(PINE_SNOW_CAP_FRACTION == Approx(0.30));
    REQUIRE(PINE_COLOR == 0xFF1B5E20u);
    REQUIRE(PINE_PRNG_SALT == 0x50494E4550494E45ull);
}

TEST_CASE("Birch constants are pinned", "[pine][birch][constants]") {
    REQUIRE(BIRCH_VARIANT_PROBABILITY == Approx(0.30));
    REQUIRE(BIRCH_TRUNK_WIDTH_MIN == Approx(4.0));
    REQUIRE(BIRCH_TRUNK_WIDTH_MAX == Approx(7.0));
    REQUIRE(BIRCH_BARK_MARK_COUNT == 5);
    REQUIRE(BIRCH_BARK_MARK_LENGTH_FRAC == Approx(0.50));
    REQUIRE(BIRCH_BRANCH_COUNT == 6);
    REQUIRE(BIRCH_SNOW_CAP_FRACTION == Approx(0.18));
    REQUIRE(BIRCH_BARK_COLOR == 0xFFEFEFE6u);
    REQUIRE(BIRCH_MARK_COLOR == 0xFF2A2A28u);
}

TEST_CASE("sim_set_scene Winter promotes some slots to trees", "[pine][scene]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Winter);

    REQUIRE(sim.currentScene == Scene::Winter);
    std::size_t treeCount = 0;
    for (const Blade& b : sim.blades) {
        if (b.isPine) {
            ++treeCount;
            REQUIRE(b.pineTierCount >= PINE_TIER_COUNT_MIN);
            REQUIRE(b.pineTierCount <= PINE_TIER_COUNT_MAX);
            REQUIRE(b.pineHeight >= PINE_HEIGHT_MIN);
            REQUIRE(b.pineHeight <= PINE_HEIGHT_MAX);
            const double widthMin = (b.treeVariant == 1) ? BIRCH_TRUNK_WIDTH_MIN : PINE_WIDTH_MIN;
            const double widthMax = (b.treeVariant == 1) ? BIRCH_TRUNK_WIDTH_MAX : PINE_WIDTH_MAX;
            REQUIRE(b.pineWidth >= widthMin);
            REQUIRE(b.pineWidth <= widthMax);
        }
    }
    REQUIRE(treeCount >= 1);
    REQUIRE(treeCount <= 25);
}

TEST_CASE("First tree matches the spec-derived PRNG snapshot", "[pine][snapshot]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    const ExpectedTree expected = first_expected_tree(sim.blades.size());

    sim_set_scene(sim, Scene::Winter);

    REQUIRE(expected.slotIndex < sim.blades.size());
    const Blade& b = sim.blades[expected.slotIndex];
    REQUIRE(b.isPine);
    REQUIRE(b.treeVariant == expected.variant);
    REQUIRE(b.pineHeight == Approx(expected.height).margin(1e-12));
    REQUIRE(b.pineWidth  == Approx(expected.width).margin(1e-12));
    REQUIRE(b.pineTierCount == expected.tierCount);
}

TEST_CASE("Grass scene restores tree slots to vanilla variants", "[pine][restore]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    const ExpectedTree expected = first_expected_tree(sim.blades.size());
    REQUIRE(expected.slotIndex < sim.blades.size());

    Blade& target = sim.blades[expected.slotIndex];
    target.isFlower = true;
    target.isMushroom = true;
    target.originalIsFlower = true;
    target.originalIsMushroom = true;

    sim_set_scene(sim, Scene::Winter);
    REQUIRE(sim.blades[expected.slotIndex].isPine);
    REQUIRE_FALSE(sim.blades[expected.slotIndex].isFlower);
    REQUIRE_FALSE(sim.blades[expected.slotIndex].isMushroom);

    sim_set_scene(sim, Scene::Grass);
    REQUIRE_FALSE(sim.blades[expected.slotIndex].isPine);
    REQUIRE(sim.blades[expected.slotIndex].treeVariant == 0);
    REQUIRE(sim.blades[expected.slotIndex].isFlower);
    REQUIRE(sim.blades[expected.slotIndex].isMushroom);
}

TEST_CASE("Winter produces both pine and birch variants over canonical seed", "[pine][birch]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Winter);

    std::size_t pineCount = 0;
    std::size_t birchCount = 0;
    for (const Blade& b : sim.blades) {
        if (!b.isPine) continue;
        if (b.treeVariant == 0) {
            ++pineCount;
            REQUIRE(b.pineWidth >= PINE_WIDTH_MIN);
            REQUIRE(b.pineWidth <= PINE_WIDTH_MAX);
        } else {
            REQUIRE(b.treeVariant == 1);
            ++birchCount;
            REQUIRE(b.pineWidth >= BIRCH_TRUNK_WIDTH_MIN);
            REQUIRE(b.pineWidth <= BIRCH_TRUNK_WIDTH_MAX);
        }
    }
    REQUIRE(pineCount >= 1);
    REQUIRE(birchCount >= 1);
}

TEST_CASE("Winter scene suppresses mushrooms on every slot", "[pine][winter][mushroom]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    // Pre-mark a handful of slots as mushrooms; Winter must clear them all.
    for (std::size_t i = 0; i < sim.blades.size(); i += 17) {
        sim.blades[i].isMushroom = true;
        sim.blades[i].originalIsMushroom = true;
    }

    sim_set_scene(sim, Scene::Winter);

    for (const Blade& b : sim.blades) REQUIRE_FALSE(b.isMushroom);

    // Switching back to Grass must restore the original mushroom flags.
    sim_set_scene(sim, Scene::Grass);
    REQUIRE(sim.blades[0].isMushroom == sim.blades[0].originalIsMushroom);
}

TEST_CASE("Winter grass height scale is pinned", "[pine][winter][scale]") {
    REQUIRE(WINTER_GRASS_HEIGHT_SCALE == Approx(0.5));
}

TEST_CASE("Tree depth constants are pinned", "[pine][depth][constants]") {
    REQUIRE(TREE_BACKGROUND_PROBABILITY == Approx(0.45));
    REQUIRE(TREE_BG_SCALE == Approx(0.62));
    REQUIRE(TREE_BG_OPACITY == Approx(0.78f));
}

TEST_CASE("Winter mixes foreground and background trees", "[pine][depth]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Winter);

    std::size_t fg = 0;
    std::size_t bg = 0;
    for (const Blade& b : sim.blades) {
        if (!b.isPine) continue;
        if (b.treeBackground) ++bg; else ++fg;
    }
    REQUIRE(fg >= 1);
    REQUIRE(bg >= 1);
}

TEST_CASE("Tree depth assignment is deterministic across re-entry", "[pine][depth]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Winter);

    std::vector<bool> firstPass;
    for (const Blade& b : sim.blades) {
        if (b.isPine) firstPass.push_back(b.treeBackground);
    }

    // Leaving and re-entering Winter must reproduce the same depth layout.
    sim_set_scene(sim, Scene::Grass);
    sim_set_scene(sim, Scene::Winter);

    std::size_t idx = 0;
    for (const Blade& b : sim.blades) {
        if (!b.isPine) continue;
        REQUIRE(idx < firstPass.size());
        REQUIRE(b.treeBackground == firstPass[idx]);
        ++idx;
    }
    REQUIRE(idx == firstPass.size());
}

TEST_CASE("Non-winter scenes clear the tree background flag", "[pine][depth][restore]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Winter);
    sim_set_scene(sim, Scene::Grass);
    for (const Blade& b : sim.blades) REQUIRE_FALSE(b.treeBackground);
}

TEST_CASE("Winter scene leaves the canonical first blade geometry bit-identical", "[pine][snapshot]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, 1.0);
    REQUIRE(sim.blades.size() == desktopgrass::test::CANONICAL_BLADE_COUNT);

    sim_set_scene(sim, Scene::Winter);

    const Blade& first = sim.blades[0];
    const auto& expected = desktopgrass::test::CANONICAL_FIRST_10[0];
    REQUIRE(first.baseX == Approx(expected.baseX).margin(1e-12));
    REQUIRE(first.height == Approx(expected.height).margin(1e-12));
    REQUIRE(first.thickness == Approx(expected.thickness).margin(1e-12));
    REQUIRE(first.hue == expected.hue);
    REQUIRE(first.swayPhaseOffset == Approx(expected.sway).margin(1e-12));
    REQUIRE(first.stiffness == Approx(expected.stiffness).margin(1e-12));
}
