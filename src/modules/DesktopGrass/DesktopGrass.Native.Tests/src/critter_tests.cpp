// critter_tests.cpp
//
// Critter subsystem tests (architecture.md §13.3 / §16). Orthogonal to Scene.
//
// Coverage:
//   * CritterKind discriminants are spec-locked ({None=0, Sheep=1}).
//   * EntityKind::Sheep == 3 (added after the original {None, Tumbleweed,
//     Snowflake} enum).
//   * SHEEP_* and CRITTER_* constants are pinned to spec values.
//   * sim_init defaults sim.currentCritter to None (no sheep until the user
//     opts in via tray).
//   * sim_set_critter(Sheep) on CANONICAL_TEST_SEED + 1920 produces
//     deterministic count K ∈ [SHEEP_COUNT_MIN, SHEEP_COUNT_MAX], with
//     every sheep entity well-formed: kind=Sheep, state=Walking, stateTimer
//     in [WALK_DURATION_MIN, MAX], speed in [WALK_SPEED_MIN, MAX], x within
//     monitor margins.
//   * sim_set_critter(None) erases all sheep but preserves scene entities
//     (snowflakes/tumbleweeds aren't touched).
//   * sim_set_scene preserves the active critter — flipping Grass→Desert
//     re-spawns sheep on the new scene.
//   * Sheep PRNG draw order is bit-identical to a side-stream Prng for the
//     locked sequence (count, then per-sheep: x, speed, dir-coin, seed,
//     stateTimer, nameIndex).
//   * Click within SHEEP_STARTLE_RADIUS pushes a sheep into Hopping, flips
//     vx away from the cursor, and resets age.
//   * Click outside SHEEP_STARTLE_RADIUS leaves sheep state untouched.

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"

#include <algorithm>
#include <cmath>
#include <cwchar>

using namespace desktopgrass;

namespace {

int count_kind(const Sim& sim, EntityKind kind) {
    int n = 0;
    for (const Entity& e : sim.entities) if (e.kind == kind) ++n;
    return n;
}

int count_sheep(const Sim& sim) {
    return count_kind(sim, EntityKind::Sheep);
}

const Entity* first_sheep(const Sim& sim) {
    for (const Entity& e : sim.entities) if (e.kind == EntityKind::Sheep) return &e;
    return nullptr;
}

} // namespace

TEST_CASE("CritterKind has spec-locked discriminants", "[critter][enum]") {
    REQUIRE(static_cast<int>(CritterKind::None)  == 0);
    REQUIRE(static_cast<int>(CritterKind::Sheep) == 1);
    REQUIRE(static_cast<int>(CritterKind::Cat)   == 2);
    REQUIRE(static_cast<int>(CritterKind::Bunny) == 3);
    REQUIRE(static_cast<int>(EntityKind::Sheep)  == 3);
    REQUIRE(static_cast<int>(EntityKind::Bunny)  == 6);
    REQUIRE(static_cast<int>(EntityKind::Butterfly) == 7);
    REQUIRE(static_cast<int>(EntityKind::Firefly) == 8);
    REQUIRE(CRITTER_DEFAULT == CritterKind::None);
}

