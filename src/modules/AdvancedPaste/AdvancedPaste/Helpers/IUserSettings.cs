// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Settings
{
    public interface IUserSettings
    {
        public bool IsAIEnabled { get; }

        public bool ShowCustomPreview { get; }

        public bool CloseAfterLosingFocus { get; }

        public bool EnableClipboardPreview { get; }

        public IReadOnlyList<AdvancedPasteCustomAction> CustomActions { get; }

        public IReadOnlyList<PasteFormats> AdditionalActions { get; }

        public PasteAIConfiguration PasteAIConfiguration { get; }

        public IReadOnlyList<AdvancedPastePythonScriptAction> PythonScriptActions { get; }

        public string PythonScriptsFolder { get; }

        public string PythonExecutablePath { get; }

        public int PythonScriptTimeoutSeconds { get; }

        public IReadOnlyDictionary<string, string> TrustedScriptHashes { get; }

        public event EventHandler Changed;

        Task SetActiveAIProviderAsync(string providerId);

        void StoreTrustedScriptHash(string scriptPath, string hash);
    }
}
