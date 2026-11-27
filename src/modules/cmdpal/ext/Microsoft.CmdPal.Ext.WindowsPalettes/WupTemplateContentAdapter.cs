// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal sealed partial class WupTemplateContentAdapter : FormContent, IDisposable
{
    private readonly PaletteTemplateContentView _content;

    internal WupTemplateContentAdapter(PaletteTemplateContentView content)
    {
        _content = content;
        Refresh();
        _content.PropertyChanged += Content_PropertyChanged;
    }

    public override ICommandResult SubmitAction(string actionId, string inputs, string data)
    {
        _content.InvokeAction(actionId, data, inputs);
        return CommandResult.KeepOpen();
    }

    public void Dispose()
    {
        _content.PropertyChanged -= Content_PropertyChanged;
        GC.SuppressFinalize(this);
    }

    private void Content_PropertyChanged(PaletteTemplateContentView sender, PalettePropertyChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        TemplateJson = _content.TemplateJson ?? string.Empty;
        DataJson = _content.DataJson ?? string.Empty;
        StateJson = "{}";
    }
}
