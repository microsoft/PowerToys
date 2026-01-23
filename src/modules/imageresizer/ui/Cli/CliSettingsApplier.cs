// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using ImageResizer.Models;
using ImageResizer.Properties;

namespace ImageResizer.Cli
{
    /// <summary>
    /// Applies CLI options to Settings object.
    /// Separated from executor logic for Single Responsibility Principle.
    /// </summary>
    public static class CliSettingsApplier
    {
        /// <summary>
        /// Applies CLI options to the settings, overriding default values.
        /// </summary>
        /// <param name="cliOptions">The CLI options to apply.</param>
        /// <param name="settings">The settings to modify.</param>
        public static void Apply(CliOptions cliOptions, Settings settings)
        {
            // Handle complex size options first
            ApplySizeOptions(cliOptions, settings);

            // Apply simple property mappings
            ApplySimpleOptions(cliOptions, settings);
        }

        private static void ApplySizeOptions(CliOptions cliOptions, Settings settings)
        {
            if (cliOptions.Width.HasValue || cliOptions.Height.HasValue)
            {
                ApplyCustomSizeOptions(cliOptions, settings);
            }
            else if (cliOptions.SizeIndex.HasValue)
            {
                ApplyPresetSizeOption(cliOptions, settings);
            }
        }

        private static void ApplyCustomSizeOptions(CliOptions cliOptions, Settings settings)
        {
            // Set dimensions (0 = auto-calculate for aspect ratio preservation)
            // Implementation: ResizeSize.ConvertToPixels() returns double.PositiveInfinity for 0 in Fit mode,
            // causing Math.Min(scaleX, scaleY) to preserve aspect ratio by selecting the non-zero scale.
            // For Fill/Stretch modes, 0 uses the original dimension instead.
            settings.CustomSize.Width = cliOptions.Width ?? 0;
            settings.CustomSize.Height = cliOptions.Height ?? 0;

            // Apply optional properties
            if (cliOptions.Unit.HasValue)
            {
                settings.CustomSize.Unit = cliOptions.Unit.Value;
            }

            if (cliOptions.Fit.HasValue)
            {
                settings.CustomSize.Fit = cliOptions.Fit.Value;
            }

            // Select custom size (index = Sizes.Count)
            settings.SelectedSizeIndex = settings.Sizes.Count;
        }

        private static void ApplyPresetSizeOption(CliOptions cliOptions, Settings settings)
        {
            var index = cliOptions.SizeIndex.Value;

            if (index >= 0 && index < settings.Sizes.Count)
            {
                settings.SelectedSizeIndex = index;
            }
            else
            {
                Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, Resources.CLI_WarningInvalidSizeIndex, index));
                CliLogger.Warn($"Invalid size index: {index}");
            }
        }

        private static void ApplySimpleOptions(CliOptions cliOptions, Settings settings)
        {
            if (cliOptions.ShrinkOnly.HasValue)
            {
                settings.ShrinkOnly = cliOptions.ShrinkOnly.Value;
            }

            if (cliOptions.Replace.HasValue)
            {
                settings.Replace = cliOptions.Replace.Value;
            }

            if (cliOptions.IgnoreOrientation.HasValue)
            {
                settings.IgnoreOrientation = cliOptions.IgnoreOrientation.Value;
            }

            if (cliOptions.RemoveMetadata.HasValue)
            {
                settings.RemoveMetadata = cliOptions.RemoveMetadata.Value;
            }

            if (cliOptions.JpegQualityLevel.HasValue)
            {
                settings.JpegQualityLevel = cliOptions.JpegQualityLevel.Value;
            }

            if (cliOptions.KeepDateModified.HasValue)
            {
                settings.KeepDateModified = cliOptions.KeepDateModified.Value;
            }

            if (!string.IsNullOrEmpty(cliOptions.FileName))
            {
                settings.FileName = cliOptions.FileName;
            }
        }
    }
}
