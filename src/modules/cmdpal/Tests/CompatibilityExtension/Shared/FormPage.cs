// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CompatibilityExtension;

internal sealed partial class FormPage : ContentPage
{
    private readonly MarkdownContent _result = new() { Body = "No submission yet." };
    private readonly CompatibilityForm _form;

    public FormPage()
    {
        Id = "compat.form";
        Name = "Open";
        Title = "Form";
        Icon = new IconInfo("\uE70F");
        _form = new CompatibilityForm(message => _result.Body = message);
    }

    public override IContent[] GetContent() => [_form, _result];
}
