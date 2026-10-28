// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ManagedCommon;
using PowerToys.ProtectedStorage;

namespace WorkspacesEditor.ViewModels;

public static class WorkspacesFailureViewModel
{
    private static readonly ProtectedStorageSetupClient Setup = new();

    public static OperationFailure LastFailure { get; private set; }

    public static Task<bool> RetryFailedOperationAsync(Func<Task> failedOperation)
    {
        return ExecuteAsync(failedOperation);
    }

    public static async Task<bool> ExecuteAsync(Func<Task> operation, bool allowSetup = false, bool allowRetry = true)
    {
        bool cleanupPending = false;
        while (true)
        {
            try
            {
                await operation();
                LastFailure = null;
                if (cleanupPending)
                {
                    MessageBox.Show(Text("ProtectedStorageMaintenanceCleanup"), Text("ProtectedStorageTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return true;
            }
            catch (Exception exception)
            {
                var storage = exception as ProtectedStorageException;
                string code = storage?.ErrorCode ?? "InvalidPayload";
                if (code == "NotProvisioned")
                {
                    Logger.LogInfo("Workspaces requires first-time protected storage setup.");
                }
                else
                {
                    Logger.LogError("Workspaces operation failed", exception);
                }

                var presentation = WorkspacesCsharpLibrary.Data.WorkspacesFailurePresentation.For(code, allowSetup, allowRetry);
                string key = presentation.MessageKey;
                string retry = presentation.RetryKind;
                LastFailure = new OperationFailure(storage?.OperationId ?? Guid.Empty, code, key, retry, storage?.NativeCode);
                bool setupAvailable = presentation.CanSetup;
                bool setupFailure = code.StartsWith("Setup", StringComparison.Ordinal);
                bool knownBusy = code == "SetupBusy" && storage != null && storage.OperationId != Guid.Empty && storage.NativeCode is 1618 or 170;
                bool canRetry = presentation.CanRetry;
                int choice = ShowFailure(
                    key,
                    canRetry,
                    setupAvailable,
                    setupFailure && !knownBusy ? "ProtectedStorageInspect" : "ProtectedStorageRetry",
                    code == "NotProvisioned" ? "ProtectedStorageSetUpOnly" : "ProtectedStorageSetup");
                if (choice == 0)
                {
                    return false;
                }

                if (choice == 1 && setupFailure)
                {
                    try
                    {
                        var retried = knownBusy ? await Setup.RetryFailedOperationAsync(storage.OperationId) : await Setup.InspectAsync();
                        if (!AcceptMaintenanceResult(retried))
                        {
                            return false;
                        }

                        cleanupPending |= retried.CleanupPending;
                    }
                    catch (Exception retryError)
                    {
                        Logger.LogError("Workspaces maintenance retry did not complete", retryError);
                        MessageBox.Show(Text("ProtectedStorageSetupUnknown"), Text("ProtectedStorageTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }

                if (choice == 2)
                {
                    using var identity = WindowsIdentity.GetCurrent();
                    if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        MessageBox.Show(Text("ProtectedStorageRunNormally"), Text("ProtectedStorageTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                        return false;
                    }

                    // Only this explicit user action may trigger Setup's authorization flow.
                    try
                    {
                        if (code == "RecoveryRequired" &&
                            MessageBox.Show(Text("ProtectedStorageConfirmBootstrapRepair"), Text("ProtectedStorageTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        {
                            return false;
                        }

                        var result = code == "RecoveryRequired" ? await Setup.RepairBootstrapWithAuthorizationAsync() : await Setup.EnsureReadyAsync();
                        if (!AcceptMaintenanceResult(result))
                        {
                            return false;
                        }

                        cleanupPending |= result.CleanupPending;
                    }
                    catch (Exception setupError)
                    {
                        Logger.LogError("Workspaces protected storage setup failed", setupError);
                        string message = setupError is ProtectedStorageException { ErrorCode: "OwnerContextRequired" } ? "ProtectedStorageRunNormally" : "ProtectedStorageSetupFailed";
                        MessageBox.Show(Text(message), Text("ProtectedStorageTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
            }
        }
    }

    private static bool AcceptMaintenanceResult(MaintenanceResult result)
    {
        result.ThrowIfFailed();
        if (result.RequiresRestart)
        {
            MessageBox.Show(Text("ProtectedStorageRestart"), Text("ProtectedStorageTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (result.CleanupPending)
        {
            Logger.LogWarning($"Protected storage maintenance cleanup pending. Operation={result.OperationId}, nativeCode={result.NativeCode}.");
        }

        if (result.DiagnosticsRetained)
        {
            Logger.LogInfo($"Protected storage authorization diagnostics retained. Operation={result.OperationId}.");
        }

        return true;
    }

    private static int ShowFailure(string key, bool retry, bool setup, string retryResource, string setupResource)
    {
        int result = 0;
        var panel = new StackPanel { Margin = new Thickness(20) };
        var window = new Window
        {
            Title = Text("ProtectedStorageTitle"),
            Content = panel,
            Width = 500,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        panel.Children.Add(new TextBlock { Text = Text(key), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        panel.Children.Add(buttons);
        void AddButton(string resource, int value)
        {
            var button = new Button { Content = Text(resource), Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(4), MinWidth = 80 };
            button.Click += (_, _) =>
            {
                result = value;
                window.Close();
            };
            buttons.Children.Add(button);
        }

        if (retry)
        {
            AddButton(retryResource, 1);
        }

        if (setup)
        {
            AddButton(setupResource, 2);
        }

        AddButton("Cancel", 0);
        window.ShowDialog();
        return result;
    }

    private static string Text(string key) => Properties.Resources.ResourceManager.GetString(key, Properties.Resources.Culture) ?? key;
}
