// mushroom_tests.cpp
//
// Tests for §5 mushroom stream + §7 mushroom-render contract.

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"

#include <algorithm>
#include <cmath>
#include <vector>

using namespace desktopgrass;

TEST_CASE("mushroom stream is deterministic for a given seed", "[mushrooms]") {
    std::vector<Blade> a, b;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, a);
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, b);
    REQUIRE(a.size() == b.size());
    for (std::size_t i = 0; i < a.size(); ++i) {
        REQUIRE(a[i].isMushroom            == b[i].isMushroom);
        REQUIRE(a[i].mushroomCapColorIdx   == b[i].mushroomCapColorIdx);
        REQUIRE(a[i].mushroomCapWidth      == b[i].mushroomCapWidth);
        REQUIRE(a[i].mushroomCapHeight     == b[i].mushroomCapHeight);
        REQUIRE(a[i].mushroomStemHeight    == b[i].mushroomStemHeight);
        REQUIRE(a[i].mushroomStemThickness == b[i].mushroomStemThickness);
    }
}

TEST_CASE("mushroom count is within 3-sigma of MUSHROOM_PROBABILITY", "[mushrooms]") {
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);
    REQUIRE(blades.size() > 100);

    std::size_t mushroomCount = 0;
    for (const Blade& b : blades) if (b.isMushroom) ++mushroomCount;

    const double n   = static_cast<double>(blades.size());
    const double p   = MUSHROOM_PROBABILITY;
    const double mu  = n * p;
    const double sd  = std::sqrt(n * p * (1.0 - p));
    // 3-sigma tolerance keeps this test stable across spec-conformant
    // PRNG sequences. For seed=0x6B6173746F, n=321 we expect ~8.03 with
    // sd≈2.80, so the inclusive 3-sigma range is roughly [0, 17].
    const double lo  = std::max(0.0, std::floor(mu - 3.0 * sd));
    REQUIRE(mushroomCount >= static_cast<std::size_t>(lo));
    REQUIRE(mushroomCount <= static_cast<std::size_t>(std::ceil(mu + 3.0 * sd)));
}

TEST_CASE("mushroom stream does not perturb the main stream", "[mushrooms][conformance]") {
    // The mushroom stream is independent (seed ^ MUSHROOM_PRNG_SALT) so
    // the main-stream first-blade values must still match the canonical.
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);
    REQUIRE(blades.size() > 0);
    REQUIRE(blades[0].baseX == Approx(4.941073726820111).margin(1e-12));
    REQUIRE(blades[0].height == Approx(24.469991818248864).margin(1e-12));
}

TEST_CASE("non-mushroom blades have zero mushroom fields", "[mushrooms]") {
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);
    for (const Blade& b : blades) {
        if (!b.isMushroom) {
            REQUIRE(b.mushroomCapColorIdx   == 0);
            REQUIRE(b.mushroomCapWidth      == 0.0);
            REQUIRE(b.mushroomCapHeight     == 0.0);
            REQUIRE(b.mushroomStemHeight    == 0.0);
            REQUIRE(b.mushroomStemThickness == 0.0);
        }
    }
}

TEST_CASE("mushroom field ranges respect spec", "[mushrooms]") {
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);
    for (const Blade& b : blades) {
        if (b.isMushroom) {
            REQUIRE(b.mushroomCapColorIdx < MUSHROOM_PALETTE_SIZE);
            REQUIRE(b.mushroomCapWidth      >= MUSHROOM_CAP_WIDTH_MIN);
            REQUIRE(b.mushroomCapWidth      <  MUSHROOM_CAP_WIDTH_MAX);
            REQUIRE(b.mushroomCapHeight     >= MUSHROOM_CAP_HEIGHT_MIN);
            REQUIRE(b.mushroomCapHeight     <  MUSHROOM_CAP_HEIGHT_MAX);
            REQUIRE(b.mushroomStemHeight    >= MUSHROOM_STEM_HEIGHT_MIN);
            REQUIRE(b.mushroomStemHeight    <  MUSHROOM_STEM_HEIGHT_MAX);
            REQUIRE(b.mushroomStemThickness >= MUSHROOM_STEM_THICKNESS_MIN);
            REQUIRE(b.mushroomStemThickness <  MUSHROOM_STEM_THICKNESS_MAX);
        }
    }
}
