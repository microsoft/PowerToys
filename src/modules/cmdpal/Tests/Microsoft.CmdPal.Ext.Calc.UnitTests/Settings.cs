// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Calc.Helper;

namespace Microsoft.CmdPal.Ext.Calc.UnitTests;

public class Settings : ISettingsInterface
{
    private readonly CalculateEngine.TrigMode trigUnit;
    private readonly bool inputUseEnglishFormat;
    private readonly bool outputUseEnglishFormat;
    private readonly bool closeOnEnter;
    private readonly bool copyResultToSearchBarIfQueryEndsWithEqualSign;
    private readonly bool autoFixQuery;
    private readonly bool saveFallbackResultsToHistory;
    private readonly bool deleteHistoryRequiresConfirmation;
    private readonly PrimaryAction primaryAction;
    private readonly bool inputNormalization;
    private readonly List<HistoryItem> historyItems = [];

    public Settings(
        CalculateEngine.TrigMode trigUnit = CalculateEngine.TrigMode.Radians,
        bool inputUseEnglishFormat = false,
        bool outputUseEnglishFormat = false,
        bool closeOnEnter = true,
        bool copyResultToSearchBarIfQueryEndsWithEqualSign = true,
        bool autoFixQuery = true,
        bool saveFallbackResultsToHistory = false,
        bool deleteHistoryRequiresConfirmation = true,
        PrimaryAction primaryAction = PrimaryAction.Default,
        bool inputNormalization = true)
    {
        this.trigUnit = trigUnit;
        this.inputUseEnglishFormat = inputUseEnglishFormat;
        this.outputUseEnglishFormat = outputUseEnglishFormat;
        this.closeOnEnter = closeOnEnter;
        this.copyResultToSearchBarIfQueryEndsWithEqualSign = copyResultToSearchBarIfQueryEndsWithEqualSign;
        this.autoFixQuery = autoFixQuery;
        this.saveFallbackResultsToHistory = saveFallbackResultsToHistory;
        this.deleteHistoryRequiresConfirmation = deleteHistoryRequiresConfirmation;
        this.primaryAction = primaryAction;
        this.inputNormalization = inputNormalization;
    }

    public CalculateEngine.TrigMode TrigUnit => trigUnit;

    public bool InputUseEnglishFormat => inputUseEnglishFormat;

    public bool OutputUseEnglishFormat => outputUseEnglishFormat;

    public bool CloseOnEnter => closeOnEnter;

    public bool CopyResultToSearchBarIfQueryEndsWithEqualSign => copyResultToSearchBarIfQueryEndsWithEqualSign;

    public bool AutoFixQuery => autoFixQuery;

    public bool SaveFallbackResultsToHistory => saveFallbackResultsToHistory;

    public bool DeleteHistoryRequiresConfirmation => deleteHistoryRequiresConfirmation;

    public PrimaryAction PrimaryAction => primaryAction;

    public bool InputNormalization => inputNormalization;

    public event EventHandler HistoryChanged;

    public event EventHandler SettingsChanged;

    public IReadOnlyList<HistoryItem> HistoryItems => historyItems;

    public void AddHistoryItem(HistoryItem historyItem)
    {
        historyItems.Add(historyItem);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveHistoryItem(Guid historyItemId)
    {
        if (historyItems.RemoveAll(item => item.Id == historyItemId) > 0)
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearHistory()
    {
        if (historyItems.Count == 0)
        {
            return;
        }

        historyItems.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
