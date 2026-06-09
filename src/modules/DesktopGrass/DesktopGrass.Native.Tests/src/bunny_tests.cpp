// bunny_tests.cpp
//
// §18 Bunny critter tests. Mirrors Win2D BunnyTests.cs.

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

Sim build_grass_sim(uint64_t seed = CANONICAL_TEST_SEED) {
    Sim sim = sim_init(seed, Monitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Grass);
    sim_set_critter(sim, CritterKind::Bunny);
    return sim;
}

Entity bunny_entity(double x = 500.0, double vx = BUNNY_HOP_SPEED_MIN) {
    Entity e{};
    e.kind = EntityKind::Bunny;
    e.size = BUNNY_BODY_RADIUS;
    e.x = x;
    e.y = STRIP_HEIGHT + HEADROOM - BUNNY_BODY_HEIGHT - BUNNY_LEG_LENGTH;
    e.vx = vx;
    e.rotationSpeed = std::abs(vx);
    e.lifetime = -1.0;
    e.state = BUNNY_STATE_HOPPING;
    e.stateTimer = BUNNY_HOP_DURATION;
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

bool bunny_name_in_pool(const Entity& e) {
    if (e.nameIndex >= sizeof(BUNNY_NAME_POOL) / sizeof(BUNNY_NAME_POOL[0])) return false;
    const wchar_t* name = BUNNY_NAME_POOL[e.nameIndex];
    for (const wchar_t* candidate : BUNNY_NAME_POOL) {
        if (std::wcscmp(name, candidate) == 0) return true;
    }
    return false;
}

} // namespace

TEST_CASE("Bunny constants are pinned to spec values", "[bunny][constants]") {
    REQUIRE(BUNNY_COUNT_MIN == 1);
    REQUIRE(BUNNY_COUNT_MAX == 2);
    REQUIRE(BUNNY_HOP_SPEED_MIN == Approx(22.0));
    REQUIRE(BUNNY_HOP_SPEED_MAX == Approx(38.0));
    REQUIRE(BUNNY_BODY_RADIUS == Approx(8.0));
    REQUIRE(BUNNY_BODY_HEIGHT == Approx(6.5));
    REQUIRE(BUNNY_HEAD_RADIUS == Approx(4.2));
    REQUIRE(BUNNY_EAR_HEIGHT == Approx(9.0));
    REQUIRE(BUNNY_EAR_WIDTH == Approx(2.2));
    REQUIRE(BUNNY_EAR_SPACING == Approx(3.0));
    REQUIRE(BUNNY_LEG_LENGTH == Approx(4.0));
    REQUIRE(BUNNY_TAIL_RADIUS == Approx(2.4));
    REQUIRE(BUNNY_BODY_COLOR == 0xFF8A6A4Au);
    REQUIRE(BUNNY_BELLY_COLOR == 0xFFC4A98Du);
    REQUIRE(BUNNY_EAR_COLOR == 0xFF8A6A4Au);
    REQUIRE(BUNNY_EAR_INNER_COLOR == 0xFFD9A0A0u);
    REQUIRE(BUNNY_TAIL_COLOR == 0xFFF7F4EBu);
    REQUIRE(BUNNY_EYE_COLOR == 0xFF1A1208u);
    REQUIRE(BUNNY_NOSE_COLOR == 0xFF8A4040u);
    REQUIRE(BUNNY_STATE_HOPPING == 0);
    REQUIRE(BUNNY_STATE_GRAZING == 1);
    REQUIRE(BUNNY_STATE_IDLE == 2);
    REQUIRE(BUNNY_STATE_SLEEPING == 3);
    REQUIRE(BUNNY_STATE_STARTLED == 4);
    REQUIRE(BUNNY_HOP_DURATION == Approx(0.40));
    REQUIRE(BUNNY_HOP_HEIGHT == Approx(8.0));
    REQUIRE(BUNNY_HOP_GAP_MIN == Approx(0.05));
    REQUIRE(BUNNY_HOP_GAP_MAX == Approx(0.20));
    REQUIRE(BUNNY_GRAZE_DURATION_MIN == Approx(2.5));
    REQUIRE(BUNNY_GRAZE_DURATION_MAX == Approx(4.5));
    REQUIRE(BUNNY_IDLE_DURATION_MIN == Approx(2.0));
    REQUIRE(BUNNY_IDLE_DURATION_MAX == Approx(4.0));
    REQUIRE(BUNNY_SLEEP_DURATION_MIN == Approx(6.0));
    REQUIRE(BUNNY_SLEEP_DURATION_MAX == Approx(12.0));
    REQUIRE(BUNNY_GRAZE_PROBABILITY == Approx(0.55));
    REQUIRE(BUNNY_IDLE_PROBABILITY == Approx(0.30));
    REQUIRE(BUNNY_SLEEP_PROB == Approx(0.05));
    REQUIRE(BUNNY_STARTLE_RADIUS == Approx(90.0));
    REQUIRE(BUNNY_STARTLE_BOOST == Approx(2.0));
    REQUIRE(BUNNY_STARTLE_HOP_HEIGHT == Approx(14.0));
    REQUIRE(BUNNY_STARTLE_DURATION == Approx(3.0));
    REQUIRE(BUNNY_NOSE_TWITCH_FREQ == Approx(6.0));
    REQUIRE(BUNNY_NOSE_TWITCH_AMP == Approx(0.5));
    REQUIRE(BUNNY_EAR_WIGGLE_FREQ == Approx(1.2));
    REQUIRE(BUNNY_EAR_WIGGLE_AMP == Approx(0.20));
    REQUIRE(BUNNY_ZZZ_CYCLE_SEC == Approx(SHEEP_ZZZ_CYCLE_SEC));
    REQUIRE(BUNNY_ZZZ_RISE == Approx(SHEEP_ZZZ_RISE * 0.7));
    REQUIRE(BUNNY_ZZZ_SIZE_START == Approx(SHEEP_ZZZ_SIZE_START * 0.7));
    REQUIRE(BUNNY_ZZZ_SIZE_END == Approx(SHEEP_ZZZ_SIZE_END * 0.7));
    REQUIRE(sizeof(BUNNY_NAME_POOL) / sizeof(BUNNY_NAME_POOL[0]) == 12);
    REQUIRE(std::wcscmp(BUNNY_NAME_POOL[0], L"Clover") == 0);
    REQUIRE(std::wcscmp(BUNNY_NAME_POOL[11], L"Snowdrop") == 0);
}

