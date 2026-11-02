// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "SeamlessCursorEngine.h"

#include <memory>

// CursorWrap runs in the same process and would wrap the cursor at outer edges that this module takes over.
// Before wrapping, it calls the exported SeamlessCursor_IsEdgeClaimed, and when it wraps, SeamlessCursor_MapWrap.
// Both answer from the snapshot below.
namespace seamless_cursor
{
    // Publishes an immutable copy of the engine; nullptr withdraws every claim. Safe from any thread.
    void PublishEdgeClaims(std::shared_ptr<const Engine> engine);

    // Whether SeamlessCursor_MapWrap answers; the map_cursor_wrap setting. Safe from any thread.
    void EnableWrapMapping(bool enabled) noexcept;
}

// edge: 0 = left, 1 = right, 2 = top, 3 = bottom; CursorWrap's EdgeType uses the same values.
// along: the coordinate along the edge (x for top and bottom, y for left and right).
extern "C" __declspec(dllexport) bool __cdecl SeamlessCursor_IsEdgeClaimed(RECT monitor, int edge, int along);

// For a cursor that CursorWrap wraps from monitor `from`, leaving through `edge` at `along`, to monitor `to`: writes the
// coordinate along the edge on `to` at the same physical position to `mapped`. Returns false when the calibration does
// not tell; CursorWrap then keeps the coordinate.
extern "C" __declspec(dllexport) bool __cdecl SeamlessCursor_MapWrap(RECT from, int edge, int along, RECT to, int* mapped);
