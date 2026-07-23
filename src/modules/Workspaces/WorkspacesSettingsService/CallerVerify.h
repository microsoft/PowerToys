// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <windows.h>
#include <string>

namespace PTSettingsSvc
{
    // Binary-identity anchor used when the install-path anchor cannot be
    // trusted (per-user installs in a user-writable folder).
    //
    // Accepting a caller on this branch requires BOTH:
    //   * VerifyMicrosoftSignature(hImage, path)  — the on-disk image carries a
    //     valid Authenticode signature that chains to a trusted machine root AND
    //     is signed by "Microsoft Corporation".  The trust decision MUST run in
    //     the service's own (SYSTEM) context — never while impersonating — or a
    //     non-admin could trust a self-signed root in their CurrentUser\Root and
    //     forge a Microsoft signer.  The caller therefore opens the (user-only
    //     readable) image handle while impersonating and passes it here AFTER
    //     RevertToSelf(), so verification reads via the handle but chains against
    //     the machine trust store.
    //   * GetBinaryVersion(exe) == GetServiceOwnVersion()  — the caller is the
    //     same release as the service.  Because the signature protects the
    //     version resource, a re-stamped version breaks the signature, and an
    //     old (downgrade) signed binary has an older version.  Version
    //     comparison ALONE is insecure — VERSIONINFO is attacker-writable
    //     metadata — which is why it must be paired with the signature.

    // True iff the image behind `hImage` has a valid embedded Authenticode
    // signature (chains to a trusted root in the CURRENT thread's context) AND
    // the signer leaf subject is Microsoft.  `hImage` must be an open read
    // handle; `pathForDisplay` is used only for provider bookkeeping/diagnostics.
    // The current thread MUST be in the service's own context (RevertToSelf()
    // already called) so the chain builds against the machine trust store.
    bool VerifyMicrosoftSignature(HANDLE hImage, const std::wstring& pathForDisplay);

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

    // NOTE: the caller-auth version check is now EXACT
    // equality (callerVersion == GetServiceOwnVersion()), inlined in
    // CallerAuth.cpp.  The former tunable floor + max-delta policy
    // (IsCallerVersionAcceptable / kMinSupportedCallerVersion /
    // kMaxMinorVersionDelta) existed only to tolerate several caller versions
    // under a shared machine-wide service; per-user (per-SID) service instances
    // make caller and service 1:1, so it was removed.
}
