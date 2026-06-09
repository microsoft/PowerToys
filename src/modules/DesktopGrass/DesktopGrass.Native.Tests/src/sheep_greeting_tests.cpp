// sheep_greeting_tests.cpp
//
// §16 sheep proximity-greeting tests. Mirrors Win2D SheepGreetingTests.cs.

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <vector>

using namespace desktopgrass;

namespace {

constexpr double Monitor1920 = 1920.0;
constexpr double EligibleAge = 2.0;
constexpr double LongTimer = 10.0;

Sim build_sheep_sim() {
    Sim sim = sim_init(CANONICAL_TEST_SEED, Monitor1920, DEFAULT_DENSITY);
    sim_set_critter(sim, CritterKind::Sheep);
    return sim;
}

std::vector<std::size_t> sheep_indices(const Sim& sim) {
    std::vector<std::size_t> indices;
    for (std::size_t i = 0; i < sim.entities.size(); ++i) {
        if (sim.entities[i].kind == EntityKind::Sheep) indices.push_back(i);
    }
    return indices;
}

void set_sheep(Sim& sim, std::size_t index, double x, double vx,
               uint8_t state = SHEEP_STATE_WALKING,
               double age = EligibleAge,
               double stateTimer = LongTimer) {
    Entity& e = sim.entities[index];
    e.x = x;
    e.vx = vx;
    e.state = state;
    e.age = age;
    e.stateTimer = stateTimer;
}

std::vector<std::size_t> prepare_two_sheep(Sim& sim, double gap = 40.0,
                                           double ageA = EligibleAge,
                                           double ageB = EligibleAge) {
    std::vector<std::size_t> indices = sheep_indices(sim);
    REQUIRE(indices.size() >= 2);

    set_sheep(sim, indices[0], 500.0, -20.0, SHEEP_STATE_WALKING, ageA);
    set_sheep(sim, indices[1], 500.0 + gap, 18.0, SHEEP_STATE_WALKING, ageB);
    for (std::size_t n = 2; n < indices.size(); ++n) {
        set_sheep(sim, indices[n], 1000.0 + 150.0 * static_cast<double>(n), 16.0);
    }
    return indices;
}

int advance_side_past_sheep_generation(Prng& side) {
    const double countDraw = prng_uniform(side, SHEEP_COUNT_MIN, SHEEP_COUNT_MAX + 1);
    int expectedCount = static_cast<int>(std::floor(countDraw));
    if (expectedCount < SHEEP_COUNT_MIN) expectedCount = SHEEP_COUNT_MIN;
    if (expectedCount > SHEEP_COUNT_MAX) expectedCount = SHEEP_COUNT_MAX;

    for (int i = 0; i < expectedCount; ++i) {
        const double margin = SHEEP_BODY_RADIUS + 8.0;
        (void)prng_uniform(side, margin, Monitor1920 - margin);
        (void)prng_uniform(side, SHEEP_WALK_SPEED_MIN, SHEEP_WALK_SPEED_MAX);
        (void)prng_uniform(side, 0.0, 1.0);
        (void)prng_next_u32(side);
        (void)prng_uniform(side, SHEEP_WALK_DURATION_MIN, SHEEP_WALK_DURATION_MAX);
        (void)prng_index(side, static_cast<uint32_t>(sizeof(SHEEP_NAME_POOL) / sizeof(SHEEP_NAME_POOL[0])));
    }
    return expectedCount;
}

int count_sheep_in_state(const Sim& sim, uint8_t state) {
    return static_cast<int>(std::count_if(sim.entities.begin(), sim.entities.end(),
        [state](const Entity& e) {
            return e.kind == EntityKind::Sheep && e.state == state;
        }));
}

} // namespace

TEST_CASE("Sheep greeting constants are pinned to spec values", "[sheep][greeting][constants]") {
    REQUIRE(SHEEP_STATE_GREETING       == 5);
    REQUIRE(SHEEP_GREET_RADIUS         == Approx(50.0));
    REQUIRE(SHEEP_GREET_DURATION_MIN   == Approx(1.6));
    REQUIRE(SHEEP_GREET_DURATION_MAX   == Approx(2.8));
    REQUIRE(SHEEP_GREET_MIN_AGE        == Approx(1.5));
    REQUIRE(SHEEP_GREET_HEAD_BOB_FREQ  == Approx(4.5));
    REQUIRE(SHEEP_GREET_HEAD_BOB_AMP   == Approx(0.7));
}

TEST_CASE("Sheep curious constants are pinned to spec values", "[sheep][curious][constants]") {
    REQUIRE(SHEEP_CURIOUS_RADIUS        == Approx(80.0));
    REQUIRE(SHEEP_CURIOUS_HEAD_TURN_MAX == Approx(0.55));
}


TEST_CASE("Eligible nearby sheep enter Greeting facing each other", "[sheep][greeting]") {
    Sim sim = build_sheep_sim();
    const std::vector<std::size_t> indices = prepare_two_sheep(sim);

    sim_tick_entities(sim, 0.016);

    const Entity& a = sim.entities[indices[0]];
    const Entity& b = sim.entities[indices[1]];
    REQUIRE(a.state == SHEEP_STATE_GREETING);
    REQUIRE(b.state == SHEEP_STATE_GREETING);
    REQUIRE(a.stateTimer >= SHEEP_GREET_DURATION_MIN);
    REQUIRE(a.stateTimer <= SHEEP_GREET_DURATION_MAX);
    REQUIRE(a.stateTimer == Approx(b.stateTimer));
    REQUIRE(a.vx > 0.0);
    REQUIRE(b.vx < 0.0);
}

