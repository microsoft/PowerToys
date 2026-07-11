// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class AudioCueSettingsTests
{
    [TestMethod]
    public void Catalog_DefinesEveryCueExactlyOnce()
    {
        CollectionAssert.AreEquivalent(Enum.GetValues<AudioCue>(), AudioCueCatalog.Cues.Select(definition => definition.Cue).ToArray());
        Assert.AreEqual(AudioCueCatalog.Cues.Count, AudioCueCatalog.Cues.Select(definition => definition.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [TestMethod]
    public void Catalog_CueSystemSoundReferencesAreValid()
    {
        Assert.AreEqual(
            AudioCueCatalog.SystemSounds.Count,
            AudioCueCatalog.SystemSounds.Select(sound => sound.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var definition in AudioCueCatalog.Cues)
        {
            foreach (var systemSoundId in definition.SystemSoundIds)
            {
                Assert.IsTrue(
                    AudioCueCatalog.TryGetSystemSound(systemSoundId, out _),
                    $"Cue {definition.Id} references unknown system sound {systemSoundId}");
            }
        }
    }

    [TestMethod]
    public void ResolveSoundId_FallsBackToDefaultForUnknownIds()
    {
        Assert.AreEqual(AudioCueCatalog.DefaultSoundId, AudioCueCatalog.ResolveSoundId(null));
        Assert.AreEqual(AudioCueCatalog.DefaultSoundId, AudioCueCatalog.ResolveSoundId("no-longer-shipped"));
        Assert.AreEqual("warm", AudioCueCatalog.ResolveSoundId("warm"));
        Assert.AreEqual("system-asterisk", AudioCueCatalog.ResolveSoundId("System-Asterisk"));
        Assert.AreEqual(AudioCueCatalog.CustomSoundId, AudioCueCatalog.ResolveSoundId("Custom"));
    }

    [TestMethod]
    public void Defaults_AreOptInWithEnabledBuiltInEffects()
    {
        var settings = new AudioCueSettings();

        Assert.IsFalse(settings.IsEnabled);
        Assert.AreEqual(50, settings.Volume);

        foreach (var cue in Enum.GetValues<AudioCue>())
        {
            var definition = AudioCueCatalog.GetDefinition(cue);
            var effect = settings.GetEffect(cue);
            Assert.AreEqual(definition.EnabledByDefault, effect.IsEnabled, $"Unexpected default enabled state for {definition.Id}");
            Assert.AreEqual(AudioCueCatalog.DefaultSoundId, AudioCueCatalog.ResolveSoundId(effect.Sound));
        }

        Assert.IsFalse(settings.GetEffect(AudioCue.FocusChange).IsEnabled, "Focus cue is chatty and must ship opt-in");
    }

    [TestMethod]
    public void WithEffect_UpdatesOnlyRequestedCue()
    {
        var settings = new AudioCueSettings();
        var disabledEffect = new AudioCueEffectSettings
        {
            IsEnabled = false,
            Sound = "warm",
        };

        var updated = settings.WithEffect(AudioCue.SelectionChange, disabledEffect);

        Assert.AreEqual(disabledEffect, updated.GetEffect(AudioCue.SelectionChange));
        Assert.AreEqual(settings.GetEffect(AudioCue.OpenPalette), updated.GetEffect(AudioCue.OpenPalette));
        Assert.AreEqual(settings.GetEffect(AudioCue.ConfirmationPopup), updated.GetEffect(AudioCue.ConfirmationPopup));
        Assert.AreNotEqual(settings.GetEffect(AudioCue.SelectionChange), updated.GetEffect(AudioCue.SelectionChange));
    }

    [TestMethod]
    public void Effects_RoundTripThroughSettingsModelJson()
    {
        var settings = JsonSerializer.Deserialize("{}", JsonSerializationContext.Default.SettingsModel)!;
        settings = settings with
        {
            AudioCues = settings.AudioCues.WithEffect(AudioCue.ToastShown, new AudioCueEffectSettings
            {
                Sound = AudioCueCatalog.CustomSoundId,
                CustomSoundPath = @"C:\sounds\toast.wav",
            }),
        };

        var json = JsonSerializer.Serialize(settings, JsonSerializationContext.Default.SettingsModel);
        var roundTripped = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel)!;

        Assert.AreEqual(settings.AudioCues.GetEffect(AudioCue.ToastShown), roundTripped.AudioCues.GetEffect(AudioCue.ToastShown));
        Assert.AreEqual(settings.AudioCues.GetEffect(AudioCue.OpenPalette), roundTripped.AudioCues.GetEffect(AudioCue.OpenPalette));
    }

    [TestMethod]
    public void ViewModel_ProvidesPerEffectSoundOptions()
    {
        var settings = JsonSerializer.Deserialize("{}", JsonSerializationContext.Default.SettingsModel)!;
        var settingsService = CreateSettingsService(() => settings, value => settings = value);
        var viewModel = new AudioCueSettingsViewModel(settingsService.Object);

        CollectionAssert.AreEqual(
            AudioCueCatalog.Cues.Select(definition => definition.Cue).ToArray(),
            viewModel.Effects.Select(effect => effect.Cue).ToArray());
        foreach (var effect in viewModel.Effects)
        {
            var definition = AudioCueCatalog.GetDefinition(effect.Cue);
            string?[] expectedSoundIds =
            [
                null,
                .. AudioCueCatalog.BuiltInSounds.Select(sound => sound.Id),
                .. definition.SystemSoundIds,
                AudioCueCatalog.CustomSoundId,
            ];
            CollectionAssert.AreEqual(expectedSoundIds, effect.SoundOptions.Select(option => option.SoundId).ToArray());
        }

        Assert.AreNotSame(viewModel.Effects[0].SoundOptions, viewModel.Effects[1].SoundOptions);
    }

    [TestMethod]
    public void ViewModel_DisablingCuePreservesChosenSound()
    {
        var settings = JsonSerializer.Deserialize("{}", JsonSerializationContext.Default.SettingsModel)!;
        var settingsService = CreateSettingsService(() => settings, value => settings = value);
        var viewModel = new AudioCueSettingsViewModel(settingsService.Object);
        var effect = viewModel.Effects.First(e => e.Cue == AudioCue.OpenPalette);

        effect.SelectedSound = effect.SoundOptions.First(option => option.SoundId == "warm");
        effect.SelectedSound = effect.SoundOptions.First(option => option.SoundId is null);

        var persisted = settings.AudioCues.GetEffect(AudioCue.OpenPalette);
        Assert.IsFalse(persisted.IsEnabled);
        Assert.AreEqual("warm", persisted.Sound);
        Assert.IsNull(effect.SelectedSound.SoundId);
    }

    [TestMethod]
    public void GlobalToggle_PersistsWithoutBroadcastingHotReload()
    {
        var settings = JsonSerializer.Deserialize("{}", JsonSerializationContext.Default.SettingsModel)!;
        var settingsService = CreateSettingsService(() => settings, value => settings = value);
        var viewModel = new AudioCueSettingsViewModel(settingsService.Object);

        viewModel.IsEnabled = true;

        Assert.IsTrue(settings.AudioCues.IsEnabled);
        settingsService.Verify(
            service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), false),
            Times.Once);
    }

    private static Mock<ISettingsService> CreateSettingsService(Func<SettingsModel> getSettings, Action<SettingsModel> setSettings)
    {
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(getSettings);
        settingsService
            .Setup(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((transform, _) => setSettings(transform(getSettings())));
        return settingsService;
    }
}
