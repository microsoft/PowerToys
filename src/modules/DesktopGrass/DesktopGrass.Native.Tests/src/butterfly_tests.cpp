// butterfly_tests.cpp - §17.6 ambient Butterfly tests.

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"

#include <algorithm>
#include <cmath>

using namespace desktopgrass;

namespace {
constexpr double Monitor1920 = 1920.0;

int count_kind(const Sim& sim, EntityKind kind) {
    return static_cast<int>(std::count_if(sim.entities.begin(), sim.entities.end(),
        [kind](const Entity& e) { return e.kind == kind; }));
}

Sim build_grass_sim(uint64_t seed = CANONICAL_TEST_SEED) {
    Sim sim = sim_init(seed, Monitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Grass);
    return sim;
}

int prng_count(Prng& side, int minCount, int maxCount) {
    const double draw = prng_uniform(side, static_cast<double>(minCount), static_cast<double>(maxCount + 1));
    int count = static_cast<int>(std::floor(draw));
    if (count < minCount) count = minCount;
    if (count > maxCount) count = maxCount;
    return count;
}

const Entity* first_butterfly(const Sim& sim) {
    for (const Entity& e : sim.entities) if (e.kind == EntityKind::Butterfly) return &e;
    return nullptr;
}
} // namespace

TEST_CASE("Butterfly constants are pinned to spec values", "[butterfly][constants]") {
    REQUIRE(BUTTERFLY_COUNT_MIN == 2);
    REQUIRE(BUTTERFLY_COUNT_MAX == 4);
    REQUIRE(BUTTERFLY_SPEED_MIN == Approx(18.0));
    REQUIRE(BUTTERFLY_SPEED_MAX == Approx(32.0));
    REQUIRE(BUTTERFLY_BODY_LENGTH == Approx(2.4));
    REQUIRE(BUTTERFLY_WING_RADIUS == Approx(3.5));
    REQUIRE(BUTTERFLY_WING_OFFSET == Approx(2.2));
    REQUIRE(BUTTERFLY_FLUTTER_FREQ == Approx(16.0));
    REQUIRE(BUTTERFLY_FLUTTER_MIN_SCALE == Approx(0.20));
    REQUIRE(BUTTERFLY_MEANDER_FREQ_Y == Approx(0.8));
    REQUIRE(BUTTERFLY_MEANDER_AMP_Y == Approx(16.0));
    REQUIRE(BUTTERFLY_MEANDER_FREQ_X == Approx(0.5));
    REQUIRE(BUTTERFLY_MEANDER_AMP_X == Approx(0.4));
    REQUIRE(BUTTERFLY_ALTITUDE_MIN == Approx(18.0));
    REQUIRE(BUTTERFLY_ALTITUDE_MAX == Approx(70.0));
    REQUIRE(BUTTERFLY_BODY_COLOR == 0xFF2A2018u);
    REQUIRE(BUTTERFLY_COLOR_COUNT == 5);
    REQUIRE(BUTTERFLY_PRNG_SALT == 0xB07DEF1E0001ull);
}

TEST_CASE("Grass generation produces butterfly count in range", "[butterfly][gen]") {
    for (uint64_t i = 0; i < 128; ++i) {
        const uint64_t seed = CANONICAL_TEST_SEED + i * 0x9E3779B97F4A7C15ull;
        Sim sim = build_grass_sim(seed);
        REQUIRE(count_kind(sim, EntityKind::Butterfly) >= BUTTERFLY_COUNT_MIN);
        REQUIRE(count_kind(sim, EntityKind::Butterfly) <= BUTTERFLY_COUNT_MAX);
    }
}

TEST_CASE("Butterflies are Grass scene only", "[butterfly][scene]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, Monitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Desert);
    REQUIRE(count_kind(sim, EntityKind::Butterfly) == 0);
    sim_set_scene(sim, Scene::Winter);
    REQUIRE(count_kind(sim, EntityKind::Butterfly) == 0);
}

TEST_CASE("Generated butterflies have speed altitude and color ranges", "[butterfly][gen]") {
    Sim sim = build_grass_sim();
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Butterfly) continue;
        REQUIRE(e.baseSpeed >= BUTTERFLY_SPEED_MIN);
        REQUIRE(e.baseSpeed <  BUTTERFLY_SPEED_MAX);
        REQUIRE(e.altitudeAnchor >= BUTTERFLY_ALTITUDE_MIN);
        REQUIRE(e.altitudeAnchor <  BUTTERFLY_ALTITUDE_MAX);
        REQUIRE(e.colorVariant < BUTTERFLY_COLOR_COUNT);
    }
}

