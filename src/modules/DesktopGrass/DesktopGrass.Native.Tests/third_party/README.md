# DesktopGrass.Native.Tests — third_party

This directory contains source vendored at known versions to keep the test
build hermetic. None of it ships in the runtime binary.

## catch2/catch.hpp

[Catch2](https://github.com/catchorg/Catch2) v2.13.10 single-header
amalgamation, vendored verbatim from the upstream release. License: Boost
Software License 1.0. Copy lives at `catch2/catch.hpp`.

We intentionally avoid pulling Catch2 from vcpkg/NuGet for this v1 — the
single-header approach builds with `cl` out of the box and removes one
moving piece from the test step.
