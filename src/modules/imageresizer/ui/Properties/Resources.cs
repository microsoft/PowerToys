// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ImageResizer.Helpers;

namespace ImageResizer.Properties
{
    /// <summary>
    /// Resource accessor class for compatibility with CLI code and tests.
    /// Wraps ResourceLoader for resource string access.
    /// </summary>
    internal static class Resources
    {
        // Size names (used by tests and ResizeSize token replacement)
        public static string Small => ResourceLoaderInstance.GetString("Small");

        public static string Medium => ResourceLoaderInstance.GetString("Medium");

        public static string Large => ResourceLoaderInstance.GetString("Large");

        public static string Phone => ResourceLoaderInstance.GetString("Phone");

        // Input page resources
        public static string Input_Custom => ResourceLoaderInstance.GetString("Input_Custom");

        // Validation messages
        public static string ValueMustBeBetween => ResourceLoaderInstance.GetString("ValueMustBeBetween");

        // CLI options
        public static string CLI_Option_Destination => ResourceLoaderInstance.GetString("CLI_Option_Destination");

        public static string CLI_Option_FileName => ResourceLoaderInstance.GetString("CLI_Option_FileName");

        public static string CLI_Option_Files => ResourceLoaderInstance.GetString("CLI_Option_Files");

        public static string CLI_Option_Fit => ResourceLoaderInstance.GetString("CLI_Option_Fit");

        public static string CLI_Option_Height => ResourceLoaderInstance.GetString("CLI_Option_Height");

        public static string CLI_Option_Help => ResourceLoaderInstance.GetString("CLI_Option_Help");

        public static string CLI_Option_IgnoreOrientation => ResourceLoaderInstance.GetString("CLI_Option_IgnoreOrientation");

        public static string CLI_Option_KeepDateModified => ResourceLoaderInstance.GetString("CLI_Option_KeepDateModified");

        public static string CLI_Option_Quality => ResourceLoaderInstance.GetString("CLI_Option_Quality");

        public static string CLI_Option_Replace => ResourceLoaderInstance.GetString("CLI_Option_Replace");

        public static string CLI_Option_ShowConfig => ResourceLoaderInstance.GetString("CLI_Option_ShowConfig");

        public static string CLI_Option_ShrinkOnly => ResourceLoaderInstance.GetString("CLI_Option_ShrinkOnly");

        public static string CLI_Option_RemoveMetadata => ResourceLoaderInstance.GetString("CLI_Option_RemoveMetadata");

        public static string CLI_Option_Size => ResourceLoaderInstance.GetString("CLI_Option_Size");

        public static string CLI_Option_Unit => ResourceLoaderInstance.GetString("CLI_Option_Unit");

        public static string CLI_Option_Width => ResourceLoaderInstance.GetString("CLI_Option_Width");

        public static string CLI_ProcessingFiles => ResourceLoaderInstance.GetString("CLI_ProcessingFiles");

        public static string CLI_ProgressFormat => ResourceLoaderInstance.GetString("CLI_ProgressFormat");

        public static string CLI_CompletedWithErrors => ResourceLoaderInstance.GetString("CLI_CompletedWithErrors");

        public static string CLI_AllFilesProcessed => ResourceLoaderInstance.GetString("CLI_AllFilesProcessed");

        public static string CLI_WarningInvalidSizeIndex => ResourceLoaderInstance.GetString("CLI_WarningInvalidSizeIndex");

        public static string CLI_NoInputFiles => ResourceLoaderInstance.GetString("CLI_NoInputFiles");

        public static string CLI_ErrorUnknownOption => GetStringOrDefault(
            "CLI_ErrorUnknownOption",
            "Unrecognized option '{0}'. Use '--' before a file name that starts with '-', or prefix it with '.\\'.");

        public static string CLI_ErrorInvalidDimension => GetStringOrDefault(
            "CLI_ErrorInvalidDimension",
            "Width and height must be finite numbers greater than or equal to zero.");

        public static string CLI_ErrorZeroDimensions => GetStringOrDefault(
            "CLI_ErrorZeroDimensions",
            "Width and height cannot both be zero for a custom size.");

        public static string CLI_ErrorPercentWidthRequired => GetStringOrDefault(
            "CLI_ErrorPercentWidthRequired",
            "A positive width is required for percentage-based Fit and Fill sizes.");

        public static string CLI_ErrorSizeIndexOutOfRange => GetStringOrDefault(
            "CLI_ErrorSizeIndexOutOfRange",
            "Size index {0} is out of range. The maximum valid index is {1}.");

        public static string Error_DimensionOutOfRange => GetStringOrDefault(
            "Error_DimensionOutOfRange",
            "The requested output dimensions are outside the supported range.");

        public static string CLI_ErrorFileNotFound => GetStringOrDefault("CLI_ErrorFileNotFound", "Input file not found.");

        public static string CLI_ErrorUnsupportedFileType => GetStringOrDefault("CLI_ErrorUnsupportedFileType", "Unsupported image file type.");

        public static string CLI_ErrorInvalidInputPath => GetStringOrDefault("CLI_ErrorInvalidInputPath", "Invalid input path: {0}");

        public static string CLI_ErrorWildcardInDirectory => GetStringOrDefault(
            "CLI_ErrorWildcardInDirectory",
            "Wildcards are supported only in the file name portion of a path.");

        public static string CLI_ErrorNoWildcardMatches => GetStringOrDefault(
            "CLI_ErrorNoWildcardMatches",
            "No files matched the wildcard pattern.");

        public static string CLI_ErrorProcessingFallback => GetStringOrDefault(
            "CLI_ErrorProcessingFallback",
            "Image processing failed with {0} (HRESULT 0x{1:X8}).");

        public static string CLI_WarningShrinkOnlyPercent => GetStringOrDefault(
            "CLI_WarningShrinkOnlyPercent",
            "Warning: Shrink-only is ignored for percentage-based sizes.");

        private static string GetStringOrDefault(string key, string defaultValue)
        {
            var value = ResourceLoaderInstance.GetString(key);
            return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal)
                ? defaultValue
                : value;
        }
    }
}
