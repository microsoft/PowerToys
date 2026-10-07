// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using MonitorPower;
using MonitorPowerExtension.Properties;
using Windows.System;

#pragma warning disable CA1305, CA1863

namespace MonitorPowerExtension.Pages;

internal sealed partial class CreateProfilePage : DynamicListPage
{
    private readonly MonitorPowerListPage? _parentPage;
    private readonly List<TargetState> _targets;
    private readonly string? _existingFileName;
    private static readonly KeyChord ToggleMonitorShortcut = KeyChordHelpers.FromModifiers(vkey: VirtualKey.Space);
    private static readonly KeyChord SaveProfileShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.S);

    public CreateProfilePage()
        : this(null)
    {
    }

    public CreateProfilePage(MonitorPowerListPage? parentPage)
    {
        _parentPage = parentPage;
        Name = Resources.create_profile_page_title;
        Title = Resources.create_profile_page_title;
        PlaceholderText = Resources.profile_name_placeholder;
        _targets = LoadTargets();
    }

    public CreateProfilePage(MonitorPowerListPage? parentPage, string existingFileName, string existingName, List<DisplayHelpers.DisplayTargetId> existingTargets)
    {
        _parentPage = parentPage;
        Name = Resources.edit_profile_page_title;
        Title = Resources.edit_profile_page_title;
        PlaceholderText = Resources.profile_name_placeholder;
        _existingFileName = existingFileName;
        SearchText = existingName;
        _targets = LoadTargets(existingTargets);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged(0);

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>();

        if (_targets.Count == 0)
        {
            items.Add(new ListItem(new NoOpCommand())
            {
                Title = Resources.no_active_displays,
                Subtitle = Resources.error_no_active_to_save,
                Icon = new IconInfo("\uE783"),
            });
            return [.. items];
        }

        items.Add(new ListItem(new NoOpCommand())
        {
            Title = Resources.select_monitors_label,
            Subtitle = Resources.select_monitors_subtitle,
            Icon = new IconInfo("\uE710"),
            MoreCommands =
            [
                CreateSaveProfileContextItem(),
                CreateCancelContextItem(),
            ],
        });

        items.AddRange(_targets.Select((target, index) => CreateTargetItem(target, index)));
        return [.. items];
    }

    private List<TargetState> LoadTargets(List<DisplayHelpers.DisplayTargetId>? preSelected = null)
    {
        try
        {
            var preselectedSet = preSelected != null
                ? new HashSet<DisplayHelpers.DisplayTargetId>(preSelected)
                : DisplayHelpers.GetActivePaths().paths
                    .Select(p => new DisplayHelpers.DisplayTargetId(p.targetInfo.adapterId, p.targetInfo.id))
                    .ToHashSet();

            // Collect named targets: include those flagged targetAvailable AND all currently-active
            // paths. On Windows Insider builds QDC_ALL_PATHS may report targetAvailable==0 for
            // monitors that are physically active, so we union both sets.
            var activeIds = DisplayHelpers.GetActivePaths().paths
                .Select(p => new DisplayHelpers.DisplayTargetId(p.targetInfo.adapterId, p.targetInfo.id))
                .ToHashSet();

            var candidates = DisplayHelpers.GetAllPaths()
                .Where(p => p.targetInfo.targetAvailable != 0 ||
                            activeIds.Contains(new DisplayHelpers.DisplayTargetId(p.targetInfo.adapterId, p.targetInfo.id)))
                .GroupBy(p => new DisplayHelpers.DisplayTargetId(p.targetInfo.adapterId, p.targetInfo.id))
                .Select(g =>
                {
                    var path = g.First();
                    var id = new DisplayHelpers.DisplayTargetId(path.targetInfo.adapterId, path.targetInfo.id);
                    var name = GetTargetName(path.targetInfo);
                    var layoutSummary = GetTargetLayoutSummary(id);
                    return (id, name, layoutSummary, keepOn: preselectedSet.Contains(id));
                })
                .Where(t => !string.IsNullOrEmpty(t.name) && t.name != Resources.unknown_display)
                .ToList();

            // Filter out ghost targets (Intel GPU) that share a GDI device name
            var deviceNameMap = DisplayHelpers.BuildTargetToDeviceNameMap();
            candidates = candidates.Where(t => deviceNameMap.ContainsKey(t.id)).ToList();

            // Disambiguate when the same physical name appears on multiple target IDs
            var nameCounts = candidates.GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.Count());
            var nameIndexes = new Dictionary<string, int>();
            var result = new List<TargetState>(candidates.Count);
            foreach (var t in candidates)
            {
                var finalName = t.name;
                if (nameCounts[t.name] > 1)
                {
                    nameIndexes.TryGetValue(t.name, out var idx);
                    nameIndexes[t.name] = idx + 1;
                    finalName = $"{t.name} #{idx + 1}";
                }

                result.Add(new TargetState(t.id, finalName, t.layoutSummary, t.keepOn));
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    private ListItem CreateTargetItem(TargetState target, int index)
    {
        return new ListItem(new ToggleTargetCommand(this, index))
        {
            Title = target.Name,
            Subtitle = GetTargetSubtitle(target),
            Icon = GetTargetIcon(target),
            MoreCommands = CreateTargetCommands(index),
        };
    }

    private IContextItem[] CreateTargetCommands(int index)
    {
        return
        [
            CreateToggleMonitorContextItem(index),
            CreateSaveProfileContextItem(),
            CreateCancelContextItem(),
        ];
    }

    private static string GetTargetSubtitle(TargetState target)
    {
        return string.Format(
            Resources.monitor_summary_format,
            target.KeepOn ? Resources.monitor_on_label : Resources.monitor_off_label,
            target.LayoutSummary);
    }

    private static IconInfo GetTargetIcon(TargetState target)
    {
        return new IconInfo(target.KeepOn ? "\uE7F4" : "\uE783");
    }

    private static string GetTargetLayoutSummary(DisplayHelpers.DisplayTargetId id)
    {
        try
        {
            var (_, modes) = DisplayHelpers.GetAllPathsWithModes();
            var sourceMode = modes.FirstOrDefault(mode =>
                mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source &&
                mode.adapterId.Equals(id.AdapterId));
            if (sourceMode.infoType != DISPLAYCONFIG_MODE_INFO_TYPE.Source)
            {
                return Resources.layout_captured_label;
            }

            return string.Format(
                Resources.layout_summary_format,
                sourceMode.modeInfo.sourceMode.width,
                sourceMode.modeInfo.sourceMode.height,
                sourceMode.modeInfo.sourceMode.position.x,
                sourceMode.modeInfo.sourceMode.position.y);
        }
        catch
        {
            return Resources.layout_captured_label;
        }
    }

    private CommandContextItem CreateToggleMonitorContextItem(int index)
    {
        return new CommandContextItem(new ToggleTargetCommand(this, index))
        {
            Title = _targets[index].KeepOn ? Resources.turn_monitor_off_title : Resources.turn_monitor_on_title,
            Subtitle = Resources.toggle_monitor_subtitle,
            Icon = new IconInfo(_targets[index].KeepOn ? "\uE783" : "\uE7F4"),
            RequestedShortcut = ToggleMonitorShortcut,
        };
    }

    private CommandContextItem CreateSaveProfileContextItem()
    {
        return new CommandContextItem(new SaveProfileCommand(this))
        {
            Title = Resources.save_profile_button,
            Subtitle = Resources.save_profile_command_subtitle,
            Icon = new IconInfo("\uE74E"),
            RequestedShortcut = SaveProfileShortcut,
        };
    }

    private CommandContextItem CreateCancelContextItem()
    {
        return new CommandContextItem(new CancelProfileCommand())
        {
            Title = Resources.cancel_profile_title,
            Subtitle = Resources.cancel_profile_subtitle,
            Icon = new IconInfo("\uE711"),
            RequestedShortcut = KeyChordHelpers.FromModifiers(vkey: VirtualKey.Escape),
        };
    }

    private void ToggleTarget(int index)
    {
        if ((uint)index >= (uint)_targets.Count)
        {
            return;
        }

        _targets[index].KeepOn = !_targets[index].KeepOn;
        RaiseItemsChanged(-2);
    }

    private ICommandResult ConfirmSaveProfile()
    {
        var name = SearchText?.Trim() ?? string.Empty;
        var selectedTargets = GetSelectedTargets();
        var validationResult = ValidateProfile(name, selectedTargets);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var selectedNames = string.Join(", ", _targets.Where(target => target.KeepOn).Select(target => target.Name));
        return CommandResult.Confirm(new ConfirmationArgs
        {
            Title = _existingFileName is null ? Resources.confirm_create_profile_title : Resources.confirm_update_profile_title,
            Description = string.Format(Resources.confirm_profile_description, name, selectedNames),
            PrimaryCommand = new CommitProfileCommand(this, name, selectedTargets),
        });
    }

    private List<DisplayHelpers.DisplayTargetId> GetSelectedTargets()
    {
        return _targets
            .Where(target => target.KeepOn)
            .Select(target => target.Id)
            .ToList();
    }

    private ICommandResult? ValidateProfile(string name, List<DisplayHelpers.DisplayTargetId> selectedTargets)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError(Resources.profile_name_required);
            return CommandResult.KeepOpen();
        }

        if (selectedTargets.Count == 0)
        {
            ShowError(Resources.profile_no_monitors);
            return CommandResult.KeepOpen();
        }

        var conflict = DisplayHelpers.GetSavedProfileConflict(name, selectedTargets, _existingFileName);
        if (conflict is not null)
        {
            ShowError(conflict);
            return CommandResult.KeepOpen();
        }

        return null;
    }

    private static void ShowError(string message)
    {
        ExtensionHost.ShowStatus(
            new StatusMessage() { Message = message, State = MessageState.Error },
            StatusContext.Extension);
    }

    private ICommandResult CommitProfile(string name, List<DisplayHelpers.DisplayTargetId> selectedTargets)
    {
        try
        {
            if (_existingFileName != null)
            {
                var msg = DisplayHelpers.OverwriteNamedProfile(_existingFileName, name, selectedTargets);
                var isError = DisplayHelpers.IsErrorResult(msg);
                ExtensionHost.ShowStatus(
                    new StatusMessage() { Message = msg, State = isError ? MessageState.Error : MessageState.Success },
                    StatusContext.Extension);
                if (!isError)
                {
                    _parentPage?.RefreshProfiles();
                }

                return isError ? CommandResult.KeepOpen() : CommandResult.GoBack();
            }

            var msg2 = DisplayHelpers.SaveNamedProfile(name, selectedTargets);
            var isError2 = DisplayHelpers.IsErrorResult(msg2);
            ExtensionHost.ShowStatus(
                new StatusMessage() { Message = msg2, State = isError2 ? MessageState.Error : MessageState.Success },
                StatusContext.Extension);
            if (!isError2)
            {
                _parentPage?.RefreshProfiles();
            }

            return isError2 ? CommandResult.KeepOpen() : CommandResult.GoBack();
        }
        catch (Exception ex)
        {
            ExtensionHost.ShowStatus(
                new StatusMessage() { Message = string.Format(Resources.error_format, ex.Message), State = MessageState.Error },
                StatusContext.Extension);
            return CommandResult.KeepOpen();
        }
    }

    private static string GetTargetName(DISPLAYCONFIG_PATH_TARGET_INFO targetInfo)
    {
        try
        {
            return DisplayHelpers.GetTargetFriendlyName(targetInfo);
        }
        catch
        {
            return Resources.unknown_display;
        }
    }

    private sealed class TargetState(DisplayHelpers.DisplayTargetId id, string name, string layoutSummary, bool keepOn)
    {
        public DisplayHelpers.DisplayTargetId Id { get; } = id;

        public string Name { get; } = name;

        public string LayoutSummary { get; } = layoutSummary;

        public bool KeepOn { get; set; } = keepOn;
    }

    private sealed partial class ToggleTargetCommand(CreateProfilePage page, int index) : InvokableCommand
    {
        public override ICommandResult Invoke()
        {
            page.ToggleTarget(index);
            return CommandResult.KeepOpen();
        }
    }

    private sealed partial class CancelProfileCommand : InvokableCommand
    {
        public override ICommandResult Invoke() => CommandResult.GoBack();
    }

    private sealed partial class SaveProfileCommand(CreateProfilePage page) : InvokableCommand
    {
        public override ICommandResult Invoke() => page.ConfirmSaveProfile();
    }

    private sealed partial class CommitProfileCommand(
        CreateProfilePage page,
        string name,
        List<DisplayHelpers.DisplayTargetId> selectedTargets) : InvokableCommand
    {
        public override ICommandResult Invoke() => page.CommitProfile(name, selectedTargets);
    }
}
