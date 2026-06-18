// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public interface ISettingsInterface
{
    public event EventHandler HistoryChanged;

    public event EventHandler SettingsChanged;

    public CalculateEngine.TrigMode TrigUnit { get; }

    public bool InputUseEnglishFormat { get; }

    public bool OutputUseEnglishFormat { get; }

    public bool CloseOnEnter { get; }

    public bool ReplaceQueryOnEnter { get; }

    public bool CopyResultToSearchBarIfQueryEndsWithEqualSign { get; }

    public bool AutoFixQuery { get; }

    public bool SaveFallbackResultsToHistory { get; }

    public bool DeleteHistoryRequiresConfirmation { get; }

    public PrimaryAction PrimaryAction { get; }

    public IReadOnlyList<HistoryItem> HistoryItems { get; }

    public void AddHistoryItem(HistoryItem historyItem);

    public void RemoveHistoryItem(Guid historyItemId);

    public void ClearHistory();
}
