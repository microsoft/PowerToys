#include "catch.hpp"

#include "Sim.h"
#include "Constants.h"
#include "snapshot_data.h"

#include <cstddef>
#include <cmath>
#include <algorithm>
#include <array>
#include <limits>
#include <vector>

using namespace desktopgrass;
using namespace desktopgrass::test;

namespace {
constexpr double kTwoPi = 6.28318530717958647692;

Sim MakeWinterTestSim() {
    return sim_init(CANONICAL_TEST_SEED, 1920.0, 1.0);
}

void TickUntilFirstSnowflake(Sim& sim) {
    for (int i = 0; i < 10000 && sim.entities.empty(); ++i) {
        sim_tick(sim, 0.01, nullptr, 0);
    }
    REQUIRE_FALSE(sim.entities.empty());
}
}

TEST_CASE("Winter constants are pinned", "[winter][constants]") {
    REQUIRE(SNOWFLAKE_EMIT_RATE_PER_1920DIP == Approx(8.0));
    REQUIRE(SNOWFLAKE_FALL_SPEED_MIN == Approx(20.0));
    REQUIRE(SNOWFLAKE_FALL_SPEED_MAX == Approx(40.0));
    REQUIRE(SNOWFLAKE_SIZE_MIN == Approx(1.5));
    REQUIRE(SNOWFLAKE_SWAY_AMPLITUDE == Approx(10.0));
    REQUIRE(SNOWFLAKE_PRNG_SALT == 0xC0FFEE1CECAFEBABull);
    REQUIRE(SNOW_TIP_RADIUS_FACTOR == Approx(1.25));
    REQUIRE(SNOW_TIP_COLOR == 0xFFFFFFFFu);
}

TEST_CASE("Winter blade cull is deterministic and ~25%", "[winter][cull]") {
    // Pinned bitmask for indices 0..31 — must match the Win2D renderer exactly so
    // both impls thin the same blades. '1' == culled (skipped in Winter).
    const char* kExpected = "10100111000100000000000010000000";
    for (uint32_t i = 0; i < 32; ++i) {
        const bool expected = kExpected[i] == '1';
        REQUIRE(winter_blade_culled(i) == expected);
    }

    REQUIRE(WINTER_CULL_MASK == 3u);

    int culled = 0;
    for (uint32_t i = 0; i < 2500; ++i) {
        if (winter_blade_culled(i)) ++culled;
    }
    REQUIRE(culled == 624); // 24.96% of 2500 — effectively the target 25%
}

TEST_CASE("SetScene Winter initializes snowflake scheduler", "[winter][scene]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);

    REQUIRE(sim.nextSnowflakeSpawnTime > sim.globalTime);
    REQUIRE(sim.nextSnowflakeSpawnTime < 100.0);
}

TEST_CASE("First winter snowflake emits on scheduled tick", "[winter][entities]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);

    TickUntilFirstSnowflake(sim);

    REQUIRE(sim.entities.size() == 1);
    REQUIRE(sim.entities[0].kind == EntityKind::Snowflake);
}

TEST_CASE("First winter snowflake matches spec-derived PRNG snapshot", "[winter][entities][snapshot]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);

    TickUntilFirstSnowflake(sim);
    REQUIRE(sim.entities.size() == 1);

    Prng expected{};
    prng_init(expected, CANONICAL_TEST_SEED ^ SNOWFLAKE_PRNG_SALT);
    const double lambda = SNOWFLAKE_EMIT_RATE_PER_1920DIP * sim.monitorWidth / 1920.0;
    const double firstInterval = prng_exponential(expected, lambda);
    const double expectedSize = prng_uniform(expected, SNOWFLAKE_SIZE_MIN, SNOWFLAKE_SIZE_MAX);
    const double expectedX = prng_uniform(expected, -20.0, sim.monitorWidth + 20.0);
    const double expectedFallSpeed = prng_uniform(expected, SNOWFLAKE_FALL_SPEED_MIN, SNOWFLAKE_FALL_SPEED_MAX);
    const double expectedRotation = prng_uniform(expected, 0.0, kTwoPi);
    const double expectedRotationSpeed = prng_uniform(expected, -1.5, 1.5);
    const uint32_t expectedSeed = prng_next_u32(expected);
    const double nextInterval = prng_exponential(expected, lambda);

    const Entity& e = sim.entities[0];
    REQUIRE(e.size == Approx(expectedSize).margin(1e-12));
    REQUIRE(e.x == Approx(expectedX).margin(1e-12));
    REQUIRE(e.vy == Approx(expectedFallSpeed).margin(1e-12));
    REQUIRE(e.rotation == Approx(expectedRotation).margin(1e-12));
    REQUIRE(e.rotationSpeed == Approx(expectedRotationSpeed).margin(1e-12));
    REQUIRE(e.seed == expectedSeed);
    REQUIRE(sim.nextSnowflakeSpawnTime == Approx(firstInterval + nextInterval).margin(1e-12));
}

