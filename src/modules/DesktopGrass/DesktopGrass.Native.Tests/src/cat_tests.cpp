// cat_tests.cpp
//
// §17 Cat critter tests. Mirrors Win2D CatTests.cs.

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"

#include <algorithm>
#include <cmath>

using namespace desktopgrass;

namespace {

int count_kind(const Sim& sim, EntityKind kind) {
    return static_cast<int>(std::count_if(sim.entities.begin(), sim.entities.end(),
        [kind](const Entity& e) { return e.kind == kind; }));
}

Entity* first_kind(Sim& sim, EntityKind kind) {
    for (Entity& e : sim.entities) if (e.kind == kind) return &e;
    return nullptr;
}

const Entity* first_kind(const Sim& sim, EntityKind kind) {
    for (const Entity& e : sim.entities) if (e.kind == kind) return &e;
    return nullptr;
}

void keep_first_cat_only(Sim& sim) {
    Entity* cat = first_kind(sim, EntityKind::Cat);
    REQUIRE(cat != nullptr);
    const Entity copy = *cat;
    sim.entities.clear();
    sim.entities.push_back(copy);
}

InputEvent click_event(double x, double y) {
    InputEvent ev{};
    ev.type = EventType::Click;
    ev.x = x;
    ev.y = y;
    ev.time = 0.0;
    return ev;
}

} // namespace

TEST_CASE("CritterKind::Cat and CRITTER_COUNT are pinned", "[cat][enum]") {
    REQUIRE(static_cast<int>(CritterKind::None)  == 0);
    REQUIRE(static_cast<int>(CritterKind::Sheep) == 1);
    REQUIRE(static_cast<int>(CritterKind::Cat)   == 2);
    REQUIRE(static_cast<int>(CritterKind::Bunny) == 3);
    REQUIRE(CRITTER_COUNT == 4);
    REQUIRE(CRITTER_DEFAULT == CritterKind::None);
}

TEST_CASE("EntityKind::Cat is pinned", "[cat][enum]") {
    REQUIRE(static_cast<int>(EntityKind::None)       == 0);
    REQUIRE(static_cast<int>(EntityKind::Tumbleweed) == 1);
    REQUIRE(static_cast<int>(EntityKind::Snowflake)  == 2);
    REQUIRE(static_cast<int>(EntityKind::Sheep)      == 3);
    REQUIRE(static_cast<int>(EntityKind::Cat)        == 4);
}

