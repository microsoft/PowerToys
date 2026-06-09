// hedgehog_tests.cpp
//
// §17.9 Hedgehog critter tests. Mirrors Win2D HedgehogTests.cs.

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"

#include <algorithm>
#include <cmath>
#include <cwchar>

using namespace desktopgrass;

namespace {

constexpr double Monitor1920 = 1920.0;

int count_kind(const Sim& sim, EntityKind kind) {
    return static_cast<int>(std::count_if(sim.entities.begin(), sim.entities.end(),
        [kind](const Entity& e) { return e.kind == kind; }));
}

Sim build_sim(uint64_t seed = CANONICAL_TEST_SEED) {
    return sim_init(seed, Monitor1920, DEFAULT_DENSITY);
}

Sim build_grass_sim(uint64_t seed = CANONICAL_TEST_SEED) {
    Sim sim = build_sim(seed);
    sim_set_scene(sim, Scene::Grass);
    sim_set_critter(sim, CritterKind::Bunny);
    return sim;
}

Entity hedgehog_entity(double x = 500.0, double vx = HEDGEHOG_WALK_SPEED_MIN) {
    Entity e{};
    e.kind = EntityKind::Hedgehog;
    e.size = HEDGEHOG_BODY_RADIUS;
    e.x = x;
    e.y = STRIP_HEIGHT + HEADROOM - HEDGEHOG_BODY_HEIGHT - HEDGEHOG_LEG_LENGTH;
    e.vx = vx;
    e.vy = 0.0;
    e.rotationSpeed = std::abs(vx);
    e.lifetime = -1.0;
    e.state = HEDGEHOG_STATE_WALKING;
    e.stateTimer = HEDGEHOG_WALK_DURATION_MIN;
    e.previousState = HEDGEHOG_STATE_WALKING;
    return e;
}

InputEvent click_event(double x, double y) {
    InputEvent ev{};
    ev.type = EventType::Click;
    ev.x = x;
    ev.y = y;
    ev.time = 0.0;
    return ev;
}

int prng_count(Prng& side, int minCount, int maxCount) {
    const double draw = prng_uniform(side, static_cast<double>(minCount), static_cast<double>(maxCount + 1));
    int count = static_cast<int>(std::floor(draw));
    if (count < minCount) count = minCount;
    if (count > maxCount) count = maxCount;
    return count;
}

void advance_sheep(Prng& side, int count) {
    for (int i = 0; i < count; ++i) {
        const double margin = SHEEP_BODY_RADIUS + 8.0;
        (void)prng_uniform(side, margin, Monitor1920 - margin);
        (void)prng_uniform(side, SHEEP_WALK_SPEED_MIN, SHEEP_WALK_SPEED_MAX);
        (void)prng_uniform(side, 0.0, 1.0);
        (void)prng_next_u32(side);
        (void)prng_uniform(side, SHEEP_WALK_DURATION_MIN, SHEEP_WALK_DURATION_MAX);
        (void)prng_index(side, static_cast<uint32_t>(sizeof(SHEEP_NAME_POOL) / sizeof(SHEEP_NAME_POOL[0])));
    }
}

void advance_cats(Prng& side, int count) {
    for (int i = 0; i < count; ++i) {
        const double margin = CAT_BODY_RADIUS + 8.0;
        (void)prng_uniform(side, margin, Monitor1920 - margin);
        (void)prng_uniform(side, CAT_WALK_SPEED_MIN, CAT_WALK_SPEED_MAX);
        (void)prng_uniform(side, 0.0, 1.0);
        (void)prng_next_u32(side);
        (void)prng_uniform(side, CAT_WALK_DURATION_MIN, CAT_WALK_DURATION_MAX);
        (void)prng_index(side, static_cast<uint32_t>(sizeof(CAT_NAME_POOL) / sizeof(CAT_NAME_POOL[0])));
        (void)prng_index(side, static_cast<uint32_t>(CAT_COAT_VARIANT_COUNT));
    }
}

void advance_bunnies(Prng& side, int count) {
    for (int i = 0; i < count; ++i) {
        (void)prng_uniform(side, 0.0, 1.0);
        (void)prng_next_u64(side);
        (void)prng_uniform(side, BUNNY_HOP_SPEED_MIN, BUNNY_HOP_SPEED_MAX);
        (void)prng_index(side, static_cast<uint32_t>(sizeof(BUNNY_NAME_POOL) / sizeof(BUNNY_NAME_POOL[0])));
    }
}

bool hedgehog_name_in_pool(const Entity& e) {
    if (e.nameIndex >= sizeof(HEDGEHOG_NAME_POOL) / sizeof(HEDGEHOG_NAME_POOL[0])) return false;
    const wchar_t* name = HEDGEHOG_NAME_POOL[e.nameIndex];
    for (const wchar_t* candidate : HEDGEHOG_NAME_POOL) {
        if (std::wcscmp(name, candidate) == 0) return true;
    }
    return false;
}

} // namespace

