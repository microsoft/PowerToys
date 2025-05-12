// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.Data.Json;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentFormViewModel(IFormContent _form, WeakReference<IPageContext> context) :
    ContentViewModel(context)
{
    private readonly ExtensionObject<IFormContent> _formModel = new(_form);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public string TemplateJson { get; protected set; } = "{}";

    public string StateJson { get; protected set; } = "{}";

    public string DataJson { get; protected set; } = "{}";

    public override void InitializeProperties()
    {
    }

    public void HandleSubmit()
    {
    }
}
