// cat_coat_tests.cpp
//
// §17 Cat coat palette and deterministic coat variant tests. Mirrors Win2D CatCoatTests.cs.

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"

#include <cmath>
#include <cstdint>

using namespace desktopgrass;

namespace {

int count_kind(const Sim& sim, EntityKind kind) {
    int n = 0;
    for (const Entity& e : sim.entities) if (e.kind == kind) ++n;
    return n;
}

constexpr CatCoatPalette EXPECTED_CAT_COATS[CAT_COAT_VARIANT_COUNT] = {
    { 0xFF6B6259u, 0xFF3D3733u, 0xFF6B6259u, 0xFF3D3733u, 0xFF1A1614u },
    { 0xFFD89A6Fu, 0xFFA56B40u, 0xFFD89A6Fu, 0xFFA56B40u, 0xFF2B1A0Eu },
    { 0xFF2A2522u, 0xFF140F0Cu, 0xFF2A2522u, 0xFF140F0Cu, 0xFFD9B85Bu },
    { 0xFFEDE9E1u, 0xFFBDB7ABu, 0xFFEDE9E1u, 0xFFBDB7ABu, 0xFF1F1817u },
    { 0xFF7A5F3Cu, 0xFF4E3F26u, 0xFF7A5F3Cu, 0xFF4E3F26u, 0xFF1A1108u },
    { 0xFFC9B898u, 0xFF8E7F6Bu, 0xFFC9B898u, 0xFF8E7F6Bu, 0xFF2E251Du },
};

uint8_t next_cat_coat_after_prefix(Prng& side) {
    (void)prng_uniform(side, CAT_BODY_RADIUS + 8.0, 1920.0 - (CAT_BODY_RADIUS + 8.0));
    (void)prng_uniform(side, CAT_WALK_SPEED_MIN, CAT_WALK_SPEED_MAX);
    (void)prng_uniform(side, 0.0, 1.0);
    (void)prng_next_u32(side);
    (void)prng_uniform(side, CAT_WALK_DURATION_MIN, CAT_WALK_DURATION_MAX);
    (void)prng_index(side, static_cast<uint32_t>(sizeof(CAT_NAME_POOL) / sizeof(CAT_NAME_POOL[0])));
    return static_cast<uint8_t>(prng_index(side, static_cast<uint32_t>(CAT_COAT_VARIANT_COUNT)));
}

} // namespace

TEST_CASE("Cat coat variant count is pinned", "[cat][coat][constants]") {
    REQUIRE(CAT_COAT_VARIANT_COUNT == 6);
}

TEST_CASE("Cat coat palette zero matches backward-compatible aliases", "[cat][coat][constants]") {
    REQUIRE(CAT_COAT_PALETTES[0].body == CAT_BODY_COLOR);
    REQUIRE(CAT_COAT_PALETTES[0].leg  == CAT_LEG_COLOR);
    REQUIRE(CAT_COAT_PALETTES[0].face == CAT_FACE_COLOR);
    REQUIRE(CAT_COAT_PALETTES[0].ear  == CAT_EAR_COLOR);
    REQUIRE(CAT_COAT_PALETTES[0].ink  == CAT_INK_COLOR);
}

TEST_CASE("All cat coat palettes are pinned", "[cat][coat][constants]") {
    for (int i = 0; i < CAT_COAT_VARIANT_COUNT; ++i) {
        CAPTURE(i);
        REQUIRE(CAT_COAT_PALETTES[i].body == EXPECTED_CAT_COATS[i].body);
        REQUIRE(CAT_COAT_PALETTES[i].leg  == EXPECTED_CAT_COATS[i].leg);
        REQUIRE(CAT_COAT_PALETTES[i].face == EXPECTED_CAT_COATS[i].face);
        REQUIRE(CAT_COAT_PALETTES[i].ear  == EXPECTED_CAT_COATS[i].ear);
        REQUIRE(CAT_COAT_PALETTES[i].ink  == EXPECTED_CAT_COATS[i].ink);
    }
}

