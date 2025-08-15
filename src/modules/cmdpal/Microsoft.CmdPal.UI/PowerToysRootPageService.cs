// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using WinRT;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Microsoft.CmdPal.UI;

internal sealed class PowerToysRootPageService : IRootPageService
{
    private readonly IServiceProvider _serviceProvider;
    private IExtensionWrapper? _activeExtension;
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

    private void OnPerformTopLevelCommand(object? context)
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

    public void OnPerformCommand(object? context, bool topLevel, AppExtensionHost? currentHost)
    {
        if (topLevel)
        {
            OnPerformTopLevelCommand(context);
        }

        if (currentHost is CommandPaletteHost host)
        {
            SetActiveExtension(host.Extension);
        }
        else
        {
            throw new InvalidOperationException("This must be a programming error - everything in Command Palette should have a CommandPaletteHost");
        }
    }

    public void SetActiveExtension(IExtensionWrapper? extension)
    {
        if (extension != _activeExtension)
        {
            // There's not really a CoDisallowSetForegroundWindow, so we don't
            // need to handle that
            _activeExtension = extension;

            var extensionWinRtObject = _activeExtension?.GetExtensionObject();
            if (extensionWinRtObject is not null)
            {
                try
                {
                    unsafe
                    {
                        var winrtObj = (IWinRTObject)extensionWinRtObject;
                        var intPtr = winrtObj.NativeObject.ThisPtr;
                        var hr = Native.CoAllowSetForegroundWindow(intPtr);
                        if (hr != 0)
                        {
                            Logger.LogWarning($"Error giving foreground rights: 0x{hr.Value:X8}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }
            }
        }
    }

    public void GoHome()
    {
        SetActiveExtension(null);
    }

    // You may ask yourself, why aren't we using CsWin32 for this?
    // The CsWin32 projected version includes some object marshalling, like so:
    //
    // HRESULT CoAllowSetForegroundWindow([MarshalAs(UnmanagedType.IUnknown)] object pUnk,...)
    //
    // And if you do it like that, then the IForegroundTransfer interface isn't marshalled correctly
    internal sealed class Native
    {
        [DllImport("OLE32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [SupportedOSPlatform("windows5.0")]
        internal static extern unsafe global::Windows.Win32.Foundation.HRESULT CoAllowSetForegroundWindow(nint pUnk, [Optional] void* lpvReserved);
    }
}
