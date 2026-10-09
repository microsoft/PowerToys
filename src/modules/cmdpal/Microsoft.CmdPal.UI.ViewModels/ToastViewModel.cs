// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ToastViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string ToastMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    public partial IconInfoViewModel? Icon { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommand))]
    [NotifyPropertyChangedFor(nameof(CommandName))]
    [NotifyPropertyChangedFor(nameof(CommandIcon))]
    [NotifyPropertyChangedFor(nameof(CommandHasIcon))]
    public partial CommandViewModel? Command { get; set; }

    public bool HasIcon => Icon?.IsSet ?? false;

    public bool HasCommand => Command?.IsSet ?? false;

    public string CommandName => Command?.Name ?? string.Empty;

    public IconInfoViewModel? CommandIcon => Command?.Icon;

    public bool CommandHasIcon => Command?.HasIcon ?? false;
}