TEST_CASE("Hedgehog constants are pinned to spec values", "[hedgehog][constants]") {
    REQUIRE(HEDGEHOG_COUNT_MIN == 0);
    REQUIRE(HEDGEHOG_COUNT_MAX == 1);
    REQUIRE(HEDGEHOG_COUNT_PROBABILITY == Approx(0.55));
    REQUIRE(HEDGEHOG_WALK_SPEED_MIN == Approx(4.0));
    REQUIRE(HEDGEHOG_WALK_SPEED_MAX == Approx(8.0));
    REQUIRE(HEDGEHOG_BODY_RADIUS == Approx(9.0));
    REQUIRE(HEDGEHOG_BODY_HEIGHT == Approx(5.5));
    REQUIRE(HEDGEHOG_HEAD_RADIUS == Approx(3.6));
    REQUIRE(HEDGEHOG_NOSE_RADIUS == Approx(0.8));
    REQUIRE(HEDGEHOG_LEG_LENGTH == Approx(2.5));
    REQUIRE(HEDGEHOG_SPIKE_COUNT == 14);
    REQUIRE(HEDGEHOG_SPIKE_LENGTH == Approx(3.0));
    REQUIRE(HEDGEHOG_SPIKE_WIDTH == Approx(1.4));
    REQUIRE(HEDGEHOG_SPIKE_ARC_START_DEG == Approx(-20.0));
    REQUIRE(HEDGEHOG_SPIKE_ARC_END_DEG == Approx(200.0));
    REQUIRE(HEDGEHOG_BODY_COLOR == 0xFF5C4633u);
    REQUIRE(HEDGEHOG_SPIKE_COLOR == 0xFF3A2A1Fu);
    REQUIRE(HEDGEHOG_SPIKE_TIP_COLOR == 0xFF1E150Eu);
    REQUIRE(HEDGEHOG_NOSE_COLOR == 0xFF1A1208u);
    REQUIRE(HEDGEHOG_EYE_COLOR == 0xFF1A1208u);
    REQUIRE(HEDGEHOG_STATE_WALKING == 0);
    REQUIRE(HEDGEHOG_STATE_SNUFFLING == 1);
    REQUIRE(HEDGEHOG_STATE_IDLE == 2);
    REQUIRE(HEDGEHOG_STATE_SLEEPING == 3);
    REQUIRE(HEDGEHOG_STATE_CURLED == 4);
    REQUIRE(HEDGEHOG_WALK_DURATION_MIN == Approx(6.0));
    REQUIRE(HEDGEHOG_WALK_DURATION_MAX == Approx(12.0));
    REQUIRE(HEDGEHOG_SNUFFLE_DURATION_MIN == Approx(3.0));
    REQUIRE(HEDGEHOG_SNUFFLE_DURATION_MAX == Approx(6.0));
    REQUIRE(HEDGEHOG_IDLE_DURATION_MIN == Approx(1.5));
    REQUIRE(HEDGEHOG_IDLE_DURATION_MAX == Approx(3.0));
    REQUIRE(HEDGEHOG_SLEEP_DURATION_MIN == Approx(10.0));
    REQUIRE(HEDGEHOG_SLEEP_DURATION_MAX == Approx(25.0));
    REQUIRE(HEDGEHOG_CURL_DURATION_MIN == Approx(3.0));
    REQUIRE(HEDGEHOG_CURL_DURATION_MAX == Approx(5.5));
    REQUIRE(HEDGEHOG_SNUFFLE_PROBABILITY == Approx(0.55));
    REQUIRE(HEDGEHOG_IDLE_PROBABILITY == Approx(0.30));
    REQUIRE(HEDGEHOG_SLEEP_PROB == Approx(0.50));
    REQUIRE(HEDGEHOG_STARTLE_RADIUS == Approx(70.0));
    REQUIRE(HEDGEHOG_SNUFFLE_HEAD_FREQ == Approx(5.0));
    REQUIRE(HEDGEHOG_SNUFFLE_HEAD_AMP == Approx(0.7));
    REQUIRE(HEDGEHOG_WADDLE_FREQ == Approx(4.0));
    REQUIRE(HEDGEHOG_WADDLE_AMP == Approx(0.8));
    REQUIRE(HEDGEHOG_ZZZ_CYCLE_SEC == Approx(SHEEP_ZZZ_CYCLE_SEC));
    REQUIRE(HEDGEHOG_ZZZ_RISE == Approx(SHEEP_ZZZ_RISE * 0.5));
    REQUIRE(HEDGEHOG_ZZZ_SIZE_START == Approx(SHEEP_ZZZ_SIZE_START * 0.6));
    REQUIRE(HEDGEHOG_ZZZ_SIZE_END == Approx(SHEEP_ZZZ_SIZE_END * 0.6));
    REQUIRE(sizeof(HEDGEHOG_NAME_POOL) / sizeof(HEDGEHOG_NAME_POOL[0]) == 12);
    REQUIRE(std::wcscmp(HEDGEHOG_NAME_POOL[0], L"Bristle") == 0);
    REQUIRE(std::wcscmp(HEDGEHOG_NAME_POOL[11], L"Burdock") == 0);
}

