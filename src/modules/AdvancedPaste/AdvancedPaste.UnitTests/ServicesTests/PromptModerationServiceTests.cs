// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Services;
using AdvancedPaste.Services.OpenAI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class PromptModerationServiceTests
{
    [TestMethod]
    public async Task DisabledOpenAIPolicy_DoesNotReadCredentialsOrSendPrompt()
    {
        var credentials = new Mock<IAICredentialsProvider>(MockBehavior.Strict);
        var service = new PromptModerationService(credentials.Object, isModerationAllowed: () => false);

        await service.ValidateAsync("Azure prompt must not reach OpenAI", CancellationToken.None);

        credentials.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task AllowedPolicyWithoutCredential_PreservesExistingSkipBehavior()
    {
        var credentials = new Mock<IAICredentialsProvider>(MockBehavior.Strict);
        credentials.Setup(provider => provider.Refresh()).Returns(false);
        credentials.Setup(provider => provider.GetKey()).Returns(string.Empty);
        var service = new PromptModerationService(credentials.Object, isModerationAllowed: () => true);

        await service.ValidateAsync("synthetic prompt", CancellationToken.None);

        credentials.Verify(provider => provider.Refresh(), Times.Once);
        credentials.Verify(provider => provider.GetKey(), Times.Once);
        credentials.VerifyNoOtherCalls();
    }
}
