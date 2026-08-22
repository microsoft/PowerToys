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

    // NOTE (Approach 4, 2026-07-07): the caller-auth version check is now EXACT
    // equality (callerVersion == GetServiceOwnVersion()), inlined in
    // CallerAuth.cpp.  The former tunable floor + max-delta policy
    // (IsCallerVersionAcceptable / kMinSupportedCallerVersion /
    // kMaxMinorVersionDelta) existed only to tolerate several caller versions
    // under a shared machine-wide service; per-user (per-SID) service instances
    // make caller and service 1:1, so it was removed (Design §12.7).
}
