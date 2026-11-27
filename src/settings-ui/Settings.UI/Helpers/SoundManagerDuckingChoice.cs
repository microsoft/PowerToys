// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    // One of the ways in which Windows can hold the other sounds down while a program is talking. The number is the one Windows keeps it under.
    public sealed class SoundManagerDuckingChoice
    {
        public SoundManagerDuckingChoice(string displayName, int value)
        {
            DisplayName = displayName;
            Value = value;
        }

        public string DisplayName { get; }

        public int Value { get; }
    }
}
