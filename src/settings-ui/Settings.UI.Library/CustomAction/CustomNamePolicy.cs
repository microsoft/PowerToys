// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Library.CustomAction
{
    public class CustomNamePolicy : JsonNamingPolicy
    {
        private Func<string, string> convertDelegate;

        public CustomNamePolicy(Func<string, string> convertDelegate)
        {
            this.convertDelegate = convertDelegate;
        }

        public override string ConvertName(string name)
        {
            return convertDelegate(name);
        }
    }
}
