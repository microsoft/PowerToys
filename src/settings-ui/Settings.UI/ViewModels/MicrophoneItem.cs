// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// AOT-compatible wrapper for microphone list items, replacing Tuple&lt;string, string&gt;
    /// which cannot be used in XAML bindings under Native AOT.
    /// </summary>
    [WinRT.GeneratedBindableCustomProperty]
    public sealed partial class MicrophoneItem
    {
        public MicrophoneItem(string item1, string item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public string Item1 { get; }

        public string Item2 { get; }
    }
}
