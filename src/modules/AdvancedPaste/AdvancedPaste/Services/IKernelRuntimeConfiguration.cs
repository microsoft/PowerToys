// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Services;

/// <summary>
/// Represents runtime information required to configure an AI kernel service.
/// </summary>
public interface IKernelRuntimeConfiguration
{
    AIServiceType ServiceType { get; }

    string ModelName { get; }

    string Endpoint { get; }

    string DeploymentName { get; }

    string ModelPath { get; }

    string SystemPrompt { get; }

    bool ModerationEnabled { get; }
}
