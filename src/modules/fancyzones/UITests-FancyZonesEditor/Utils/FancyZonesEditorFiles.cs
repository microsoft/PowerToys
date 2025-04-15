// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditorCommon.Data;

namespace Microsoft.FancyZonesEditor.UITests.Utils
{
    public class FancyZonesEditorFiles
    {
        public IOTestHelper ParamsIOHelper { get; }

        public IOTestHelper AppliedLayoutsIOHelper { get; }

        public IOTestHelper CustomLayoutsIOHelper { get; }

        public IOTestHelper DefaultLayoutsIOHelper { get; }

        public IOTestHelper LayoutHotkeysIOHelper { get; }

        public IOTestHelper LayoutTemplatesIOHelper { get; }

        public FancyZonesEditorFiles()
        {
            ParamsIOHelper = new IOTestHelper(new EditorParameters().File);
            AppliedLayoutsIOHelper = new IOTestHelper(new AppliedLayouts().File);
            CustomLayoutsIOHelper = new IOTestHelper(new CustomLayouts().File);
            DefaultLayoutsIOHelper = new IOTestHelper(new DefaultLayouts().File);
            LayoutHotkeysIOHelper = new IOTestHelper(new LayoutHotkeys().File);
            LayoutTemplatesIOHelper = new IOTestHelper(new LayoutTemplates().File);
        }

        public void Restore()
        {
            ParamsIOHelper.RestoreData();
            AppliedLayoutsIOHelper.RestoreData();
            CustomLayoutsIOHelper.RestoreData();
            DefaultLayoutsIOHelper.RestoreData();
            LayoutHotkeysIOHelper.RestoreData();
            LayoutTemplatesIOHelper.RestoreData();
        }
    }
}
