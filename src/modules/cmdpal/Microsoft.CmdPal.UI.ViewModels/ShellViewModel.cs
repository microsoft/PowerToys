// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ShellViewModel(IServiceProvider _serviceProvider) : ObservableObject,
    IRecipient<NavigateToPageMessage>
{
    [ObservableProperty]
    public partial bool IsLoaded { get; set; } = false;

    [ObservableProperty]
    public partial DetailsViewModel? Details { get; set; }

    [ObservableProperty]
    public partial bool IsDetailsVisible { get; set; }

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; set; }

    [RelayCommand]
    public async Task<bool> LoadAsync()
    {
        WeakReferenceMessenger.Default.Register<NavigateToPageMessage>(this);

        var tlcManager = _serviceProvider.GetService<TopLevelCommandManager>();
        await tlcManager!.LoadBuiltinsAsync();
        IsLoaded = true;

        // Built-ins have loaded. We can display our page at this point.
        var page = new MainListPage(_serviceProvider);
        WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(new(page!)));

        // After loading built-ins, and starting navigation, kick off a thread to load extensions.
        tlcManager.LoadExtensionsCommand.Execute(null);
        _ = Task.Run(async () =>
        {
            await tlcManager.LoadExtensionsCommand.ExecutionTask!;
            if (tlcManager.LoadExtensionsCommand.ExecutionTask.Status != TaskStatus.RanToCompletion)
            {
                // TODO: Handle failure case
            }
        });

        return true;
    }

    public void Receive(NavigateToPageMessage message) => CurrentPage = message.Page;
}