TEST_CASE("Butterfly PRNG draw order matches side stream", "[butterfly][prng]") {
    Prng side;
    prng_init(side, CANONICAL_TEST_SEED ^ BUTTERFLY_PRNG_SALT);
    Sim sim = build_grass_sim();

    const int expectedCount = prng_count(side, BUTTERFLY_COUNT_MIN, BUTTERFLY_COUNT_MAX);
    REQUIRE(count_kind(sim, EntityKind::Butterfly) == expectedCount);

    int seen = 0;
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Butterfly) continue;
        const double xFrac = prng_uniform(side, 0.0, 1.0);
        const double yFrac = prng_uniform(side, 0.0, 1.0);
        const uint64_t vxSign = prng_next_u64(side) & 1ull;
        const double expectedDir = vxSign != 0ull ? 1.0 : -1.0;
        const double expectedSpeed = prng_uniform(side, BUTTERFLY_SPEED_MIN, BUTTERFLY_SPEED_MAX);
        const uint8_t expectedColor = static_cast<uint8_t>(prng_index(side, static_cast<uint32_t>(BUTTERFLY_COLOR_COUNT)));
        const double expectedPhaseY = prng_uniform(side, 0.0, 2.0 * 3.14159265358979323846);
        const double expectedPhaseX = prng_uniform(side, 0.0, 2.0 * 3.14159265358979323846);
        const double expectedAltitude = BUTTERFLY_ALTITUDE_MIN + yFrac * (BUTTERFLY_ALTITUDE_MAX - BUTTERFLY_ALTITUDE_MIN);
        const double expectedVx = expectedDir * expectedSpeed * (1.0 + BUTTERFLY_MEANDER_AMP_X * std::sin(expectedPhaseX));

        REQUIRE(e.x == Approx(xFrac * Monitor1920));
        REQUIRE(e.altitudeAnchor == Approx(expectedAltitude));
        REQUIRE(e.baseSpeed == Approx(expectedSpeed));
        REQUIRE(e.vx == Approx(expectedVx));
        REQUIRE(e.colorVariant == expectedColor);
        REQUIRE(e.phaseY == Approx(expectedPhaseY));
        REQUIRE(e.phaseX == Approx(expectedPhaseX));
        ++seen;
    }
    REQUIRE(seen == expectedCount);
}

TEST_CASE("Butterfly edge wrap preserves altitude anchor", "[butterfly][motion]") {
    Sim sim = build_grass_sim();
    auto it = std::find_if(sim.entities.begin(), sim.entities.end(), [](const Entity& e) { return e.kind == EntityKind::Butterfly; });
    REQUIRE(it != sim.entities.end());
    const double margin = BUTTERFLY_WING_OFFSET + BUTTERFLY_WING_RADIUS;
    it->x = Monitor1920 + margin + 1.0;
    it->vx = std::abs(it->vx);
    const double altitude = it->altitudeAnchor;
    sim.currentScene = Scene::Desert;

    sim_tick_entities(sim, 0.016);

    REQUIRE(it->x == Approx(-margin));
    REQUIRE(it->altitudeAnchor == Approx(altitude));
}

TEST_CASE("Butterflies do not interact with cuts or pets", "[butterfly][interaction]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, Monitor1920, DEFAULT_DENSITY);
    sim.entities.clear();
    Entity butterfly{};
    butterfly.kind = EntityKind::Butterfly;
    butterfly.x = 500.0;
    butterfly.y = sim.windowHeight - STRIP_HEIGHT - 5.0;
    butterfly.vx = BUTTERFLY_SPEED_MIN;
    butterfly.baseSpeed = BUTTERFLY_SPEED_MIN;
    butterfly.altitudeAnchor = BUTTERFLY_ALTITUDE_MIN;
    butterfly.lifetime = -1.0;
    sim.entities.push_back(butterfly);
    Entity sheep{};
    sheep.kind = EntityKind::Sheep;
    sheep.x = butterfly.x;
    sheep.y = sim.windowHeight - SHEEP_BODY_HEIGHT - SHEEP_LEG_LENGTH;
    sheep.vx = SHEEP_WALK_SPEED_MIN;
    sheep.state = SHEEP_STATE_WALKING;
    sheep.stateTimer = 10.0;
    sim.entities.push_back(sheep);

    InputEvent ev{};
    ev.type = EventType::Click;
    ev.x = butterfly.x;
    ev.y = butterfly.y;
    sim_apply_click(sim, ev);

    REQUIRE(sim.entities[0].kind == EntityKind::Butterfly);
    REQUIRE(sim.entities[0].baseSpeed == Approx(BUTTERFLY_SPEED_MIN));
    REQUIRE(sim.entities[1].state == SHEEP_STATE_WALKING);
    for (const Blade& b : sim.blades) REQUIRE(b.cutAnimStart < 0.0);
}

TEST_CASE("Butterfly wing scale stays within flutter bounds", "[butterfly][render]") {
    for (int i = 0; i < 200; ++i) {
        const double t = i * 0.05;
        const double scale = butterfly_wing_scale(t, 1.3);
        REQUIRE(scale >= BUTTERFLY_FLUTTER_MIN_SCALE);
        REQUIRE(scale <= 1.0);
    }
}

