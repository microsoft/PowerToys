// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditorCommon.Data;

namespace FancyZones.UITests.Utils;

/// <summary>
/// The FancyZones JSON files a test seeds or inspects, all under
/// <c>%LocalAppData%\Microsoft\PowerToys\FancyZones</c>.
/// </summary>
/// <remarks>
/// Replaces the legacy <c>FancyZonesEditorFiles</c>. The legacy type lived in
/// <c>FancyZonesEditor.UITests</c>, which references the WinAppDriver harness; this one depends only
/// on <c>FancyZonesEditorCommon</c>. <c>app-zone-history.json</c> is read as raw JSON (see
/// <see cref="ZoneHistory"/>) rather than through the legacy project's <c>AppZoneHistory</c> wrapper.
/// </remarks>
public sealed class FancyZonesFiles
{
    public FancyZonesFiles()
    {
        Parameters = new JsonFile(new EditorParameters().File);
        AppliedLayouts = new JsonFile(new AppliedLayouts().File);
        CustomLayouts = new JsonFile(new CustomLayouts().File);
        DefaultLayouts = new JsonFile(new DefaultLayouts().File);
        LayoutHotkeys = new JsonFile(new LayoutHotkeys().File);
        LayoutTemplates = new JsonFile(new LayoutTemplates().File);
        AppZoneHistory = new JsonFile(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "FancyZones",
            "app-zone-history.json"));
    }

    public JsonFile Parameters { get; }

    public JsonFile AppliedLayouts { get; }

    public JsonFile CustomLayouts { get; }

    public JsonFile DefaultLayouts { get; }

    public JsonFile LayoutHotkeys { get; }

    public JsonFile LayoutTemplates { get; }

    public JsonFile AppZoneHistory { get; }

    public void RestoreAll()
    {
        Parameters.Restore();
        AppliedLayouts.Restore();
        CustomLayouts.Restore();
        DefaultLayouts.Restore();
        LayoutHotkeys.Restore();
        LayoutTemplates.Restore();
        AppZoneHistory.Restore();
    }
}