TEST_CASE("Far apart eligible sheep do not greet", "[sheep][greeting]") {
    Sim sim = build_sheep_sim();
    const std::vector<std::size_t> indices = prepare_two_sheep(sim, 200.0);

    for (int i = 0; i < 3; ++i) sim_tick_entities(sim, 0.016);

    REQUIRE(sim.entities[indices[0]].state == SHEEP_STATE_WALKING);
    REQUIRE(sim.entities[indices[1]].state == SHEEP_STATE_WALKING);
}

TEST_CASE("Sheep under greeting minimum age do not greet", "[sheep][greeting]") {
    Sim sim = build_sheep_sim();
    const std::vector<std::size_t> indices = prepare_two_sheep(sim, 40.0, 0.5, EligibleAge);

    sim_tick_entities(sim, 0.016);

    REQUIRE(sim.entities[indices[0]].state == SHEEP_STATE_WALKING);
    REQUIRE(sim.entities[indices[1]].state == SHEEP_STATE_WALKING);
}

TEST_CASE("Sleeping hopping and greeting sheep are not greeting-eligible", "[sheep][greeting]") {
    const uint8_t blockedStates[] = {
        SHEEP_STATE_SLEEPING,
        SHEEP_STATE_HOPPING,
        SHEEP_STATE_GREETING,
    };

    for (uint8_t blockedState : blockedStates) {
        Sim sim = build_sheep_sim();
        const std::vector<std::size_t> indices = prepare_two_sheep(sim);
        set_sheep(sim, indices[0], 500.0, -20.0, blockedState, EligibleAge);

        sim_tick_entities(sim, 0.016);

        REQUIRE(sim.entities[indices[0]].state == blockedState);
        REQUIRE(sim.entities[indices[1]].state == SHEEP_STATE_WALKING);
    }
}

TEST_CASE("Greeting expiry returns sheep to Walking with vx flipped", "[sheep][greeting]") {
    Sim sim = build_sheep_sim();
    const std::vector<std::size_t> indices = prepare_two_sheep(sim);

    sim_tick_entities(sim, 0.016);
    const double duration = sim.entities[indices[0]].stateTimer;
    const double aGreetingVx = sim.entities[indices[0]].vx;
    const double bGreetingVx = sim.entities[indices[1]].vx;

    sim_tick_entities(sim, duration + 0.01);

    REQUIRE(sim.entities[indices[0]].state == SHEEP_STATE_WALKING);
    REQUIRE(sim.entities[indices[1]].state == SHEEP_STATE_WALKING);
    REQUIRE(sim.entities[indices[0]].vx == Approx(-aGreetingVx));
    REQUIRE(sim.entities[indices[1]].vx == Approx(-bGreetingVx));
}

TEST_CASE("Greeting trigger consumes one PRNG draw per pair", "[sheep][greeting][prng]") {
    Prng side;
    prng_init(side, CANONICAL_TEST_SEED ^ CRITTER_PRNG_SALT);

    Sim sim = build_sheep_sim();
    const int expectedCount = advance_side_past_sheep_generation(side);
    REQUIRE(static_cast<int>(sheep_indices(sim).size()) == expectedCount);
    const std::vector<std::size_t> indices = prepare_two_sheep(sim);

    const double expectedDuration = prng_uniform(side,
                                                 SHEEP_GREET_DURATION_MIN,
                                                 SHEEP_GREET_DURATION_MAX);
    sim_tick_entities(sim, 0.016);

    REQUIRE(sim.entities[indices[0]].stateTimer == Approx(expectedDuration));
    REQUIRE(sim.entities[indices[1]].stateTimer == Approx(expectedDuration));
}

TEST_CASE("Single sheep cannot enter Greeting", "[sheep][greeting]") {
    Sim sim = build_sheep_sim();
    sim.currentScene = Scene::Desert;
    REQUIRE(sim.entities.size() >= 1);
    sim.entities.erase(sim.entities.begin() + 1, sim.entities.end());
    set_sheep(sim, 0, 500.0, 20.0);

    sim_tick_entities(sim, 0.016);

    REQUIRE(sim.entities.size() == 1);
    REQUIRE(sim.entities[0].state == SHEEP_STATE_WALKING);
}

TEST_CASE("Three sheep cluster greets only the first encountered pair", "[sheep][greeting]") {
    Sim sim = build_sheep_sim();
    std::vector<std::size_t> indices = sheep_indices(sim);
    REQUIRE(indices.size() >= 2);
    if (indices.size() < 3) {
        sim.entities.push_back(sim.entities[indices[1]]);
        indices = sheep_indices(sim);
    }
    REQUIRE(indices.size() >= 3);

    set_sheep(sim, indices[0], 500.0, -20.0);
    set_sheep(sim, indices[1], 540.0, 18.0);
    set_sheep(sim, indices[2], 580.0, 16.0);

    sim_tick_entities(sim, 0.016);

    REQUIRE(sim.entities[indices[0]].state == SHEEP_STATE_GREETING);
    REQUIRE(sim.entities[indices[1]].state == SHEEP_STATE_GREETING);
    REQUIRE(sim.entities[indices[2]].state == SHEEP_STATE_WALKING);
    REQUIRE(count_sheep_in_state(sim, SHEEP_STATE_GREETING) == 2);
}
