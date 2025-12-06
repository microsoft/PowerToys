// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using SettingsUILibrary = Settings.UI.Library;
using SettingsUILibraryHelpers = Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// JSON serialization context for Native AOT compatibility.
    /// This context provides source-generated serialization for all PowerToys settings types.
    /// </summary>
    /// <remarks>
    /// <para><strong>⚠️ CRITICAL REQUIREMENT FOR ALL NEW SETTINGS CLASSES ⚠️</strong></para>
    /// <para>
    /// When adding a new PowerToys module or any class that inherits from <see cref="BasePTModuleSettings"/>,
    /// you <strong>MUST</strong> add a <c>[JsonSerializable(typeof(YourNewSettingsClass))]</c> attribute
    /// to this class. This is a MANDATORY step for Native AOT compatibility.
    /// </para>
    /// <para><strong>Steps to add a new settings class:</strong></para>
    /// <list type="number">
    /// <item><description>Create your new settings class (e.g., <c>MyNewModuleSettings</c>) that inherits from <see cref="BasePTModuleSettings"/></description></item>
    /// <item><description>Add <c>[JsonSerializable(typeof(MyNewModuleSettings))]</c> attribute to this <see cref="SettingsSerializationContext"/> class</description></item>
    /// <item><description>If you have a corresponding Properties class, also add <c>[JsonSerializable(typeof(MyNewModuleProperties))]</c></description></item>
    /// <item><description>Rebuild the project - source generator will create serialization code at compile time</description></item>
    /// </list>
    /// <para><strong>⚠️ Failure to register types will cause runtime errors:</strong></para>
    /// <para>
    /// If you forget to add the <c>[JsonSerializable]</c> attribute, calling <c>ToJsonString()</c> or
    /// deserialization methods will throw <see cref="InvalidOperationException"/> at runtime with a clear
    /// error message indicating which type is missing registration.
    /// </para>
    /// </remarks>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        IncludeFields = true)]

    // Main Settings Classes
    [JsonSerializable(typeof(GeneralSettings))]
    [JsonSerializable(typeof(AdvancedPasteSettings))]
    [JsonSerializable(typeof(AlwaysOnTopSettings))]
    [JsonSerializable(typeof(AwakeSettings))]
    [JsonSerializable(typeof(CmdNotFoundSettings))]
    [JsonSerializable(typeof(ColorPickerSettings))]
    [JsonSerializable(typeof(ColorPickerSettingsVersion1))]
    [JsonSerializable(typeof(CropAndLockSettings))]
    [JsonSerializable(typeof(CursorWrapSettings))]
    [JsonSerializable(typeof(EnvironmentVariablesSettings))]
    [JsonSerializable(typeof(FancyZonesSettings))]
    [JsonSerializable(typeof(FileLocksmithSettings))]
    [JsonSerializable(typeof(FindMyMouseSettings))]
    [JsonSerializable(typeof(HostsSettings))]
    [JsonSerializable(typeof(ImageResizerSettings))]
    [JsonSerializable(typeof(KeyboardManagerSettings))]
    [JsonSerializable(typeof(SettingsUILibrary.LightSwitchSettings))]
    [JsonSerializable(typeof(MeasureToolSettings))]
    [JsonSerializable(typeof(MouseHighlighterSettings))]
    [JsonSerializable(typeof(MouseJumpSettings))]
    [JsonSerializable(typeof(MousePointerCrosshairsSettings))]
    [JsonSerializable(typeof(MouseWithoutBordersSettings))]
    [JsonSerializable(typeof(NewPlusSettings))]
    [JsonSerializable(typeof(PeekSettings))]
    [JsonSerializable(typeof(PowerAccentSettings))]
    [JsonSerializable(typeof(PowerLauncherSettings))]
    [JsonSerializable(typeof(PowerOcrSettings))]
    [JsonSerializable(typeof(PowerPreviewSettings))]
    [JsonSerializable(typeof(PowerRenameSettings))]
    [JsonSerializable(typeof(RegistryPreviewSettings))]
    [JsonSerializable(typeof(ShortcutGuideSettings))]
    [JsonSerializable(typeof(WorkspacesSettings))]
    [JsonSerializable(typeof(ZoomItSettings))]

    // Properties Classes
    [JsonSerializable(typeof(AdvancedPasteProperties))]
    [JsonSerializable(typeof(AlwaysOnTopProperties))]
    [JsonSerializable(typeof(AwakeProperties))]
    [JsonSerializable(typeof(CmdPalProperties))]
    [JsonSerializable(typeof(ColorPickerProperties))]
    [JsonSerializable(typeof(ColorPickerPropertiesVersion1))]
    [JsonSerializable(typeof(CropAndLockProperties))]
    [JsonSerializable(typeof(CursorWrapProperties))]
    [JsonSerializable(typeof(EnvironmentVariablesProperties))]
    [JsonSerializable(typeof(FileLocksmithProperties))]
    [JsonSerializable(typeof(FileLocksmithLocalProperties))]
    [JsonSerializable(typeof(FindMyMouseProperties))]
    [JsonSerializable(typeof(FZConfigProperties))]
    [JsonSerializable(typeof(HostsProperties))]
    [JsonSerializable(typeof(ImageResizerProperties))]
    [JsonSerializable(typeof(KeyboardManagerProperties))]
    [JsonSerializable(typeof(KeyboardManagerProfile))]
    [JsonSerializable(typeof(LightSwitchProperties))]
    [JsonSerializable(typeof(MeasureToolProperties))]
    [JsonSerializable(typeof(MouseHighlighterProperties))]
    [JsonSerializable(typeof(MouseJumpProperties))]
    [JsonSerializable(typeof(MousePointerCrosshairsProperties))]
    [JsonSerializable(typeof(MouseWithoutBordersProperties))]
    [JsonSerializable(typeof(NewPlusProperties))]
    [JsonSerializable(typeof(PeekProperties))]
    [JsonSerializable(typeof(SettingsUILibrary.PeekPreviewSettings))]
    [JsonSerializable(typeof(PowerAccentProperties))]
    [JsonSerializable(typeof(PowerLauncherProperties))]
    [JsonSerializable(typeof(PowerOcrProperties))]
    [JsonSerializable(typeof(PowerPreviewProperties))]
    [JsonSerializable(typeof(PowerRenameProperties))]
    [JsonSerializable(typeof(PowerRenameLocalProperties))]
    [JsonSerializable(typeof(RegistryPreviewProperties))]
    [JsonSerializable(typeof(ShortcutConflictProperties))]
    [JsonSerializable(typeof(ShortcutGuideProperties))]
    [JsonSerializable(typeof(WorkspacesProperties))]
    [JsonSerializable(typeof(ZoomItProperties))]

    // Base Property Types (used throughout settings)
    [JsonSerializable(typeof(BoolProperty))]
    [JsonSerializable(typeof(StringProperty))]
    [JsonSerializable(typeof(IntProperty))]
    [JsonSerializable(typeof(DoubleProperty))]

    // Helper and Utility Types
    [JsonSerializable(typeof(HotkeySettings))]
    [JsonSerializable(typeof(ColorFormatModel))]
    [JsonSerializable(typeof(ImageSize))]
    [JsonSerializable(typeof(KeysDataModel))]
    [JsonSerializable(typeof(EnabledModules))]
    [JsonSerializable(typeof(GeneralSettingsCustomAction))]
    [JsonSerializable(typeof(OutGoingGeneralSettings))]
    [JsonSerializable(typeof(OutGoingLanguageSettings))]
    [JsonSerializable(typeof(AdvancedPasteCustomActions))]
    [JsonSerializable(typeof(AdvancedPasteAdditionalActions))]
    [JsonSerializable(typeof(AdvancedPasteCustomAction))]
    [JsonSerializable(typeof(AdvancedPasteAdditionalAction))]
    [JsonSerializable(typeof(AdvancedPastePasteAsFileAction))]
    [JsonSerializable(typeof(AdvancedPasteTranscodeAction))]
    [JsonSerializable(typeof(PasteAIConfiguration))]
    [JsonSerializable(typeof(PasteAIProviderDefinition))]
    [JsonSerializable(typeof(ImageResizerSizes))]
    [JsonSerializable(typeof(ImageResizerCustomSizeProperty))]
    [JsonSerializable(typeof(KeyboardKeysProperty))]
    [JsonSerializable(typeof(SettingsUILibraryHelpers.SearchLocation))]

    // IPC Send Message Wrapper Classes (Snd*)
    [JsonSerializable(typeof(SndAwakeSettings))]
    [JsonSerializable(typeof(SndCursorWrapSettings))]
    [JsonSerializable(typeof(SndFindMyMouseSettings))]
    [JsonSerializable(typeof(SndLightSwitchSettings))]
    [JsonSerializable(typeof(SndMouseHighlighterSettings))]
    [JsonSerializable(typeof(SndMouseJumpSettings))]
    [JsonSerializable(typeof(SndMousePointerCrosshairsSettings))]
    [JsonSerializable(typeof(SndPowerAccentSettings))]
    [JsonSerializable(typeof(SndPowerPreviewSettings))]
    [JsonSerializable(typeof(SndPowerRenameSettings))]
    [JsonSerializable(typeof(SndShortcutGuideSettings))]

    // IPC Message Generic Wrapper Types (SndModuleSettings<T>)
    [JsonSerializable(typeof(SndModuleSettings<SndAwakeSettings>))]
    [JsonSerializable(typeof(SndModuleSettings<SndCursorWrapSettings>))]
    [JsonSerializable(typeof(SndModuleSettings<SndFindMyMouseSettings>))]
    [JsonSerializable(typeof(SndModuleSettings<SndLightSwitchSettings>))]
    [JsonSerializable(typeof(SndModuleSettings<SndMouseHighlighterSettings>))]
    [JsonSerializable(typeof(SndModuleSettings<SndMouseJumpSettings>))]
    [JsonSerializable(typeof(SndModuleSettings<SndMousePointerCrosshairsSettings>))]
    [JsonSerializable(typeof(SndModuleSettings<SndPowerAccentSettings>))]
    [JsonSerializable(typeof(SndModuleSettings<SndPowerPreviewSettings>))]
    [JsonSerializable(typeof(SndModuleSettings<SndPowerRenameSettings>))]
    [JsonSerializable(typeof(SndModuleSettings<SndShortcutGuideSettings>))]

    public partial class SettingsSerializationContext : JsonSerializerContext
    {
    }
}
