// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Testing;

internal static class FixtureMonitorIdentity
{
    private const string Connector0 = "DISPLAY#DELD0E6#";
    private const string Connector1 = "DISPLAY#LEN65BF#";

    public static bool IsFixture(string id) =>
        id.Contains(Connector0, StringComparison.OrdinalIgnoreCase) ||
        id.Contains(Connector1, StringComparison.OrdinalIgnoreCase);

    public static int ConnectorForId(string id) =>
        id.Contains(Connector0, StringComparison.OrdinalIgnoreCase) ? 0 :
        id.Contains(Connector1, StringComparison.OrdinalIgnoreCase) ? 1 :
        throw new ArgumentException("Not a fixture monitor identity.", nameof(id));
}
