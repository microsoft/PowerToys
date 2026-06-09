// Sim.cpp
//
// Pure simulation code. Ports docs/architecture.md §§3-10 verbatim.

#include "Sim.h"

#include <cmath>
#include <algorithm>
#include <chrono>
#include <ctime>
#include <limits>

namespace desktopgrass {

namespace {
constexpr double TWO_PI = 6.28318530717958647692;
}

// ---------------------------------------------------------------------------
// PRNG
// ---------------------------------------------------------------------------

uint64_t splitmix64(uint64_t z) noexcept {
    z = z + 0x9E3779B97F4A7C15ull;
    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9ull;
    z = (z ^ (z >> 27)) * 0x94D049BB133111EBull;
    return z ^ (z >> 31);
}

void prng_init(Prng& p, uint64_t seed) noexcept {
    p.state = splitmix64(seed);
    if (p.state == 0) {
        // Belt-and-suspenders for the (highly unlikely) zero state. Matches the
        // spec.
        p.state = 0x9E3779B97F4A7C15ull;
    }
}

uint64_t prng_next_u64(Prng& p) noexcept {
    uint64_t x = p.state;
    x ^= x << 13;
    x ^= x >> 7;
    x ^= x << 17;
    p.state = x;
    return x;
}

uint32_t prng_next_u32(Prng& p) noexcept {
    return static_cast<uint32_t>(prng_next_u64(p) >> 32);
}

double prng_next_unit(Prng& p) noexcept {
    // Top 53 bits → IEEE-754 mantissa precision.
    return static_cast<double>(prng_next_u64(p) >> 11) * (1.0 / 9007199254740992.0);
}

double prng_uniform(Prng& p, double lo, double hi) noexcept {
    return lo + prng_next_unit(p) * (hi - lo);
}

double prng_exponential(Prng& p, double lambda) noexcept {
    return -std::log(1.0 - prng_uniform(p, 0.0, 1.0)) / lambda;
}

uint32_t prng_index(Prng& p, uint32_t n) noexcept {
    return static_cast<uint32_t>(prng_next_unit(p) * static_cast<double>(n));
}

double bunny_hop_y_offset(double age, bool startled) noexcept {
    const double height = startled ? BUNNY_STARTLE_HOP_HEIGHT : BUNNY_HOP_HEIGHT;
    double t = age / BUNNY_HOP_DURATION;
    if (t < 0.0) t = 0.0;
    if (t > 1.0) t = 1.0;
    return 4.0 * height * t * (1.0 - t);
}

uint8_t bunny_choose_rest_state(Prng& p) noexcept {
    const double sleepProb = BUNNY_SLEEP_PROB;
    const double r = prng_uniform(p, 0.0, 1.0);
    if (r < sleepProb) return BUNNY_STATE_SLEEPING;

    const double activeWeight = BUNNY_GRAZE_PROBABILITY + BUNNY_IDLE_PROBABILITY;
    const double activeT = (activeWeight > 0.0 && sleepProb < 1.0)
        ? (r - sleepProb) / (1.0 - sleepProb)
        : 0.0;
    return activeT < (BUNNY_GRAZE_PROBABILITY / activeWeight)
        ? BUNNY_STATE_GRAZING
        : BUNNY_STATE_IDLE;
}

uint8_t hedgehog_choose_rest_state(Prng& p) noexcept {
    const double sleepProb = HEDGEHOG_SLEEP_PROB;
    const double r = prng_uniform(p, 0.0, 1.0);
    if (r < sleepProb) return HEDGEHOG_STATE_SLEEPING;

    const double activeWeight = HEDGEHOG_SNUFFLE_PROBABILITY + HEDGEHOG_IDLE_PROBABILITY;
    const double activeT = (activeWeight > 0.0 && sleepProb < 1.0)
        ? (r - sleepProb) / (1.0 - sleepProb)
        : 0.0;
    return activeT < (HEDGEHOG_SNUFFLE_PROBABILITY / activeWeight)
        ? HEDGEHOG_STATE_SNUFFLING
        : HEDGEHOG_STATE_IDLE;
}

// ---------------------------------------------------------------------------
// Blade generation. Field-draw order MUST be (height, thickness, hue,
// swayPhaseOffset, stiffness) — see architecture.md §5.
// ---------------------------------------------------------------------------

void generate_blades(uint64_t seed, double monitorWidth, double density,
                     std::vector<Blade>& out)
{
    out.clear();
    if (monitorWidth <= 0.0 || density <= 0.0) return;

    Prng p;
    prng_init(p, seed);

    // Independent stream for regrowth jitter. Seeding it from `seed XOR salt`
    // makes the jitter deterministic for a given seed *without* advancing the
    // main PRNG state — existing snapshot tests / cross-impl conformance
    // (10,787 unique colors for the canonical seed) stay bit-identical.
    Prng pRegrow;
    prng_init(pRegrow, seed ^ REGROW_PRNG_SALT);

    // Flower stream — INDEPENDENT from main and regrowth. Every blade
    // consumes exactly one unconditional draw (the probability check);
    // flowers additionally consume 3 more draws. The order MUST be:
    // probability, then (if flower) head-color, head-radius, height-bonus.
    Prng pFlower;
    prng_init(pFlower, seed ^ FLOWER_PRNG_SALT);

    // Mushroom stream (PROTOTYPE — Native-only). Fourth independent stream
    // salted with MUSHROOM_PRNG_SALT so adding mushrooms does NOT perturb
    // the existing flower / regrowth / main sequences.
    Prng pMushroom;
    prng_init(pMushroom, seed ^ MUSHROOM_PRNG_SALT);

    // Cut-floor (stubble) stream. Independent salted stream so the per-blade
    // mowed residual height does NOT perturb any existing sequence.
    Prng pCutFloor;
    prng_init(pCutFloor, seed ^ CUT_FLOOR_PRNG_SALT);

    double x = 0.0;
    while (true) {
        double step = prng_uniform(p, BLADE_SPACING_MIN, BLADE_SPACING_MAX) / density;
        x += step;
        if (x >= monitorWidth) break;

        Blade b{};
        b.baseX            = x;
        b.height           = prng_uniform(p, BLADE_HEIGHT_MIN,    BLADE_HEIGHT_MAX);
        b.thickness        = prng_uniform(p, BLADE_THICKNESS_MIN, BLADE_THICKNESS_MAX);
        b.hue              = static_cast<uint8_t>(prng_index(p, PALETTE_SIZE));
        b.swayPhaseOffset  = prng_uniform(p, 0.0, 2.0 * 3.14159265358979323846);
        b.stiffness        = prng_uniform(p, STIFFNESS_MIN, STIFFNESS_MAX);

        b.cutHeight        = 1.0;
        b.gustVelocity     = 0.0;
        b.cutAnimStart     = -1.0;
        b.cutInitialHeight = 1.0;
        b.cutFloor         = prng_uniform(pCutFloor, CUT_FLOOR_MIN, CUT_FLOOR_MAX);
        b.effectiveLean    = 0.0;

        // Regrowth jitter — independent stream. Draw delay first, then duration
        // (field-draw order MUST match across all three impls).
        b.regrowDelay      = prng_uniform(pRegrow, REGROW_DELAY_MIN,    REGROW_DELAY_MAX);
        b.regrowDuration   = prng_uniform(pRegrow, REGROW_DURATION_MIN, REGROW_DURATION_MAX);
        b.regrowStart      = -1.0;

        // Flower stream (architecture.md §5). Order MUST be: probability,
        // then head-color, head-radius, height-bonus IF flower.
        const bool isFlower = prng_uniform(pFlower, 0.0, 1.0) < FLOWER_PROBABILITY;
        b.isFlower = isFlower;
        if (isFlower) {
            b.flowerHeadColorIdx = static_cast<uint8_t>(prng_index(pFlower, FLOWER_PALETTE_SIZE));
            b.flowerHeadRadius   = prng_uniform(pFlower, FLOWER_HEAD_RADIUS_MIN, FLOWER_HEAD_RADIUS_MAX);
            b.heightBonus        = prng_uniform(pFlower, FLOWER_HEIGHT_BONUS_MIN, FLOWER_HEIGHT_BONUS_MAX);
        } else {
            b.flowerHeadColorIdx = 0;
            b.flowerHeadRadius   = 0.0;
            b.heightBonus        = 1.0;
        }

        // Mushroom stream (PROTOTYPE). One unconditional draw (probability
        // check), then 5 more conditional draws for cap color, cap width,
        // cap height, stem height, stem thickness.
        const bool isMushroom = prng_uniform(pMushroom, 0.0, 1.0) < MUSHROOM_PROBABILITY;
        b.isMushroom = isMushroom;
        if (isMushroom) {
            b.mushroomCapColorIdx     = static_cast<uint8_t>(prng_index(pMushroom, MUSHROOM_PALETTE_SIZE));
            b.mushroomCapWidth        = prng_uniform(pMushroom, MUSHROOM_CAP_WIDTH_MIN,      MUSHROOM_CAP_WIDTH_MAX);
            b.mushroomCapHeight       = prng_uniform(pMushroom, MUSHROOM_CAP_HEIGHT_MIN,     MUSHROOM_CAP_HEIGHT_MAX);
            b.mushroomStemHeight      = prng_uniform(pMushroom, MUSHROOM_STEM_HEIGHT_MIN,    MUSHROOM_STEM_HEIGHT_MAX);
            b.mushroomStemThickness   = prng_uniform(pMushroom, MUSHROOM_STEM_THICKNESS_MIN, MUSHROOM_STEM_THICKNESS_MAX);
        } else {
            b.mushroomCapColorIdx     = 0;
            b.mushroomCapWidth        = 0.0;
            b.mushroomCapHeight       = 0.0;
            b.mushroomStemHeight      = 0.0;
            b.mushroomStemThickness   = 0.0;
        }

        b.originalIsFlower   = b.isFlower;
        b.originalIsMushroom = b.isMushroom;

        out.push_back(b);
    }
}

namespace {

void restore_original_variants(Blade& b) noexcept {
    b.isCactus       = false;
    b.cactusType     = 0;
    b.cactusHeight   = 0.0;
    b.cactusWidth    = 0.0;
    b.cactusArmSide  = +1;
    b.isPine         = false;
    b.pineTierCount  = 0;
    b.treeVariant    = 0;
    b.pineHeight     = 0.0;
    b.pineWidth      = 0.0;
    b.treeBackground = false;
    b.isMaple        = false;
    b.mapleHeight    = 0.0;
    b.mapleTrunkWidth = 0.0;
    b.mapleCanopyRadius = 0.0;
    b.mapleCanopyColorIdx = 0;
    b.mapleIsBare    = false;
    b.leafPuffCooldownEnd = 0.0;
    b.isCoral        = false;
    b.coralType      = 0;
    b.coralHeight    = 0.0;
    b.coralWidth     = 0.0;
    b.coralColorIdx  = 0;
    b.isFlower       = b.originalIsFlower;
    b.isMushroom     = b.originalIsMushroom;
}

int tumbleweed_count_for_width(double monitorWidth) noexcept {
    if (monitorWidth < 480.0) return 0;
    const double scaled = std::floor(monitorWidth / 1920.0 * static_cast<double>(TUMBLEWEED_COUNT_PER_1920DIP));
    int count = static_cast<int>(scaled);
    if (count < 1) count = 1;
    return std::min(count, MAX_ENTITIES_PER_MONITOR);
}

// Deterministic [0,1) hash used to give each tumbleweed its own bounce
// cadence/height without drawing from the shared PRNG stream (which would
// shift the spec-pinned spawn snapshots).
double tumbleweed_hash01(uint32_t seed, uint32_t salt) noexcept {
    uint32_t x = seed + salt * 0x9E3779B9u;
    x ^= x >> 16; x *= 0x7FEB352Du;
    x ^= x >> 15; x *= 0x846CA68Bu;
    x ^= x >> 16;
    return static_cast<double>(x >> 8) * (1.0 / 16777216.0);
}

double tumbleweed_bounce_period(uint32_t seed) noexcept {
    return TUMBLEWEED_BOUNCE_PERIOD_MIN
         + (TUMBLEWEED_BOUNCE_PERIOD_MAX - TUMBLEWEED_BOUNCE_PERIOD_MIN)
           * tumbleweed_hash01(seed, 11u);
}

double tumbleweed_hop_height(uint32_t seed, double size) noexcept {
    const double frac = TUMBLEWEED_BOUNCE_HEIGHT_MIN_FRAC
        + (TUMBLEWEED_BOUNCE_HEIGHT_MAX_FRAC - TUMBLEWEED_BOUNCE_HEIGHT_MIN_FRAC)
          * tumbleweed_hash01(seed, 7u);
    return size * frac;
}

// Per-hop jittered gap so a single tumbleweed's bounces aren't perfectly
// metronomic; salted with the current second so each landing differs.
double tumbleweed_next_gap(uint32_t seed, double age) noexcept {
    const uint32_t salt = seed ^ static_cast<uint32_t>(std::floor(age));
    return tumbleweed_bounce_period(seed) * (0.6 + 0.8 * tumbleweed_hash01(salt, 17u));
}

Entity make_tumbleweed(Prng& prng, double monitorWidth, double groundY) noexcept {
    Entity e{};
    e.kind = EntityKind::Tumbleweed;
    e.size = prng_uniform(prng, TUMBLEWEED_SIZE_MIN, TUMBLEWEED_SIZE_MAX);
    e.x    = prng_uniform(prng, 0.0, monitorWidth);
    e.y    = groundY - prng_uniform(prng, TUMBLEWEED_Y_OFFSET_MIN, TUMBLEWEED_Y_OFFSET_MAX);
    const double speed = prng_uniform(prng, TUMBLEWEED_SPEED_MIN, TUMBLEWEED_SPEED_MAX);
    const double direction = prng_uniform(prng, 0.0, 1.0) < 0.5 ? -1.0 : 1.0;
    e.vx = direction * speed;
    e.vy = 0.0;
    e.rotation      = prng_uniform(prng, 0.0, TWO_PI);
    e.rotationSpeed = e.vx / e.size;
    e.age           = 0.0;
    e.lifetime      = -1.0;
    e.seed          = prng_next_u32(prng);
    e.altitudeAnchor = e.y; // grounded baseline the hop returns to
    e.stateTimer     = tumbleweed_hash01(e.seed, 3u) * tumbleweed_bounce_period(e.seed);
    return e;
}

void respawn_tumbleweed(Entity& e, Prng& prng, double monitorWidth,
                        double groundY, bool fromLeft) noexcept {
    e.size = prng_uniform(prng, TUMBLEWEED_SIZE_MIN, TUMBLEWEED_SIZE_MAX);
    e.y    = groundY - prng_uniform(prng, TUMBLEWEED_Y_OFFSET_MIN, TUMBLEWEED_Y_OFFSET_MAX);
    const double speed = prng_uniform(prng, TUMBLEWEED_SPEED_MIN, TUMBLEWEED_SPEED_MAX);
    e.x  = fromLeft ? -e.size : monitorWidth + e.size;
    e.vx = fromLeft ? speed : -speed;
    e.vy = 0.0;
    e.rotationSpeed = e.vx / e.size;
    e.age = 0.0;
    e.lifetime = -1.0;
    e.altitudeAnchor = e.y;
    e.stateTimer     = tumbleweed_hash01(e.seed, 3u) * tumbleweed_bounce_period(e.seed);
}

Entity make_leaf(Prng& prng, double monitorWidth) noexcept {
    Entity e{};
    e.kind = EntityKind::Leaf;
    const double xFrac = prng_uniform(prng, 0.0, 1.0);
    const double spawnX = xFrac * monitorWidth;
    const double fallSpeed = prng_uniform(prng, LEAF_FALL_SPEED_MIN, LEAF_FALL_SPEED_MAX);
    e.phaseX = prng_uniform(prng, 0.0, TWO_PI);
    const double rotationSpeedMag = prng_uniform(prng, LEAF_ROTATION_SPEED_MIN, LEAF_ROTATION_SPEED_MAX);
    const double rotationSign = (prng_next_u64(prng) & 1ull) != 0ull ? 1.0 : -1.0;
    e.rotationSpeed = rotationSpeedMag * rotationSign;
    e.rotation = prng_uniform(prng, 0.0, TWO_PI);
    e.size = prng_uniform(prng, LEAF_SIZE_MIN, LEAF_SIZE_MAX);
    e.colorVariant = static_cast<uint8_t>(prng_index(prng, LEAF_COLOR_COUNT));
    e.x0 = spawnX;
    e.x = spawnX + LEAF_HORIZONTAL_DRIFT_AMP * std::sin(e.phaseX);
    e.y = LEAF_SPAWN_Y_OFFSET;
    e.vx = 0.0;
    e.vy = fallSpeed;
    e.baseSpeed = fallSpeed;
    e.age = 0.0;
    e.lifetime = -1.0;
    return e;
}

// Leaf shaken loose by a cursor hover (§16.6). Same visual leaf, but spawned at
// the canopy with an outward burst velocity (vx) that decays in the tick before
// it settles into the usual sinusoidal flutter-down. Draw order is locked to
// the Win2D mirror so both impls stay bit-identical.
Entity make_puff_leaf(Prng& prng, double cx, double cy, double canopyR) noexcept {
    Entity e{};
    e.kind = EntityKind::Leaf;
    e.phaseX = prng_uniform(prng, 0.0, TWO_PI);
    const double rotationSpeedMag = prng_uniform(prng, LEAF_ROTATION_SPEED_MIN, LEAF_ROTATION_SPEED_MAX);
    const double rotationSign = (prng_next_u64(prng) & 1ull) != 0ull ? 1.0 : -1.0;
    e.rotationSpeed = rotationSpeedMag * rotationSign;
    e.rotation = prng_uniform(prng, 0.0, TWO_PI);
    e.size = prng_uniform(prng, LEAF_SIZE_MIN, LEAF_SIZE_MAX);
    e.colorVariant = static_cast<uint8_t>(prng_index(prng, LEAF_COLOR_COUNT));
    const double ang = prng_uniform(prng, 0.0, TWO_PI);
    const double speed = prng_uniform(prng, LEAF_PUFF_BURST_SPEED_MIN, LEAF_PUFF_BURST_SPEED_MAX);
    const double offFrac = prng_uniform(prng, 0.0, LEAF_PUFF_START_OFFSET_FRAC);
    const double fallSpeed = prng_uniform(prng, LEAF_FALL_SPEED_MIN, LEAF_FALL_SPEED_MAX);
    e.x0 = cx + std::cos(ang) * canopyR * offFrac;
    e.y  = cy + std::sin(ang) * canopyR * offFrac;
    e.vx = std::cos(ang) * speed;
    e.vy = fallSpeed;
    e.baseSpeed = fallSpeed;
    e.x = e.x0 + LEAF_HORIZONTAL_DRIFT_AMP * std::sin(e.phaseX);
    e.age = 0.0;
    e.lifetime = -1.0;
    return e;
}

// Powder kicked up by a click on the Winter snowbank (§21). Spawns at the click
// with an upward, outward burst that gravity (positive vy, y is screen-down)
// pulls back to ground; horizontal velocity decays via drag in the tick, and
// the particle fades out over its lifetime. Draw order is locked to the Win2D
// mirror so both impls stay bit-identical: size, theta, speed, offA, offR,
// lifetime.
Entity make_snow_puff(Prng& prng, double cx, double cy, double groundY,
                      double sizeScale = 1.0, double speedScale = 1.0) noexcept {
    Entity e{};
    e.kind = EntityKind::SnowPuff;
    e.size = prng_uniform(prng, SNOW_PUFF_SIZE_MIN, SNOW_PUFF_SIZE_MAX);
    const double theta = prng_uniform(prng, -SNOW_PUFF_SPREAD_RAD, SNOW_PUFF_SPREAD_RAD);
    const double speed = prng_uniform(prng, SNOW_PUFF_BURST_SPEED_MIN, SNOW_PUFF_BURST_SPEED_MAX);
    const double offA = prng_uniform(prng, 0.0, TWO_PI);
    const double offR = prng_uniform(prng, 0.0, SNOW_PUFF_START_RADIUS);
    e.lifetime = prng_uniform(prng, SNOW_PUFF_LIFETIME_MIN, SNOW_PUFF_LIFETIME_MAX);
    // Scales are applied AFTER every draw so the PRNG sequence is identical to
    // the unscaled (click) puff — only the resulting magnitudes change.
    e.size *= sizeScale;
    const double scaledSpeed = speed * speedScale;
    e.x  = cx + std::cos(offA) * offR;
    // Bias the start to at/above ground so a puff never spawns under the bank.
    e.y  = std::min(cy - std::fabs(std::sin(offA)) * offR, groundY);
    e.vx = std::sin(theta) * scaledSpeed;
    e.vy = -std::cos(theta) * scaledSpeed;
    e.age = 0.0;
    return e;
}

} // anonymous

void generate_cacti_for_desert(Sim& sim) noexcept {
    Prng cactusPrng;
    prng_init(cactusPrng, sim.entitySeed ^ CACTUS_PRNG_SALT);
    double lastPlacedRight = -std::numeric_limits<double>::infinity();

    for (Blade& b : sim.blades) {
        restore_original_variants(b);

        const double r = prng_uniform(cactusPrng, 0.0, 1.0);
        if (r >= CACTUS_PROBABILITY) continue;

        b.isCactus      = true;
        b.isFlower      = false;
        b.isMushroom    = false;
        b.cactusHeight  = prng_uniform(cactusPrng, CACTUS_HEIGHT_MIN, CACTUS_HEIGHT_MAX);
        b.cactusWidth   = prng_uniform(cactusPrng, CACTUS_WIDTH_MIN, CACTUS_WIDTH_MAX);

        const double armDraw = prng_uniform(cactusPrng, 0.0, 1.0);
        const double noArmThreshold = 1.0 - CACTUS_ARM_PROBABILITY;
        const double twoArmThreshold = noArmThreshold + CACTUS_TWO_ARM_PROBABILITY * CACTUS_ARM_PROBABILITY;
        if (armDraw < noArmThreshold) {
            b.cactusType = 0;
            b.cactusArmSide = +1;
        } else if (armDraw < twoArmThreshold) {
            b.cactusType = 2;
            b.cactusArmSide = +1;
        } else {
            b.cactusType = 1;
            b.cactusArmSide = prng_uniform(cactusPrng, 0.0, 1.0) < 0.5
                ? static_cast<int8_t>(-1)
                : static_cast<int8_t>(+1);
        }

        if (b.cactusHeight < CACTUS_ARM_MIN_HEIGHT) {
            b.cactusType = 0;
            b.cactusArmSide = +1;
        }

        // Minimum-spacing rule (§Props). Armed cacti reach out to ~1.55 *
        // cactusWidth on each arm side (see Renderer drawCactusArm: arm tip
        // ends at width*1.2 plus the half-stroke width*0.35). Reject any
        // placement that would directly overlap a previously placed cactus,
        // but leave every PRNG draw above intact so the stream stays
        // bit-identical regardless of the placement outcome.
        const double halfWidth = (b.cactusType != 0)
            ? b.cactusWidth * 1.55
            : b.cactusWidth * 0.5;
        if (b.baseX - halfWidth < lastPlacedRight + PROP_MIN_GAP_DIP) {
            restore_original_variants(b);
            continue;
        }
        lastPlacedRight = b.baseX + halfWidth;
    }
}

void generate_tumbleweeds(Sim& sim) noexcept {
    prng_init(sim.tumbleweedPrng, sim.entitySeed ^ TUMBLEWEED_PRNG_SALT);
    const int count = tumbleweed_count_for_width(sim.monitorWidth);
    for (int i = 0; i < count; ++i) {
        sim.entities.push_back(make_tumbleweed(sim.tumbleweedPrng, sim.monitorWidth, sim.windowHeight));
    }
}

void generate_pines_for_winter(Sim& sim) noexcept {
    Prng pinePrng;
    prng_init(pinePrng, sim.entitySeed ^ PINE_PRNG_SALT);
    double lastFgPineRight = -std::numeric_limits<double>::infinity();
    double lastBgPineRight = -std::numeric_limits<double>::infinity();

    for (Blade& b : sim.blades) {
        restore_original_variants(b);
        // Winter biome suppresses mushrooms — they don't fit a snowy, cold scene.
        b.isMushroom = false;

        const double r = prng_uniform(pinePrng, 0.0, 1.0);
        if (r >= PINE_PROBABILITY) continue;

        // Draw order: r (consumed above), variant, height, width, tierCount.
        // Locked sequence so both impls are bit-identical regardless of variant.
        const double variantDraw = prng_uniform(pinePrng, 0.0, 1.0);
        const uint8_t variant    = (variantDraw < BIRCH_VARIANT_PROBABILITY)
                                   ? static_cast<uint8_t>(1)
                                   : static_cast<uint8_t>(0);

        b.isPine      = true;
        b.isFlower    = false;
        b.isMushroom  = false;
        b.treeVariant = variant;
        b.pineHeight  = prng_uniform(pinePrng, PINE_HEIGHT_MIN, PINE_HEIGHT_MAX);

        if (variant == 1) {
            b.pineWidth = prng_uniform(pinePrng, BIRCH_TRUNK_WIDTH_MIN, BIRCH_TRUNK_WIDTH_MAX);
        } else {
            b.pineWidth = prng_uniform(pinePrng, PINE_WIDTH_MIN, PINE_WIDTH_MAX);
        }

        // Tier count drawn for every tree slot to keep the PRNG stream
        // bit-identical across variant outcomes. Only used by pines.
        const double tierDraw = prng_uniform(pinePrng,
            static_cast<double>(PINE_TIER_COUNT_MIN),
            static_cast<double>(PINE_TIER_COUNT_MAX + 1));
        int tiers = static_cast<int>(std::floor(tierDraw));
        if (tiers < PINE_TIER_COUNT_MIN) tiers = PINE_TIER_COUNT_MIN;
        if (tiers > PINE_TIER_COUNT_MAX) tiers = PINE_TIER_COUNT_MAX;
        b.pineTierCount = static_cast<uint8_t>(tiers);

        // Depth layer (§15.4): drawn last in the locked per-tree sequence so the
        // earlier attributes stay bit-identical. Background trees render smaller
        // and hazier, behind the snowbank, for a sense of fore/background depth.
        const double depthDraw = prng_uniform(pinePrng, 0.0, 1.0);
        b.treeBackground = depthDraw < TREE_BACKGROUND_PROBABILITY;

        // Minimum-spacing rule (§Props). Pine canopy base spans `pineWidth`;
        // birch branches fan out to roughly 4× the trunk width (Renderer
        // kBranches: branchBaseLen = trunkW*3.0, lenMul up to 1.6, sin up to
        // ~0.87). Background and foreground trees track independently because
        // they render at distinct z-bands — bg behind the snowbank, fg in
        // front — and a bg silhouette partially hidden by an fg tree is
        // intentional parallax rather than visual overlap.
        double halfWidth = (variant == 1) ? b.pineWidth * 4.0 : b.pineWidth * 0.5;
        if (b.treeBackground) halfWidth *= TREE_BG_SCALE;
        double& lastRight = b.treeBackground ? lastBgPineRight : lastFgPineRight;
        if (b.baseX - halfWidth < lastRight + PROP_MIN_GAP_DIP) {
            restore_original_variants(b);
            // Winter scene-wide suppression — re-apply after the restore so a
            // rejected tree slot doesn't reveal a mushroom that the scene
            // would otherwise hide.
            b.isMushroom = false;
            continue;
        }
        lastRight = b.baseX + halfWidth;
    }
}

void generate_maples_for_autumn(Sim& sim) noexcept {
    Prng maplePrng;
    prng_init(maplePrng, sim.entitySeed ^ MAPLE_PRNG_SALT);
    double lastPlacedRight = -std::numeric_limits<double>::infinity();

    for (Blade& b : sim.blades) {
        restore_original_variants(b);

        const double r = prng_uniform(maplePrng, 0.0, 1.0);
        if (r >= MAPLE_PROBABILITY) continue;

        b.isMaple = true;
        b.isFlower = false;
        b.isMushroom = false;
        b.mapleHeight = prng_uniform(maplePrng, MAPLE_HEIGHT_MIN, MAPLE_HEIGHT_MAX);
        b.mapleTrunkWidth = prng_uniform(maplePrng, MAPLE_TRUNK_WIDTH_MIN, MAPLE_TRUNK_WIDTH_MAX);
        b.mapleCanopyRadius = prng_uniform(maplePrng, MAPLE_CANOPY_RADIUS_MIN, MAPLE_CANOPY_RADIUS_MAX);
        b.mapleCanopyColorIdx = static_cast<uint8_t>(prng_index(maplePrng, MAPLE_CANOPY_COLOR_COUNT));
        b.mapleIsBare = prng_uniform(maplePrng, 0.0, 1.0) < MAPLE_BARE_FRACTION;

        // Minimum-spacing rule (§Props). Canopy is the widest part of the
        // tree; mapleCanopyRadius is its visual half-width directly.
        const double halfWidth = b.mapleCanopyRadius;
        if (b.baseX - halfWidth < lastPlacedRight + PROP_MIN_GAP_DIP) {
            restore_original_variants(b);
            continue;
        }
        lastPlacedRight = b.baseX + halfWidth;
    }
}

void generate_coral_for_ocean(Sim& sim) noexcept {
    Prng coralPrng;
    prng_init(coralPrng, sim.entitySeed ^ CORAL_PRNG_SALT);
    double lastPlacedRight = -std::numeric_limits<double>::infinity();

    for (Blade& b : sim.blades) {
        restore_original_variants(b);
        // Underwater scene suppresses mushrooms — they don't fit a reef.
        b.isMushroom = false;

        const double r = prng_uniform(coralPrng, 0.0, 1.0);
        if (r >= CORAL_PROBABILITY) continue;

        // Draw order: probability (consumed above), height, width, type,
        // color. Locked so PRNG stream is reproducible.
        b.isCoral = true;
        b.isFlower = false;
        b.isMushroom = false;
        b.coralHeight = prng_uniform(coralPrng, CORAL_HEIGHT_MIN, CORAL_HEIGHT_MAX);
        b.coralWidth  = prng_uniform(coralPrng, CORAL_WIDTH_MIN,  CORAL_WIDTH_MAX);
        b.coralType   = static_cast<uint8_t>(prng_index(coralPrng, CORAL_TYPE_COUNT));
        b.coralColorIdx = static_cast<uint8_t>(prng_index(coralPrng, CORAL_COLOR_COUNT));

        // Minimum-spacing rule (§Props). Coral is denser than the land
        // props (~3× the probability), but still placed left-to-right so a
        // single right-edge tracker is enough to prevent direct overlap.
        const double halfWidth = b.coralWidth * 0.5;
        if (b.baseX - halfWidth < lastPlacedRight + PROP_MIN_GAP_DIP) {
            restore_original_variants(b);
            // Ocean scene-wide suppression — re-apply after restore.
            b.isMushroom = false;
            continue;
        }
        lastPlacedRight = b.baseX + halfWidth;
    }
}

// §17 Ocean fish helpers. Mirror the Win2D Sim.TargetFishCount/SpawnFish/
// SpawnInitialFish so the fish stream draws byte-identically across impls.

// Deterministic round-half-to-even that does NOT depend on the FPU/MXCSR
// rounding mode (unlike std::nearbyint). std::floor and std::fmod are
// mode-independent, so this matches C# Math.Round (MidpointRounding.ToEven)
// even if an injected DLL alters the process floating-point control word.
static double round_half_to_even(double x) noexcept {
    double r = std::floor(x);
    double frac = x - r;
    if (frac > 0.5) {
        r += 1.0;
    } else if (frac == 0.5 && std::fmod(r, 2.0) != 0.0) {
        r += 1.0;
    }
    return r;
}

static int target_fish_count(const Sim& sim) noexcept {
    const double scaled = FISH_COUNT_PER_1920DIP * sim.monitorWidth / 1920.0;
    int n = static_cast<int>(round_half_to_even(scaled));
    if (n < FISH_COUNT_MIN) n = FISH_COUNT_MIN;
    if (n > FISH_COUNT_MAX) n = FISH_COUNT_MAX;
    return n;
}

static Entity spawn_fish(Sim& sim, bool fromEdge) noexcept {
    Entity e{};
    e.kind = EntityKind::Fish;
    e.size = prng_uniform(sim.fishPrng, FISH_SIZE_MIN, FISH_SIZE_MAX);
    const double speed = prng_uniform(sim.fishPrng, FISH_SPEED_MIN, FISH_SPEED_MAX);
    const bool swimsRight = (prng_next_u64(sim.fishPrng) & 1ull) != 0ull;
    e.vx = swimsRight ? speed : -speed;
    e.vy = 0.0;
    const double altitude = prng_uniform(sim.fishPrng, FISH_ALTITUDE_MIN, FISH_ALTITUDE_MAX);
    e.y = sim.windowHeight - altitude;
    if (fromEdge) {
        e.x = swimsRight ? -e.size - 4.0 : sim.monitorWidth + e.size + 4.0;
    } else {
        e.x = prng_uniform(sim.fishPrng, 0.0, sim.monitorWidth);
    }
    e.phaseX = prng_uniform(sim.fishPrng, 0.0, TWO_PI);
    e.colorVariant = static_cast<uint8_t>(prng_index(sim.fishPrng, FISH_COLOR_COUNT));
    e.age = 0.0;
    e.lifetime = -1.0; // edge-respawned, not lifetime-bounded
    e.seed = prng_next_u32(sim.fishPrng);
    return e;
}

static void spawn_initial_fish(Sim& sim) noexcept {
    if (sim.monitorWidth <= 0.0) return;
    const int target = target_fish_count(sim);
    for (int i = 0; i < target
            && static_cast<int>(sim.entities.size()) < MAX_ENTITIES_PER_MONITOR; ++i) {
        sim.entities.push_back(spawn_fish(sim, false));
    }
}

// ---------------------------------------------------------------------------
// Sway / gust dynamics
// ---------------------------------------------------------------------------

void update_blade_dynamics(Blade& b, double globalTime, double dt,
                           double swaySpeedScale, double swayAmpScale) noexcept {
    // 1. Gust velocity decays exponentially.
    b.gustVelocity *= std::exp(-DECAY_RATE * dt);

    // 2. Sway is a pure function of globalTime + per-blade phase offset.
    //    swaySpeedScale stretches the phase advance; swayAmpScale scales the lean.
    const double swayPhase = b.swayPhaseOffset + globalTime * BASE_SWAY_SPEED * swaySpeedScale;
    const double baseLean  = std::sin(swayPhase) * BASE_AMPLITUDE * swayAmpScale * b.stiffness;

    // 3. Combined lean.
    b.effectiveLean = baseLean + b.gustVelocity * GUST_TO_LEAN_FACTOR;
}

// ---------------------------------------------------------------------------
// Cut animation
// ---------------------------------------------------------------------------

void advance_cut(Blade& b, double globalTime) noexcept {
    // Phase 1: cut animation is running.
    if (b.cutAnimStart >= 0.0) {
        const double elapsed = globalTime - b.cutAnimStart;
        const double t       = elapsed / CUT_DURATION_SEC;
        if (t >= 1.0) {
            b.cutHeight    = b.cutFloor;
            b.cutAnimStart = -1.0;
            // Schedule regrowth only if the per-blade jitter is well-defined.
            // Production blades from generate_blades always satisfy this;
            // test fixtures that zero-init Blade end up with delay=0 and
            // therefore stay cut, which matches their pre-regrowth contract.
            if (b.regrowDelay > 0.0 && b.regrowDuration > 0.0) {
                b.regrowStart = globalTime + b.regrowDelay;
            }
        } else {
            // Lerp from the height at cut time down to the per-blade stubble
            // floor. With cutFloor == 0 (hand-built fixtures) this reduces to
            // the original cutInitialHeight * (1 - t).
            b.cutHeight = b.cutFloor + (b.cutInitialHeight - b.cutFloor) * (1.0 - t);
        }
        return;
    }

    // Phase 2: regrowth scheduled / running. Idle if regrowStart < 0, the
    // scheduled time hasn't arrived yet, or duration is non-positive.
    if (b.regrowStart < 0.0 || globalTime < b.regrowStart) return;
    if (b.regrowDuration <= 0.0) {
        b.cutHeight   = 1.0;
        b.regrowStart = -1.0;
        return;
    }

    const double elapsed = globalTime - b.regrowStart;
    const double t       = elapsed / b.regrowDuration;
    if (t >= 1.0) {
        b.cutHeight   = 1.0;
        b.regrowStart = -1.0;
    } else {
        // Linear regrowth from the stubble floor back to full height. Same
        // easing curve as the cut animation, reversed and over a longer span.
        // With cutFloor == 0 this reduces to the original cutHeight = t.
        b.cutHeight = b.cutFloor + (1.0 - b.cutFloor) * t;
    }
}

std::vector<persistence::CutRecord> sim_get_cuts(const Sim& sim) {
    std::vector<persistence::CutRecord> cuts;
    cuts.reserve(sim.blades.size());

    for (std::size_t i = 0; i < sim.blades.size(); ++i) {
        const Blade& b = sim.blades[i];
        const bool hasCutState = b.cutAnimStart >= 0.0
                              || b.regrowStart >= 0.0
                              || b.cutHeight < 1.0;
        if (!hasCutState) continue;

        double originalCutTime = sim.globalTime;
        if (b.cutAnimStart >= 0.0) {
            originalCutTime = b.cutAnimStart;
        } else if (b.regrowStart >= 0.0) {
            originalCutTime = b.regrowStart - b.regrowDelay - CUT_DURATION_SEC;
        }

        const double totalRegrowTime = CUT_DURATION_SEC
                                     + std::max(0.0, b.regrowDelay)
                                     + std::max(0.0, b.regrowDuration);
        if (sim.globalTime - originalCutTime >= totalRegrowTime && b.regrowDuration > 0.0) {
            continue;
        }

        cuts.push_back(persistence::CutRecord{
            static_cast<int>(i),
            originalCutTime - sim.globalTime
        });
    }

    return cuts;
}

void sim_apply_cuts(Sim& sim, const std::vector<persistence::CutRecord>& cuts) noexcept {
    for (const persistence::CutRecord& cut : cuts) {
        if (cut.bladeIndex < 0) continue;
        const std::size_t index = static_cast<std::size_t>(cut.bladeIndex);
        if (index >= sim.blades.size()) continue;

        Blade& b = sim.blades[index];
        const double cutTime = cut.cutTime <= 0.0 ? sim.globalTime + cut.cutTime : cut.cutTime;
        const double age = std::max(0.0, sim.globalTime - cutTime);

        b.cutInitialHeight = 1.0;
        if (age < CUT_DURATION_SEC) {
            const double t = age / CUT_DURATION_SEC;
            b.cutAnimStart = cutTime;
            b.cutHeight = b.cutFloor + (1.0 - b.cutFloor) * (1.0 - t);
            b.regrowStart = -1.0;
            continue;
        }

        b.cutAnimStart = -1.0;
        b.cutHeight = b.cutFloor;
        if (b.regrowDelay <= 0.0 || b.regrowDuration <= 0.0) {
            b.regrowStart = -1.0;
            continue;
        }

        const double regrowStart = cutTime + CUT_DURATION_SEC + b.regrowDelay;
        const double regrowElapsed = sim.globalTime - regrowStart;
        if (regrowElapsed < 0.0) {
            b.regrowStart = regrowStart;
            continue;
        }

        if (regrowElapsed >= b.regrowDuration) {
            b.cutHeight = 1.0;
            b.regrowStart = -1.0;
            continue;
        }

        b.cutHeight = b.cutFloor + (1.0 - b.cutFloor) * (regrowElapsed / b.regrowDuration);
        b.regrowStart = regrowStart;
    }
}

// ---------------------------------------------------------------------------
// Move / click event handlers
// ---------------------------------------------------------------------------

void sim_apply_move(Sim& sim, const InputEvent& e) noexcept {
    // Reject non-finite cursor coordinates before touching the baseline or
    // emitting anything: NaN compares false, so an unguarded NaN would slip past
    // the band checks below, poison prevCursor, and (now that move can emit)
    // spawn degenerate puffs.
    if (!std::isfinite(e.x) || !std::isfinite(e.y) || !std::isfinite(e.time)) return;

    // §16.6 leaf puff: hovering a leafy maple canopy shakes leaves loose.
    // Independent of the gust band so it fires even directly over the crown.
    if (sim.currentScene == Scene::Autumn && std::isfinite(e.x) && std::isfinite(e.y)) {
        const double groundY2 = sim.windowHeight;
        for (Blade& b : sim.blades) {
            if (!b.isMaple || b.mapleIsBare) continue;
            if (b.cutHeight < LEAF_PUFF_MIN_CUT_HEIGHT) continue;
            if (sim.globalTime < b.leafPuffCooldownEnd) continue;
            const double cx = b.baseX;
            const double cy = groundY2 - b.mapleHeight * b.cutHeight;
            const double canopyR = b.mapleCanopyRadius * b.cutHeight;
            const double hoverR = canopyR * LEAF_PUFF_HOVER_RADIUS_MUL;
            const double dx = e.x - cx;
            const double dy = e.y - cy;
            if (dx * dx + dy * dy > hoverR * hoverR) continue;
            const int count = LEAF_PUFF_COUNT_MIN
                + static_cast<int>(prng_index(sim.leafPuffPrng,
                                              LEAF_PUFF_COUNT_MAX - LEAF_PUFF_COUNT_MIN + 1));
            bool emittedAny = false;
            for (int i = 0; i < count; ++i) {
                if (sim.entities.size() >= static_cast<std::size_t>(MAX_ENTITIES_PER_MONITOR)) break;
                sim.entities.push_back(make_puff_leaf(sim.leafPuffPrng, cx, cy, canopyR));
                emittedAny = true;
            }
            // Only arm the cooldown when leaves actually shed; if the entity cap
            // was full the hover was a visual no-op and may retry next move.
            if (emittedAny) b.leafPuffCooldownEnd = sim.globalTime + LEAF_PUFF_COOLDOWN_SEC;
        }
    }

    const double groundY        = sim.windowHeight;
    const double gustBandTop    = groundY - STRIP_HEIGHT - HEADROOM;
    const double gustBandBottom = groundY;

    // First / re-init event: don't emit an impulse.
    const bool firstEvent =
        sim.prevCursorTime < 0.0 ||
        (e.time - sim.prevCursorTime) > CURSOR_REINIT_GAP_SEC;

    if (firstEvent) {
        sim.prevCursorX    = e.x;
        sim.prevCursorTime = e.time;
        return;
    }

    if (e.y < gustBandTop || e.y > gustBandBottom) {
        // Outside the gust band — update baseline (so the *next* in-band event
        // gets a sensible velocity), but emit no impulse.
        sim.prevCursorX    = e.x;
        sim.prevCursorTime = e.time;
        return;
    }

    const double dt_ev  = std::max(e.time - sim.prevCursorTime, 1.0 / 1000.0);
    const double velX   = (e.x - sim.prevCursorX) / dt_ev;
    const double capped = std::max(-MAX_CURSOR_SPEED, std::min(velX, MAX_CURSOR_SPEED));

    sim.prevCursorX    = e.x;
    sim.prevCursorTime = e.time;

    // §21.1 snow drift: brushing the cursor low and fast across the Winter
    // snowbank kicks up a gentle wisp of powder (the move-driven analogue of the
    // autumn leaf-puff). Gated by a low band near the ground, a minimum cursor
    // speed, and a global cooldown so it stays calm. Like the click puff we draw
    // the full locked PRNG sequence per intended grain and only append when
    // capacity allows, so the stream is independent of the entity count.
    if (sim.currentScene == Scene::Winter) {
        const double driftBandTop = groundY - SNOW_DRIFT_REACH_DIP;
        if (e.y >= driftBandTop && e.y <= groundY
            && std::fabs(capped) >= SNOW_DRIFT_MIN_SPEED
            && sim.globalTime >= sim.snowDriftCooldownEnd) {
            const int count = SNOW_DRIFT_COUNT_MIN
                + static_cast<int>(prng_index(sim.snowDriftPrng,
                                              SNOW_DRIFT_COUNT_MAX - SNOW_DRIFT_COUNT_MIN + 1));
            for (int i = 0; i < count; ++i) {
                // Kick the powder up from the snow surface beneath the cursor
                // (x follows the cursor, y anchored to the ground) rather than
                // from wherever the cursor floats in the band.
                Entity puff = make_snow_puff(sim.snowDriftPrng, e.x, groundY, groundY,
                                             SNOW_DRIFT_SIZE_SCALE, SNOW_DRIFT_SPEED_SCALE);
                if (sim.entities.size() < static_cast<std::size_t>(MAX_ENTITIES_PER_MONITOR)) {
                    sim.entities.push_back(puff);
                }
            }
            // Arm the cooldown on every qualifying burst (even if the cap was
            // full) so a saturated entity pool can't spin at the OS move rate.
            sim.snowDriftCooldownEnd = sim.globalTime + SNOW_DRIFT_COOLDOWN_SEC;
        }
    }

    const double impulseMagnitude = std::fabs(capped) * IMPULSE_SCALE;
    const double signDir = (capped > 0.0) ? 1.0 : (capped < 0.0 ? -1.0 : 0.0);
    if (signDir == 0.0 || impulseMagnitude == 0.0) return;

    for (Blade& b : sim.blades) {
        const double dxAbs = std::fabs(b.baseX - e.x);
        if (dxAbs >= GUST_RADIUS) continue;

        const double tRaw  = 1.0 - dxAbs / GUST_RADIUS;
        const double s     = std::max(0.0, std::min(1.0, tRaw));
        const double smooth = s * s * (3.0 - 2.0 * s);
        b.gustVelocity += impulseMagnitude * smooth * signDir;
    }
}

void sim_apply_click(Sim& sim, const InputEvent& e) noexcept {
    // Reject non-finite cursor coordinates before anything else: NaN compares
    // false, so an unguarded NaN click would slip past the radius checks below
    // and cut every blade (and emit a degenerate puff).
    if (!std::isfinite(e.x) || !std::isfinite(e.y)) return;

    const double groundY       = sim.windowHeight;
    const double cutBandTop    = groundY - STRIP_HEIGHT;
    const double cutBandBottom = groundY;

    if (e.y < cutBandTop || e.y > cutBandBottom) return;

    // Snow puff (§21): a click on the Winter snowbank kicks up a burst of
    // powder. We always draw the full locked PRNG sequence per intended puff
    // and only append when capacity allows, so the stream stays independent of
    // the transient entity count.
    if (sim.currentScene == Scene::Winter) {
        const int count = SNOW_PUFF_COUNT_MIN
            + static_cast<int>(prng_index(sim.snowPuffPrng,
                                          SNOW_PUFF_COUNT_MAX - SNOW_PUFF_COUNT_MIN + 1));
        for (int i = 0; i < count; ++i) {
            Entity puff = make_snow_puff(sim.snowPuffPrng, e.x, e.y, groundY);
            if (sim.entities.size() < static_cast<std::size_t>(MAX_ENTITIES_PER_MONITOR)) {
                sim.entities.push_back(puff);
            }
        }
    }

    for (Blade& b : sim.blades) {
        if (std::fabs(b.baseX - e.x) >= CUT_RADIUS) continue;
        // Already at (or below) its stubble floor — can't be cut shorter.
        if (b.cutHeight <= b.cutFloor) continue;
        if (b.cutAnimStart >= 0.0) continue;

        b.cutAnimStart     = sim.globalTime;
        b.cutInitialHeight = b.cutHeight;
        // Cancel any pending or in-progress regrowth: we're going back down.
        b.regrowStart      = -1.0;
    }

    // Sheep startle (§16). A click within SHEEP_STARTLE_RADIUS of a sheep
    // makes it hop AWAY from the cursor: state → Hopping, vx flipped to the
    // away direction, speed boosted (capped so repeated clicks don't
    // compound), age reset so the hop arc starts fresh.
    for (Entity& ent : sim.entities) {
        if (ent.kind != EntityKind::Sheep) continue;
        const double dxClick = ent.x - e.x;
        if (std::fabs(dxClick) >= SHEEP_STARTLE_RADIUS) continue;
        const double awayDir = dxClick >= 0.0 ? 1.0 : -1.0;
        const double speed = std::min(std::abs(ent.vx) * SHEEP_STARTLE_BOOST,
                                      SHEEP_WALK_SPEED_MAX * SHEEP_STARTLE_BOOST);
        ent.vx = speed * awayDir;
        ent.state      = SHEEP_STATE_HOPPING;
        ent.stateTimer = SHEEP_HOP_DURATION;
        ent.age        = 0.0;
    }

    // Cat pounce (§17). Passive and click-only: a nearby cat pounces TOWARD
    // the click, reusing the sheep Hopping state byte as Pouncing.
    for (Entity& ent : sim.entities) {
        if (ent.kind != EntityKind::Cat) continue;
        const double dxClick = e.x - ent.x;
        if (std::fabs(dxClick) >= CAT_POUNCE_RADIUS) continue;
        const double towardDir = dxClick >= 0.0 ? 1.0 : -1.0;
        ent.vx = towardDir * CAT_POUNCE_SPEED;
        ent.state      = CAT_STATE_POUNCING;
        ent.stateTimer = CAT_POUNCE_DURATION;
        ent.age        = 0.0;
    }

    // Bunny startle (§18). Bunnies are shy: nearby clicks wake/break their
    // current pose and send them hopping AWAY from the click point.
    for (Entity& ent : sim.entities) {
        if (ent.kind != EntityKind::Bunny) continue;
        const double dxClick = ent.x - e.x;
        const double dyClick = ent.y - e.y;
        if ((dxClick * dxClick + dyClick * dyClick) > (BUNNY_STARTLE_RADIUS * BUNNY_STARTLE_RADIUS)) continue;
        const double awayDir = dxClick >= 0.0 ? 1.0 : -1.0;
        double baseSpeed = ent.rotationSpeed;
        if (baseSpeed <= 0.0) baseSpeed = std::min(std::max(std::abs(ent.vx), BUNNY_HOP_SPEED_MIN), BUNNY_HOP_SPEED_MAX);
        ent.rotationSpeed = baseSpeed;
        ent.vx = awayDir * baseSpeed * BUNNY_STARTLE_BOOST;
        ent.state      = BUNNY_STATE_STARTLED;
        ent.stateTimer = BUNNY_STARTLE_DURATION;
        ent.age        = 0.0;
    }

    // Hedgehog startle (§17.9). Passive defense: stop, curl, keep vx direction.
    for (Entity& ent : sim.entities) {
        if (ent.kind != EntityKind::Hedgehog || ent.state == HEDGEHOG_STATE_CURLED) continue;
        const double dxClick = ent.x - e.x;
        const double dyClick = ent.y - e.y;
        if ((dxClick * dxClick + dyClick * dyClick) > (HEDGEHOG_STARTLE_RADIUS * HEDGEHOG_STARTLE_RADIUS)) continue;
        ent.previousState = ent.state == HEDGEHOG_STATE_SLEEPING ? HEDGEHOG_STATE_WALKING : ent.state;
        ent.previousStateTimer = ent.state == HEDGEHOG_STATE_SLEEPING ? 0.0 : ent.stateTimer;
        ent.state = HEDGEHOG_STATE_CURLED;
        ent.stateTimer = prng_uniform(sim.critterPrng, HEDGEHOG_CURL_DURATION_MIN, HEDGEHOG_CURL_DURATION_MAX);
        ent.age = 0.0;
    }
}

// ---------------------------------------------------------------------------
// Ambient gusts (§8.1)
// ---------------------------------------------------------------------------

void sim_apply_ambient_gust(Sim& sim, double x, double signDir, double magFactor) noexcept {
    const double impulseMagnitude = MAX_CURSOR_SPEED * magFactor * IMPULSE_SCALE;
    const double radius           = GUST_RADIUS * AMBIENT_GUST_RADIUS_FACTOR;
    if (signDir == 0.0 || impulseMagnitude == 0.0 || radius <= 0.0) return;

    for (Blade& b : sim.blades) {
        const double dxAbs = std::fabs(b.baseX - x);
        if (dxAbs >= radius) continue;

        const double tRaw   = 1.0 - dxAbs / radius;
        const double s      = std::max(0.0, std::min(1.0, tRaw));
        const double smooth = s * s * (3.0 - 2.0 * s);
        b.gustVelocity += impulseMagnitude * smooth * signDir;
    }
}

void sim_tick_ambient_gusts(Sim& sim) noexcept {
    // Per-fire draw order is fixed (§8.1): x, signDir, magFactor, interval.
    while (sim.globalTime >= sim.nextAmbientGustTime) {
        const double x         = prng_uniform(sim.ambientPrng, 0.0, sim.monitorWidth);
        const double signDir   = prng_uniform(sim.ambientPrng, 0.0, 1.0) < 0.5 ? -1.0 : 1.0;
        const double magFactor = prng_uniform(sim.ambientPrng,
                                              AMBIENT_GUST_MAG_FACTOR_MIN,
                                              AMBIENT_GUST_MAG_FACTOR_MAX);
        sim_apply_ambient_gust(sim, x, signDir, magFactor);

        const double interval  = prng_uniform(sim.ambientPrng,
                                              AMBIENT_GUST_INTERVAL_MIN,
                                              AMBIENT_GUST_INTERVAL_MAX);
        sim.nextAmbientGustTime += interval;
    }
}

// ---------------------------------------------------------------------------
// Scenes (§13)
// ---------------------------------------------------------------------------

namespace {

int resolve_count_from_prng(Prng& prng, int minCount, int maxCount) noexcept {
    const double countDraw = prng_uniform(prng,
        static_cast<double>(minCount),
        static_cast<double>(maxCount + 1));
    int count = static_cast<int>(std::floor(countDraw));
    if (count < minCount) count = minCount;
    if (count > maxCount) count = maxCount;
    return count;
}

int resolve_critter_count(Sim& sim, int minCount, int maxCount, bool allowOverride) noexcept {
    if (allowOverride && sim.critterCountOverride > 0) {
        return std::min(sim.critterCountOverride, PET_COUNT_MAX_PER_MONITOR);
    }
    return resolve_count_from_prng(sim.critterPrng, minCount, maxCount);
}

double flyer_grass_top_y(const Sim& sim) noexcept {
    return sim.windowHeight - BLADE_HEIGHT_MAX;
}

double butterfly_velocity(const Entity& e) noexcept {
    const double dir = e.vx >= 0.0 ? 1.0 : -1.0;
    return e.baseSpeed * dir * (1.0 + BUTTERFLY_MEANDER_AMP_X
        * std::sin(e.age * BUTTERFLY_MEANDER_FREQ_X + e.phaseX));
}

double firefly_velocity(const Entity& e) noexcept {
    const double dir = e.vx >= 0.0 ? 1.0 : -1.0;
    return e.baseSpeed * dir * (1.0 + FIREFLY_DRIFT_AMP_X
        * std::sin(e.age * FIREFLY_DRIFT_FREQ_X + e.phaseX));
}

void update_butterfly_position(Entity& e, const Sim& sim) noexcept {
    e.vx = butterfly_velocity(e);
    e.y = flyer_grass_top_y(sim) - e.altitudeAnchor
        + BUTTERFLY_MEANDER_AMP_Y * std::sin(e.age * BUTTERFLY_MEANDER_FREQ_Y + e.phaseY);
}

void update_firefly_position(Entity& e, const Sim& sim) noexcept {
    e.vx = firefly_velocity(e);
    e.y = flyer_grass_top_y(sim) - e.altitudeAnchor
        + FIREFLY_DRIFT_AMP_Y * std::sin(e.age * FIREFLY_DRIFT_FREQ_Y + e.phaseY);
}

void update_bird_position(Entity& e, const Sim& sim) noexcept {
    e.y = flyer_grass_top_y(sim) - e.altitudeAnchor
        + BIRD_DRIFT_AMP_Y * std::sin(e.age * BIRD_DRIFT_FREQ_Y + e.phaseY);
}

void remove_critters(Sim& sim) noexcept {
    sim.entities.erase(
        std::remove_if(sim.entities.begin(), sim.entities.end(),
            [](const Entity& e) {
                return e.kind == EntityKind::Sheep
                    || e.kind == EntityKind::Cat
                    || e.kind == EntityKind::Bunny
                    || e.kind == EntityKind::Hedgehog
                    || e.kind == EntityKind::Butterfly
                    || e.kind == EntityKind::Firefly;
            }),
        sim.entities.end());
}

void remove_scene_transition_entities(Sim& sim) noexcept {
    sim.entities.clear();
}

void generate_critters_sheep(Sim& sim, bool allowOverride) noexcept {
    const int count = resolve_critter_count(sim, SHEEP_COUNT_MIN, SHEEP_COUNT_MAX, allowOverride);

    const double groundY = sim.windowHeight;
    for (int i = 0; i < count
         && static_cast<int>(sim.entities.size()) < MAX_ENTITIES_PER_MONITOR; ++i) {
        Entity e{};
        e.kind = EntityKind::Sheep;
        e.size = SHEEP_BODY_RADIUS;
        // Spawn anywhere across the width, leaving an edge margin so the
        // bounce-on-edge logic doesn't fire instantly.
        const double margin = e.size + 8.0;
        e.x  = prng_uniform(sim.critterPrng, margin, sim.monitorWidth - margin);
        // Sit the sheep so its leg-tips touch the ground line. Body center
        // is one body-height + leg-length above the ground.
        e.y  = groundY - SHEEP_BODY_HEIGHT - SHEEP_LEG_LENGTH;
        const double speed = prng_uniform(sim.critterPrng,
                                          SHEEP_WALK_SPEED_MIN,
                                          SHEEP_WALK_SPEED_MAX);
        const double dir = prng_uniform(sim.critterPrng, 0.0, 1.0) < 0.5 ? -1.0 : 1.0;
        e.vx = dir * speed;
        e.vy = 0.0;
        e.rotation = 0.0;
        e.rotationSpeed = 0.0;
        e.age = 0.0;
        e.lifetime = -1.0;
        e.seed = prng_next_u32(sim.critterPrng);
        // Initial state: Walking, with a random walk-leg duration.
        e.state = SHEEP_STATE_WALKING;
        e.stateTimer = prng_uniform(sim.critterPrng,
                                    SHEEP_WALK_DURATION_MIN,
                                    SHEEP_WALK_DURATION_MAX);
        e.nameIndex = static_cast<uint8_t>(prng_index(sim.critterPrng,
            static_cast<uint32_t>(sizeof(SHEEP_NAME_POOL) / sizeof(SHEEP_NAME_POOL[0]))));
        sim.entities.push_back(e);
    }
}

void generate_critters_cat(Sim& sim, bool allowOverride) noexcept {
    const int count = resolve_critter_count(sim, CAT_COUNT_MIN, CAT_COUNT_MAX, allowOverride);

    const double groundY = sim.windowHeight;
    for (int i = 0; i < count
         && static_cast<int>(sim.entities.size()) < MAX_ENTITIES_PER_MONITOR; ++i) {
        Entity e{};
        e.kind = EntityKind::Cat;
        e.size = CAT_BODY_RADIUS;
        const double margin = e.size + 8.0;
        e.x = prng_uniform(sim.critterPrng, margin, sim.monitorWidth - margin);
        e.y = groundY - CAT_BODY_HEIGHT - CAT_LEG_LENGTH;
        const double speed = prng_uniform(sim.critterPrng,
                                          CAT_WALK_SPEED_MIN,
                                          CAT_WALK_SPEED_MAX);
        const double dir = prng_uniform(sim.critterPrng, 0.0, 1.0) < 0.5 ? -1.0 : 1.0;
        e.vx = dir * speed;
        e.vy = 0.0;
        e.rotation = 0.0;
        e.rotationSpeed = 0.0;
        e.age = 0.0;
        e.lifetime = -1.0;
        e.seed = prng_next_u32(sim.critterPrng);
        e.state = CAT_STATE_WALKING;
        e.stateTimer = prng_uniform(sim.critterPrng,
                                    CAT_WALK_DURATION_MIN,
                                    CAT_WALK_DURATION_MAX);
        e.nameIndex = static_cast<uint8_t>(prng_index(sim.critterPrng,
            static_cast<uint32_t>(sizeof(CAT_NAME_POOL) / sizeof(CAT_NAME_POOL[0]))));
        e.coatVariantIndex = static_cast<uint8_t>(prng_index(sim.critterPrng,
            static_cast<uint32_t>(CAT_COAT_VARIANT_COUNT)));
        sim.entities.push_back(e);
    }
}

void generate_critters_bunny(Sim& sim, bool allowOverride) noexcept {
    const int count = resolve_critter_count(sim, BUNNY_COUNT_MIN, BUNNY_COUNT_MAX, allowOverride);

    const double groundY = sim.windowHeight;
    for (int i = 0; i < count
         && static_cast<int>(sim.entities.size()) < MAX_ENTITIES_PER_MONITOR; ++i) {
        Entity e{};
        e.kind = EntityKind::Bunny;
        e.size = BUNNY_BODY_RADIUS;
        const double margin = e.size + 8.0;
        const double usableWidth = std::max(0.0, sim.monitorWidth - 2.0 * margin);
        const double xFrac = prng_uniform(sim.critterPrng, 0.0, 1.0);
        e.x = margin + xFrac * usableWidth;
        const uint64_t vxSign = prng_next_u64(sim.critterPrng) & 1ull;
        const double dir = (vxSign != 0ull) ? 1.0 : -1.0;
        const double speed = prng_uniform(sim.critterPrng,
                                          BUNNY_HOP_SPEED_MIN,
                                          BUNNY_HOP_SPEED_MAX);
        e.vx = dir * speed;
        e.vy = 0.0;
        e.rotation = 0.0;
        e.rotationSpeed = speed;
        e.age = 0.0;
        e.lifetime = -1.0;
        e.seed = static_cast<uint32_t>(i + 1);
        e.state = BUNNY_STATE_HOPPING;
        e.stateTimer = BUNNY_HOP_DURATION;
        e.nameIndex = static_cast<uint8_t>(prng_index(sim.critterPrng,
            static_cast<uint32_t>(sizeof(BUNNY_NAME_POOL) / sizeof(BUNNY_NAME_POOL[0]))));
        e.y = groundY - BUNNY_BODY_HEIGHT - BUNNY_LEG_LENGTH;
        sim.entities.push_back(e);
    }
}

void generate_critters_hedgehog(Sim& sim) noexcept {
    const int count = prng_uniform(sim.critterPrng, 0.0, 1.0) < HEDGEHOG_COUNT_PROBABILITY ? 1 : 0;

    const double groundY = sim.windowHeight;
    for (int i = 0; i < count
         && static_cast<int>(sim.entities.size()) < MAX_ENTITIES_PER_MONITOR; ++i) {
        Entity e{};
        e.kind = EntityKind::Hedgehog;
        e.size = HEDGEHOG_BODY_RADIUS;
        const double margin = e.size + 8.0;
        const double usableWidth = std::max(0.0, sim.monitorWidth - 2.0 * margin);
        const double xFrac = prng_uniform(sim.critterPrng, 0.0, 1.0);
        e.x = margin + xFrac * usableWidth;
        const uint64_t vxSign = prng_next_u64(sim.critterPrng) & 1ull;
        const double dir = (vxSign != 0ull) ? 1.0 : -1.0;
        const double speed = prng_uniform(sim.critterPrng,
                                          HEDGEHOG_WALK_SPEED_MIN,
                                          HEDGEHOG_WALK_SPEED_MAX);
        e.vx = dir * speed;
        e.vy = 0.0;
        e.rotation = 0.0;
        e.rotationSpeed = speed;
        e.age = 0.0;
        e.lifetime = -1.0;
        e.seed = static_cast<uint32_t>(i + 1);
        e.state = HEDGEHOG_STATE_WALKING;
        e.stateTimer = HEDGEHOG_WALK_DURATION_MIN;
        e.previousState = HEDGEHOG_STATE_WALKING;
        e.previousStateTimer = 0.0;
        e.nameIndex = static_cast<uint8_t>(prng_index(sim.critterPrng,
            static_cast<uint32_t>(sizeof(HEDGEHOG_NAME_POOL) / sizeof(HEDGEHOG_NAME_POOL[0]))));
        e.y = groundY - HEDGEHOG_BODY_HEIGHT - HEDGEHOG_LEG_LENGTH;
        sim.entities.push_back(e);
    }
}

void generate_butterflies(Sim& sim) noexcept {
    if (sim.monitorWidth <= 0.0) return;

    Prng butterflyPrng;
    prng_init(butterflyPrng, sim.entitySeed ^ BUTTERFLY_PRNG_SALT);
    const int count = resolve_count_from_prng(butterflyPrng, BUTTERFLY_COUNT_MIN, BUTTERFLY_COUNT_MAX);

    for (int i = 0; i < count
         && static_cast<int>(sim.entities.size()) < MAX_ENTITIES_PER_MONITOR; ++i) {
        Entity e{};
        e.kind = EntityKind::Butterfly;
        e.size = BUTTERFLY_WING_RADIUS;
        const double xFrac = prng_uniform(butterflyPrng, 0.0, 1.0);
        const double yFrac = prng_uniform(butterflyPrng, 0.0, 1.0);
        const uint64_t vxSign = prng_next_u64(butterflyPrng) & 1ull;
        const double dir = (vxSign != 0ull) ? 1.0 : -1.0;
        e.baseSpeed = prng_uniform(butterflyPrng, BUTTERFLY_SPEED_MIN, BUTTERFLY_SPEED_MAX);
        e.rotationSpeed = e.baseSpeed;
        e.colorVariant = static_cast<uint8_t>(prng_index(butterflyPrng, static_cast<uint32_t>(BUTTERFLY_COLOR_COUNT)));
        e.phaseY = prng_uniform(butterflyPrng, 0.0, TWO_PI);
        e.phaseX = prng_uniform(butterflyPrng, 0.0, TWO_PI);
        e.altitudeAnchor = BUTTERFLY_ALTITUDE_MIN
            + yFrac * (BUTTERFLY_ALTITUDE_MAX - BUTTERFLY_ALTITUDE_MIN);
        e.x = xFrac * sim.monitorWidth;
        e.vx = dir * e.baseSpeed;
        e.vy = 0.0;
        e.age = 0.0;
        e.lifetime = -1.0;
        e.seed = static_cast<uint32_t>(i + 1);
        update_butterfly_position(e, sim);
        sim.entities.push_back(e);
    }
}

void generate_fireflies(Sim& sim) noexcept {
    if (sim.monitorWidth <= 0.0) return;

    Prng fireflyPrng;
    prng_init(fireflyPrng, sim.entitySeed ^ FIREFLY_PRNG_SALT);
    const int count = resolve_count_from_prng(fireflyPrng, FIREFLY_COUNT_MIN, FIREFLY_COUNT_MAX);

    for (int i = 0; i < count
         && static_cast<int>(sim.entities.size()) < MAX_ENTITIES_PER_MONITOR; ++i) {
        Entity e{};
        e.kind = EntityKind::Firefly;
        e.size = FIREFLY_BODY_RADIUS;
        const double xFrac = prng_uniform(fireflyPrng, 0.0, 1.0);
        const double yFrac = prng_uniform(fireflyPrng, 0.0, 1.0);
        const uint64_t vxSign = prng_next_u64(fireflyPrng) & 1ull;
        const double dir = (vxSign != 0ull) ? 1.0 : -1.0;
        e.baseSpeed = prng_uniform(fireflyPrng, FIREFLY_DRIFT_SPEED_MIN, FIREFLY_DRIFT_SPEED_MAX);
        e.rotationSpeed = e.baseSpeed;
        e.blinkPeriod = prng_uniform(fireflyPrng, FIREFLY_BLINK_PERIOD_MIN, FIREFLY_BLINK_PERIOD_MAX);
        e.blinkPhase = prng_uniform(fireflyPrng, 0.0, 1.0);
        e.phaseY = prng_uniform(fireflyPrng, 0.0, TWO_PI);
        e.phaseX = prng_uniform(fireflyPrng, 0.0, TWO_PI);
        e.altitudeAnchor = FIREFLY_ALTITUDE_MIN
            + yFrac * (FIREFLY_ALTITUDE_MAX - FIREFLY_ALTITUDE_MIN);
        e.x = xFrac * sim.monitorWidth;
        e.vx = dir * e.baseSpeed;
        e.vy = 0.0;
        e.age = 0.0;
        e.lifetime = -1.0;
        e.seed = static_cast<uint32_t>(i + 1);
        update_firefly_position(e, sim);
        sim.entities.push_back(e);
    }
}

void generate_grass_critters_all(Sim& sim) noexcept {
    generate_critters_sheep(sim, false);
    generate_critters_cat(sim, false);
    generate_critters_bunny(sim, false);
    generate_critters_hedgehog(sim);
}

void generate_critters_for_kind(Sim& sim) noexcept {
    prng_init(sim.critterPrng, sim.entitySeed ^ CRITTER_PRNG_SALT);
    if (sim.currentScene != Scene::Grass) return;

    switch (sim.currentCritter) {
    case CritterKind::None:  break;
    case CritterKind::Sheep: generate_critters_sheep(sim, true);    break;
    case CritterKind::Cat:   generate_critters_cat(sim, true);      break;
    case CritterKind::Bunny: generate_grass_critters_all(sim);      break;
    }

    generate_butterflies(sim);
    generate_fireflies(sim);
}

void restore_bunny_base_speed(Entity& e) noexcept {
    double baseSpeed = e.rotationSpeed;
    if (baseSpeed <= 0.0) {
        baseSpeed = std::min(std::max(std::abs(e.vx), BUNNY_HOP_SPEED_MIN), BUNNY_HOP_SPEED_MAX);
        e.rotationSpeed = baseSpeed;
    }
    const double dir = e.vx >= 0.0 ? 1.0 : -1.0;
    e.vx = dir * baseSpeed;
}

void start_bunny_hopping(Sim& sim, Entity& e, bool includeGap) noexcept {
    restore_bunny_base_speed(e);
    e.state = BUNNY_STATE_HOPPING;
    e.stateTimer = BUNNY_HOP_DURATION;
    if (includeGap) {
        e.stateTimer += prng_uniform(sim.critterPrng, BUNNY_HOP_GAP_MIN, BUNNY_HOP_GAP_MAX);
    }
    e.age = 0.0;
}

void enter_bunny_rest_state(Sim& sim, Entity& e) noexcept {
    const uint8_t next = bunny_choose_rest_state(sim.critterPrng);
    e.state = next;
    if (next == BUNNY_STATE_GRAZING) {
        e.stateTimer = prng_uniform(sim.critterPrng,
                                    BUNNY_GRAZE_DURATION_MIN,
                                    BUNNY_GRAZE_DURATION_MAX);
    } else if (next == BUNNY_STATE_IDLE) {
        e.stateTimer = prng_uniform(sim.critterPrng,
                                    BUNNY_IDLE_DURATION_MIN,
                                    BUNNY_IDLE_DURATION_MAX);
    } else {
        e.stateTimer = prng_uniform(sim.critterPrng,
                                    BUNNY_SLEEP_DURATION_MIN,
                                    BUNNY_SLEEP_DURATION_MAX);
    }
    e.age = 0.0;
}

double hedgehog_duration_for_state(Sim& sim, uint8_t state) noexcept {
    switch (state) {
    case HEDGEHOG_STATE_SNUFFLING:
        return prng_uniform(sim.critterPrng, HEDGEHOG_SNUFFLE_DURATION_MIN, HEDGEHOG_SNUFFLE_DURATION_MAX);
    case HEDGEHOG_STATE_IDLE:
        return prng_uniform(sim.critterPrng, HEDGEHOG_IDLE_DURATION_MIN, HEDGEHOG_IDLE_DURATION_MAX);
    case HEDGEHOG_STATE_SLEEPING:
        return prng_uniform(sim.critterPrng, HEDGEHOG_SLEEP_DURATION_MIN, HEDGEHOG_SLEEP_DURATION_MAX);
    case HEDGEHOG_STATE_CURLED:
        return prng_uniform(sim.critterPrng, HEDGEHOG_CURL_DURATION_MIN, HEDGEHOG_CURL_DURATION_MAX);
    case HEDGEHOG_STATE_WALKING:
    default:
        return prng_uniform(sim.critterPrng, HEDGEHOG_WALK_DURATION_MIN, HEDGEHOG_WALK_DURATION_MAX);
    }
}

} // anonymous

void sim_set_scene(Sim& sim, Scene s) noexcept {
    // Reset the spindrift cooldown so re-entering Winter can kick up powder
    // immediately rather than waiting out a stale gate.
    sim.snowDriftCooldownEnd = 0.0;
    sim.currentScene = s;
    // Scene transitions clear all roaming entities; each scene repopulates its
    // own below.
    remove_scene_transition_entities(sim);

    // Every scene transition starts from a clean blade-variant slate so that
    // e.g. Desert→Winter doesn't leave cacti on screen. Desert then promotes
    // selected slots back into cacti below.
    for (Blade& b : sim.blades) {
        restore_original_variants(b);
    }

    switch (s) {
    case Scene::Grass:
        break;
    case Scene::Desert:
        generate_cacti_for_desert(sim);
        generate_tumbleweeds(sim);
        break;
    case Scene::Winter: {
        generate_pines_for_winter(sim);
        prng_init(sim.snowflakePrng, sim.entitySeed ^ SNOWFLAKE_PRNG_SALT);
        prng_init(sim.snowPuffPrng, sim.entitySeed ^ SNOW_PUFF_PRNG_SALT);
        prng_init(sim.snowDriftPrng, sim.entitySeed ^ SNOW_DRIFT_PRNG_SALT);
        const double lambda = SNOWFLAKE_EMIT_RATE_PER_1920DIP * sim.monitorWidth / 1920.0;
        sim.nextSnowflakeSpawnTime = sim.globalTime + prng_exponential(sim.snowflakePrng, lambda);
        break;
    }
    case Scene::Autumn:
        generate_maples_for_autumn(sim);
        prng_init(sim.leafPrng, sim.entitySeed ^ LEAF_PRNG_SALT);
        prng_init(sim.leafPuffPrng, sim.entitySeed ^ LEAF_PUFF_PRNG_SALT);
        sim.nextLeafSpawnTime = sim.globalTime;
        break;
    case Scene::Ocean:
        generate_coral_for_ocean(sim);
        prng_init(sim.bubblePrng, sim.entitySeed ^ BUBBLE_PRNG_SALT);
        sim.nextBubbleSpawnTime = sim.globalTime;
        prng_init(sim.fishPrng, sim.entitySeed ^ FISH_PRNG_SALT);
        spawn_initial_fish(sim);
        break;
    }

    // Grass critters/flyers are scene-gated inside generate_critters_for_kind.
    // Run LAST so scene entity snapshots stay pinned.
    generate_critters_for_kind(sim);
}

void sim_set_critter(Sim& sim, CritterKind c) noexcept {
    sim.currentCritter = c;
    // Erase only critter entities — scene entities (tumbleweeds, snowflakes)
    // are preserved across critter toggles.
    remove_critters(sim);
    generate_critters_for_kind(sim);
}

void sim_set_critter_count(Sim& sim, int n) noexcept {
    sim.critterCountOverride = (n > 0) ? n : 0;
    remove_critters(sim);
    generate_critters_for_kind(sim);
}

double bird_flyby_sample_interval(Prng& p) noexcept {
    return prng_exponential(p, BIRD_FLYBY_SPAWN_RATE_PER_HOUR / 3600.0);
}

void sim_spawn_bird_flyby(Sim& sim) noexcept {
    if (sim.monitorWidth <= 0.0) return;

    const int flockSize = resolve_count_from_prng(sim.birdFlybyPrng, BIRD_FLOCK_SIZE_MIN, BIRD_FLOCK_SIZE_MAX);
    const uint64_t directionBit = prng_next_u64(sim.birdFlybyPrng) & 1ull;
    const double direction = directionBit != 0ull ? 1.0 : -1.0;
    const double leaderAltitude = prng_uniform(sim.birdFlybyPrng, BIRD_ALTITUDE_MIN, BIRD_ALTITUDE_MAX);
    const double leaderSpeed = prng_uniform(sim.birdFlybyPrng, BIRD_SPEED_MIN, BIRD_SPEED_MAX);
    const uint64_t formationStyle = prng_next_u64(sim.birdFlybyPrng) & 1ull;

    struct BirdDraw { double wingPhaseOffset; double verticalDriftPhase; };
    BirdDraw draws[BIRD_FLOCK_SIZE_MAX]{};
    for (int i = 0; i < flockSize; ++i) {
        draws[i].wingPhaseOffset = prng_uniform(sim.birdFlybyPrng,
            -BIRD_WING_FLAP_PHASE_JITTER, BIRD_WING_FLAP_PHASE_JITTER);
        draws[i].verticalDriftPhase = prng_uniform(sim.birdFlybyPrng, 0.0, TWO_PI);
    }

    if (static_cast<int>(sim.entities.size()) + flockSize > MAX_ENTITIES_PER_MONITOR) return;

    const double spawnX = direction > 0.0 ? -50.0 : sim.monitorWidth + 50.0;
    const double sinAngle = std::sin(BIRD_FLOCK_V_ANGLE_DEG * 3.14159265358979323846 / 180.0);
    for (int i = 0; i < flockSize; ++i) {
        const double along = -static_cast<double>(i) * BIRD_FLOCK_FORMATION_SPACING;
        double perpendicular = 0.0;
        if (formationStyle == 0ull) {
            const int armIndex = (i + 1) / 2;
            const double side = (i % 2) == 0 ? 1.0 : -1.0;
            perpendicular = side * static_cast<double>(armIndex) * BIRD_FLOCK_FORMATION_SPACING * sinAngle;
        } else {
            perpendicular = static_cast<double>(i) * BIRD_FLOCK_FORMATION_SPACING * sinAngle;
        }

        Entity e{};
        e.kind = EntityKind::Bird;
        e.size = BIRD_WING_SPAN * 0.5;
        e.x = spawnX + direction * along;
        e.x0 = e.x;
        e.vx = direction * leaderSpeed;
        e.vy = 0.0;
        e.baseSpeed = leaderSpeed;
        e.altitudeAnchor = leaderAltitude - perpendicular;
        e.phaseX = draws[i].wingPhaseOffset;
        e.phaseY = draws[i].verticalDriftPhase;
        e.age = 0.0;
        e.lifetime = -1.0;
        e.spawnTime = sim.globalTime;
        e.formationOffsetAlongFlight = along;
        e.formationOffsetPerpendicular = perpendicular;
        e.colorVariant = static_cast<uint8_t>(formationStyle);
        e.seed = static_cast<uint32_t>(i + 1);
        update_bird_position(e, sim);
        sim.entities.push_back(e);
    }
}

void sim_tick_bird_flybys(Sim& sim) noexcept {
    if (sim.currentScene != Scene::Grass || sim.monitorWidth <= 0.0) return;
    if (sim.globalTime < sim.nextBirdFlybyAtTime) return;

    sim_spawn_bird_flyby(sim);
    sim.nextBirdFlybyAtTime = sim.globalTime + bird_flyby_sample_interval(sim.birdFlybyPrng);
}

// ---------------------------------------------------------------------------
// Roaming entities (§13.2)
//
// Generic tick: integrate position, advance rotation, age the entity.
// Per-kind branches handle off-screen respawn (tumbleweed), horizontal
// sway (snowflake), and end-of-life culling (snowflake). The skeleton is
// safe to call on an empty vector — it walks zero iterations and exits.
// ---------------------------------------------------------------------------

void sim_tick_entities(Sim& sim, double dt) noexcept {
    const double groundY = sim.windowHeight;

    // Forward pass: update positions / rotations / generic age.
    for (Entity& e : sim.entities) {
        e.x        += e.vx * dt;
        e.y        += e.vy * dt;
        e.rotation += e.rotationSpeed * dt;
        e.age      += dt;

        if (e.kind == EntityKind::Tumbleweed) {
            if (e.x < -e.size - 10.0) {
                respawn_tumbleweed(e, sim.tumbleweedPrng, sim.monitorWidth, groundY, false);
            } else if (e.x > sim.monitorWidth + e.size + 10.0) {
                respawn_tumbleweed(e, sim.tumbleweedPrng, sim.monitorWidth, groundY, true);
            }

            // Gentle staggered hop: the generic pass above already advanced y
            // by vy*dt. While airborne, gravity pulls it back to the baseline;
            // once grounded, count down to the next launch.
            const double yBase = e.altitudeAnchor;
            const bool airborne = (e.vy != 0.0) || (e.y < yBase - 1e-9);
            if (airborne) {
                e.vy += TUMBLEWEED_BOUNCE_GRAVITY * dt;
                if (e.vy >= 0.0 && e.y >= yBase) {
                    e.y  = yBase;
                    e.vy = 0.0;
                    e.stateTimer = tumbleweed_next_gap(e.seed, e.age);
                }
            } else {
                e.stateTimer -= dt;
                if (e.stateTimer <= 0.0) {
                    const double hopH = tumbleweed_hop_height(e.seed, e.size);
                    e.vy = -std::sqrt(2.0 * TUMBLEWEED_BOUNCE_GRAVITY * hopH);
                }
            }
        }
    }

    for (Entity& e : sim.entities) {
        if (e.kind != EntityKind::Snowflake) continue;
        const double phase = static_cast<double>(e.seed) / 4294967295.0 * TWO_PI;
        e.vx = SNOWFLAKE_SWAY_AMPLITUDE * SNOWFLAKE_SWAY_FREQUENCY * TWO_PI
             * std::cos(e.age * TWO_PI * SNOWFLAKE_SWAY_FREQUENCY + phase);
    }

    for (Entity& e : sim.entities) {
        if (e.kind == EntityKind::Leaf) {
            // Puff leaves carry an outward burst (vx) that drifts the anchor
            // out, then decays so they settle into the ordinary flutter. Ambient
            // leaves have vx == 0 and so are completely unaffected.
            if (e.vx != 0.0) {
                e.x0 += e.vx * dt;
                e.vx *= std::exp(-LEAF_PUFF_DRAG * dt);
                if (std::fabs(e.vx) < 0.5) e.vx = 0.0;
            }
            e.x = e.x0 + LEAF_HORIZONTAL_DRIFT_AMP
                * std::sin(e.age * LEAF_HORIZONTAL_DRIFT_FREQ + e.phaseX);
        } else if (e.kind == EntityKind::Bubble) {
            // Apply horizontal wobble in absolute terms (x0 = spawn column).
            // The generic vx*dt above is zero for bubbles so this fully
            // controls x.
            e.x = e.x0 + BUBBLE_WOBBLE_AMPLITUDE
                * std::sin(e.age * BUBBLE_WOBBLE_FREQUENCY * TWO_PI + e.phaseX);
        }
    }

    // Snow-puff powder (§21): gravity pulls the upward burst back toward the
    // ground (y is screen-down) while horizontal velocity decays via drag. The
    // generic pass above already integrated position and age; culling is by
    // lifetime (below) plus the y > groundY rule.
    for (Entity& e : sim.entities) {
        if (e.kind == EntityKind::SnowPuff) {
            e.vy += SNOW_PUFF_GRAVITY * dt;
            e.vx *= std::exp(-SNOW_PUFF_DRAG * dt);
        }
    }

    for (Entity& e : sim.entities) {
        if (e.kind == EntityKind::Butterfly) {
            const double margin = BUTTERFLY_WING_OFFSET + BUTTERFLY_WING_RADIUS;
            if (e.x > sim.monitorWidth + margin) {
                e.x = -margin;
            } else if (e.x < -margin) {
                e.x = sim.monitorWidth + margin;
            }
            update_butterfly_position(e, sim);
        } else if (e.kind == EntityKind::Firefly) {
            const double margin = FIREFLY_GLOW_RADIUS;
            if (e.x > sim.monitorWidth + margin) {
                e.x = -margin;
            } else if (e.x < -margin) {
                e.x = sim.monitorWidth + margin;
            }
            update_firefly_position(e, sim);
        } else if (e.kind == EntityKind::Bird) {
            update_bird_position(e, sim);
        }
    }

    sim.entities.erase(
        std::remove_if(sim.entities.begin(), sim.entities.end(),
            [groundY, &sim](const Entity& e) {
                return (e.lifetime > 0.0 && e.age >= e.lifetime)
                    || (e.kind == EntityKind::Snowflake && e.y > groundY)
                    || (e.kind == EntityKind::Leaf && e.y > groundY)
                    || (e.kind == EntityKind::SnowPuff && e.y > groundY)
                    || (e.kind == EntityKind::Bird
                        && ((e.vx >= 0.0 && e.x > sim.monitorWidth + 50.0)
                         || (e.vx < 0.0 && e.x < -50.0)))
                    || (e.kind == EntityKind::Bubble && e.y < -e.size)
                    || (e.kind == EntityKind::Fish
                        && ((e.vx >= 0.0 && e.x > sim.monitorWidth + e.size + 4.0)
                         || (e.vx < 0.0 && e.x < -e.size - 4.0)));
            }),
        sim.entities.end());

    // Critter tick — Sheep (§16). State machine drives behavior:
    //   Walking  : moves at vx, bounces on edges, leg cycle in renderer.
    //   Grazing  : frozen, head will be drawn pointing down at the grass.
    //   Idle     : frozen, head sweeps side-to-side in the renderer.
    //   Sleeping : frozen, body tucks down, Z glyphs drift up.
    //   Hopping  : moves AND visually arcs upward (renderer applies the
    //              parabolic Y offset). Triggered by random transition or
    //              by sim_apply_click within SHEEP_STARTLE_RADIUS.
    //   Greeting : frozen, faces another sheep until both walk apart.
    // The generic forward pass already added (vx * dt) to e.x; for frozen
    // states we undo that integration so the sheep stays planted.
    for (Entity& e : sim.entities) {
        if (e.kind != EntityKind::Sheep) continue;

        const bool frozen = (e.state == SHEEP_STATE_GRAZING)
                         || (e.state == SHEEP_STATE_IDLE)
                         || (e.state == SHEEP_STATE_SLEEPING)
                         || (e.state == SHEEP_STATE_GREETING);
        if (frozen) {
            e.x -= e.vx * dt;
        }

        // Edge bounce — runs in every state. Even a stationary sheep that
        // spawned near the edge gets reflected on its first walk tick.
        const double margin = e.size + 2.0;
        if (e.x < margin) {
            e.x  = margin;
            e.vx = std::abs(e.vx);
        } else if (e.x > sim.monitorWidth - margin) {
            e.x  = sim.monitorWidth - margin;
            e.vx = -std::abs(e.vx);
        }

        // State transitions. Decrement the timer; when it expires, pick the
        // next state and roll a fresh duration. PRNG draws are sequenced
        // so cross-impl bit-identity is preservable once Win2D mirrors.
        e.stateTimer -= dt;
        if (e.stateTimer <= 0.0) {
            const uint8_t oldState = e.state;
            if (oldState == SHEEP_STATE_WALKING) {
                const double r = prng_uniform(sim.critterPrng, 0.0, 1.0);
                if (r < SHEEP_GRAZE_PROBABILITY) {
                    e.state = SHEEP_STATE_GRAZING;
                    e.stateTimer = prng_uniform(sim.critterPrng,
                                                SHEEP_GRAZE_DURATION_MIN,
                                                SHEEP_GRAZE_DURATION_MAX);
                } else if (r < SHEEP_GRAZE_PROBABILITY + SHEEP_IDLE_PROBABILITY) {
                    e.state = SHEEP_STATE_IDLE;
                    e.stateTimer = prng_uniform(sim.critterPrng,
                                                SHEEP_IDLE_DURATION_MIN,
                                                SHEEP_IDLE_DURATION_MAX);
                } else {
                    e.state = SHEEP_STATE_HOPPING;
                    e.stateTimer = SHEEP_HOP_DURATION;
                }
            } else if (oldState == SHEEP_STATE_IDLE) {
                const double r = prng_uniform(sim.critterPrng, 0.0, 1.0);
                const double sleepProb = SHEEP_SLEEP_FROM_IDLE_PROB;
                if (r < sleepProb) {
                    e.state = SHEEP_STATE_SLEEPING;
                    e.stateTimer = prng_uniform(sim.critterPrng,
                                                SHEEP_SLEEP_DURATION_MIN,
                                                SHEEP_SLEEP_DURATION_MAX);
                } else {
                    e.state = SHEEP_STATE_WALKING;
                    e.stateTimer = prng_uniform(sim.critterPrng,
                                                SHEEP_WALK_DURATION_MIN,
                                                SHEEP_WALK_DURATION_MAX);
                }
            } else {
                // Grazing / Sleeping / Hopping / Greeting → return to Walking.
                e.state = SHEEP_STATE_WALKING;
                e.stateTimer = prng_uniform(sim.critterPrng,
                                            SHEEP_WALK_DURATION_MIN,
                                            SHEEP_WALK_DURATION_MAX);
                if (oldState == SHEEP_STATE_GREETING) {
                    e.vx = -e.vx;
                }
            }
            // age reset so hop arc / walk cycle / sleep-Z animation start
            // from phase 0 at every state entry (else the hop would catch
            // mid-arc and a long-running sheep's leg cycle would jitter).
            e.age = 0.0;
        }
    }

    // Pair-wise sheep greeting trigger. Runs after all per-sheep transitions
    // and before the snowflake spawner so critter PRNG draw order stays locked.
    auto canGreet = [](const Entity& sheep) noexcept {
        return sheep.kind == EntityKind::Sheep
            && (sheep.state == SHEEP_STATE_WALKING
             || sheep.state == SHEEP_STATE_GRAZING
             || sheep.state == SHEEP_STATE_IDLE)
            && sheep.age >= SHEEP_GREET_MIN_AGE;
    };
    for (std::size_t i = 0; i < sim.entities.size(); ++i) {
        Entity& a = sim.entities[i];
        if (!canGreet(a)) continue;

        for (std::size_t j = i + 1; j < sim.entities.size(); ++j) {
            Entity& b = sim.entities[j];
            if (!canGreet(b)) continue;

            const double dx = b.x - a.x;
            if (std::abs(dx) >= SHEEP_GREET_RADIUS) continue;

            const double duration = prng_uniform(sim.critterPrng,
                                                SHEEP_GREET_DURATION_MIN,
                                                SHEEP_GREET_DURATION_MAX);
            const double dir = (dx >= 0.0) ? 1.0 : -1.0;
            a.vx =  dir * std::abs(a.vx);
            b.vx = -dir * std::abs(b.vx);
            a.state = SHEEP_STATE_GREETING;
            b.state = SHEEP_STATE_GREETING;
            a.stateTimer = duration;
            b.stateTimer = duration;
            a.age = 0.0;
            b.age = 0.0;
            break;
        }
    }

    // Cat (§17). Cats are passive: no proximity chase and no greeting loop.
    // Idle/Sleep freeze in place; click pounce uses Hopping as Pouncing.
    for (Entity& e : sim.entities) {
        if (e.kind != EntityKind::Cat) continue;

        const bool frozen = (e.state == CAT_STATE_IDLE)
                         || (e.state == CAT_STATE_SLEEPING);
        if (frozen) {
            e.x -= e.vx * dt;
        }

        const double margin = e.size + 2.0;
        if (e.x < margin) {
            e.x  = margin;
            e.vx = std::abs(e.vx);
        } else if (e.x > sim.monitorWidth - margin) {
            e.x  = sim.monitorWidth - margin;
            e.vx = -std::abs(e.vx);
        }

        e.stateTimer -= dt;
        if (e.stateTimer <= 0.0) {
            if (e.state == CAT_STATE_WALKING) {
                const double r = prng_uniform(sim.critterPrng, 0.0, 1.0);
                if (r < CAT_IDLE_PROBABILITY) {
                    e.state = CAT_STATE_IDLE;
                    e.stateTimer = prng_uniform(sim.critterPrng,
                                                CAT_IDLE_DURATION_MIN,
                                                CAT_IDLE_DURATION_MAX);
                } else if (r < CAT_IDLE_PROBABILITY + CAT_SLEEP_PROBABILITY) {
                    e.state = CAT_STATE_SLEEPING;
                    e.stateTimer = prng_uniform(sim.critterPrng,
                                                CAT_SLEEP_DURATION_MIN,
                                                CAT_SLEEP_DURATION_MAX);
                } else {
                    e.stateTimer = prng_uniform(sim.critterPrng,
                                                CAT_WALK_DURATION_MIN,
                                                CAT_WALK_DURATION_MAX);
                }
            } else if (e.state == CAT_STATE_IDLE) {
                const double sleepProb = CAT_SLEEP_FROM_IDLE_PROB;
                const double r = prng_uniform(sim.critterPrng, 0.0, 1.0);
                if (r < sleepProb) {
                    e.state = CAT_STATE_SLEEPING;
                    e.stateTimer = prng_uniform(sim.critterPrng,
                                                CAT_SLEEP_DURATION_MIN,
                                                CAT_SLEEP_DURATION_MAX);
                } else {
                    e.state = CAT_STATE_WALKING;
                    e.stateTimer = prng_uniform(sim.critterPrng,
                                                CAT_WALK_DURATION_MIN,
                                                CAT_WALK_DURATION_MAX);
                }
            } else {
                e.state = CAT_STATE_WALKING;
                e.stateTimer = prng_uniform(sim.critterPrng,
                                            CAT_WALK_DURATION_MIN,
                                            CAT_WALK_DURATION_MAX);
            }
            e.age = 0.0;
        }
    }

    // Bunny (§18). No walk cycle: normal movement is hop arcs, with stationary
    // grazing/idle/sleeping poses. Startled bunnies flee-hop until timer expiry.
    for (Entity& e : sim.entities) {
        if (e.kind != EntityKind::Bunny) continue;

        const bool stationary = (e.state == BUNNY_STATE_GRAZING)
                             || (e.state == BUNNY_STATE_IDLE)
                             || (e.state == BUNNY_STATE_SLEEPING)
                             || (e.state == BUNNY_STATE_HOPPING && e.age > BUNNY_HOP_DURATION);
        if (stationary) {
            e.x -= e.vx * dt;
        }

        const double margin = e.size + 2.0;
        if (e.x < margin) {
            e.x  = margin;
            e.vx = std::abs(e.vx);
        } else if (e.x > sim.monitorWidth - margin) {
            e.x  = sim.monitorWidth - margin;
            e.vx = -std::abs(e.vx);
        }

        e.stateTimer -= dt;
        if (e.stateTimer <= 0.0) {
            const uint8_t oldState = e.state;
            if (oldState == BUNNY_STATE_HOPPING) {
                enter_bunny_rest_state(sim, e);
            } else if (oldState == BUNNY_STATE_STARTLED) {
                start_bunny_hopping(sim, e, false);
            } else {
                start_bunny_hopping(sim, e, true);
            }
        }
    }

    // Hedgehog (§17.9). Very slow walking; snuffling/idle/sleep/curled are stationary.
    for (Entity& e : sim.entities) {
        if (e.kind != EntityKind::Hedgehog) continue;

        if (e.state != HEDGEHOG_STATE_WALKING) {
            e.x -= e.vx * dt;
        }

        const double margin = e.size + 2.0;
        if (e.x < margin) {
            e.x  = margin;
            e.vx = std::abs(e.vx);
        } else if (e.x > sim.monitorWidth - margin) {
            e.x  = sim.monitorWidth - margin;
            e.vx = -std::abs(e.vx);
        }

        e.stateTimer -= dt;
        if (e.stateTimer <= 0.0) {
            const uint8_t oldState = e.state;
            if (oldState == HEDGEHOG_STATE_WALKING) {
                const uint8_t next = hedgehog_choose_rest_state(sim.critterPrng);
                e.state = next;
                e.stateTimer = hedgehog_duration_for_state(sim, next);
            } else if (oldState == HEDGEHOG_STATE_CURLED) {
                uint8_t resume = e.previousState;
                if (resume == HEDGEHOG_STATE_SLEEPING || resume > HEDGEHOG_STATE_CURLED || resume == HEDGEHOG_STATE_CURLED) {
                    resume = HEDGEHOG_STATE_WALKING;
                }
                e.state = resume;
                e.stateTimer = e.previousStateTimer > 0.0
                    ? e.previousStateTimer
                    : hedgehog_duration_for_state(sim, resume);
                e.previousState = HEDGEHOG_STATE_WALKING;
                e.previousStateTimer = 0.0;
            } else {
                e.state = HEDGEHOG_STATE_WALKING;
                e.stateTimer = hedgehog_duration_for_state(sim, HEDGEHOG_STATE_WALKING);
            }
            e.age = 0.0;
        }
    }

    if (sim.currentScene == Scene::Winter) {
        const double lambda = SNOWFLAKE_EMIT_RATE_PER_1920DIP * sim.monitorWidth / 1920.0;
        while (sim.globalTime >= sim.nextSnowflakeSpawnTime
               && static_cast<int>(sim.entities.size()) < MAX_ENTITIES_PER_MONITOR) {
            Entity e{};
            e.kind          = EntityKind::Snowflake;
            e.size          = prng_uniform(sim.snowflakePrng, SNOWFLAKE_SIZE_MIN, SNOWFLAKE_SIZE_MAX);
            e.x             = prng_uniform(sim.snowflakePrng, -20.0, sim.monitorWidth + 20.0);
            const double fallSpeed = prng_uniform(sim.snowflakePrng, SNOWFLAKE_FALL_SPEED_MIN, SNOWFLAKE_FALL_SPEED_MAX);
            e.y             = -e.size - 4.0;
            e.vx            = 0.0;
            e.vy            = fallSpeed;
            e.rotation      = prng_uniform(sim.snowflakePrng, 0.0, TWO_PI);
            e.rotationSpeed = prng_uniform(sim.snowflakePrng, -1.5, 1.5);
            e.age           = 0.0;
            e.lifetime      = (groundY + e.size) / fallSpeed + SNOWFLAKE_LIFETIME_PADDING_SEC;
            e.seed          = prng_next_u32(sim.snowflakePrng);
            sim.entities.push_back(e);
            sim.nextSnowflakeSpawnTime += prng_exponential(sim.snowflakePrng, lambda);
        }
    }

    sim_tick_bird_flybys(sim);

    if (sim.currentScene == Scene::Autumn && sim.monitorWidth > 0.0) {
        const double lambda = LEAF_SPAWN_RATE_PER_SEC_1920DIP * sim.monitorWidth / 1920.0;
        while (lambda > 0.0
               && sim.globalTime >= sim.nextLeafSpawnTime
               && static_cast<int>(sim.entities.size()) < MAX_ENTITIES_PER_MONITOR) {
            sim.entities.push_back(make_leaf(sim.leafPrng, sim.monitorWidth));
            sim.nextLeafSpawnTime += prng_exponential(sim.leafPrng, lambda);
        }
    }

    if (sim.currentScene == Scene::Ocean && sim.monitorWidth > 0.0) {
        // Bubbles rise from the seafloor toward the canvas top, with a gentle
        // sinusoidal horizontal wobble (applied during the per-kind pass
        // above). Lifetime ensures cleanup even if the cap-by-y condition is
        // somehow missed on a window resize.
        const double lambda = BUBBLE_EMIT_RATE_PER_1920DIP * sim.monitorWidth / 1920.0;
        while (lambda > 0.0 && sim.globalTime >= sim.nextBubbleSpawnTime
               && static_cast<int>(sim.entities.size()) < MAX_ENTITIES_PER_MONITOR) {
            Entity e{};
            e.kind = EntityKind::Bubble;
            e.size = prng_uniform(sim.bubblePrng, BUBBLE_SIZE_MIN, BUBBLE_SIZE_MAX);
            const double xFrac = prng_uniform(sim.bubblePrng, 0.0, 1.0);
            const double spawnX = xFrac * sim.monitorWidth;
            const double rise = prng_uniform(sim.bubblePrng, BUBBLE_RISE_SPEED_MIN, BUBBLE_RISE_SPEED_MAX);
            e.phaseX = prng_uniform(sim.bubblePrng, 0.0, TWO_PI);
            e.x0 = spawnX;
            e.x = spawnX;
            e.y = groundY + e.size + 2.0;
            e.vx = 0.0;
            e.vy = -rise; // negative = up
            e.baseSpeed = rise;
            e.age = 0.0;
            // Travel distance from below ground to above canvas = groundY + 2*size.
            e.lifetime = (groundY + 2.0 * e.size) / rise + BUBBLE_LIFETIME_PADDING_SEC;
            e.seed = prng_next_u32(sim.bubblePrng);
            sim.entities.push_back(e);
            sim.nextBubbleSpawnTime += prng_exponential(sim.bubblePrng, lambda);
        }

        // Keep the fish population at the target. Edge despawn happens in the
        // cleanup pass above.
        const int target = target_fish_count(sim);
        int currentFish = 0;
        for (const Entity& e : sim.entities) {
            if (e.kind == EntityKind::Fish) ++currentFish;
        }
        while (currentFish < target
               && static_cast<int>(sim.entities.size()) < MAX_ENTITIES_PER_MONITOR) {
            sim.entities.push_back(spawn_fish(sim, true));
            ++currentFish;
        }
    }
}

// ---------------------------------------------------------------------------
// Stroke geometry. architecture.md §7.
// ---------------------------------------------------------------------------

Stroke compute_blade_stroke(const Blade& b, double groundY, Scene scene) noexcept {
    Stroke s{};
    s.argb      = SCENE_PALETTES[static_cast<int>(scene)][b.hue];
    s.thickness = b.thickness;

    if (b.cutHeight < CUT_STUMP_THRESHOLD) {
        s.base    = { b.baseX, groundY };
        s.control = { b.baseX, groundY - 1.0 };
        s.tip     = { b.baseX, groundY - STUMP_HEIGHT };
        return s;
    }

    const double heightScale =
        (scene == Scene::Desert && !b.isCactus && !b.isMushroom) ? DESERT_GRASS_HEIGHT_SCALE :
        (scene == Scene::Winter && !b.isPine && !b.isMushroom)   ? WINTER_GRASS_HEIGHT_SCALE :
        1.0;
    const double L = b.height * b.heightBonus * b.cutHeight * heightScale;

    // Chord preservation: blades have a fixed length L. As effectiveLean
    // grows, the tip arcs over (Y drops) rather than the blade stretching
    // diagonally. Clamp to MAX_LEAN_FRACTION * L so the sqrt is always
    // positive even under strong gust impulses.
    double lean = b.effectiveLean;
    const double maxLean = MAX_LEAN_FRACTION * L;
    if (lean >  maxLean) lean =  maxLean;
    if (lean < -maxLean) lean = -maxLean;

    const double dropFactor = std::sqrt(1.0 - (lean / L) * (lean / L));

    const double tipX = b.baseX + lean;
    const double tipY = groundY - L * dropFactor;

    // Rooted-bend control point: directly above the base, at a fraction
    // CTRL_OFFSET_FACTOR of the (current, foreshortened) blade height.
    s.base    = { b.baseX, groundY };
    s.control = { b.baseX, groundY - L * CTRL_OFFSET_FACTOR * dropFactor };
    s.tip     = { tipX, tipY };
    return s;
}

// ---------------------------------------------------------------------------
// Sim glue
// ---------------------------------------------------------------------------

Sim sim_init(uint64_t seed, double monitorWidth, double density) {
    Sim s;
    s.windowHeight = STRIP_HEIGHT + HEADROOM;
    s.monitorWidth = monitorWidth;
    s.entitySeed   = seed;
    s.entities.reserve(MAX_ENTITIES_PER_MONITOR);
    generate_blades(seed, monitorWidth, density, s.blades);

    // §8.1 ambient gust scheduler — fifth independent stream, salted off
    // the same seed. The first interval is drawn immediately so the first
    // puff never fires at t=0 and every subsequent fire consumes exactly
    // 4 PRNG draws.
    prng_init(s.ambientPrng, seed ^ AMBIENT_GUST_PRNG_SALT);
    s.nextAmbientGustTime = s.globalTime
                          + prng_uniform(s.ambientPrng,
                                         AMBIENT_GUST_INTERVAL_MIN,
                                         AMBIENT_GUST_INTERVAL_MAX);

    prng_init(s.leafPrng, s.entitySeed ^ LEAF_PRNG_SALT);
    s.nextLeafSpawnTime = s.globalTime;
    prng_init(s.leafPuffPrng, s.entitySeed ^ LEAF_PUFF_PRNG_SALT);
    prng_init(s.snowPuffPrng, s.entitySeed ^ SNOW_PUFF_PRNG_SALT);
    prng_init(s.snowDriftPrng, s.entitySeed ^ SNOW_DRIFT_PRNG_SALT);
    prng_init(s.birdFlybyPrng, s.entitySeed ^ BIRD_FLYBY_PRNG_SALT);
    s.nextBirdFlybyAtTime = s.globalTime + bird_flyby_sample_interval(s.birdFlybyPrng);
    return s;
}

void sim_regenerate(Sim& sim, uint64_t seed, double monitorWidth, double density) {
    sim.globalTime     = 0.0;
    sim.prevCursorX    = 0.0;
    sim.prevCursorTime = -1.0;
    sim.monitorWidth   = monitorWidth;
    sim.entitySeed     = seed;
    sim.entities.clear();
    if (sim.entities.capacity() < static_cast<std::size_t>(MAX_ENTITIES_PER_MONITOR)) {
        sim.entities.reserve(MAX_ENTITIES_PER_MONITOR);
    }
    generate_blades(seed, monitorWidth, density, sim.blades);

    prng_init(sim.ambientPrng, seed ^ AMBIENT_GUST_PRNG_SALT);
    sim.nextAmbientGustTime = sim.globalTime
                            + prng_uniform(sim.ambientPrng,
                                           AMBIENT_GUST_INTERVAL_MIN,
                                           AMBIENT_GUST_INTERVAL_MAX);

    prng_init(sim.leafPrng, sim.entitySeed ^ LEAF_PRNG_SALT);
    sim.nextLeafSpawnTime = sim.globalTime;
    prng_init(sim.leafPuffPrng, sim.entitySeed ^ LEAF_PUFF_PRNG_SALT);
    prng_init(sim.snowPuffPrng, sim.entitySeed ^ SNOW_PUFF_PRNG_SALT);
    prng_init(sim.snowDriftPrng, sim.entitySeed ^ SNOW_DRIFT_PRNG_SALT);
    prng_init(sim.birdFlybyPrng, sim.entitySeed ^ BIRD_FLYBY_PRNG_SALT);
    sim.nextBirdFlybyAtTime = sim.globalTime + bird_flyby_sample_interval(sim.birdFlybyPrng);
}

void sim_tick(Sim& sim, double dt,
              const InputEvent* events, std::size_t numEvents) noexcept
{
    sim.globalTime += dt;

    for (std::size_t i = 0; i < numEvents; ++i) {
        const InputEvent& e = events[i];
        switch (e.type) {
            case EventType::Move:  sim_apply_move(sim, e);  break;
            case EventType::Click: sim_apply_click(sim, e); break;
        }
    }

    sim_tick_ambient_gusts(sim);

    for (Blade& b : sim.blades) {
        update_blade_dynamics(b, sim.globalTime, dt, sim.swaySpeedScale, sim.swayAmpScale);
        advance_cut(b, sim.globalTime);
    }

    sim_tick_entities(sim, dt);
}

} // namespace desktopgrass