TEST_CASE("Snowflake sway velocity wobbles from seed phase", "[winter][entities]") {
    Sim sim = MakeWinterTestSim();
    sim.currentScene = Scene::Desert;
    Entity e{};
    e.kind = EntityKind::Snowflake;
    e.seed = 0;
    e.age = 0.0;
    e.lifetime = 100.0;
    sim.entities.push_back(e);

    sim_tick_entities(sim, 0.0);

    const double expectedVx = SNOWFLAKE_SWAY_AMPLITUDE * SNOWFLAKE_SWAY_FREQUENCY * kTwoPi * std::cos(0.0);
    REQUIRE(sim.entities.size() == 1);
    REQUIRE(sim.entities[0].vx == Approx(expectedVx).margin(1e-12));
}

TEST_CASE("Snowflakes are culled after lifetime", "[winter][entities]") {
    Sim sim = MakeWinterTestSim();
    sim.currentScene = Scene::Desert;
    Entity e{};
    e.kind = EntityKind::Snowflake;
    e.lifetime = 1.0;
    e.age = 0.9;
    sim.entities.push_back(e);

    sim_tick_entities(sim, 0.2);

    REQUIRE(sim.entities.empty());
}

TEST_CASE("Snowflakes are culled below ground line", "[winter][entities]") {
    Sim sim = MakeWinterTestSim();
    sim.currentScene = Scene::Desert;
    Entity e{};
    e.kind = EntityKind::Snowflake;
    e.y = sim.windowHeight + 5.0;
    e.lifetime = 100.0;
    sim.entities.push_back(e);

    sim_tick_entities(sim, 0.0);

    REQUIRE(sim.entities.empty());
}

TEST_CASE("Winter snowflake emitter honors max entity cap", "[winter][entities]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);
    sim.nextSnowflakeSpawnTime = sim.globalTime;
    for (int i = 0; i < MAX_ENTITIES_PER_MONITOR; ++i) {
        Entity e{};
        e.kind = EntityKind::Snowflake;
        e.lifetime = 100.0;
        sim.entities.push_back(e);
    }

    sim_tick_entities(sim, 0.0);

    REQUIRE(sim.entities.size() <= static_cast<std::size_t>(MAX_ENTITIES_PER_MONITOR));
    REQUIRE(sim.entities.size() == static_cast<std::size_t>(MAX_ENTITIES_PER_MONITOR));
}

TEST_CASE("Winter scene does not perturb first-blade snapshot", "[winter][snapshot]") {
    Sim sim = MakeWinterTestSim();
    REQUIRE(sim.blades[0].baseX == Approx(CANONICAL_FIRST_10[0].baseX).margin(1e-12));

    sim_set_scene(sim, Scene::Winter);
    REQUIRE(sim.blades[0].baseX == Approx(CANONICAL_FIRST_10[0].baseX).margin(1e-12));

    sim_set_scene(sim, Scene::Grass);
    REQUIRE(sim.blades[0].baseX == Approx(CANONICAL_FIRST_10[0].baseX).margin(1e-12));
}

