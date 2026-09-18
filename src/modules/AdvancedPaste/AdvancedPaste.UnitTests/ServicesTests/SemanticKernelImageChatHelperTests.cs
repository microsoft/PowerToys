// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Services.CustomActions;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class SemanticKernelImageChatHelperTests
{
    // Guards against regression of #49838
    [TestMethod]
    public void CreateExecutionSettings_ForOpenAI_DoesNotSetReasoningEffort()
    {
        var settings = SemanticKernelImageChatHelper.CreateExecutionSettings(AIServiceType.OpenAI);

        Assert.IsNotNull(settings, "Expected non-null execution settings.");
        Assert.IsInstanceOfType(settings, typeof(OpenAIPromptExecutionSettings), "Expected OpenAIPromptExecutionSettings for OpenAI provider.");

        var openAISettings = (OpenAIPromptExecutionSettings)settings;

        // Assert that ReasoningEffort is not set (is null) to prevent HTTP 400 on models that don't support it
        Assert.IsNull(openAISettings.ReasoningEffort, "ReasoningEffort should not be set (see #49838)");
    }
}
