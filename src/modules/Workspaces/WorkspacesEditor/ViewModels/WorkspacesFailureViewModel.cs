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
        while (true)
        {
            try
            {
                await operation();
                LastFailure = null;
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogError("Workspaces operation failed", exception);
                var storage = exception as ProtectedStorageException;
                string code = storage?.ErrorCode ?? "InvalidPayload";
                string key = code switch
                {
                    "RevisionConflict" => "ProtectedStorageConflict",
                    "OutcomeUnknown" => "ProtectedStorageUnknown",
                    "CleanupPending" => "ProtectedStorageCleanupPending",
                    "OwnerContextRequired" => "ProtectedStorageRunNormally",
                    "IncompatibleVersion" => "ProtectedStorageIncompatible",
                    "SetupRestartRequired" => "ProtectedStorageRestart",
                    "SetupOutcomeUnknown" => "ProtectedStorageSetupUnknown",
                    "InvalidPayload" => "ProtectedStorageInvalid",
                    _ => "ProtectedStorageUnavailable",
                };
                string retry = code switch
                {
                    "OutcomeUnknown" or "SetupOutcomeUnknown" => "InspectUnknownOutcome",
                    "SetupRestartRequired" => "RetryAfterRestart",
                    "OwnerContextRequired" or "SetupAuthorizationRequired" => "RetryAfterAuthorization",
                    "InvalidPayload" or "SetupFailed" => "RetryAfterInputFix",
                    _ => "RetryNow",
                };
                LastFailure = new OperationFailure(storage?.OperationId ?? Guid.Empty, code, key, retry, storage?.NativeCode);
                bool setupAvailable = allowSetup && code is "NotProvisioned" or "AuthorizationRequired" or "RecoveryRequired" or "SetupAuthorizationRequired";
                bool setupFailure = code.StartsWith("Setup", StringComparison.Ordinal);
                bool knownBusy = code == "SetupBusy" && storage != null && storage.OperationId != Guid.Empty && storage.NativeCode is 1618 or 170;
                bool canRetry = allowRetry && retry is not ("RetryAfterRestart" or "RetryAfterAuthorization");
                int choice = ShowFailure(key, canRetry, setupAvailable, setupFailure && !knownBusy ? "ProtectedStorageInspect" : "ProtectedStorageRetry");
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
            MessageBox.Show(Text("ProtectedStorageMaintenanceCleanup"), Text("ProtectedStorageTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        return true;
    }

    private static int ShowFailure(string key, bool retry, bool setup, string retryResource)
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
            AddButton("ProtectedStorageSetup", 2);
        }

        AddButton("Cancel", 0);
        window.ShowDialog();
        return result;
    }

    private static string Text(string key) => Properties.Resources.ResourceManager.GetString(key, Properties.Resources.Culture) ?? key;
}
