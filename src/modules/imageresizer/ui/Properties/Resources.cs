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
        public static string Small => ResourceLoaderInstance.ResourceLoader.GetString("Small");

        public static string Medium => ResourceLoaderInstance.ResourceLoader.GetString("Medium");

        public static string Large => ResourceLoaderInstance.ResourceLoader.GetString("Large");

        public static string Phone => ResourceLoaderInstance.ResourceLoader.GetString("Phone");

        // Input page resources
        public static string Input_Custom => ResourceLoaderInstance.ResourceLoader.GetString("Input_Custom");

        // Validation messages
        public static string ValueMustBeBetween => ResourceLoaderInstance.ResourceLoader.GetString("ValueMustBeBetween");

        // CLI options
        public static string CLI_Option_Destination => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Destination");

        public static string CLI_Option_FileName => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_FileName");

        public static string CLI_Option_Files => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Files");

        public static string CLI_Option_Fit => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Fit");

        public static string CLI_Option_Height => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Height");

        public static string CLI_Option_Help => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Help");

        public static string CLI_Option_IgnoreOrientation => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_IgnoreOrientation");

        public static string CLI_Option_KeepDateModified => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_KeepDateModified");

        public static string CLI_Option_Quality => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Quality");

        public static string CLI_Option_Replace => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Replace");

        public static string CLI_Option_ShowConfig => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_ShowConfig");

        public static string CLI_Option_ShrinkOnly => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_ShrinkOnly");

        public static string CLI_Option_RemoveMetadata => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_RemoveMetadata");

        public static string CLI_Option_Size => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Size");

        public static string CLI_Option_Unit => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Unit");

        public static string CLI_Option_Width => ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Width");

        public static string CLI_ProcessingFiles => ResourceLoaderInstance.ResourceLoader.GetString("CLI_ProcessingFiles");

        public static string CLI_ProgressFormat => ResourceLoaderInstance.ResourceLoader.GetString("CLI_ProgressFormat");

        public static string CLI_CompletedWithErrors => ResourceLoaderInstance.ResourceLoader.GetString("CLI_CompletedWithErrors");

        public static string CLI_AllFilesProcessed => ResourceLoaderInstance.ResourceLoader.GetString("CLI_AllFilesProcessed");

        public static string CLI_WarningInvalidSizeIndex => ResourceLoaderInstance.ResourceLoader.GetString("CLI_WarningInvalidSizeIndex");

        public static string CLI_NoInputFiles => ResourceLoaderInstance.ResourceLoader.GetString("CLI_NoInputFiles");
    }
}
