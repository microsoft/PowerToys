// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Awake.Core.Models;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Awake.Core
{
    internal static class AwakeJson
    {
        internal static string Serialize(AwakeSettings settings)
        {
            return JsonSerializer.Serialize(settings, AwakeJsonContext.Default.AwakeSettings);
        }

        internal static string Serialize(SystemPowerCapabilities capabilities)
        {
            return JsonSerializer.Serialize(capabilities, AwakeJsonContext.Default.SystemPowerCapabilities);
        }
    }
}
