// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Microsoft.CmdPal.UI;

internal sealed class PowerToysRootPageService : IRootPageService
{
    private readonly IServiceProvider _serviceProvider;
    private Lazy<MainListPage> _mainListPage;

    public PowerToysRootPageService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        _mainListPage = new Lazy<MainListPage>(() =>
        {
            return new MainListPage(_serviceProvider);
        });
    }

    public async Task PreLoadAsync()
    {
        var tlcManager = _serviceProvider.GetService<TopLevelCommandManager>()!;
        await tlcManager.LoadBuiltinsAsync();
    }

    public Microsoft.CommandPalette.Extensions.IPage GetRootPage()
    {
        return _mainListPage.Value;
    }

    public async Task PostLoadRootPageAsync()
    {
        var tlcManager = _serviceProvider.GetService<TopLevelCommandManager>()!;

        // After loading built-ins, and starting navigation, kick off a thread to load extensions.
        tlcManager.LoadExtensionsCommand.Execute(null);

        await tlcManager.LoadExtensionsCommand.ExecutionTask!;
        if (tlcManager.LoadExtensionsCommand.ExecutionTask.Status != TaskStatus.RanToCompletion)
        {
            // TODO: Handle failure case
        }
    }

    public void OnPerformTopLevelCommand(object? context)
    {
        try
        {
            if (context is IListItem listItem)
            {
                _mainListPage.Value.UpdateHistory(listItem);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update history in PowerToysRootPageService");
            Logger.LogError(ex.ToString());
        }
    }
}
