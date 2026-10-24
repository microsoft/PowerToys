// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    internal sealed class TextExpanderPromptEditRequestedEventArgs : EventArgs
    {
        public TextExpanderPromptEditRequestedEventArgs(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }

        public int End { get; }
    }
}
