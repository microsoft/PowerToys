// desert_tests.cpp - §14 Desert scene cacti + tumbleweeds.

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"
#include "snapshot_data.h"

#include <cmath>
#include <cstddef>
#include <vector>

using namespace desktopgrass;

namespace {

constexpr double kMonitor1920 = 1920.0;

struct ExpectedCactus {
    std::size_t slotIndex = 0;
    uint8_t type = 0;
    double height = 0.0;
    double width = 0.0;
    int8_t armSide = +1;
};

ExpectedCactus first_expected_cactus(std::size_t bladeCount) {
    Prng p;
    prng_init(p, CANONICAL_TEST_SEED ^ CACTUS_PRNG_SALT);

    for (std::size_t i = 0; i < bladeCount; ++i) {
        const double r = prng_uniform(p, 0.0, 1.0);
        if (r >= CACTUS_PROBABILITY) continue;

        ExpectedCactus expected{};
        expected.slotIndex = i;
        expected.height = prng_uniform(p, CACTUS_HEIGHT_MIN, CACTUS_HEIGHT_MAX);
        expected.width = prng_uniform(p, CACTUS_WIDTH_MIN, CACTUS_WIDTH_MAX);

        const double armDraw = prng_uniform(p, 0.0, 1.0);
        const double noArmThreshold = 1.0 - CACTUS_ARM_PROBABILITY;
        const double twoArmThreshold = noArmThreshold + CACTUS_TWO_ARM_PROBABILITY * CACTUS_ARM_PROBABILITY;
        if (armDraw < noArmThreshold) {
            expected.type = 0;
        } else if (armDraw < twoArmThreshold) {
            expected.type = 2;
        } else {
            expected.type = 1;
            expected.armSide = prng_uniform(p, 0.0, 1.0) < 0.5
                ? static_cast<int8_t>(-1)
                : static_cast<int8_t>(+1);
        }
        if (expected.height < CACTUS_ARM_MIN_HEIGHT) {
            expected.type = 0;
            expected.armSide = +1;
        }
        return expected;
    }

    FAIL("canonical seed produced no cactus slot");
    return {};
}

int expected_tumbleweed_count(double monitorWidth) {
    if (monitorWidth < 480.0) return 0;
    int count = static_cast<int>(std::floor(monitorWidth / 1920.0 * static_cast<double>(TUMBLEWEED_COUNT_PER_1920DIP)));
    return count < 1 ? 1 : count;
}

} // anonymous

TEST_CASE("Desert constants are pinned", "[desert][constants]") {
    REQUIRE(CACTUS_PROBABILITY == Approx(0.005));
    REQUIRE(CACTUS_HEIGHT_MIN == Approx(30.0));
    REQUIRE(CACTUS_HEIGHT_MAX == Approx(70.0));
    REQUIRE(CACTUS_COLOR == 0xFF2D7A2Du);
    REQUIRE(TUMBLEWEED_COUNT_PER_1920DIP == 4);
    REQUIRE(TUMBLEWEED_SPEED_MAX == Approx(72.0));
    REQUIRE(TUMBLEWEED_PRNG_SALT == 0x7B0117CA7B0117CAull);
}

TEST_CASE("sim_set_scene Desert clears entities and generates cacti", "[desert][scene]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    Entity fake{};
    fake.kind = EntityKind::Snowflake;
    sim.entities.push_back(fake);

    sim_set_scene(sim, Scene::Desert);

    REQUIRE(sim.currentScene == Scene::Desert);
    REQUIRE(sim.entities.size() == static_cast<std::size_t>(expected_tumbleweed_count(kMonitor1920)));
    for (const Entity& e : sim.entities) REQUIRE(e.kind == EntityKind::Tumbleweed);

    std::size_t cactusCount = 0;
    for (const Blade& b : sim.blades) if (b.isCactus) ++cactusCount;
    REQUIRE(cactusCount >= 1);
    REQUIRE(cactusCount <= 10);
}

TEST_CASE("First cactus matches the spec-derived PRNG snapshot", "[desert][cactus]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    const ExpectedCactus expected = first_expected_cactus(sim.blades.size());

    sim_set_scene(sim, Scene::Desert);

    REQUIRE(expected.slotIndex < sim.blades.size());
    const Blade& b = sim.blades[expected.slotIndex];
    REQUIRE(b.isCactus);
    REQUIRE(b.cactusType == expected.type);
    REQUIRE(b.cactusHeight == Approx(expected.height).margin(1e-12));
    REQUIRE(b.cactusWidth == Approx(expected.width).margin(1e-12));
    if (expected.type == 1) REQUIRE(b.cactusArmSide == expected.armSide);
}