TEST_CASE("Snowflakes do not emit in non-winter scenes", "[winter][entities][scene]") {
    Sim sim = MakeWinterTestSim();

    sim_set_scene(sim, Scene::Grass);
    sim.nextSnowflakeSpawnTime = 0.0;
    sim_tick(sim, 2.0, nullptr, 0);
    REQUIRE(std::none_of(sim.entities.begin(), sim.entities.end(),
        [](const Entity& e) { return e.kind == EntityKind::Snowflake; }));

    sim_set_scene(sim, Scene::Desert);
    sim.entities.clear();
    sim.nextSnowflakeSpawnTime = 0.0;
    sim_tick(sim, 2.0, nullptr, 0);
    REQUIRE(sim.entities.empty());
}

namespace {
int count_snow_puffs(const Sim& sim) {
    int n = 0;
    for (const Entity& e : sim.entities)
        if (e.kind == EntityKind::SnowPuff) ++n;
    return n;
}

InputEvent WinterClick(const Sim& sim, double x) {
    InputEvent ev{};
    ev.type = EventType::Click;
    ev.x    = x;
    ev.y    = sim.windowHeight - 5.0;
    ev.time = sim.globalTime;
    return ev;
}
}

TEST_CASE("Snow puff constants are pinned", "[winter][puff][constants]") {
    REQUIRE(SNOW_PUFF_COUNT_MIN == 9);
    REQUIRE(SNOW_PUFF_COUNT_MAX == 16);
    REQUIRE(SNOW_PUFF_SIZE_MIN == Approx(3.5));
    REQUIRE(SNOW_PUFF_SIZE_MAX == Approx(8.0));
    REQUIRE(SNOW_PUFF_GRAVITY == Approx(150.0));
    REQUIRE(SNOW_PUFF_DRAG == Approx(1.6));
    REQUIRE(SNOW_PUFF_SPREAD_RAD == Approx(1.25));
    REQUIRE(SNOW_PUFF_PRNG_SALT == 0x5503FF1E5503FF1Eull);
}

TEST_CASE("Clicking the winter snowbank sheds a snow puff burst", "[winter][puff]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);

    InputEvent ev = WinterClick(sim, 400.0);
    sim_apply_click(sim, ev);

    const int puffs = count_snow_puffs(sim);
    REQUIRE(puffs >= SNOW_PUFF_COUNT_MIN);
    REQUIRE(puffs <= SNOW_PUFF_COUNT_MAX);

    // Every puff launches upward (y is screen-down, so up is negative vy) and
    // spawns at or above the ground line.
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::SnowPuff) continue;
        REQUIRE(e.vy < 0.0);
        REQUIRE(e.y <= sim.windowHeight + 1e-9);
    }
}

TEST_CASE("Snow puff only fires in Winter", "[winter][puff][scene]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Grass);

    InputEvent ev = WinterClick(sim, 400.0);
    sim_apply_click(sim, ev);

    REQUIRE(count_snow_puffs(sim) == 0);
}

TEST_CASE("A non-finite click sheds no snow puff", "[winter][puff][guard]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);

    InputEvent ev{};
    ev.type = EventType::Click;
    ev.x    = std::numeric_limits<double>::quiet_NaN();
    ev.y    = sim.windowHeight - 5.0;
    ev.time = sim.globalTime;
    sim_apply_click(sim, ev);

    REQUIRE(count_snow_puffs(sim) == 0);
}

TEST_CASE("Snow puff burst rises then settles and is culled", "[winter][puff]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);

    InputEvent ev = WinterClick(sim, 400.0);
    sim_apply_click(sim, ev);
    REQUIRE(count_snow_puffs(sim) > 0);

    // 4 s easily exceeds SNOW_PUFF_LIFETIME_MAX (1.8 s); every puff should be
    // culled (lifetime expiry and/or falling back below the ground line).
    for (int i = 0; i < 200; ++i) sim_tick_entities(sim, 0.02);
    REQUIRE(count_snow_puffs(sim) == 0);
}

TEST_CASE("Snow puff draw order matches a side PRNG stream", "[winter][puff][prng]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);

    InputEvent ev = WinterClick(sim, 300.0);
    sim_apply_click(sim, ev);

    Prng side{};
    prng_init(side, CANONICAL_TEST_SEED ^ SNOW_PUFF_PRNG_SALT);
    const int expectedCount = SNOW_PUFF_COUNT_MIN
        + static_cast<int>(prng_index(side, SNOW_PUFF_COUNT_MAX - SNOW_PUFF_COUNT_MIN + 1));
    REQUIRE(count_snow_puffs(sim) == expectedCount);

    // The first locked draw inside make_snow_puff is `size`.
    const double expectedSize = prng_uniform(side, SNOW_PUFF_SIZE_MIN, SNOW_PUFF_SIZE_MAX);
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::SnowPuff) continue;
        REQUIRE(e.size == Approx(expectedSize).margin(1e-12));
        break;
    }
}

