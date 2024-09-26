// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace AdvancedPaste.Models;

public sealed class CustomActionActivatedEventArgs(string text, bool pasteResult) : EventArgs
{
    public string Text { get; private init; } = text;

    public bool PasteResult { get; private init; } = pasteResult;
}
