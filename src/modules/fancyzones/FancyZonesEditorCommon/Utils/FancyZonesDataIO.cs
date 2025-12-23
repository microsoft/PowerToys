// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using FancyZonesEditorCommon.Data;

namespace FancyZonesEditorCommon.Utils
{
    /// <summary>
    /// Unified helper for all FancyZones data file I/O operations.
    /// Centralizes reading and writing of all JSON configuration files.
    /// </summary>
    public static class FancyZonesDataIO
    {
        private static TWrapper ReadData<TData, TWrapper>(
            Func<TData> createInstance,
            Func<TData, string> fileSelector,
            Func<TData, string, TWrapper> readFunc)
        {
            var instance = createInstance();
            string filePath = fileSelector(instance);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {Path.GetFileName(filePath)}", filePath);
            }

            return readFunc(instance, filePath);
        }

        private static void WriteData<TData, TWrapper>(
            Func<TData> createInstance,
            Func<TData, string> fileSelector,
            Func<TData, TWrapper, string> serializeFunc,
            TWrapper data)
        {
            var instance = createInstance();
            var filePath = fileSelector(instance);

            IOUtils ioUtils = new IOUtils();
            ioUtils.WriteFile(filePath, serializeFunc(instance, data));
        }

        // AppliedLayouts operations
        public static AppliedLayouts.AppliedLayoutsListWrapper ReadAppliedLayouts()
        {
            return ReadData(
                () => new AppliedLayouts(),
                instance => instance.File,
                (instance, file) => instance.Read(file));
        }

        public static void WriteAppliedLayouts(AppliedLayouts.AppliedLayoutsListWrapper data)
        {
            WriteData(
                () => new AppliedLayouts(),
                instance => instance.File,
                (instance, wrapper) => instance.Serialize(wrapper),
                data);
        }

        // CustomLayouts operations
        public static CustomLayouts.CustomLayoutListWrapper ReadCustomLayouts()
        {
            return ReadData(
                () => new CustomLayouts(),
                instance => instance.File,
                (instance, file) => instance.Read(file));
        }

        public static void WriteCustomLayouts(CustomLayouts.CustomLayoutListWrapper data)
        {
            WriteData(
                () => new CustomLayouts(),
                instance => instance.File,
                (instance, wrapper) => instance.Serialize(wrapper),
                data);
        }

        // LayoutTemplates operations
        public static LayoutTemplates.TemplateLayoutsListWrapper ReadLayoutTemplates()
        {
            return ReadData(
                () => new LayoutTemplates(),
                instance => instance.File,
                (instance, file) => instance.Read(file));
        }

        public static void WriteLayoutTemplates(LayoutTemplates.TemplateLayoutsListWrapper data)
        {
            WriteData(
                () => new LayoutTemplates(),
                instance => instance.File,
                (instance, wrapper) => instance.Serialize(wrapper),
                data);
        }

        // LayoutHotkeys operations
        public static LayoutHotkeys.LayoutHotkeysWrapper ReadLayoutHotkeys()
        {
            return ReadData(
                () => new LayoutHotkeys(),
                instance => instance.File,
                (instance, file) => instance.Read(file));
        }

        public static void WriteLayoutHotkeys(LayoutHotkeys.LayoutHotkeysWrapper data)
        {
            WriteData(
                () => new LayoutHotkeys(),
                instance => instance.File,
                (instance, wrapper) => instance.Serialize(wrapper),
                data);
        }

        // EditorParameters operations
        public static EditorParameters.ParamsWrapper ReadEditorParameters()
        {
            return ReadData(
                () => new EditorParameters(),
                instance => instance.File,
                (instance, file) => instance.Read(file));
        }

        public static void WriteEditorParameters(EditorParameters.ParamsWrapper data)
        {
            WriteData(
                () => new EditorParameters(),
                instance => instance.File,
                (instance, wrapper) => instance.Serialize(wrapper),
                data);
        }

        // DefaultLayouts operations
        public static DefaultLayouts.DefaultLayoutsListWrapper ReadDefaultLayouts()
        {
            return ReadData(
                () => new DefaultLayouts(),
                instance => instance.File,
                (instance, file) => instance.Read(file));
        }

        public static void WriteDefaultLayouts(DefaultLayouts.DefaultLayoutsListWrapper data)
        {
            WriteData(
                () => new DefaultLayouts(),
                instance => instance.File,
                (instance, wrapper) => instance.Serialize(wrapper),
                data);
        }
    }
}
