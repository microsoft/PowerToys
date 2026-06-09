// entity_skeleton_tests.cpp
//
// Entity subsystem skeleton tests (architecture.md §13.2).
//
// Coverage:
//   * EntityKind discriminants match the spec ({None=0, Tumbleweed=1,
//     Snowflake=2}).
//   * MAX_ENTITIES_PER_MONITOR is the locked cap (= 64).
//   * sim_init defaults sim.entities to empty, capacity >= cap.
//   * sim_set_scene clears entities (currently a no-op since the Grass
//     scene generates none; §14/§15 add per-scene generators).
//   * sim_tick_entities is safe on empty (no exceptions, no growth).
//   * Tick on empty entities does not perturb other sim state (blades
//     untouched, ambient PRNG untouched).

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"

using namespace desktopgrass;

TEST_CASE("EntityKind has spec-locked discriminants", "[entities][enum]") {
    REQUIRE(static_cast<int>(EntityKind::None)       == 0);
    REQUIRE(static_cast<int>(EntityKind::Tumbleweed) == 1);
    REQUIRE(static_cast<int>(EntityKind::Snowflake)  == 2);
    REQUIRE(static_cast<int>(EntityKind::Sheep)      == 3);
    REQUIRE(static_cast<int>(EntityKind::Cat)        == 4);
    REQUIRE(static_cast<int>(EntityKind::Bunny)      == 6);
    REQUIRE(static_cast<int>(EntityKind::Butterfly)  == 7);
    REQUIRE(static_cast<int>(EntityKind::Firefly)    == 8);
    REQUIRE(static_cast<int>(EntityKind::Bird)       == 9);
    REQUIRE(MAX_ENTITIES_PER_MONITOR == 64);
}

TEST_CASE("sim_init reserves entities capacity", "[entities][init]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, DEFAULT_DENSITY);
    REQUIRE(sim.entities.empty());
    REQUIRE(sim.entities.capacity() >= static_cast<std::size_t>(MAX_ENTITIES_PER_MONITOR));
    REQUIRE(sim.entitySeed == CANONICAL_TEST_SEED);
}

TEST_CASE("sim_set_scene clears entities", "[entities][scene]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, 1.0);
    // Push a fake entity directly to verify scene-transition removal runs.
    Entity fake{};
    fake.kind = EntityKind::Tumbleweed;
    fake.x = 100.0;
    sim.entities.push_back(fake);
    REQUIRE(sim.entities.size() == 1);

    sim_set_scene(sim, Scene::Winter);
    REQUIRE(sim.entities.empty());
    REQUIRE(sim.entities.capacity() >= static_cast<std::size_t>(MAX_ENTITIES_PER_MONITOR));
}

TEST_CASE("sim_tick_entities is a no-op on empty outside Grass", "[entities][tick]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, 1.0);
    sim.currentScene = Scene::Desert;
    const auto bladesBefore = sim.blades.size();
    const auto prngBefore   = sim.ambientPrng.state;

    sim_tick_entities(sim, 0.016);
    sim_tick_entities(sim, 0.5);

    REQUIRE(sim.entities.empty());
    REQUIRE(sim.blades.size() == bladesBefore);
    REQUIRE(sim.ambientPrng.state == prngBefore);
}

TEST_CASE("sim_tick_entities advances a populated entity", "[entities][tick]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, 1.0);
    sim.currentScene = Scene::Desert;
    Entity e{};
    e.kind          = EntityKind::Tumbleweed;
    e.x             = 100.0;
    e.y             = 50.0;
    e.vx            = 50.0;    // DIP/sec
    e.vy            = 0.0;
    e.size          = 10.0;
    e.rotation      = 0.5;
    e.rotationSpeed = 1.0;     // rad/sec
    e.age           = 0.0;
    e.lifetime      = -1.0;    // infinite
    e.seed          = 0xDEADBEEF;
    sim.entities.push_back(e);

    const double dt = 0.5;
    sim_tick_entities(sim, dt);

    REQUIRE(sim.entities.size() == 1);
    const Entity& after = sim.entities[0];
    REQUIRE(after.x == Approx(100.0 + 50.0 * dt));
    REQUIRE(after.y == Approx(50.0));
    REQUIRE(after.rotation == Approx(0.5 + 1.0 * dt));
    REQUIRE(after.age == Approx(0.0 + dt));
    REQUIRE(after.kind == EntityKind::Tumbleweed);
}

TEST_CASE("sim_tick calls sim_tick_entities (wiring check)", "[entities][tick]") {
    Sim sim = sim_init(CANONICAL_TEST_SEED, 1920.0, 1.0);
    sim.currentScene = Scene::Desert;
    Entity e{};
    e.kind = EntityKind::Snowflake;
    e.x = 0.0; e.y = 0.0;
    e.vx = 10.0; e.vy = 20.0;
    e.size = 2.0;
    e.age = 0.0; e.lifetime = 100.0;
    sim.entities.push_back(e);

    sim_tick(sim, 0.1, nullptr, 0);
    REQUIRE(sim.entities.size() == 1);
    REQUIRE(sim.entities[0].x == Approx(1.0));   // 10 * 0.1
    REQUIRE(sim.entities[0].y == Approx(2.0));   // 20 * 0.1
}