TEST_CASE("Cat coat body colors are distinct", "[cat][coat][constants]") {
    for (int i = 0; i < CAT_COAT_VARIANT_COUNT; ++i) {
        for (int j = i + 1; j < CAT_COAT_VARIANT_COUNT; ++j) {
            CAPTURE(i);
            CAPTURE(j);
            REQUIRE(CAT_COAT_PALETTES[i].body != CAT_COAT_PALETTES[j].body);
        }
    }
}

TEST_CASE("Canonical cat flock pins deterministic coat variants", "[cat][coat][gen]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Cat);

    const uint8_t expectedCoats[] = { 1 };
    int seen = 0;
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Cat) continue;
        REQUIRE(seen < static_cast<int>(sizeof(expectedCoats) / sizeof(expectedCoats[0])));
        REQUIRE(e.coatVariantIndex == expectedCoats[seen]);
        ++seen;
    }
    REQUIRE(seen == static_cast<int>(sizeof(expectedCoats) / sizeof(expectedCoats[0])));
}

TEST_CASE("Cat coat PRNG draw follows nameIndex", "[cat][coat][prng]") {
    Prng side;
    prng_init(side, CANONICAL_TEST_SEED ^ CRITTER_PRNG_SALT);

    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Cat);

    const double countDraw = prng_uniform(side, CAT_COUNT_MIN, CAT_COUNT_MAX + 1);
    int expectedCount = static_cast<int>(std::floor(countDraw));
    if (expectedCount < CAT_COUNT_MIN) expectedCount = CAT_COUNT_MIN;
    if (expectedCount > CAT_COUNT_MAX) expectedCount = CAT_COUNT_MAX;
    REQUIRE(count_kind(sim, EntityKind::Cat) == expectedCount);

    int seen = 0;
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Cat) continue;
        const uint8_t expectedCoat = next_cat_coat_after_prefix(side);
        REQUIRE(e.coatVariantIndex == expectedCoat);
        ++seen;
    }
    REQUIRE(seen == expectedCount);
}

TEST_CASE("Generated cat coats always stay within palette range", "[cat][coat][gen]") {
    for (uint64_t i = 0; i < 128; ++i) {
        const uint64_t seed = CANONICAL_TEST_SEED + i * 0x9E3779B97F4A7C15ull;
        Sim sim = sim_init(seed, 1920.0, DEFAULT_DENSITY);
        sim_set_critter(sim, CritterKind::Cat);

        int seen = 0;
        for (const Entity& e : sim.entities) {
            if (e.kind != EntityKind::Cat) continue;
            REQUIRE(e.coatVariantIndex < CAT_COAT_VARIANT_COUNT);
            ++seen;
        }
        REQUIRE(seen >= CAT_COUNT_MIN);
        REQUIRE(seen <= CAT_COUNT_MAX);
    }
}

TEST_CASE("Sheep keep default coat variant zero", "[cat][coat][sheep]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Sheep);

    REQUIRE(count_kind(sim, EntityKind::Sheep) >= SHEEP_COUNT_MIN);
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Sheep) continue;
        REQUIRE(e.coatVariantIndex == 0);
    }
}

TEST_CASE("Fixed cat count coat PRNG skips only the count draw", "[cat][coat][count][prng]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Cat);
    sim_set_critter_count(sim, 3);
    REQUIRE(count_kind(sim, EntityKind::Cat) == 3);

    Prng side;
    prng_init(side, CANONICAL_TEST_SEED ^ CRITTER_PRNG_SALT);

    int seen = 0;
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Cat) continue;
        const uint8_t expectedCoat = next_cat_coat_after_prefix(side);
        REQUIRE(e.coatVariantIndex == expectedCoat);
        ++seen;
    }
    REQUIRE(seen == 3);
}