TEST_CASE("Grass generation produces bunny count in range", "[bunny][gen]") {
    for (uint64_t i = 0; i < 128; ++i) {
        const uint64_t seed = CANONICAL_TEST_SEED + i * 0x9E3779B97F4A7C15ull;
        Sim sim = build_grass_sim(seed);
        const int bunnies = count_kind(sim, EntityKind::Bunny);
        REQUIRE(bunnies >= BUNNY_COUNT_MIN);
        REQUIRE(bunnies <= BUNNY_COUNT_MAX);
    }
}

TEST_CASE("Bunnies are Grass scene only", "[bunny][scene]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, Monitor1920, DEFAULT_DENSITY);
    sim_set_scene(sim, Scene::Desert);
    REQUIRE(count_kind(sim, EntityKind::Bunny) == 0);
    sim_set_scene(sim, Scene::Winter);
    REQUIRE(count_kind(sim, EntityKind::Bunny) == 0);
    sim_set_critter(sim, CritterKind::Bunny);
    REQUIRE(count_kind(sim, EntityKind::Bunny) == 0);
}

TEST_CASE("Generated bunnies have speed range", "[bunny][gen]") {
    Sim sim = build_grass_sim();
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Bunny) continue;
        REQUIRE(std::abs(e.vx) >= BUNNY_HOP_SPEED_MIN);
        REQUIRE(std::abs(e.vx) < BUNNY_HOP_SPEED_MAX);
        REQUIRE(e.rotationSpeed == Approx(std::abs(e.vx)));
    }
}

TEST_CASE("Generated bunnies have names in pool", "[bunny][gen]") {
    Sim sim = build_grass_sim();
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Bunny) continue;
        REQUIRE(bunny_name_in_pool(e));
    }
}

TEST_CASE("Bunny PRNG draw order follows sheep and cats", "[bunny][prng]") {
    Prng side;
    prng_init(side, CANONICAL_TEST_SEED ^ CRITTER_PRNG_SALT);

    Sim sim = build_grass_sim();

    const int sheepCount = prng_count(side, SHEEP_COUNT_MIN, SHEEP_COUNT_MAX);
    advance_sheep(side, sheepCount);
    const int catCount = prng_count(side, CAT_COUNT_MIN, CAT_COUNT_MAX);
    advance_cats(side, catCount);
    const int bunnyCount = prng_count(side, BUNNY_COUNT_MIN, BUNNY_COUNT_MAX);
    REQUIRE(count_kind(sim, EntityKind::Bunny) == bunnyCount);

    int seen = 0;
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Bunny) continue;
        const double margin = BUNNY_BODY_RADIUS + 8.0;
        const double xFrac = prng_uniform(side, 0.0, 1.0);
        const double expectedX = margin + xFrac * (Monitor1920 - 2.0 * margin);
        const uint64_t vxSign = prng_next_u64(side) & 1ull;
        const double expectedDir = vxSign != 0ull ? 1.0 : -1.0;
        const double expectedSpeed = prng_uniform(side, BUNNY_HOP_SPEED_MIN, BUNNY_HOP_SPEED_MAX);
        const uint8_t expectedName = static_cast<uint8_t>(prng_index(side,
            static_cast<uint32_t>(sizeof(BUNNY_NAME_POOL) / sizeof(BUNNY_NAME_POOL[0]))));
        REQUIRE(e.x == Approx(expectedX));
        REQUIRE(e.vx == Approx(expectedDir * expectedSpeed));
        REQUIRE(e.nameIndex == expectedName);
        ++seen;
    }
    REQUIRE(seen == bunnyCount);
}