TEST_CASE("Cat constants are pinned to spec values", "[cat][constants]") {
    REQUIRE(CAT_COUNT_MIN == 1);
    REQUIRE(CAT_COUNT_MAX == 2);
    REQUIRE(CAT_WALK_SPEED_MIN == Approx(10.0));
    REQUIRE(CAT_WALK_SPEED_MAX == Approx(22.0));
    REQUIRE(CAT_POUNCE_SPEED   == Approx(60.0));

    REQUIRE(CAT_BODY_RADIUS    == Approx(11.0));
    REQUIRE(CAT_BODY_HEIGHT    == Approx(7.0));
    REQUIRE(CAT_HEAD_RADIUS    == Approx(4.5));
    REQUIRE(CAT_LEG_LENGTH     == Approx(5.0));
    REQUIRE(CAT_TAIL_LENGTH    == Approx(13.0));
    REQUIRE(CAT_TAIL_THICKNESS == Approx(1.6));
    REQUIRE(CAT_EAR_HEIGHT     == Approx(4.5));

    REQUIRE(CAT_BODY_COLOR == 0xFF6B6259u);
    REQUIRE(CAT_LEG_COLOR  == 0xFF3D3733u);
    REQUIRE(CAT_FACE_COLOR == 0xFF6B6259u);
    REQUIRE(CAT_EAR_COLOR  == 0xFF3D3733u);
    REQUIRE(CAT_INK_COLOR  == 0xFF1A1614u);

    REQUIRE(CAT_WALK_PERIOD    == Approx(0.50));
    REQUIRE(CAT_LEG_CYCLE_AMP  == Approx(1.6));
    REQUIRE(CAT_HEAD_BOB_AMP   == Approx(0.4));
    REQUIRE(CAT_TAIL_SWAY_FREQ == Approx(1.2));
    REQUIRE(CAT_TAIL_SWAY_AMP  == Approx(0.35));

    REQUIRE(CAT_STATE_WALKING  == SHEEP_STATE_WALKING);
    REQUIRE(CAT_STATE_IDLE     == SHEEP_STATE_IDLE);
    REQUIRE(CAT_STATE_SLEEPING == SHEEP_STATE_SLEEPING);
    REQUIRE(CAT_STATE_POUNCING == SHEEP_STATE_HOPPING);

    REQUIRE(CAT_WALK_DURATION_MIN  == Approx(6.0));
    REQUIRE(CAT_WALK_DURATION_MAX  == Approx(10.0));
    REQUIRE(CAT_IDLE_DURATION_MIN  == Approx(4.0));
    REQUIRE(CAT_IDLE_DURATION_MAX  == Approx(8.0));
    REQUIRE(CAT_SLEEP_DURATION_MIN == Approx(20.0));
    REQUIRE(CAT_SLEEP_DURATION_MAX == Approx(40.0));
    REQUIRE(CAT_POUNCE_DURATION    == Approx(0.45));

    REQUIRE(CAT_IDLE_PROBABILITY == Approx(0.65));
    REQUIRE(CAT_SLEEP_PROBABILITY == Approx(0.30));
    REQUIRE(CAT_SLEEP_FROM_IDLE_PROB == Approx(0.50));

    REQUIRE(CAT_POUNCE_RADIUS == Approx(80.0));
    REQUIRE(CAT_POUNCE_HEIGHT == Approx(9.0));
    REQUIRE(CAT_CURIOUS_RADIUS == Approx(100.0));
    REQUIRE(CAT_CURIOUS_HEAD_TURN_MAX == Approx(0.7));
}

TEST_CASE("sim_init defaults to None and does not generate cats until selected", "[cat][init]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    REQUIRE(sim.currentCritter == CritterKind::None);
    REQUIRE(count_kind(sim, EntityKind::Cat) == 0);

    sim_set_critter(sim, CritterKind::Cat);
    REQUIRE(count_kind(sim, EntityKind::Cat) >= CAT_COUNT_MIN);
}

TEST_CASE("sim_set_critter(Cat) produces deterministic cats", "[cat][gen]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Cat);

    REQUIRE(sim.currentCritter == CritterKind::Cat);
    const int k = count_kind(sim, EntityKind::Cat);
    REQUIRE(k >= CAT_COUNT_MIN);
    REQUIRE(k <= CAT_COUNT_MAX);

    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::Cat) continue;
        REQUIRE(e.state == CAT_STATE_WALKING);
        REQUIRE(e.stateTimer >= CAT_WALK_DURATION_MIN);
        REQUIRE(e.stateTimer <  CAT_WALK_DURATION_MAX);
        REQUIRE(std::fabs(e.vx) >= CAT_WALK_SPEED_MIN);
        REQUIRE(std::fabs(e.vx) <  CAT_WALK_SPEED_MAX);
        const double margin = e.size + 8.0;
        REQUIRE(e.x >= margin);
        REQUIRE(e.x <= sim.monitorWidth - margin);
        REQUIRE(e.y == Approx(sim.windowHeight - CAT_BODY_HEIGHT - CAT_LEG_LENGTH));
        REQUIRE(e.size == Approx(CAT_BODY_RADIUS));
        REQUIRE(e.lifetime < 0.0);
        REQUIRE(e.nameIndex < sizeof(CAT_NAME_POOL) / sizeof(CAT_NAME_POOL[0]));
        REQUIRE(e.coatVariantIndex < CAT_COAT_VARIANT_COUNT);
    }
}

