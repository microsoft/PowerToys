// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public partial class PowerScriptsViewModel
{
    public ObservableCollection<PowerScriptActivityItem> ActivityItems { get; } = new();

    public bool IsActivityBusy { get; private set; }

    public bool CanRefreshActivity => !IsActivityBusy;

    public string ActivityError { get; private set; } = string.Empty;

    public bool HasActivityError => !string.IsNullOrEmpty(ActivityError);

    public bool IsActivityEmpty => ActivityItems.Count == 0 && !IsActivityBusy && !HasActivityError;

    public async Task RefreshActivityAsync(bool clearCompleted = false)
    {
        if (IsActivityBusy)
        {
            return;
        }

        IsActivityBusy = true;
        NotifyActivityState();
        try
        {
            var json = await InvokeHostAsync(new[] { "activity", clearCompleted ? "clear" : "list" });
            var snapshot = JsonSerializer.Deserialize<List<PowerScriptActivityItem>>(json, JsonOptions)
                ?? throw new InvalidDataException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
            if (snapshot.Any(item => item is null || item.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.ScriptId) || string.IsNullOrWhiteSpace(item.ScriptName) ||
                string.IsNullOrWhiteSpace(item.ConsumerId) || string.IsNullOrWhiteSpace(item.Status)) ||
                snapshot.Select(item => item.Id).Distinct().Count() != snapshot.Count)
            {
                throw new InvalidDataException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
            }

            var ids = snapshot.Select(item => item.Id).ToHashSet();
            for (var index = ActivityItems.Count - 1; index >= 0; index--)
            {
                if (!ids.Contains(ActivityItems[index].Id))
                {
                    ActivityItems.RemoveAt(index);
                }
            }

            for (var index = 0; index < snapshot.Count; index++)
            {
                var existing = ActivityItems.FirstOrDefault(item => item.Id == snapshot[index].Id);
                if (existing is not null)
                {
                    var previousIndex = ActivityItems.IndexOf(existing);
                    if (previousIndex != index)
                    {
                        ActivityItems.Move(previousIndex, index);
                    }

                    existing.UpdateStatus(snapshot[index]);
                }
                else
                {
                    ActivityItems.Insert(index, snapshot[index]);
                }
            }

            ActivityError = string.Empty;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
            InvalidOperationException or JsonException or TimeoutException or Win32Exception)
        {
            ActivityError = exception.Message;
            ManagedCommon.Logger.LogError("Unable to refresh or clear PowerScripts Activity.", exception);
        }
        finally
        {
            IsActivityBusy = false;
            NotifyActivityState();
        }
    }

    private void NotifyActivityState()
    {
        OnPropertyChanged(nameof(IsActivityBusy));
        OnPropertyChanged(nameof(CanRefreshActivity));
        OnPropertyChanged(nameof(ActivityError));
        OnPropertyChanged(nameof(HasActivityError));
        OnPropertyChanged(nameof(IsActivityEmpty));
    }
}