TEST_CASE("Grass scene restores original flower and mushroom slot variants", "[desert][restore]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    const ExpectedCactus expected = first_expected_cactus(sim.blades.size());
    REQUIRE(expected.slotIndex < sim.blades.size());

    Blade& target = sim.blades[expected.slotIndex];
    target.isFlower = true;
    target.isMushroom = true;
    target.originalIsFlower = true;
    target.originalIsMushroom = true;

    sim_set_scene(sim, Scene::Desert);
    REQUIRE(sim.blades[expected.slotIndex].isCactus);
    REQUIRE_FALSE(sim.blades[expected.slotIndex].isFlower);
    REQUIRE_FALSE(sim.blades[expected.slotIndex].isMushroom);

    sim_set_scene(sim, Scene::Grass);
    REQUIRE_FALSE(sim.blades[expected.slotIndex].isCactus);
    REQUIRE(sim.blades[expected.slotIndex].isFlower);
    REQUIRE(sim.blades[expected.slotIndex].isMushroom);
}

TEST_CASE("Desert generates the expected tumbleweed count", "[desert][tumbleweed]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Desert);

    const int expected = expected_tumbleweed_count(kMonitor1920);
    REQUIRE(expected >= 1);
    REQUIRE(sim.entities.size() == static_cast<std::size_t>(expected));
}

TEST_CASE("First tumbleweed matches the spec-derived PRNG snapshot", "[desert][tumbleweed]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Desert);
    REQUIRE_FALSE(sim.entities.empty());

    Prng p;
    prng_init(p, CANONICAL_TEST_SEED ^ TUMBLEWEED_PRNG_SALT);
    const double expectedSize = prng_uniform(p, TUMBLEWEED_SIZE_MIN, TUMBLEWEED_SIZE_MAX);
    const double expectedX = prng_uniform(p, 0.0, kMonitor1920);
    const double expectedY = sim.windowHeight - prng_uniform(p, TUMBLEWEED_Y_OFFSET_MIN, TUMBLEWEED_Y_OFFSET_MAX);
    const double speed = prng_uniform(p, TUMBLEWEED_SPEED_MIN, TUMBLEWEED_SPEED_MAX);
    const double direction = prng_uniform(p, 0.0, 1.0) < 0.5 ? -1.0 : 1.0;
    const double expectedVx = direction * speed;
    const double expectedRotation = prng_uniform(p, 0.0, 6.28318530717958647692);

    const Entity& e = sim.entities[0];
    REQUIRE(e.kind == EntityKind::Tumbleweed);
    REQUIRE(e.size == Approx(expectedSize).margin(1e-12));
    REQUIRE(e.x == Approx(expectedX).margin(1e-12));
    REQUIRE(e.y == Approx(expectedY).margin(1e-12));
    REQUIRE(e.vx == Approx(expectedVx).margin(1e-12));
    REQUIRE(e.rotation == Approx(expectedRotation).margin(1e-12));
    REQUIRE(e.rotationSpeed == Approx(expectedVx / expectedSize).margin(1e-12));
}

TEST_CASE("Tumbleweed respawns at the opposite edge when off-screen", "[desert][tumbleweed]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Desert);
    REQUIRE_FALSE(sim.entities.empty());

    sim.entities[0].x = sim.monitorWidth + 100.0;
    sim_tick_entities(sim, 0.0);

    const Entity& e = sim.entities[0];
    REQUIRE(e.kind == EntityKind::Tumbleweed);
    REQUIRE(e.x == Approx(-e.size).margin(1e-12));
    REQUIRE(e.vx > 0.0);
}

TEST_CASE("Tumbleweed hops above its baseline then settles", "[desert][tumbleweed]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Desert);
    REQUIRE_FALSE(sim.entities.empty());

    double yBase = sim.entities[0].altitudeAnchor;
    REQUIRE(sim.entities[0].y == Approx(yBase).margin(1e-9)); // starts grounded

    double minY = sim.entities[0].y;
    for (int i = 0; i < 900; ++i) {
        sim_tick_entities(sim, 1.0 / 60.0);
        Entity& t = sim.entities[0];
        // Pin x on-screen so it doesn't roll off and respawn mid-test.
        if (t.x < 50.0) t.x = 50.0;
        if (t.x > sim.monitorWidth - 50.0) t.x = sim.monitorWidth - 50.0;
        yBase = t.altitudeAnchor;
        minY = std::min(minY, t.y);
        REQUIRE(t.y <= yBase + 1e-6); // never sinks below the baseline
    }

    REQUIRE(minY < yBase - 1.0); // it left the ground at least once
}

TEST_CASE("Desert scene leaves the canonical first blade geometry bit-identical", "[desert][snapshot]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, kMonitor1920, 1.0);
    REQUIRE(sim.blades.size() == desktopgrass::test::CANONICAL_BLADE_COUNT);

    sim_set_scene(sim, Scene::Desert);

    const Blade& first = sim.blades[0];
    const auto& expected = desktopgrass::test::CANONICAL_FIRST_10[0];
    REQUIRE(first.baseX == Approx(expected.baseX).margin(1e-12));
    REQUIRE(first.height == Approx(expected.height).margin(1e-12));
    REQUIRE(first.thickness == Approx(expected.thickness).margin(1e-12));
    REQUIRE(first.hue == expected.hue);
    REQUIRE(first.swayPhaseOffset == Approx(expected.sway).margin(1e-12));
    REQUIRE(first.stiffness == Approx(expected.stiffness).margin(1e-12));
}
