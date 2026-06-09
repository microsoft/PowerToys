// prng_tests.cpp
//
// Conformance + snapshot tests for the PRNG (architecture.md §3).

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"
#include "snapshot_data.h"

using namespace desktopgrass;
using namespace desktopgrass::test;

TEST_CASE("PRNG matches the canonical 16-output snapshot", "[prng][snapshot]") {
    Prng p;
    prng_init(p, CANONICAL_TEST_SEED);

    for (std::size_t i = 0; i < 16; ++i) {
        uint64_t v = prng_next_u64(p);
        INFO("index = " << i);
        REQUIRE(v == CANONICAL_PRNG_SNAPSHOT[i]);
    }
}

TEST_CASE("PRNG is deterministic for a given seed", "[prng]") {
    Prng a, b;
    prng_init(a, CANONICAL_TEST_SEED);
    prng_init(b, CANONICAL_TEST_SEED);
    for (int i = 0; i < 1000; ++i) {
        REQUIRE(prng_next_u64(a) == prng_next_u64(b));
    }
}

TEST_CASE("PRNG decorrelates seed=0 via splitmix64", "[prng]") {
    // seed == 0 must not produce a stuck-at-zero PRNG.
    Prng p;
    prng_init(p, 0);
    REQUIRE(p.state != 0);
    uint64_t a = prng_next_u64(p);
    uint64_t b = prng_next_u64(p);
    REQUIRE(a != 0);
    REQUIRE(b != 0);
    REQUIRE(a != b);
}

TEST_CASE("prng_next_unit is in [0, 1)", "[prng]") {
    Prng p;
    prng_init(p, CANONICAL_TEST_SEED);
    for (int i = 0; i < 10000; ++i) {
        double u = prng_next_unit(p);
        REQUIRE(u >= 0.0);
        REQUIRE(u <  1.0);
    }
}

TEST_CASE("prng_uniform stays within [lo, hi)", "[prng]") {
    Prng p;
    prng_init(p, 12345);
    for (int i = 0; i < 10000; ++i) {
        double v = prng_uniform(p, 8.0, 40.0);
        REQUIRE(v >= 8.0);
        REQUIRE(v <  40.0);
    }
}

TEST_CASE("prng_index is in [0, n)", "[prng]") {
    Prng p;
    prng_init(p, 42);
    bool sawZero = false;
    bool sawFive = false;
    for (int i = 0; i < 10000; ++i) {
        uint32_t v = prng_index(p, PALETTE_SIZE);
        REQUIRE(v < PALETTE_SIZE);
        if (v == 0) sawZero = true;
        if (v == 5) sawFive = true;
    }
    // Distribution sanity. Not strict — just confirms we cover both extremes.
    REQUIRE(sawZero);
    REQUIRE(sawFive);
}
