// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace TopToolbar.Actions
{
    public sealed class ActionProgress
    {
        public double? Percent { get; set; }

        public string Note { get; set; } = string.Empty;
    }
}
