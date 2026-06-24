// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AdvancedPaste.Models;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AdvancedPaste.Services.CustomActions
{
    public class PasteAIConfig
    {
        public AIServiceType ProviderType { get; set; }

        public string Model { get; set; }

        public string ApiKey { get; set; }

        public string Endpoint { get; set; }

        public string DeploymentName { get; set; }

        public string LocalModelPath { get; set; }

        public string ModelPath { get; set; }

        public string SystemPrompt { get; set; }

        public bool ModerationEnabled { get; set; }
    }
}
