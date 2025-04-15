// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace WorkspacesEditor.Utils
{
    public class DashCaseNamingPolicy : JsonNamingPolicy
    {
        public static DashCaseNamingPolicy Instance { get; } = new DashCaseNamingPolicy();

        public override string ConvertName(string name)
        {
            return name.UpperCamelCaseToDashCase();
        }
    }
}
