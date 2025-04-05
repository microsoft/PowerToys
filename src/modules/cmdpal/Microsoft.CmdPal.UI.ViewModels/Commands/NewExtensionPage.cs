// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

public partial class NewExtensionPage : ContentPage
{
    private NewExtensionForm _inputForm = new();
    private NewExtensionFormBase? _resultForm;

    public override IContent[] GetContent()
    {
        return _resultForm != null ? [_resultForm] : [_inputForm];
    }

    public NewExtensionPage()
    {
        Name = Properties.Resources.builtin_create_extension_name;
        Title = Properties.Resources.builtin_create_extension_title;
        Icon = IconHelpers.FromRelativePath("Assets\\CreateExtension.svg");

        _inputForm.FormSubmitted += FormSubmitted;
    }

    private void FormSubmitted(NewExtensionFormBase sender, NewExtensionFormBase? args)
    {
        if (_resultForm != null)
        {
            _resultForm.FormSubmitted -= FormSubmitted;
        }

        _resultForm = args;
        if (_resultForm != null)
        {
            _resultForm.FormSubmitted += FormSubmitted;
        }
        else
        {
            _inputForm = new();
            _inputForm.FormSubmitted += FormSubmitted;
        }

        RaiseItemsChanged(1);
    }
}