TEST_CASE("Sheep constants are pinned to spec values", "[critter][constants]") {
    REQUIRE(SHEEP_COUNT_MIN      == 2);
    REQUIRE(SHEEP_COUNT_MAX      == 3);
    REQUIRE(sizeof(PET_COUNT_OPTIONS) / sizeof(PET_COUNT_OPTIONS[0]) == 6);
    for (int i = 0; i < 6; ++i) REQUIRE(PET_COUNT_OPTIONS[i] == i + 1);
    REQUIRE(PET_COUNT_DEFAULT_SHEEP == SHEEP_COUNT_MIN);
    REQUIRE(PET_COUNT_DEFAULT_CAT == CAT_COUNT_MIN);
    REQUIRE(PET_COUNT_MAX_PER_MONITOR == 6);
    REQUIRE(sizeof(SHEEP_NAME_POOL) / sizeof(SHEEP_NAME_POOL[0]) == 8);
    REQUIRE(sizeof(CAT_NAME_POOL) / sizeof(CAT_NAME_POOL[0]) == 8);
    REQUIRE(std::wcscmp(SHEEP_NAME_POOL[0], L"Bessie") == 0);
    REQUIRE(std::wcscmp(SHEEP_NAME_POOL[7], L"Hazel") == 0);
    REQUIRE(std::wcscmp(CAT_NAME_POOL[0], L"Mittens") == 0);
    REQUIRE(std::wcscmp(CAT_NAME_POOL[7], L"Juno") == 0);
    REQUIRE(PET_NAME_HOVER_RADIUS == Approx(50.0));
    REQUIRE(PET_NAME_FADE_DURATION == Approx(1.5));
    REQUIRE(PET_NAME_FONT_SIZE == Approx(11.0));
    REQUIRE(PET_NAME_OFFSET_Y == Approx(-8.0));
    REQUIRE(PET_NAME_COLOR == 0xFFFFFFFFu);
    REQUIRE(PET_NAME_SHADOW_COLOR == 0xC0000000u);
    REQUIRE(SHEEP_WALK_SPEED_MIN == Approx(14.0));
    REQUIRE(SHEEP_WALK_SPEED_MAX == Approx(26.0));
    REQUIRE(SHEEP_BODY_RADIUS    == Approx(12.0));
    REQUIRE(SHEEP_HEAD_RADIUS    == Approx(5.0));
    REQUIRE(SHEEP_LEG_LENGTH     == Approx(5.5));

    REQUIRE(SHEEP_STATE_WALKING  == 0);
    REQUIRE(SHEEP_STATE_GRAZING  == 1);
    REQUIRE(SHEEP_STATE_IDLE     == 2);
    REQUIRE(SHEEP_STATE_SLEEPING == 3);
    REQUIRE(SHEEP_STATE_HOPPING  == 4);

    REQUIRE(SHEEP_HOP_DURATION   == Approx(0.55));
    REQUIRE(SHEEP_HOP_HEIGHT     == Approx(11.0));
    REQUIRE(SHEEP_STARTLE_RADIUS == Approx(64.0));
    REQUIRE(SHEEP_STARTLE_BOOST  == Approx(1.6));

    REQUIRE(SHEEP_GRAZE_PROBABILITY     == Approx(0.60));
    REQUIRE(SHEEP_IDLE_PROBABILITY      == Approx(0.25));
    REQUIRE(SHEEP_SLEEP_FROM_IDLE_PROB  == Approx(0.30));
}

TEST_CASE("sim_init defaults critter to None", "[critter][init]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    REQUIRE(sim.currentCritter == CritterKind::None);
    REQUIRE(count_sheep(sim) == 0);
}

TEST_CASE("sim_set_critter(Sheep) produces deterministic flock", "[critter][gen]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Sheep);

    REQUIRE(sim.currentCritter == CritterKind::Sheep);
    const int k = count_sheep(sim);
    REQUIRE(k >= SHEEP_COUNT_MIN);
    REQUIRE(k <= SHEEP_COUNT_MAX);

    const double groundY = sim.windowHeight;
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Sheep) continue;
        REQUIRE(e.state == SHEEP_STATE_WALKING);
        REQUIRE(e.stateTimer >= SHEEP_WALK_DURATION_MIN);
        REQUIRE(e.stateTimer <  SHEEP_WALK_DURATION_MAX);
        REQUIRE(std::fabs(e.vx) >= SHEEP_WALK_SPEED_MIN);
        REQUIRE(std::fabs(e.vx) <  SHEEP_WALK_SPEED_MAX);
        const double margin = e.size + 8.0;
        REQUIRE(e.x >= margin);
        REQUIRE(e.x <= sim.monitorWidth - margin);
        REQUIRE(e.y == Approx(groundY - SHEEP_BODY_HEIGHT - SHEEP_LEG_LENGTH));
        REQUIRE(e.lifetime < 0.0); // infinite — sheep don't expire
        REQUIRE(e.nameIndex < sizeof(SHEEP_NAME_POOL) / sizeof(SHEEP_NAME_POOL[0]));
    }
}