TEST_CASE("Hedgehog count distribution is probabilistic rare sighting", "[hedgehog][gen]") {
    constexpr int N = 1000;
    int present = 0;
    for (uint64_t i = 0; i < N; ++i) {
        const uint64_t seed = CANONICAL_TEST_SEED + i * 0x9E3779B97F4A7C15ull;
        Sim sim = build_grass_sim(seed);
        const int count = count_kind(sim, EntityKind::Hedgehog);
        REQUIRE(count >= HEDGEHOG_COUNT_MIN);
        REQUIRE(count <= HEDGEHOG_COUNT_MAX);
        present += count;
    }
    REQUIRE(static_cast<double>(present) / N == Approx(HEDGEHOG_COUNT_PROBABILITY).margin(0.05));
}

TEST_CASE("Hedgehogs are Grass scene only", "[hedgehog][scene]") {
    Sim sim = build_sim();
    sim_set_scene(sim, Scene::Desert);
    REQUIRE(count_kind(sim, EntityKind::Hedgehog) == 0);
    sim_set_scene(sim, Scene::Winter);
    REQUIRE(count_kind(sim, EntityKind::Hedgehog) == 0);
}

TEST_CASE("Generated hedgehogs have speed range", "[hedgehog][gen]") {
    bool sawHedgehog = false;
    for (uint64_t i = 0; i < 128; ++i) {
        Sim sim = build_grass_sim(CANONICAL_TEST_SEED + i * 0xD1B54A32D192ED03ull);
        for (const Entity& e : sim.entities) {
            if (e.kind != EntityKind::Hedgehog) continue;
            sawHedgehog = true;
            REQUIRE(std::abs(e.vx) >= HEDGEHOG_WALK_SPEED_MIN);
            REQUIRE(std::abs(e.vx) <= HEDGEHOG_WALK_SPEED_MAX);
            REQUIRE(e.rotationSpeed == Approx(std::abs(e.vx)));
        }
    }
    REQUIRE(sawHedgehog);
}

TEST_CASE("Generated hedgehogs have names in pool", "[hedgehog][gen]") {
    bool sawHedgehog = false;
    for (uint64_t i = 0; i < 128; ++i) {
        Sim sim = build_grass_sim(CANONICAL_TEST_SEED + i * 0x94D049BB133111EBull);
        for (const Entity& e : sim.entities) {
            if (e.kind != EntityKind::Hedgehog) continue;
            sawHedgehog = true;
            REQUIRE(hedgehog_name_in_pool(e));
        }
    }
    REQUIRE(sawHedgehog);
}

