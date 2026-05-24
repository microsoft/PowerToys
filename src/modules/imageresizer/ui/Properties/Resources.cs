// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    }
}
