// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace TemplateCmdPalExtension;

internal sealed partial class TemplateCmdPalExtensionPage : ListPage
{
    public TemplateCmdPalExtensionPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "TemplateDisplayName";
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new NoOpCommand()) { Title = "TODO: Implement your extension here" }
        ];
    }
}
