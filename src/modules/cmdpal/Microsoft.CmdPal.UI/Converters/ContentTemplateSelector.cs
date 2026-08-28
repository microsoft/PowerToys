// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

public partial class ContentTemplateSelector : DataTemplateSelector
{
    public DataTemplate? FormTemplate { get; set; }

    public DataTemplate? MarkdownTemplate { get; set; }

    public DataTemplate? TreeTemplate { get; set; }

    public DataTemplate? TextTemplate { get; set; }

    public DataTemplate? PlainTextTemplate { get; set; }

    public DataTemplate? ImageTemplate { get; set; }

    public DataTemplate? HeaderTemplate { get; set; }

    public DataTemplate? PropertyTemplate { get; set; }

    public DataTemplate? PropertyGridTemplate { get; set; }

    public DataTemplate? SectionTemplate { get; set; }

    public DataTemplate? LinkTemplate { get; set; }

    public DataTemplate? TagsTemplate { get; set; }

    public DataTemplate? CommandsTemplate { get; set; }

    public DataTemplate? SeparatorTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        ContentFormViewModel => FormTemplate,
        ContentMarkdownViewModel => MarkdownTemplate,
        ContentTreeViewModel => TreeTemplate,
        ContentTextViewModel => TextTemplate,
        ContentPlainTextViewModel => PlainTextTemplate,
        ContentImageViewModel => ImageTemplate,
        ContentHeaderViewModel => HeaderTemplate,
        ContentPropertyViewModel => PropertyTemplate,
        ContentPropertyGridViewModel => PropertyGridTemplate,
        ContentSectionViewModel => SectionTemplate,
        ContentLinkViewModel => LinkTemplate,
        ContentTagsViewModel => TagsTemplate,
        ContentCommandsViewModel => CommandsTemplate,
        ContentSeparatorViewModel => SeparatorTemplate,
        _ => null,
    };
}
