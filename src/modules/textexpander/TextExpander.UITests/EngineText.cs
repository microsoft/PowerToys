// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Stands in for the engine's resource loader in the test process, which shows the engine's notice from its sources:
/// every string is the English that comes with it, as when the engine's resources can't be read.
/// </summary>
internal static class EngineText
{
    public static string Get(string key, string english) => english;
}
