// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Resolved, ready-to-use description of the AI endpoint a request should be sent to.
    /// </summary>
    public sealed class AIProviderConfig
    {
        public AIServiceType ProviderType { get; set; } = AIServiceType.Unknown;

        public string? Model { get; set; }

        public string? ApiKey { get; set; }

        public string? Endpoint { get; set; }

        public string? DeploymentName { get; set; }

        public string? ModelPath { get; set; }
    }
}
