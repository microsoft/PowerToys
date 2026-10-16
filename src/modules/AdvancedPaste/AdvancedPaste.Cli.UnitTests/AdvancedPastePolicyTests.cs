// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Services;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
public class AdvancedPastePolicyTests
{
    [DataTestMethod]
    [DataRow("unknown-provider")]
    [DataRow("OpenAI")]
    public void OpenAIAndUnknownProvider_RespectOpenAIPolicy(string serviceType)
    {
        var checkedService = AIServiceType.Unknown;
        var provider = new PasteAIProviderDefinition { ServiceType = serviceType };

        var allowed = AdvancedPastePolicy.IsProviderAllowed(provider, () => true, service =>
        {
            checkedService = service;
            return service != AIServiceType.OpenAI;
        });

        Assert.IsFalse(allowed);
        Assert.AreEqual(AIServiceType.OpenAI, checkedService);
    }

    [DataTestMethod]
    [DataRow("unknown-provider")]
    [DataRow("AzureOpenAI")]
    public void OnlineProviders_RespectOnlinePolicy(string serviceType)
    {
        var provider = new PasteAIProviderDefinition { ServiceType = serviceType };

        Assert.IsFalse(AdvancedPastePolicy.IsProviderAllowed(provider, () => false, _ => true));
    }

    [TestMethod]
    public void LocalProvider_DoesNotRequireOnlinePolicy()
    {
        var provider = new PasteAIProviderDefinition { ServiceType = "PhiSilica" };

        Assert.IsTrue(AdvancedPastePolicy.IsProviderAllowed(provider, () => false, _ => true));
    }
}
