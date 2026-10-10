// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using MonitorPower;
using MonitorPowerExtension.Properties;
using Windows.System;

#pragma warning disable CA1305, CA1863

namespace MonitorPowerExtension.Pages;

public sealed partial class MonitorPowerListPage : ListPage
{
    public const string MonitorPowerListPageId = "MonitorPower_ListPage";

    private static readonly IconInfo AppIcon = new("\uE7F4");
    private static readonly IconInfo ProfileIcon = new("\uE81C");
    private static readonly KeyChord CreateProfileShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.N);
    private static readonly KeyChord EditProfileShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.E);
    private static readonly KeyChord DeleteProfileShortcut = KeyChordHelpers.FromModifiers(vkey: VirtualKey.Delete);
    private static int _isApplyingProfile;

    public MonitorPowerListPage()
    {
        Id = MonitorPowerListPageId;
        Icon = AppIcon;
        Title = Resources.page_title;
        Name = Resources.page_title;
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>
        {
            new ListItem(new ApplyBuiltInProfileCommand(BuiltInDisplayProfile.AllDisplays))
            {
                Title = Resources.all_displays_title,
                Subtitle = Resources.all_displays_subtitle,
                Icon = ProfileIcon,
                MoreCommands = [CreateProfileContextItem()],
            },
            new ListItem(new ApplyBuiltInProfileCommand(BuiltInDisplayProfile.PrimaryDisplayOnly))
            {
                Title = Resources.primary_display_only_title,
                Subtitle = Resources.primary_display_only_subtitle,
                Icon = ProfileIcon,
                MoreCommands = [CreateProfileContextItem()],
            },
        };

        foreach (var (fileName, profileName) in DisplayHelpers.GetSavedProfiles())
        {
            var moreCommands = new List<IContextItem>
            {
                CreateProfileContextItem(),
            };

            var profileData = DisplayHelpers.LoadNamedProfile(fileName);
            if (profileData != null)
            {
                moreCommands.Add(new CommandContextItem(new CreateProfilePage(this, fileName, profileData.Value.Name, profileData.Value.Targets))
                {
                    Title = Resources.edit_profile_title,
                    Subtitle = Resources.edit_profile_subtitle,
                    Icon = new IconInfo("\uE70F"),
                    RequestedShortcut = EditProfileShortcut,
                });
            }

            moreCommands.Add(new CommandContextItem(new ConfirmDeleteSavedProfileCommand(this, fileName, profileName))
            {
                Title = Resources.delete_profile_title,
                Subtitle = Resources.delete_profile_subtitle,
                Icon = new IconInfo("\uE74D"),
                IsCritical = true,
                RequestedShortcut = DeleteProfileShortcut,
            });

            items.Add(new ListItem(new ApplySavedProfileCommand(fileName))
            {
                Title = profileName,
                Subtitle = Resources.apply_profile_subtitle,
                Icon = ProfileIcon,
                MoreCommands = [.. moreCommands],
            });
        }

        return [.. items];
    }

    internal void RefreshProfiles() => RaiseItemsChanged();

    private CommandContextItem CreateProfileContextItem()
    {
        return new CommandContextItem(new CreateProfilePage(this))
        {
            Title = Resources.save_profile_title,
            Subtitle = Resources.save_profile_subtitle,
            Icon = new IconInfo("\uE710"),
            RequestedShortcut = CreateProfileShortcut,
        };
    }

    private static void ShowOperationResult(string message)
    {
        ShowStatus(message, DisplayHelpers.IsErrorResult(message) ? MessageState.Error : MessageState.Success);
    }

    private static CommandResult StartProfileApplication(Func<string> apply)
    {
        if (Interlocked.CompareExchange(ref _isApplyingProfile, 1, 0) != 0)
        {
            ShowStatus(Resources.apply_profile_busy, MessageState.Info);
            return CommandResult.KeepOpen();
        }

        ShowStatus(Resources.apply_profile_status, MessageState.Info);
        _ = Task.Run(() =>
        {
            try
            {
                ShowOperationResult(apply());
            }
            catch (Exception ex)
            {
                ShowStatus(string.Format(Resources.error_format, ex.Message), MessageState.Error);
            }
            finally
            {
                Interlocked.Exchange(ref _isApplyingProfile, 0);
            }
        });

        return CommandResult.KeepOpen();
    }

    private static void ShowStatus(string message, MessageState state)
    {
        ExtensionHost.ShowStatus(
            new StatusMessage() { Message = message, State = state },
            StatusContext.Extension);
    }

    private sealed partial class ApplyBuiltInProfileCommand : InvokableCommand
    {
        private readonly BuiltInDisplayProfile _profile;

        public ApplyBuiltInProfileCommand(BuiltInDisplayProfile profile)
        {
            _profile = profile;
            Name = Resources.apply_profile_title;
            Icon = ProfileIcon;
        }

        public override CommandResult Invoke()
        {
            return StartProfileApplication(() => _profile switch
            {
                BuiltInDisplayProfile.AllDisplays => DisplayHelpers.SetAllDisplays(),
                BuiltInDisplayProfile.PrimaryDisplayOnly => DisplayHelpers.SetPrimaryDisplayOnly(progress => ShowStatus(progress, MessageState.Info)),
                _ => throw new InvalidOperationException(),
            });
        }
    }

    private sealed partial class ApplySavedProfileCommand : InvokableCommand
    {
        private readonly string _fileName;

        public ApplySavedProfileCommand(string fileName)
        {
            _fileName = fileName;
            Name = Resources.apply_profile_title;
            Icon = ProfileIcon;
        }

        public override CommandResult Invoke()
        {
            return StartProfileApplication(() =>
                DisplayHelpers.ApplyNamedProfile(_fileName, progress => ShowStatus(progress, MessageState.Info)));
        }
    }

    private sealed partial class ConfirmDeleteSavedProfileCommand(
        MonitorPowerListPage page,
        string fileName,
        string profileName) : InvokableCommand
    {
        public override CommandResult Invoke()
        {
            return CommandResult.Confirm(new ConfirmationArgs
            {
                Title = Resources.confirm_delete_profile_title,
                Description = string.Format(Resources.confirm_delete_profile_description, profileName),
                PrimaryCommand = new DeleteSavedProfileCommand(page, fileName),
                IsPrimaryCommandCritical = true,
            });
        }
    }

    private sealed partial class DeleteSavedProfileCommand(
        MonitorPowerListPage page,
        string fileName) : InvokableCommand
    {
        public override CommandResult Invoke()
        {
            try
            {
                DisplayHelpers.DeleteSavedProfile(fileName);
                page.RefreshProfiles();
                ShowStatus(Resources.profile_deleted, MessageState.Success);
            }
            catch (Exception ex)
            {
                ShowStatus(string.Format(Resources.error_format, ex.Message), MessageState.Error);
            }

            return CommandResult.KeepOpen();
        }
    }
}
