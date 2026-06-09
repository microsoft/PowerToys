// flower_tests.cpp
//
// Tests for §5 flower stream + §7 head-render contract.

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"

#include <cmath>
#include <vector>

using namespace desktopgrass;

TEST_CASE("flower stream is deterministic for a given seed", "[flowers]") {
    std::vector<Blade> a, b;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, a);
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, b);
    REQUIRE(a.size() == b.size());
    for (std::size_t i = 0; i < a.size(); ++i) {
        REQUIRE(a[i].isFlower           == b[i].isFlower);
        REQUIRE(a[i].flowerHeadColorIdx == b[i].flowerHeadColorIdx);
        REQUIRE(a[i].flowerHeadRadius   == b[i].flowerHeadRadius);
        REQUIRE(a[i].heightBonus        == b[i].heightBonus);
    }
}

TEST_CASE("flower count is within 3-sigma of FLOWER_PROBABILITY", "[flowers]") {
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);
    REQUIRE(blades.size() > 100);

    std::size_t flowerCount = 0;
    for (const Blade& b : blades) if (b.isFlower) ++flowerCount;

    const double n   = static_cast<double>(blades.size());
    const double p   = FLOWER_PROBABILITY;
    const double mu  = n * p;
    const double sd  = std::sqrt(n * p * (1.0 - p));
    // 3-sigma tolerance keeps this test stable across spec-conformant
    // PRNG sequences. For seed=0x6B6173746F, n=321 we expect ~12.84 with
    // sd≈3.51, so [2,24] is the acceptable range.
    REQUIRE(flowerCount >= static_cast<std::size_t>(std::floor(mu - 3.0 * sd)));
    REQUIRE(flowerCount <= static_cast<std::size_t>(std::ceil(mu + 3.0 * sd)));
}

TEST_CASE("flower stream does not perturb the main stream", "[flowers][conformance]") {
    // Regenerate blades and assert the main-stream fields match the
    // canonical snapshot. This is implicitly covered by blade_gen_tests
    // (the first/last 10 still match), but pin it here for clarity.
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);
    REQUIRE(blades.size() > 0);
    REQUIRE(blades[0].baseX == Approx(4.941073726820111).margin(1e-12));
    REQUIRE(blades[0].height == Approx(24.469991818248864).margin(1e-12));
}
