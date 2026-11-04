// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.Management.Deployment;

namespace FileExplorerScripts;

internal static class ShellRegistration
{
    public const string ClassId = "{4D97E44F-258D-480D-AC48-7A43F3088284}";
    private const string Verb = @"Software\Classes\*\shell\PowerToysPowerScripts";
    private const string ClassKey = @"Software\Classes\CLSID\" + ClassId;

    public static async Task RegisterAsync()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "PowerToys.FileExplorerScripts.ShellExtension.dll");
        if (!File.Exists(dll))
        {
            throw new FileNotFoundException("The Explorer PowerScripts shell extension is not installed.", dll);
        }

        using (var registration = Registry.CurrentUser.CreateSubKey(ClassKey + @"\InProcServer32"))
        {
            registration.SetValue(string.Empty, dll);
            registration.SetValue("ThreadingModel", "Apartment");
        }

        using (var verb = Registry.CurrentUser.CreateSubKey(Verb))
        {
            verb.SetValue(string.Empty, "PowerScripts");
            verb.SetValue("ExplorerCommandHandler", ClassId);
            verb.SetValue("MultiSelectModel", "Player");
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            var manager = new PackageManager();
            var installed = manager.FindPackagesForUser(string.Empty)
                .FirstOrDefault(package => package.Id.Name == "Microsoft.PowerToys.FileExplorerScriptsContextMenu");
            if (installed is null || !string.Equals(installed.EffectiveExternalLocation.Path.TrimEnd('\\'), AppContext.BaseDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
#if DEBUG
                var options = new RegisterPackageOptions { ExternalLocationUri = new Uri(AppContext.BaseDirectory), DeveloperMode = true };
                var result = await manager.RegisterPackageByUriAsync(
                    new Uri(Path.Combine(AppContext.BaseDirectory, "FileExplorerScriptsPackage", "AppxManifest.xml")), options);
#else
                var options = new AddPackageOptions { ExternalLocationUri = new Uri(AppContext.BaseDirectory) };
                var result = await manager.AddPackageByUriAsync(new Uri(Path.Combine(AppContext.BaseDirectory, "FileExplorerScriptsPackage.msix")), options);
#endif
                if (result.ExtendedErrorCode is not null)
                {
                    throw new InvalidOperationException(result.ErrorText, result.ExtendedErrorCode);
                }
            }
        }
    }

    public static void UnregisterClassic()
    {
        Registry.CurrentUser.DeleteSubKeyTree(Verb, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(ClassKey, throwOnMissingSubKey: false);
    }
}
