// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <string>

namespace PTSettingsSvc
{
    // Binary-identity anchor used when the install-path anchor cannot be
    // trusted (per-user installs in a user-writable folder — Design-v6-Final.md
    // §7 fallback branch / §15 #5 option d).
    //
    // Accepting a caller on this branch requires BOTH:
    //   * VerifyMicrosoftSignature(exe)  — the on-disk image carries a valid
    //     Authenticode signature that chains to a trusted machine root AND is
    //     signed by "Microsoft Corporation".  The check runs in the service's
    //     own context, so a user poisoning their HKCU cert stores cannot affect
    //     it (contrast the §13 package-identity attack).
    //   * GetBinaryVersion(exe) == GetServiceOwnVersion()  — the caller is the
    //     same release as the service.  Because the signature protects the
    //     version resource, a re-stamped version breaks the signature, and an
    //     old (downgrade) signed binary has an older version.  Version
    //     comparison ALONE is insecure — VERSIONINFO is attacker-writable
    //     metadata — which is why it must be paired with the signature.

    // True iff the file at `path` has a valid embedded Authenticode signature
    // (chains to a trusted root) AND the signer leaf subject is Microsoft.
    bool VerifyMicrosoftSignature(const std::wstring& path);

    // 64-bit file version (dwFileVersionMS<<32 | dwFileVersionLS) from the
    // VS_FIXEDFILEINFO of `path`.  0 if the file has no version resource.
    unsigned long long GetBinaryVersion(const std::wstring& path);

    // Version of the running service executable (this module).  0 if the
    // service binary carries no version resource (production builds must).
    unsigned long long GetServiceOwnVersion();

    // Packs a (major, minor, build, revision) tuple into the same 64-bit layout
    // GetBinaryVersion returns: major<<48 | minor<<32 | build<<16 | revision.
    constexpr unsigned long long MakeVersion(unsigned short major,
                                             unsigned short minor,
                                             unsigned short build,
                                             unsigned short revision)
    {
        return (static_cast<unsigned long long>(major) << 48) |
               (static_cast<unsigned long long>(minor) << 32) |
               (static_cast<unsigned long long>(build) << 16) |
               static_cast<unsigned long long>(revision);
    }

    // --- Version-acceptance policy (Design §12.7, decided 2026-06-30) ----------
    // Replaces the exact `caller == service` rule, which broke multi-user /
    // multi-version (a machine-wide singleton service can be only one version,
    // so the latest install would reject every other-version caller).  A caller
    // is version-acceptable iff BOTH bounds hold:
    //   1. ABSOLUTE FLOOR: callerVersion >= kMinSupportedCallerVersion.  This is
    //      the real anti-downgrade control — set it to exclude any version known
    //      to be vulnerable.  Bump it when a bad old version must be cut off.
    //   2. BOUNDED STALENESS (max delta): the caller's MINOR-release number is
    //      within kMaxMinorVersionDelta of the service's, so a caller can be at
    //      most N monthly releases away from the running service.
    // The signature check (VerifyMicrosoftSignature) is still required and is
    // what makes the version fields trustworthy.

    // Oldest caller MINOR release still accepted.  PowerToys versions are
    // 0.<minor>.<build>; the minor is the monthly release train.  Set to the
    // first v6 shipping minor at release; placeholder baseline below.
    constexpr unsigned long long kMinSupportedCallerVersion = MakeVersion(0, 100, 0, 0);

    // Max number of MINOR releases a caller may trail (or lead) the service.
    constexpr unsigned int kMaxMinorVersionDelta = 3;

    // True iff `callerVersion` satisfies the floor + max-delta policy against the
    // running `serviceVersion`.  Both are packed (GetBinaryVersion layout).
    bool IsCallerVersionAcceptable(unsigned long long callerVersion,
                                   unsigned long long serviceVersion);
}