TEST_CASE("Hedgehog PRNG draw order follows sheep cats and bunnies", "[hedgehog][prng]") {
    Prng side;
    prng_init(side, CANONICAL_TEST_SEED ^ CRITTER_PRNG_SALT);
    Sim sim = build_grass_sim();

    const int sheepCount = prng_count(side, SHEEP_COUNT_MIN, SHEEP_COUNT_MAX);
    advance_sheep(side, sheepCount);
    const int catCount = prng_count(side, CAT_COUNT_MIN, CAT_COUNT_MAX);
    advance_cats(side, catCount);
    const int bunnyCount = prng_count(side, BUNNY_COUNT_MIN, BUNNY_COUNT_MAX);
    advance_bunnies(side, bunnyCount);

    const double hasDraw = prng_uniform(side, 0.0, 1.0);
    const int hedgehogCount = hasDraw < HEDGEHOG_COUNT_PROBABILITY ? 1 : 0;
    REQUIRE(count_kind(sim, EntityKind::Hedgehog) == hedgehogCount);

    int seen = 0;
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Hedgehog) continue;
        const double margin = HEDGEHOG_BODY_RADIUS + 8.0;
        const double xFrac = prng_uniform(side, 0.0, 1.0);
        const double expectedX = margin + xFrac * (Monitor1920 - 2.0 * margin);
        const uint64_t vxSign = prng_next_u64(side) & 1ull;
        const double expectedDir = vxSign != 0ull ? 1.0 : -1.0;
        const double expectedSpeed = prng_uniform(side, HEDGEHOG_WALK_SPEED_MIN, HEDGEHOG_WALK_SPEED_MAX);
        const uint8_t expectedName = static_cast<uint8_t>(prng_index(side,
            static_cast<uint32_t>(sizeof(HEDGEHOG_NAME_POOL) / sizeof(HEDGEHOG_NAME_POOL[0]))));
        REQUIRE(e.x == Approx(expectedX));
        REQUIRE(e.vx == Approx(expectedDir * expectedSpeed));
        REQUIRE(e.nameIndex == expectedName);
        ++seen;
    }
    REQUIRE(seen == hedgehogCount);
}

TEST_CASE("Hedgehog edge bounce flips direction", "[hedgehog][motion]") {
    Sim sim = build_sim();
    sim.currentScene = Scene::Desert;
    sim.entities.clear();
    Entity e = hedgehog_entity(Monitor1920 - (HEDGEHOG_BODY_RADIUS + 2.0) + 0.1, HEDGEHOG_WALK_SPEED_MIN);
    e.stateTimer = 10.0;
    sim.entities.push_back(e);

    sim_tick_entities(sim, 0.016);

    REQUIRE(sim.entities.front().vx < 0.0);
}

TEST_CASE("Hedgehog startle radius curls without flipping vx", "[hedgehog][click]") {
    Sim sim = build_sim();
    sim.entities.clear();
    Entity e = hedgehog_entity(500.0, -HEDGEHOG_WALK_SPEED_MIN);
    e.state = HEDGEHOG_STATE_WALKING;
    e.stateTimer = 10.0;
    sim.entities.push_back(e);

    sim_apply_click(sim, click_event(e.x + 10.0, e.y));
    REQUIRE(sim.entities.front().state == HEDGEHOG_STATE_CURLED);
    REQUIRE(sim.entities.front().vx == Approx(-HEDGEHOG_WALK_SPEED_MIN));
    REQUIRE(sim.entities.front().stateTimer >= HEDGEHOG_CURL_DURATION_MIN);
    REQUIRE(sim.entities.front().stateTimer <= HEDGEHOG_CURL_DURATION_MAX);

    Sim outside = build_sim();
    outside.entities.clear();
    Entity far = hedgehog_entity(500.0, HEDGEHOG_WALK_SPEED_MIN);
    outside.entities.push_back(far);
    sim_apply_click(outside, click_event(far.x + HEDGEHOG_STARTLE_RADIUS + 10.0, far.y));
    REQUIRE(outside.entities.front().state == HEDGEHOG_STATE_WALKING);
    REQUIRE(outside.entities.front().vx == Approx(HEDGEHOG_WALK_SPEED_MIN));
}

TEST_CASE("Hedgehog curl auto uncurls to previous state", "[hedgehog][state]") {
    Sim sim = build_sim();
    sim.entities.clear();
    Entity e = hedgehog_entity(500.0, HEDGEHOG_WALK_SPEED_MIN);
    e.state = HEDGEHOG_STATE_IDLE;
    e.stateTimer = 2.5;
    sim.entities.push_back(e);

    sim_apply_click(sim, click_event(e.x, e.y));
    REQUIRE(sim.entities.front().state == HEDGEHOG_STATE_CURLED);
    sim_tick_entities(sim, HEDGEHOG_CURL_DURATION_MAX + 0.1);

    REQUIRE(sim.entities.front().state == HEDGEHOG_STATE_IDLE);
    REQUIRE(sim.entities.front().vx == Approx(HEDGEHOG_WALK_SPEED_MIN));
}