TEST_CASE("Sheep PRNG draw order matches a side stream", "[critter][prng]") {
    // Independent side stream that walks the documented sequence:
    //   count
    //   per-sheep: x, speed, dir-coin, seed, stateTimer, nameIndex
    Prng side;
    prng_init(side, CANONICAL_TEST_SEED ^ CRITTER_PRNG_SALT);

    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Sheep);

    const double countDraw = prng_uniform(side, SHEEP_COUNT_MIN, SHEEP_COUNT_MAX + 1);
    int expectedCount = static_cast<int>(std::floor(countDraw));
    if (expectedCount < SHEEP_COUNT_MIN) expectedCount = SHEEP_COUNT_MIN;
    if (expectedCount > SHEEP_COUNT_MAX) expectedCount = SHEEP_COUNT_MAX;
    REQUIRE(count_sheep(sim) == expectedCount);

    int seen = 0;
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Sheep) continue;
        const double margin = SHEEP_BODY_RADIUS + 8.0;
        const double expectedX = prng_uniform(side, margin, 1920.0 - margin);
        const double expectedSpeed = prng_uniform(side, SHEEP_WALK_SPEED_MIN, SHEEP_WALK_SPEED_MAX);
        const double dirCoin = prng_uniform(side, 0.0, 1.0);
        const double expectedDir = (dirCoin < 0.5) ? -1.0 : 1.0;
        const uint32_t expectedSeed = prng_next_u32(side);
        const double expectedTimer = prng_uniform(side, SHEEP_WALK_DURATION_MIN, SHEEP_WALK_DURATION_MAX);
        const uint8_t expectedNameIndex = static_cast<uint8_t>(prng_index(side,
            static_cast<uint32_t>(sizeof(SHEEP_NAME_POOL) / sizeof(SHEEP_NAME_POOL[0]))));

        REQUIRE(e.x == Approx(expectedX));
        REQUIRE(e.vx == Approx(expectedSpeed * expectedDir));
        REQUIRE(e.seed == expectedSeed);
        REQUIRE(e.stateTimer == Approx(expectedTimer));
        REQUIRE(e.nameIndex == expectedNameIndex);
        ++seen;
    }
    REQUIRE(seen == expectedCount);
}

TEST_CASE("canonical critter name indices are stable and species-local", "[critter][names]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Sheep);
    const uint8_t expectedSheepNames[] = { 4, 7 };
    int sheepSeen = 0;
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Sheep) continue;
        REQUIRE(sheepSeen < static_cast<int>(sizeof(expectedSheepNames) / sizeof(expectedSheepNames[0])));
        REQUIRE(e.nameIndex == expectedSheepNames[sheepSeen]);
        REQUIRE(std::wcscmp(SHEEP_NAME_POOL[e.nameIndex], sheepSeen == 0 ? L"Pippin" : L"Hazel") == 0);
        ++sheepSeen;
    }
    REQUIRE(sheepSeen == 2);

    sim_set_critter(sim, CritterKind::Cat);
    const uint8_t expectedCatNames[] = { 4 };
    int catSeen = 0;
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Cat) continue;
        REQUIRE(catSeen < static_cast<int>(sizeof(expectedCatNames) / sizeof(expectedCatNames[0])));
        REQUIRE(e.nameIndex == expectedCatNames[catSeen]);
        REQUIRE(e.nameIndex < sizeof(CAT_NAME_POOL) / sizeof(CAT_NAME_POOL[0]));
        REQUIRE(std::wcscmp(CAT_NAME_POOL[e.nameIndex], L"Smokey") == 0);
        ++catSeen;
    }
    REQUIRE(catSeen == 1);
}

