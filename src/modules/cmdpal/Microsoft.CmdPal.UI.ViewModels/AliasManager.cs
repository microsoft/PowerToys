using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class AliasManager : IDisposable
{
    private readonly ITopLevelCommandManager _topLevelCommandManager;
    private readonly ISettingsService _settingsService;
    private bool _disposed;

    public AliasManager(ITopLevelCommandManager topLevelCommandManager, ISettingsService settingsService)
    {
        _topLevelCommandManager = topLevelCommandManager;
        _settingsService = settingsService;
    }

    public void UpdateAlias(string commandId, Alias? newAlias, List<string> keysToRemove)
    {
        foreach (var key in keysToRemove)
        {
            var topLevelCommand = _topLevelCommandManager.LookupCommand(commandId);
            if (topLevelCommand is not null)
            {
                topLevelCommand.AliasText = string.Empty;
                
                // Unsubscribe from events here if applicable
                // topLevelCommand.SomeEvent -= HandleEvent;
            }
        }

        _settingsService.UpdateSettings(s =>
        {
            var updatedAliases = s.Aliases.RemoveRange(keysToRemove);
            if (newAlias is not null)
            {
                updatedAliases = updatedAliases.Add(newAlias.SearchPrefix, newAlias);
            }
            return s with { Aliases = updatedAliases };
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        // Clean up service references here
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
