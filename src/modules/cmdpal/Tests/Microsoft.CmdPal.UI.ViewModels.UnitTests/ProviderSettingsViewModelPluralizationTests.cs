// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Tests for pluralization logic in ProviderSettingsViewModel.ExtensionSubtext property.
/// Verifies correct singular/plural forms for command counts and fallback command counts
/// as displayed in the CmdPal settings → Extensions page.
/// </summary>
[TestClass]
public class ProviderSettingsViewModelPluralizationTests
{
    private sealed partial class TestExtension : AppExtensionWrapper
    {
        public TestExtension()
            : base(null!, null!)
        {
        }

        public override string ExtensionDisplayName => "TestExtension";

        public override Version Version => new(1, 0, 0, 0);
    }

    private sealed partial class TestCommandProvider : CommandProvider
    {
        public override string DisplayName => "TestProvider";

        public override IListPage GetListPage(IListPageNavigator navigator) => null!;
    }

    private static CommandProviderWrapper CreateProvider(
        string displayName,
        int topLevelCommandCount,
        int fallbackCommandCount,
        IExtensionWrapper? extension = null)
    {
        var provider = new TestCommandProvider();
        var topLevelItems = new TopLevelViewModel[topLevelCommandCount];
        for (int i = 0; i < topLevelCommandCount; i++)
        {
            topLevelItems[i] = new TopLevelViewModel($"Command{i}", string.Empty, null!, extension);
        }

        var fallbackItems = new FallbackItemWrapper[fallbackCommandCount];
        for (int i = 0; i < fallbackCommandCount; i++)
        {
            fallbackItems[i] = new FallbackItemWrapper(new FallbackCommandItem()
            {
                Id = $"Fallback{i}",
                Title = $"Fallback{i}",
            });
        }

        var wrapper = new CommandProviderWrapper(
            provider,
            providerId: "test.provider",
            topLevelItems: topLevelItems,
            fallbackItems: fallbackItems,
            settingsPage: null,
            icon: null,
            extension: extension);

        return wrapper;
    }

    private static ProviderSettingsViewModel CreateViewModel(
        CommandProviderWrapper provider,
        bool isEnabled = true)
    {
        var mockSettingsService = new Mock<ISettingsService>();
        var providerSettings = new ProviderSettings
        {
            IsEnabled = isEnabled,
            FallbackCommands = ImmutableDictionary<string, FallbackSettings>.Empty,
        };

        return new ProviderSettingsViewModel(provider, providerSettings, mockSettingsService.Object);
    }

    [TestMethod]
    public void ExtensionSubtext_ZeroCommands_UsesPluralForm()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 0, fallbackCommandCount: 0, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("0 commands"), $"Expected '0 commands' but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_OneCommand_UsesSingularForm()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 1, fallbackCommandCount: 0, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("1 command"), $"Expected '1 command' (singular) but got: {subtext}");
        Assert.IsFalse(subtext.Contains("1 commands"), $"Should not contain '1 commands' (plural) but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_TwoCommands_UsesPluralForm()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 2, fallbackCommandCount: 0, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("2 commands"), $"Expected '2 commands' but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_FiveCommands_UsesPluralForm()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 5, fallbackCommandCount: 0, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("5 commands"), $"Expected '5 commands' but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_OneFallbackCommand_UsesSingularForm()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 2, fallbackCommandCount: 1, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("1 fallback command"), $"Expected '1 fallback command' (singular) but got: {subtext}");
        Assert.IsFalse(subtext.Contains("1 fallback commands"), $"Should not contain '1 fallback commands' (plural) but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_TwoFallbackCommands_UsesPluralForm()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 2, fallbackCommandCount: 2, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("2 fallback commands"), $"Expected '2 fallback commands' but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_OneCommandOneFallback_BothSingular()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 1, fallbackCommandCount: 1, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("1 command"), $"Expected '1 command' (singular) but got: {subtext}");
        Assert.IsTrue(subtext.Contains("1 fallback command"), $"Expected '1 fallback command' (singular) but got: {subtext}");
        Assert.IsFalse(subtext.Contains("1 commands"), $"Should not contain '1 commands' (plural) but got: {subtext}");
        Assert.IsFalse(subtext.Contains("1 fallback commands"), $"Should not contain '1 fallback commands' (plural) but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_OneCommandMultipleFallback_MixedSingularPlural()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 1, fallbackCommandCount: 3, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("1 command"), $"Expected '1 command' (singular) but got: {subtext}");
        Assert.IsTrue(subtext.Contains("3 fallback commands"), $"Expected '3 fallback commands' (plural) but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_MultipleCommandsOneFallback_MixedPluralSingular()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 5, fallbackCommandCount: 1, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("5 commands"), $"Expected '5 commands' (plural) but got: {subtext}");
        Assert.IsTrue(subtext.Contains("1 fallback command"), $"Expected '1 fallback command' (singular) but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_MultipleCommandsMultipleFallback_BothPlural()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 3, fallbackCommandCount: 5, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("3 commands"), $"Expected '3 commands' but got: {subtext}");
        Assert.IsTrue(subtext.Contains("5 fallback commands"), $"Expected '5 fallback commands' but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_LargeCommandCount_UsesPluralForm()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 100, fallbackCommandCount: 0, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("100 commands"), $"Expected '100 commands' but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_DisabledProvider_DoesNotShowCommandCounts()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 5, fallbackCommandCount: 2, extension: extension);
        var viewModel = CreateViewModel(provider, isEnabled: false);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        // When disabled, the subtext should NOT show command counts, only disabled message
        Assert.IsFalse(subtext.Contains("5 commands"), $"Disabled extension should not show command count but got: {subtext}");
        Assert.IsFalse(subtext.Contains("2 fallback commands"), $"Disabled extension should not show fallback count but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_BuiltinProvider_WithoutExtension()
    {
        // Arrange - Built-in providers don't have an IExtensionWrapper
        var provider = CreateProvider("BuiltInProvider", topLevelCommandCount: 3, fallbackCommandCount: 0, extension: null);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("3 commands"), $"Expected '3 commands' for built-in provider but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_ZeroFallbackCommands_DoesNotMentionFallback()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 2, fallbackCommandCount: 0, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsFalse(subtext.Contains("fallback"), $"Should not mention fallback when count is 0 but got: {subtext}");
    }

    [TestMethod]
    public void ExtensionSubtext_IncludesExtensionName()
    {
        // Arrange
        var extension = new TestExtension();
        var provider = CreateProvider("TestProvider", topLevelCommandCount: 1, fallbackCommandCount: 0, extension: extension);
        var viewModel = CreateViewModel(provider);

        // Act
        var subtext = viewModel.ExtensionSubtext;

        // Assert
        Assert.IsTrue(subtext.Contains("TestExtension"), $"Expected extension name 'TestExtension' in subtext but got: {subtext}");
    }
}
