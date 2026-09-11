// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;

namespace AdvancedPaste.Cli;

internal sealed class SystemClipboardAdapter : IClipboardAdapter
{
    public string ReadText()
    {
        if (Clipboard.ContainsText(TextDataFormat.Html))
        {
            return Clipboard.GetText(TextDataFormat.Html);
        }

        return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
    }

    public void WriteText(string text)
        => Clipboard.SetText(text);
}