TEST_CASE("Bunny edge bounce flips direction", "[bunny][motion]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, Monitor1920, DEFAULT_DENSITY);
    sim.currentScene = Scene::Desert;
    sim.entities.clear();
    Entity e = bunny_entity(Monitor1920 - (BUNNY_BODY_RADIUS + 2.0) + 0.1, BUNNY_HOP_SPEED_MIN);
    e.stateTimer = 10.0;
    sim.entities.push_back(e);

    sim_tick_entities(sim, 0.016);

    REQUIRE(sim.entities.front().vx < 0.0);
}

TEST_CASE("Bunny startle radius hops away and outside click does nothing", "[bunny][click]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, Monitor1920, DEFAULT_DENSITY);
    sim.entities.clear();
    Entity e = bunny_entity(500.0, -BUNNY_HOP_SPEED_MIN);
    e.state = BUNNY_STATE_IDLE;
    e.stateTimer = 3.0;
    sim.entities.push_back(e);

    sim_apply_click(sim, click_event(500.0 - 20.0, e.y));
    REQUIRE(sim.entities.front().state == BUNNY_STATE_STARTLED);
    REQUIRE(sim.entities.front().vx > 0.0);
    REQUIRE(sim.entities.front().stateTimer == Approx(BUNNY_STARTLE_DURATION));

    Entity after = sim.entities.front();
    const double vxBefore = after.vx;
    const uint8_t stateBefore = after.state;
    sim_apply_click(sim, click_event(after.x + BUNNY_STARTLE_RADIUS + 10.0, after.y));
    REQUIRE(sim.entities.front().state == stateBefore);
    REQUIRE(sim.entities.front().vx == Approx(vxBefore));
}

TEST_CASE("Bunny wakes from sleep on startle", "[bunny][click]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, Monitor1920, DEFAULT_DENSITY);
    sim.entities.clear();
    Entity e = bunny_entity(500.0, BUNNY_HOP_SPEED_MIN);
    e.state = BUNNY_STATE_SLEEPING;
    e.stateTimer = 10.0;
    sim.entities.push_back(e);

    sim_apply_click(sim, click_event(e.x + 10.0, e.y));

    REQUIRE(sim.entities.front().state == BUNNY_STATE_STARTLED);
    REQUIRE(sim.entities.front().state != BUNNY_STATE_SLEEPING);
    REQUIRE(sim.entities.front().vx < 0.0);
}

TEST_CASE("Bunny hop arc is bounded", "[bunny][motion]") {
    REQUIRE(bunny_hop_y_offset(0.0, false) == Approx(0.0));
    REQUIRE(bunny_hop_y_offset(BUNNY_HOP_DURATION, false) == Approx(0.0).margin(1e-12));
    const double peak = bunny_hop_y_offset(BUNNY_HOP_DURATION * 0.5, false);
    REQUIRE(peak > 0.0);
    REQUIRE(peak <= BUNNY_HOP_HEIGHT);
}

TEST_CASE("Bunny state transition probabilities are stable", "[bunny][state]") {
    Prng p;
    prng_init(p, CANONICAL_TEST_SEED ^ CRITTER_PRNG_SALT);
    constexpr int N = 10000;
    int graze = 0;
    int idle = 0;
    int sleep = 0;
    for (int i = 0; i < N; ++i) {
        const uint8_t state = bunny_choose_rest_state(p);
        if (state == BUNNY_STATE_GRAZING) ++graze;
        else if (state == BUNNY_STATE_IDLE) ++idle;
        else if (state == BUNNY_STATE_SLEEPING) ++sleep;
    }

    const double sleepProb = BUNNY_SLEEP_PROB;
    const double activeWeight = BUNNY_GRAZE_PROBABILITY + BUNNY_IDLE_PROBABILITY;
    const double expectedGraze = (1.0 - sleepProb) * BUNNY_GRAZE_PROBABILITY / activeWeight;
    const double expectedIdle = (1.0 - sleepProb) * BUNNY_IDLE_PROBABILITY / activeWeight;
    REQUIRE(static_cast<double>(sleep) / N == Approx(sleepProb).margin(0.02));
    REQUIRE(static_cast<double>(graze) / N == Approx(expectedGraze).margin(0.02));
    REQUIRE(static_cast<double>(idle) / N == Approx(expectedIdle).margin(0.02));
}

TEST_CASE("Bunny sleep probability is stable", "[bunny][state]") {
    constexpr int N = 20000;
    Prng p;
    prng_init(p, CANONICAL_TEST_SEED ^ 0x1234ull);
    int sleep = 0;
    for (int i = 0; i < N; ++i) {
        if (bunny_choose_rest_state(p) == BUNNY_STATE_SLEEPING) ++sleep;
    }
    REQUIRE(static_cast<double>(sleep) / N == Approx(BUNNY_SLEEP_PROB).margin(0.02));
}
