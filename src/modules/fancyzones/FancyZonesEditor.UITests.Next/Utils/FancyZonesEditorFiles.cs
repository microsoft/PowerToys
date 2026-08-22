// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditorCommon.Data;

namespace FancyZonesEditor.UITests.Utils;

public sealed class FancyZonesEditorFiles
{
    public FancyZonesEditorFiles()
    {
        Parameters = new JsonFile(new EditorParameters().File);
        AppliedLayouts = new JsonFile(new AppliedLayouts().File);
        CustomLayouts = new JsonFile(new CustomLayouts().File);
        DefaultLayouts = new JsonFile(new DefaultLayouts().File);
        LayoutHotkeys = new JsonFile(new LayoutHotkeys().File);
        LayoutTemplates = new JsonFile(new LayoutTemplates().File);
    }

    public JsonFile Parameters { get; }

    public JsonFile AppliedLayouts { get; }

    public JsonFile CustomLayouts { get; }

    public JsonFile DefaultLayouts { get; }

    public JsonFile LayoutHotkeys { get; }

    public JsonFile LayoutTemplates { get; }

    public void RestageAll()
    {
        Parameters.Restage();
        AppliedLayouts.Restage();
        CustomLayouts.Restage();
        DefaultLayouts.Restage();
        LayoutHotkeys.Restage();
        LayoutTemplates.Restage();
    }

    public void RestoreAll()
    {
        Parameters.Restore();
        AppliedLayouts.Restore();
        CustomLayouts.Restore();
        DefaultLayouts.Restore();
        LayoutHotkeys.Restore();
        LayoutTemplates.Restore();
    }
}