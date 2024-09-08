// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace AdvancedPaste.Models;

public sealed class CustomActionActivatedEventArgs(string text, bool forcePasteCustom) : EventArgs
{
    public string Text { get; private set; } = text;

    public bool ForcePasteCustom { get; private set; } = forcePasteCustom;
}
