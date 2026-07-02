// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media;

namespace Awake.Core.Models
{
    /// <summary>
    /// A running application with a visible window, surfaced in the flyout's "While app runs"
    /// picker. <see cref="IconBytes"/> is captured off the UI thread during enumeration; the
    /// XAML <see cref="Icon"/> is built from it on the UI thread before binding.
    /// </summary>
    public sealed class RunningAppInfo
    {
        public int ProcessId { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string WindowTitle { get; init; } = string.Empty;

        public byte[]? IconBytes { get; init; }

        public ImageSource? Icon { get; set; }
    }
}
