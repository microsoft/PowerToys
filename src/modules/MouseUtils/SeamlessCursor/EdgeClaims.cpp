// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "EdgeClaims.h"

#include <atomic>

namespace
{
    // Written on the Seamless hook thread, read on the CursorWrap hook thread.
    std::atomic<std::shared_ptr<const seamless_cursor::Engine>> g_claims;
    std::atomic_bool g_mapWraps = true;
}

namespace seamless_cursor
{
    void PublishEdgeClaims(std::shared_ptr<const Engine> engine)
    {
        g_claims.store(std::move(engine));
    }

    void EnableWrapMapping(bool enabled) noexcept
    {
        g_mapWraps = enabled;
    }
}

extern "C" __declspec(dllexport) bool __cdecl SeamlessCursor_IsEdgeClaimed(RECT monitor, int edge, int along)
{
    if (edge < 0 || edge >= static_cast<int>(seamless_cursor::edgeCount))
    {
        return false;
    }

    const auto claims = g_claims.load();
    return claims && claims->IsEdgeClaimed(monitor, static_cast<seamless_cursor::Edge>(edge), along);
}

extern "C" __declspec(dllexport) bool __cdecl SeamlessCursor_MapWrap(RECT from, int edge, int along, RECT to, int* mapped)
{
    if (!mapped || !g_mapWraps || edge < 0 || edge >= static_cast<int>(seamless_cursor::edgeCount))
    {
        return false;
    }

    const auto claims = g_claims.load();
    if (!claims)
    {
        return false;
    }

    const auto result = claims->MapJump(from, static_cast<seamless_cursor::Edge>(edge), along, to);
    if (!result)
    {
        return false;
    }

    *mapped = *result;
    return true;
}