TEST_CASE("Snow puff salt is unique among winter PRNG salts", "[winter][puff][prng]") {
    const std::array<uint64_t, 15> otherSalts = {
        REGROW_PRNG_SALT, FLOWER_PRNG_SALT, MUSHROOM_PRNG_SALT,
        AMBIENT_GUST_PRNG_SALT, CACTUS_PRNG_SALT, TUMBLEWEED_PRNG_SALT,
        CRITTER_PRNG_SALT, BUTTERFLY_PRNG_SALT, FIREFLY_PRNG_SALT,
        BIRD_FLYBY_PRNG_SALT, SNOWFLAKE_PRNG_SALT,
        PINE_PRNG_SALT, LEAF_PUFF_PRNG_SALT, SNOW_DRIFT_PRNG_SALT,
    };
    for (uint64_t s : otherSalts) {
        REQUIRE(SNOW_PUFF_PRNG_SALT != s);
    }
}

// ---------------------------------------------------------------------------
// §21.1 snow drift (cursor-move spindrift)
// ---------------------------------------------------------------------------

namespace {
// Prime the cursor baseline, then brush across at `x0`→`x1` over `dt` seconds in
// the low snow band. Returns the velocity-carrying second event already applied.
void WinterDrift(Sim& sim, double x0, double x1, double dt) {
    const double y = sim.windowHeight - 5.0;
    InputEvent prime{};
    prime.type = EventType::Move;
    prime.x = x0; prime.y = y; prime.time = sim.globalTime;
    sim_apply_move(sim, prime);

    InputEvent move{};
    move.type = EventType::Move;
    move.x = x1; move.y = y; move.time = sim.globalTime + dt;
    sim_apply_move(sim, move);
}
}

TEST_CASE("Snow drift constants are pinned", "[winter][drift][constants]") {
    REQUIRE(SNOW_DRIFT_COUNT_MIN == 4);
    REQUIRE(SNOW_DRIFT_COUNT_MAX == 8);
    REQUIRE(SNOW_DRIFT_REACH_DIP == Approx(70.0));
    REQUIRE(SNOW_DRIFT_MIN_SPEED == Approx(90.0));
    REQUIRE(SNOW_DRIFT_COOLDOWN_SEC == Approx(0.12));
    REQUIRE(SNOW_DRIFT_SIZE_SCALE == Approx(0.9));
    REQUIRE(SNOW_DRIFT_SPEED_SCALE == Approx(0.85));
    REQUIRE(SNOW_DRIFT_PRNG_SALT == 0x5D81F77D5D81F77Dull);
}

TEST_CASE("Brushing the cursor across the snowbank kicks up a drift wisp", "[winter][drift]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);

    WinterDrift(sim, 300.0, 360.0, 0.05); // 60 DIP / 0.05 s = 1200 DIP/s

    const int puffs = count_snow_puffs(sim);
    REQUIRE(puffs >= SNOW_DRIFT_COUNT_MIN);
    REQUIRE(puffs <= SNOW_DRIFT_COUNT_MAX);

    // Drift grains are smaller than a click burst and still launch upward.
    for (const Entity& e : sim.entities) {
        if (e.kind != EntityKind::SnowPuff) continue;
        REQUIRE(e.vy < 0.0);
        REQUIRE(e.size <= SNOW_PUFF_SIZE_MAX * SNOW_DRIFT_SIZE_SCALE + 1e-9);
        // Drift puffs originate at the snow surface beneath the cursor, not at
        // the cursor's floating height: y sits within START_RADIUS of the
        // ground even though the cursor is 5 DIP above it.
        const double groundY = sim.windowHeight;
        REQUIRE(e.y <= groundY + 1e-9);
        REQUIRE(e.y >= groundY - SNOW_PUFF_START_RADIUS - 1e-9);
    }
}