TEST_CASE("Cat PRNG draw order matches a side stream", "[cat][prng]") {
    // count, then per-cat: x, speed, dir-coin, seed, stateTimer, nameIndex, coatVariantIndex
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
        const double margin = CAT_BODY_RADIUS + 8.0;
        const double expectedX = prng_uniform(side, margin, 1920.0 - margin);
        const double expectedSpeed = prng_uniform(side, CAT_WALK_SPEED_MIN, CAT_WALK_SPEED_MAX);
        const double dirCoin = prng_uniform(side, 0.0, 1.0);
        const double expectedDir = (dirCoin < 0.5) ? -1.0 : 1.0;
        const uint32_t expectedSeed = prng_next_u32(side);
        const double expectedTimer = prng_uniform(side, CAT_WALK_DURATION_MIN, CAT_WALK_DURATION_MAX);
        const uint8_t expectedNameIndex = static_cast<uint8_t>(prng_index(side,
            static_cast<uint32_t>(sizeof(CAT_NAME_POOL) / sizeof(CAT_NAME_POOL[0]))));
        const uint8_t expectedCoatVariantIndex = static_cast<uint8_t>(prng_index(side,
            static_cast<uint32_t>(CAT_COAT_VARIANT_COUNT)));

        REQUIRE(e.x == Approx(expectedX));
        REQUIRE(e.vx == Approx(expectedSpeed * expectedDir));
        REQUIRE(e.seed == expectedSeed);
        REQUIRE(e.stateTimer == Approx(expectedTimer));
        REQUIRE(e.nameIndex == expectedNameIndex);
        REQUIRE(e.coatVariantIndex == expectedCoatVariantIndex);
        ++seen;
    }
    REQUIRE(seen == expectedCount);
}

TEST_CASE("sim_set_critter(None) clears ambient cats", "[cat][toggle]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Cat);
    REQUIRE(count_kind(sim, EntityKind::Cat) >= CAT_COUNT_MIN);

    sim_set_critter(sim, CritterKind::None);
    REQUIRE(sim.currentCritter == CritterKind::None);
    REQUIRE(count_kind(sim, EntityKind::Cat) == 0);
    REQUIRE(count_kind(sim, EntityKind::Bunny) == 0);
}

TEST_CASE("Switching between critter species replaces the previous species", "[cat][toggle]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Cat);
    REQUIRE(count_kind(sim, EntityKind::Cat) >= CAT_COUNT_MIN);
    REQUIRE(count_kind(sim, EntityKind::Sheep) == 0);

    sim_set_critter(sim, CritterKind::Sheep);
    REQUIRE(count_kind(sim, EntityKind::Cat) == 0);
    REQUIRE(count_kind(sim, EntityKind::Sheep) >= SHEEP_COUNT_MIN);

    sim_set_critter(sim, CritterKind::Cat);
    REQUIRE(count_kind(sim, EntityKind::Sheep) == 0);
    REQUIRE(count_kind(sim, EntityKind::Cat) >= CAT_COUNT_MIN);
}

TEST_CASE("sim_set_scene gates active Cat to Grass", "[cat][scene]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Cat);
    const int catsGrass = count_kind(sim, EntityKind::Cat);
    REQUIRE(catsGrass >= CAT_COUNT_MIN);

    sim_set_scene(sim, Scene::Desert);
    REQUIRE(sim.currentCritter == CritterKind::Cat);
    REQUIRE(count_kind(sim, EntityKind::Cat) == 0);

    sim_set_scene(sim, Scene::Winter);
    REQUIRE(sim.currentCritter == CritterKind::Cat);
    REQUIRE(count_kind(sim, EntityKind::Cat) == 0);

    sim_set_scene(sim, Scene::Grass);
    REQUIRE(count_kind(sim, EntityKind::Cat) == catsGrass);
}

