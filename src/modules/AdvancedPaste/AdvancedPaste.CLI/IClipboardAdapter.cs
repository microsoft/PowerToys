// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.Cli;

internal interface IClipboardAdapter
{
    string ReadText();

    void WriteText(string text);
}
