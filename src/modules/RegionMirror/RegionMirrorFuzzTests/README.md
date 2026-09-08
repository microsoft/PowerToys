# RegionMirror geometry fuzz target

`GeometryFuzzer.cpp` exports the native libFuzzer entry point. It covers the same header-only geometry and command-line parser used by the application, without starting capture or creating windows.

The harness checks:

- Rejected command-line input leaves its output rectangle unchanged.
- Accepted input describes a valid bounded region and survives serialization and reparsing.
- Extra components, embedded nulls, arbitrary UTF-16 code units, negative origins, and integer limits reach the parser.
- Normalizing a selection is independent of drag direction and never inverts an edge.
- Aspect fitting stays inside its target, centers its padding, fills at least one target dimension, and limits aspect-ratio rounding to less than one pixel on the fitted axis.
- Invalid rectangles and fits smaller than one pixel return an empty rectangle.

Inputs are interpreted both as ASCII bytes and as little-endian UTF-16. The same bytes also generate arbitrary rectangles and valid regions with bounded dimensions; this keeps successful geometry paths reachable. Inputs larger than 4096 bytes are skipped. Seed files contain ASCII without a trailing newline.

Build and run the x64 Release fuzz target only during final acceptance validation, following the repository's [native fuzzing instructions](../../../../doc/devdocs/tools/fuzzingtesting.md). The target requires ASan and libFuzzer; assertions use `abort()` so that invariant failures remain enabled in Release. ARM64 fuzzing is not supported by the repository's native fuzz configuration.

After a successful build, run the resulting executable with the corpus folder, `-dict=RegionMirror.dict`, `-max_len=4096`, and a bounded duration such as `-max_total_time=60`. Record the exit code and any crash artifact. A compiled harness or a short smoke run does not establish capture, Teams, or monitor behavior; use the adjacent manual acceptance checklist for those scenarios.

This proof of concept does not submit jobs to OneFuzz or alter the shared fuzzing pipeline.
