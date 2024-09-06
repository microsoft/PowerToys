// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace TemplateExtension;

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
internal sealed class TemplateExtensionPage : ListPage
{
    public TemplateExtensionPage()
    {
        Icon = new(string.Empty);
        Name = "TemplateDisplayName";
    }

    public override ISection[] GetItems()
    {
        return [
            new ListSection()
            {
                Items = [
                    new ListItem(new NoOpAction()) { Title = "TODO: Implement your extension here" }
                ],
            }
        ];
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public class TemplateExtensionActionsProvider : ICommandProvider
{
    public string DisplayName => $"TemplateDisplayName Commands";

    public IconDataType Icon => new(string.Empty);

    private readonly IListItem[] _actions = [
        new ListItem(new TemplateExtensionPage()),
    ];

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    public IListItem[] TopLevelCommands()
    {
        return _actions;
    }
}