TEST_CASE("sim_set_critter_count(0) preserves random sheep count draw", "[critter][count]") {
    bool sawMin = false;
    bool sawMax = false;
    for (uint64_t i = 0; i < 64; ++i) {
        const uint64_t seed = CANONICAL_TEST_SEED + i * 0x9E3779B97F4A7C15ull;
        Sim sim = sim_init(seed, 1920.0, DEFAULT_DENSITY);
        sim_set_critter_count(sim, 3);
        sim_set_critter_count(sim, 0);
        sim_set_critter(sim, CritterKind::Sheep);

        Prng side;
        prng_init(side, seed ^ CRITTER_PRNG_SALT);
        const double countDraw = prng_uniform(side, SHEEP_COUNT_MIN, SHEEP_COUNT_MAX + 1);
        int expectedCount = static_cast<int>(std::floor(countDraw));
        if (expectedCount < SHEEP_COUNT_MIN) expectedCount = SHEEP_COUNT_MIN;
        if (expectedCount > SHEEP_COUNT_MAX) expectedCount = SHEEP_COUNT_MAX;

        REQUIRE(count_sheep(sim) == expectedCount);
        sawMin = sawMin || expectedCount == SHEEP_COUNT_MIN;
        sawMax = sawMax || expectedCount == SHEEP_COUNT_MAX;
    }
    REQUIRE(sawMin);
    REQUIRE(sawMax);
}

TEST_CASE("fixed sheep count override skips the count PRNG draw", "[critter][count][prng]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Sheep);
    sim_set_critter_count(sim, 3);

    REQUIRE(sim.critterCountOverride == 3);
    REQUIRE(count_sheep(sim) == 3);

    Prng side;
    prng_init(side, CANONICAL_TEST_SEED ^ CRITTER_PRNG_SALT);
    int seen = 0;
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Sheep) continue;
        const double margin = SHEEP_BODY_RADIUS + 8.0;
        const double expectedX = prng_uniform(side, margin, 1920.0 - margin);
        const double expectedSpeed = prng_uniform(side, SHEEP_WALK_SPEED_MIN, SHEEP_WALK_SPEED_MAX);
        const double dirCoin = prng_uniform(side, 0.0, 1.0);
        const double expectedDir = (dirCoin < 0.5) ? -1.0 : 1.0;
        const uint32_t expectedSeed = prng_next_u32(side);
        const double expectedTimer = prng_uniform(side, SHEEP_WALK_DURATION_MIN, SHEEP_WALK_DURATION_MAX);
        const uint8_t expectedNameIndex = static_cast<uint8_t>(prng_index(side,
            static_cast<uint32_t>(sizeof(SHEEP_NAME_POOL) / sizeof(SHEEP_NAME_POOL[0]))));

        REQUIRE(e.x == Approx(expectedX));
        REQUIRE(e.vx == Approx(expectedSpeed * expectedDir));
        REQUIRE(e.seed == expectedSeed);
        REQUIRE(e.stateTimer == Approx(expectedTimer));
        REQUIRE(e.nameIndex == expectedNameIndex);
        ++seen;
    }
    REQUIRE(seen == 3);
}

TEST_CASE("fixed critter count override supports tray range and clamps", "[critter][count]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Sheep);

    sim_set_critter_count(sim, 6);
    REQUIRE(count_sheep(sim) == 6);

    sim_set_critter_count(sim, 8);
    REQUIRE(count_sheep(sim) == PET_COUNT_MAX_PER_MONITOR);

    sim_set_critter(sim, CritterKind::Cat);
    sim_set_critter_count(sim, 2);
    REQUIRE(count_kind(sim, EntityKind::Cat) == 2);
    REQUIRE(count_sheep(sim) == 0);
}

