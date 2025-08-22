// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public class CommandPalettePageViewModelFactory
    : IPageViewModelFactoryService
{
    private readonly TaskScheduler _scheduler;
    private readonly SettingsModel _settingsModel;

    public CommandPalettePageViewModelFactory(TaskScheduler scheduler, SettingsModel settingsModel)
    {
        _scheduler = scheduler;
        _settingsModel = settingsModel ?? throw new ArgumentNullException(nameof(settingsModel));
    }

    public PageViewModel? TryCreatePageViewModel(IPage page, bool nested, AppExtensionHost host)
    {
        var isPinyinInput = _settingsModel.IsPinYinInput;
        var matchOptions = new MatchOption
        {
            Language = isPinyinInput ? MatchLanguage.Chinese : MatchLanguage.English,
        };

        return page switch
        {
            IListPage listPage => new ListViewModel(listPage, _scheduler, host, matchOptions) { IsNested = nested },
            IContentPage contentPage => new CommandPaletteContentPageViewModel(contentPage, _scheduler, host),
            _ => null,
        };
    }
}