TEST_CASE("Snow drift only fires in Winter", "[winter][drift][scene]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Grass);

    WinterDrift(sim, 300.0, 360.0, 0.05);

    REQUIRE(count_snow_puffs(sim) == 0);
}

TEST_CASE("A slow cursor brush kicks up no drift", "[winter][drift][gate]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);

    WinterDrift(sim, 300.0, 302.0, 0.05); // 2 DIP / 0.05 s = 40 DIP/s < 90

    REQUIRE(count_snow_puffs(sim) == 0);
}

TEST_CASE("A high cursor brush above the snow band kicks up no drift", "[winter][drift][gate]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);

    // Inside the gust band but far above the low drift band near the ground.
    const double y = sim.windowHeight - SNOW_DRIFT_REACH_DIP - 20.0;
    InputEvent prime{};
    prime.type = EventType::Move; prime.x = 300.0; prime.y = y; prime.time = sim.globalTime;
    sim_apply_move(sim, prime);
    InputEvent move{};
    move.type = EventType::Move; move.x = 360.0; move.y = y; move.time = sim.globalTime + 0.05;
    sim_apply_move(sim, move);

    REQUIRE(count_snow_puffs(sim) == 0);
}

TEST_CASE("Snow drift respects the global cooldown", "[winter][drift][cooldown]") {
    Sim sim = MakeWinterTestSim();
    sim_set_scene(sim, Scene::Winter);

    WinterDrift(sim, 300.0, 360.0, 0.05);
    const int first = count_snow_puffs(sim);
    REQUIRE(first >= SNOW_DRIFT_COUNT_MIN);

    // Same frame (globalTime unchanged): a second qualifying brush is gated.
    InputEvent again{};
    again.type = EventType::Move;
    again.x = 420.0; again.y = sim.windowHeight - 5.0; again.time = sim.globalTime + 0.10;
    sim_apply_move(sim, again);
    REQUIRE(count_snow_puffs(sim) == first);

    // Advance past the cooldown: a fresh brush kicks up another wisp.
    sim.globalTime += SNOW_DRIFT_COOLDOWN_SEC + 0.01;
    InputEvent later{};
    later.type = EventType::Move;
    later.x = 480.0; later.y = sim.windowHeight - 5.0; later.time = sim.globalTime + 0.05;
    sim_apply_move(sim, later);
    REQUIRE(count_snow_puffs(sim) > first);
}

TEST_CASE("Snow drift moves leave the click puff stream untouched", "[winter][drift][prng]") {
    Sim a = MakeWinterTestSim();
    sim_set_scene(a, Scene::Winter);
    Sim b = MakeWinterTestSim();
    sim_set_scene(b, Scene::Winter);

    // a brushes up some drift wisps first; b does not.
    WinterDrift(a, 300.0, 360.0, 0.05);
    const std::size_t aPreClick = a.entities.size();

    // Both click identically; the click puffs must match byte-for-byte because
    // the click stream is a separate PRNG from the drift stream.
    InputEvent ca = WinterClick(a, 800.0);
    sim_apply_click(a, ca);
    InputEvent cb = WinterClick(b, 800.0);
    sim_apply_click(b, cb);

    // Collect the click puffs from each (a's are those appended after the drift).
    std::vector<Entity> aClick(a.entities.begin() + static_cast<std::ptrdiff_t>(aPreClick), a.entities.end());
    std::vector<Entity> bClick;
    for (const Entity& e : b.entities)
        if (e.kind == EntityKind::SnowPuff) bClick.push_back(e);

    REQUIRE(aClick.size() == bClick.size());
    for (std::size_t i = 0; i < aClick.size(); ++i) {
        REQUIRE(aClick[i].size == Approx(bClick[i].size).margin(1e-12));
        REQUIRE(aClick[i].vx == Approx(bClick[i].vx).margin(1e-12));
        REQUIRE(aClick[i].vy == Approx(bClick[i].vy).margin(1e-12));
        REQUIRE(aClick[i].lifetime == Approx(bClick[i].lifetime).margin(1e-12));
    }
}