TEST_CASE("sim_set_critter(None) clears all ground critters",
          "[critter][toggle]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Sheep);
    REQUIRE(count_sheep(sim) >= SHEEP_COUNT_MIN);
    REQUIRE(count_kind(sim, EntityKind::Cat) == 0);

    sim_set_critter(sim, CritterKind::None);
    REQUIRE(count_sheep(sim) == 0);
    REQUIRE(count_kind(sim, EntityKind::Cat) == 0);
    REQUIRE(count_kind(sim, EntityKind::Bunny) == 0);
    REQUIRE(count_kind(sim, EntityKind::Hedgehog) == 0);
}

TEST_CASE("sim_set_scene gates active sheep to Grass", "[critter][scene]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Sheep);
    const int sheepCountGrass = count_sheep(sim);
    REQUIRE(sheepCountGrass >= SHEEP_COUNT_MIN);

    sim_set_scene(sim, Scene::Desert);
    REQUIRE(count_sheep(sim) == 0);
    REQUIRE(sim.currentCritter == CritterKind::Sheep);

    sim_set_scene(sim, Scene::Winter);
    REQUIRE(count_sheep(sim) == 0);
    REQUIRE(sim.currentCritter == CritterKind::Sheep);

    sim_set_scene(sim, Scene::Grass);
    REQUIRE(count_sheep(sim) == sheepCountGrass);
}

TEST_CASE("Click within SHEEP_STARTLE_RADIUS triggers hop away", "[critter][click]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Sheep);

    Entity* target = nullptr;
    for (Entity& e : sim.entities) {
        if (e.kind == EntityKind::Sheep) { target = &e; break; }
    }
    REQUIRE(target != nullptr);

    // Click 16 DIP to the left of the sheep — well within startle radius,
    // inside the cut band (so the early y-gate doesn't reject).
    const double clickX = target->x - 16.0;
    const double clickY = sim.windowHeight - 20.0;
    target->age = 5.0; // pre-set age to verify reset

    InputEvent ev{};
    ev.type = EventType::Click;
    ev.x = clickX;
    ev.y = clickY;
    ev.time = 0.0;
    sim_apply_click(sim, ev);

    Entity* after = nullptr;
    for (Entity& e : sim.entities) {
        if (e.kind == EntityKind::Sheep) { after = &e; break; }
    }
    REQUIRE(after != nullptr);
    REQUIRE(after->state == SHEEP_STATE_HOPPING);
    REQUIRE(after->stateTimer == Approx(SHEEP_HOP_DURATION));
    REQUIRE(after->age == Approx(0.0));
    REQUIRE(after->vx > 0.0); // sheep was right of click → vx flipped to +
    REQUIRE(std::fabs(after->vx) <= SHEEP_WALK_SPEED_MAX * SHEEP_STARTLE_BOOST);
}

TEST_CASE("Click outside SHEEP_STARTLE_RADIUS leaves sheep alone", "[critter][click]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Sheep);

    Entity* target = nullptr;
    for (Entity& e : sim.entities) {
        if (e.kind == EntityKind::Sheep) { target = &e; break; }
    }
    REQUIRE(target != nullptr);
    const uint8_t stateBefore = target->state;
    const double  vxBefore    = target->vx;

    // Click far away (300 DIP) but still in the cut band.
    const double clickX = target->x + SHEEP_STARTLE_RADIUS + 200.0;
    const double clickY = sim.windowHeight - 20.0;
    InputEvent ev{};
    ev.type = EventType::Click;
    ev.x = clickX;
    ev.y = clickY;
    ev.time = 0.0;
    sim_apply_click(sim, ev);

    Entity* after = nullptr;
    for (Entity& e : sim.entities) {
        if (e.kind == EntityKind::Sheep) { after = &e; break; }
    }
    REQUIRE(after != nullptr);
    REQUIRE(after->state == stateBefore);
    REQUIRE(after->vx == Approx(vxBefore));
}
