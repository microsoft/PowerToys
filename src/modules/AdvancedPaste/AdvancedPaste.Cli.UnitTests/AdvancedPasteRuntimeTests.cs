// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Cli;
using AdvancedPaste.Models;
using AdvancedPaste.Services;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
public class AdvancedPasteRuntimeTests
{
    [TestMethod]
    public async Task AiActions_WhenDisabledInSettings_AreRejectedBeforeExecution()
    {
        var package = new DataPackage();
        package.SetText("input");

        foreach (var request in new[]
        {
            new CliActionRequest("paste-with-ai", null, "prompt", null),
            new CliActionRequest("fix-spelling-and-grammar", null, null, null),
            new CliActionRequest(null, "saved-action", null, null),
        })
        {
            var executor = new TestPasteFormatExecutor();
            var runtime = new AdvancedPasteRuntime(executor, new TestUserSettings(isAIEnabled: false), isAdvancedPasteEnabled: () => true);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => runtime.ExecuteAsync(request, package.GetView(), CancellationToken.None));

            Assert.IsFalse(executor.WasCalled);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenAdvancedPasteIsDisabledByPolicy_IsRejectedBeforeExecution()
    {
        var package = new DataPackage();
        package.SetText("input");
        var executor = new TestPasteFormatExecutor();
        var runtime = new AdvancedPasteRuntime(executor, new TestUserSettings(isAIEnabled: true), isAdvancedPasteEnabled: () => false);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => runtime.ExecuteAsync(new CliActionRequest("plain-text", null, null, null), package.GetView(), CancellationToken.None));

        Assert.IsFalse(executor.WasCalled);
    }

    private sealed class TestPasteFormatExecutor : IPasteFormatExecutor
    {
        public bool WasCalled { get; private set; }

        public Task<DataPackage> ExecutePasteFormatAsync(
            PasteFormat pasteFormat,
            DataPackageView input,
            PasteActionSource source,
            CancellationToken cancellationToken,
            IProgress<double> progress)
        {
            WasCalled = true;
            return Task.FromResult(new DataPackage());
        }
    }

    private sealed class TestUserSettings(bool isAIEnabled) : IUserSettings
    {
        public bool IsAIEnabled { get; } = isAIEnabled;

        public bool ShowCustomPreview => false;

        public bool ShowAIPaste => false;

        public bool CloseAfterLosingFocus => false;

        public bool EnableClipboardPreview => false;

        public IReadOnlyList<AdvancedPasteCustomAction> CustomActions => Array.Empty<AdvancedPasteCustomAction>();

        public IReadOnlyList<PasteFormats> AdditionalActions => Array.Empty<PasteFormats>();

        public string FixSpellingAndGrammarPrompt => string.Empty;

        public string FixSpellingAndGrammarSystemPrompt => string.Empty;

        public string FixSpellingAndGrammarProviderId => string.Empty;

        public bool FixSpellingAndGrammarCoachingEnabled => false;

        public bool FixSpellingAndGrammarCoachingShortcutSet => false;

        public string FixSpellingAndGrammarCoachingPrompt => string.Empty;

        public string FixSpellingAndGrammarCoachingSystemPrompt => string.Empty;

        public string FixSpellingAndGrammarCoachingProviderId => string.Empty;

        public PasteAIConfiguration PasteAIConfiguration { get; } = new();

        public event EventHandler Changed
        {
            add { }
            remove { }
        }

        public Task SetActiveAIProviderAsync(string providerId) => Task.CompletedTask;
    }
}
