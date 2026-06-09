// scene_tests.cpp
//
// Scene infrastructure tests (architecture.md §13).
//
// Coverage:
//   * Scene enum discriminants match the spec ({Grass=0, Desert=1, Winter=2, Autumn=3}).
//   * sim_init defaults currentScene to SCENE_DEFAULT (= Grass).
//   * sim_set_scene does not perturb blade positions/dimensions/hues or
//     any non-scene PRNG stream.
//   * Per-scene palette tables are 6 entries each with full-alpha ARGB.
//   * SCENE_PALETTES[Grass] is bit-identical to the original §4 PALETTE.

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"
#include "snapshot_data.h"

#include <cstdint>

using namespace desktopgrass;

TEST_CASE("Scene enum has spec-locked discriminants", "[scene][enum]") {
    REQUIRE(static_cast<int>(Scene::Grass)  == 0);
    REQUIRE(static_cast<int>(Scene::Desert) == 1);
    REQUIRE(static_cast<int>(Scene::Winter) == 2);
    REQUIRE(static_cast<int>(Scene::Autumn) == 3);
    REQUIRE(static_cast<int>(Scene::Ocean)  == 4);
    REQUIRE(SCENE_COUNT == 5);
    REQUIRE(static_cast<int>(SCENE_DEFAULT) == 0);
}

TEST_CASE("sim_init defaults currentScene to Grass", "[scene][init]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    REQUIRE(sim.currentScene == Scene::Grass);
}

TEST_CASE("sim_set_scene does not perturb blade geometry or hues", "[scene][independence]") {
    Sim a = sim_init(CANONICAL_TEST_SEED, 1920.0, 1.0);
    Sim b = sim_init(CANONICAL_TEST_SEED, 1920.0, 1.0);

    // Same seed → same blades initially.
    REQUIRE(a.blades.size() == b.blades.size());
    REQUIRE(a.blades.size() == desktopgrass::test::CANONICAL_BLADE_COUNT);

    sim_set_scene(b, Scene::Desert);

    REQUIRE(b.currentScene == Scene::Desert);
    REQUIRE(a.currentScene == Scene::Grass);
    REQUIRE(a.blades.size() == b.blades.size());
    for (size_t i = 0; i < a.blades.size(); ++i) {
        REQUIRE(a.blades[i].baseX     == Approx(b.blades[i].baseX));
        REQUIRE(a.blades[i].height    == Approx(b.blades[i].height));
        REQUIRE(a.blades[i].thickness == Approx(b.blades[i].thickness));
        REQUIRE(a.blades[i].hue       == b.blades[i].hue);
    }
    // Desert cacti may mutate variant tags, but geometry and ambient PRNG stay untouched.
    REQUIRE(a.ambientPrng.state == b.ambientPrng.state);
    REQUIRE(a.nextAmbientGustTime == Approx(b.nextAmbientGustTime));
}

TEST_CASE("sim_set_scene round-trips through all values", "[scene][api]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Desert);  REQUIRE(sim.currentScene == Scene::Desert);
    sim_set_scene(sim, Scene::Winter);  REQUIRE(sim.currentScene == Scene::Winter);
    sim_set_scene(sim, Scene::Autumn);  REQUIRE(sim.currentScene == Scene::Autumn);
    sim_set_scene(sim, Scene::Ocean);   REQUIRE(sim.currentScene == Scene::Ocean);
    sim_set_scene(sim, Scene::Grass);   REQUIRE(sim.currentScene == Scene::Grass);
}

TEST_CASE("Per-scene palette tables are 6 ARGB entries with full alpha", "[scene][palette]") {
    for (int s = 0; s < SCENE_COUNT; ++s) {
        for (int i = 0; i < PALETTE_SIZE; ++i) {
            const uint32_t argb = SCENE_PALETTES[s][i];
            const uint8_t alpha = static_cast<uint8_t>((argb >> 24) & 0xFFu);
            REQUIRE(alpha == 0xFFu);
        }
    }
}

TEST_CASE("Grass scene palette is bit-identical to the original §4 PALETTE", "[scene][palette]") {
    for (int i = 0; i < PALETTE_SIZE; ++i) {
        REQUIRE(SCENE_PALETTES[static_cast<int>(Scene::Grass)][i] == PALETTE[i]);
    }
}

TEST_CASE("Desert palette values match spec §13", "[scene][palette]") {
    constexpr uint32_t expected[PALETTE_SIZE] = {
        0xFFC9A26Bu, 0xFFB48A56u, 0xFFD9B57Au,
        0xFF8F6E3Fu, 0xFFE6C896u, 0xFFA67843u,
    };
    for (int i = 0; i < PALETTE_SIZE; ++i) {
        REQUIRE(SCENE_PALETTES[static_cast<int>(Scene::Desert)][i] == expected[i]);
        REQUIRE(DESERT_PALETTE[i] == expected[i]);
    }
}

TEST_CASE("Winter palette values match spec §13", "[scene][palette]") {
    constexpr uint32_t expected[PALETTE_SIZE] = {
        0xFFE8EEF5u, 0xFFB7C4D2u, 0xFFCBD8E5u,
        0xFFD7E2EEu, 0xFFA8B7C6u, 0xFFEEF3F8u,
    };
    for (int i = 0; i < PALETTE_SIZE; ++i) {
        REQUIRE(SCENE_PALETTES[static_cast<int>(Scene::Winter)][i] == expected[i]);
        REQUIRE(WINTER_PALETTE[i] == expected[i]);
    }
}
