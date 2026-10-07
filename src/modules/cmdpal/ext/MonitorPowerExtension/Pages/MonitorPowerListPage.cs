// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using MonitorPowerExtension.Properties;

#pragma warning disable CA1305, CA1863

namespace MonitorPowerExtension.Pages;

public sealed partial class MonitorPowerListPage : ListPage
{
    public const string MonitorPowerListPageId = "MonitorPower_ListPage";

    private static readonly IconInfo AppIcon = new("\uE7F4");
    private static readonly IconInfo ProfileIcon = new("\uE81C");

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
            new ListItem(new CommandItem(new CreateProfilePage(this)))
            {
                Title = Resources.save_profile_title,
                Subtitle = Resources.save_profile_subtitle,
                Icon = ProfileIcon,
            },
        };

        foreach (var (fileName, profileName) in DisplayHelpers.GetSavedProfiles())
        {
            var moreCommands = new List<IContextItem>();

            var profileData = DisplayHelpers.LoadNamedProfile(fileName);
            if (profileData != null)
            {
                moreCommands.Add(new CommandContextItem(new CreateProfilePage(this, fileName, profileData.Value.Name, profileData.Value.Targets))
                {
                    Title = Resources.edit_profile_title,
                    Icon = new IconInfo("\uE70F"),
                });
            }

            moreCommands.Add(new CommandContextItem(new DeleteSavedProfileCommand(this, fileName))
            {
                Title = Resources.delete_profile_title,
                Icon = new IconInfo("\uE74D"),
                IsCritical = true,
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
            ExtensionHost.ShowStatus(
                new StatusMessage() { Message = "Attivazione profilo in corso... Attendi l'accensione dei monitor.", State = MessageState.Info },
                StatusContext.Extension);

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var msg = DisplayHelpers.ApplyNamedProfile(_fileName, (progressMsg, state) =>
                    {
                        ExtensionHost.ShowStatus(
                            new StatusMessage() { Message = progressMsg, State = state },
                            StatusContext.Extension);
                    });

                    var isError = msg.StartsWith(Resources.error_prefix, StringComparison.OrdinalIgnoreCase);
                    ExtensionHost.ShowStatus(
                        new StatusMessage() { Message = msg, State = isError ? MessageState.Error : MessageState.Success },
                        StatusContext.Extension);
                }
                catch (Exception ex)
                {
                    ExtensionHost.ShowStatus(
                        new StatusMessage() { Message = "Errore: " + ex.Message, State = MessageState.Error },
                        StatusContext.Extension);
                }
            });

            return CommandResult.KeepOpen();
        }
    }

    private sealed partial class DeleteSavedProfileCommand : InvokableCommand
    {
        private readonly MonitorPowerListPage _page;
        private readonly string _fileName;

        public DeleteSavedProfileCommand(MonitorPowerListPage page, string fileName)
        {
            _page = page;
            _fileName = fileName;
            Name = Resources.delete_profile_title;
        }

        public override CommandResult Invoke()
        {
            DisplayHelpers.DeleteSavedProfile(_fileName);
            _page.RefreshProfiles();
            ExtensionHost.ShowStatus(
                new StatusMessage() { Message = Resources.profile_deleted, State = MessageState.Success },
                StatusContext.Extension);
            return CommandResult.KeepOpen();
        }
    }
}
