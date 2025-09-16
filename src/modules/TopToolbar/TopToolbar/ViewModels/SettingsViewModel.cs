// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using TopToolbar.Models;
using TopToolbar.Services;

namespace TopToolbar.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly ToolbarConfigService _service;

        public ObservableCollection<ButtonGroup> Groups { get; } = new();

        private ButtonGroup _selectedGroup;

        public ButtonGroup SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                SetProperty(ref _selectedGroup, value);
                OnPropertyChanged(nameof(HasSelectedGroup));
                OnPropertyChanged(nameof(HasNoSelectedGroup));
            }
        }

        private ToolbarButton _selectedButton;

        public ToolbarButton SelectedButton
        {
            get => _selectedButton;
            set
            {
                SetProperty(ref _selectedButton, value);
                OnPropertyChanged(nameof(HasSelectedButton));
            }
        }

        public bool HasSelectedGroup => SelectedGroup != null;

        public bool HasNoSelectedGroup => SelectedGroup == null;

        public bool HasSelectedButton => SelectedButton != null;

        public SettingsViewModel(ToolbarConfigService service)
        {
            _service = service;
        }

        public async Task LoadAsync(DispatcherQueue dispatcher)
        {
            var cfg = await _service.LoadAsync();

            void Apply()
            {
                Groups.Clear();
                foreach (var g in cfg.Groups)
                {
                    Groups.Add(g);
                }

                if (SelectedGroup == null && Groups.Count > 0)
                {
                    SelectedGroup = Groups[0];
                    SelectedButton = SelectedGroup.Buttons.FirstOrDefault();
                }
            }

            if (dispatcher.HasThreadAccess)
            {
                Apply();
            }
            else
            {
                var tcs = new TaskCompletionSource();
                dispatcher.TryEnqueue(() =>
                {
                    Apply();
                    tcs.SetResult();
                });
                await tcs.Task;
            }
        }

        public async Task SaveAsync()
        {
            var cfg = new ToolbarConfig { Groups = Groups.ToList() };
            await _service.SaveAsync(cfg);
        }

        public void AddGroup()
        {
            Groups.Add(new ButtonGroup { Name = "New Group" });
            SelectedGroup = Groups.LastOrDefault();
            SelectedButton = SelectedGroup?.Buttons.FirstOrDefault();
        }

        public void RemoveGroup(ButtonGroup group)
        {
            Groups.Remove(group);
            if (SelectedGroup == group)
            {
                SelectedGroup = Groups.FirstOrDefault();
                SelectedButton = SelectedGroup?.Buttons.FirstOrDefault();
            }
        }

        public void AddButton(ButtonGroup group)
        {
            group.Buttons.Add(new ToolbarButton { Name = "New Button", IconGlyph = "\uE10F", Action = new ToolbarAction { Command = "notepad.exe" } });

            var idx = Groups.IndexOf(group);
            if (idx >= 0)
            {
                Groups[idx] = group;
            }

            SelectedGroup = group;
            SelectedButton = group.Buttons.LastOrDefault();
        }

        public void RemoveButton(ButtonGroup group, ToolbarButton button)
        {
            group.Buttons.Remove(button);
            var idx = Groups.IndexOf(group);
            if (idx >= 0)
            {
                Groups[idx] = group;
            }

            if (SelectedButton == button)
            {
                SelectedButton = group.Buttons.FirstOrDefault();
            }
        }
    }
}
