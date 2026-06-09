// snapshot_gen.cpp
// One-shot tool that prints the canonical PRNG + blade snapshot. Used to seed
// constants in DesktopGrass.Native.Tests/src/snapshot_data.h. Not part of the
// shipped binary. Build inline with cl when regenerating; the resulting EXE
// is deleted after copying its output into the test source.

#include <cstdio>
#include <cstdint>
#include "../src/Sim.h"

int main() {
    using namespace desktopgrass;

    Prng p;
    prng_init(p, CANONICAL_TEST_SEED);

    std::printf("// canonical PRNG snapshot (seed = 0x6B6173746F)\n");
    std::printf("constexpr uint64_t CANONICAL_PRNG_SNAPSHOT[16] = {\n");
    for (int i = 0; i < 16; ++i) {
        uint64_t v = prng_next_u64(p);
        std::printf("    0x%016llXull,\n", static_cast<unsigned long long>(v));
    }
    std::printf("};\n");

    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);
    std::printf("\n// blade count: %zu\n", blades.size());
    std::printf("constexpr size_t CANONICAL_BLADE_COUNT = %zu;\n", blades.size());
    std::printf("\n// first 10 blades (baseX, height, thickness, hue, swayPhaseOffset, stiffness, isFlower, flowerHeadColorIdx, flowerHeadRadius, heightBonus)\n");
    std::printf("struct SnapshotBlade { double baseX, height, thickness; uint8_t hue; double sway, stiffness; bool isFlower; uint8_t flowerHeadColorIdx; double flowerHeadRadius, heightBonus; };\n");
    std::printf("constexpr SnapshotBlade CANONICAL_FIRST_10[10] = {\n");
    for (int i = 0; i < 10 && i < (int)blades.size(); ++i) {
        const Blade& b = blades[i];
        std::printf("    { %.17g, %.17g, %.17g, %u, %.17g, %.17g, %s, %u, %.17g, %.17g },\n",
                    b.baseX, b.height, b.thickness, (unsigned)b.hue,
                    b.swayPhaseOffset, b.stiffness,
                    b.isFlower ? "true" : "false",
                    (unsigned)b.flowerHeadColorIdx,
                    b.flowerHeadRadius, b.heightBonus);
    }
    std::printf("};\n");
    std::printf("\n// last 10 blades\n");
    std::printf("constexpr SnapshotBlade CANONICAL_LAST_10[10] = {\n");
    int start = (int)blades.size() - 10;
    if (start < 0) start = 0;
    for (int i = start; i < (int)blades.size(); ++i) {
        const Blade& b = blades[i];
        std::printf("    { %.17g, %.17g, %.17g, %u, %.17g, %.17g, %s, %u, %.17g, %.17g },\n",
                    b.baseX, b.height, b.thickness, (unsigned)b.hue,
                    b.swayPhaseOffset, b.stiffness,
                    b.isFlower ? "true" : "false",
                    (unsigned)b.flowerHeadColorIdx,
                    b.flowerHeadRadius, b.heightBonus);
    }
    std::printf("};\n");
    return 0;
}