TEST_CASE("Click within CAT_POUNCE_RADIUS pounces toward the click", "[cat][click]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Cat);
    keep_first_cat_only(sim);

    Entity& cat = sim.entities.front();
    cat.x = 500.0;
    cat.vx = -CAT_WALK_SPEED_MIN;
    cat.age = 5.0;

    sim_apply_click(sim, click_event(cat.x + 16.0, sim.windowHeight - 20.0));

    const Entity& after = sim.entities.front();
    REQUIRE(after.state == CAT_STATE_POUNCING);
    REQUIRE(after.stateTimer == Approx(CAT_POUNCE_DURATION));
    REQUIRE(after.age == Approx(0.0));
    REQUIRE(after.vx == Approx(CAT_POUNCE_SPEED));
}

TEST_CASE("Click outside CAT_POUNCE_RADIUS leaves cat alone", "[cat][click]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Cat);
    keep_first_cat_only(sim);

    Entity& cat = sim.entities.front();
    cat.x = 500.0;
    cat.vx = -CAT_WALK_SPEED_MIN;
    const uint8_t stateBefore = cat.state;
    const double vxBefore = cat.vx;

    sim_apply_click(sim, click_event(cat.x + CAT_POUNCE_RADIUS + 5.0, sim.windowHeight - 20.0));

    const Entity& after = sim.entities.front();
    REQUIRE(after.state == stateBefore);
    REQUIRE(after.vx == Approx(vxBefore));
}

TEST_CASE("Cats do not greet other cats", "[cat][greeting]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Cat);
    keep_first_cat_only(sim);

    Entity first = sim.entities.front();
    first.x = 400.0;
    first.vx = CAT_WALK_SPEED_MIN;
    first.state = CAT_STATE_WALKING;
    first.stateTimer = 10.0;
    first.age = SHEEP_GREET_MIN_AGE + 1.0;
    Entity second = first;
    second.x = first.x + 20.0;
    second.vx = -CAT_WALK_SPEED_MIN;
    sim.entities.clear();
    sim.entities.push_back(first);
    sim.entities.push_back(second);

    sim_tick_entities(sim, 0.016);

    REQUIRE(count_kind(sim, EntityKind::Cat) == 2);
    for (const Entity& e : sim.entities) {
        if (e.kind == EntityKind::Cat) REQUIRE(e.state != SHEEP_STATE_GREETING);
    }
}

TEST_CASE("Cats do not greet sheep", "[cat][greeting]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Cat);
    keep_first_cat_only(sim);

    Entity cat = sim.entities.front();
    cat.x = 400.0;
    cat.vx = CAT_WALK_SPEED_MIN;
    cat.state = CAT_STATE_WALKING;
    cat.stateTimer = 10.0;
    cat.age = SHEEP_GREET_MIN_AGE + 1.0;

    Entity sheep{};
    sheep.kind = EntityKind::Sheep;
    sheep.size = SHEEP_BODY_RADIUS;
    sheep.x = cat.x + 20.0;
    sheep.y = sim.windowHeight - SHEEP_BODY_HEIGHT - SHEEP_LEG_LENGTH;
    sheep.vx = -SHEEP_WALK_SPEED_MIN;
    sheep.age = SHEEP_GREET_MIN_AGE + 1.0;
    sheep.lifetime = -1.0;
    sheep.state = SHEEP_STATE_WALKING;
    sheep.stateTimer = 10.0;

    sim.entities.clear();
    sim.entities.push_back(cat);
    sim.entities.push_back(sheep);

    sim_tick_entities(sim, 0.016);

    REQUIRE(count_kind(sim, EntityKind::Cat) == 1);
    REQUIRE(count_kind(sim, EntityKind::Sheep) == 1);
    for (const Entity& e : sim.entities) REQUIRE(e.state != SHEEP_STATE_GREETING);
}