TEST_CASE("Hedgehog wakes from sleep on startle and does not resume sleep", "[hedgehog][click]") {
    Sim sim = build_sim();
    sim.entities.clear();
    Entity e = hedgehog_entity(500.0, HEDGEHOG_WALK_SPEED_MIN);
    e.state = HEDGEHOG_STATE_SLEEPING;
    e.stateTimer = 10.0;
    sim.entities.push_back(e);

    sim_apply_click(sim, click_event(e.x + 10.0, e.y));
    REQUIRE(sim.entities.front().state == HEDGEHOG_STATE_CURLED);
    REQUIRE(sim.entities.front().state != HEDGEHOG_STATE_SLEEPING);
    sim_tick_entities(sim, HEDGEHOG_CURL_DURATION_MAX + 0.1);

    REQUIRE(sim.entities.front().state == HEDGEHOG_STATE_WALKING);
    REQUIRE(sim.entities.front().state != HEDGEHOG_STATE_SLEEPING);
}

TEST_CASE("Hedgehog state transition probabilities are stable", "[hedgehog][state]") {
    Prng p;
    prng_init(p, CANONICAL_TEST_SEED ^ CRITTER_PRNG_SALT);
    constexpr int N = 10000;
    int snuffle = 0;
    int idle = 0;
    int sleep = 0;
    for (int i = 0; i < N; ++i) {
        const uint8_t state = hedgehog_choose_rest_state(p);
        if (state == HEDGEHOG_STATE_SNUFFLING) ++snuffle;
        else if (state == HEDGEHOG_STATE_IDLE) ++idle;
        else if (state == HEDGEHOG_STATE_SLEEPING) ++sleep;
    }

    const double sleepProb = HEDGEHOG_SLEEP_PROB;
    const double activeWeight = HEDGEHOG_SNUFFLE_PROBABILITY + HEDGEHOG_IDLE_PROBABILITY;
    const double expectedSnuffle = (1.0 - sleepProb) * HEDGEHOG_SNUFFLE_PROBABILITY / activeWeight;
    const double expectedIdle = (1.0 - sleepProb) * HEDGEHOG_IDLE_PROBABILITY / activeWeight;
    REQUIRE(static_cast<double>(sleep) / N == Approx(sleepProb).margin(0.02));
    REQUIRE(static_cast<double>(snuffle) / N == Approx(expectedSnuffle).margin(0.02));
    REQUIRE(static_cast<double>(idle) / N == Approx(expectedIdle).margin(0.02));
}

TEST_CASE("Hedgehog sleep probability is stable", "[hedgehog][state]") {
    constexpr int N = 20000;
    Prng p;
    prng_init(p, CANONICAL_TEST_SEED ^ 0x1234ull);
    int sleep = 0;
    for (int i = 0; i < N; ++i) {
        if (hedgehog_choose_rest_state(p) == HEDGEHOG_STATE_SLEEPING) ++sleep;
    }
    REQUIRE(static_cast<double>(sleep) / N == Approx(HEDGEHOG_SLEEP_PROB).margin(0.02));
}

TEST_CASE("Hedgehog has no active interaction states", "[hedgehog][state]") {
    Prng p;
    prng_init(p, CANONICAL_TEST_SEED ^ 0xCAFEull);
    for (int i = 0; i < 1000; ++i) {
        const uint8_t state = hedgehog_choose_rest_state(p);
        REQUIRE((state == HEDGEHOG_STATE_SNUFFLING
              || state == HEDGEHOG_STATE_IDLE
              || state == HEDGEHOG_STATE_SLEEPING));
    }

    Sim sim = build_sim();
    sim.entities.clear();
    Entity e = hedgehog_entity(500.0, HEDGEHOG_WALK_SPEED_MIN);
    e.stateTimer = 10.0;
    sim.entities.push_back(e);
    sim_tick_entities(sim, 0.016);
    REQUIRE(sim.entities.front().state == HEDGEHOG_STATE_WALKING);
    REQUIRE(std::abs(sim.entities.front().vx) == Approx(HEDGEHOG_WALK_SPEED_MIN));
}
