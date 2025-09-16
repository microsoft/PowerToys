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
    public class ToolbarViewModel : ObservableObject
    {
        private readonly ToolbarConfigService _configService;

        public ObservableCollection<ButtonGroup> Groups { get; } = new();

        public ToolbarViewModel(ToolbarConfigService configService)
        {
            _configService = configService;
        }

        public async Task LoadAsync(DispatcherQueue dispatcher)
        {
            var cfg = await _configService.LoadAsync();

            void Apply()
            {
                Groups.Clear();
                foreach (var g in cfg.Groups)
                {
                    Groups.Add(g);
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
            await _configService.SaveAsync(cfg);
        }
    }
}
