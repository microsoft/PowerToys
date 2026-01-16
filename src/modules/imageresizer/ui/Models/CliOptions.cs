// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.CommandLine.Parsing;
using System.Globalization;
using ImageResizer.Cli.Commands;

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type

namespace ImageResizer.Models
{
    /// <summary>
    /// Represents the command-line options for ImageResizer CLI mode.
    /// </summary>
    public class CliOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to show help information.
        /// </summary>
        public bool ShowHelp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show current configuration.
        /// </summary>
        public bool ShowConfig { get; set; }

        /// <summary>
        /// Gets or sets the destination directory for resized images.
        /// </summary>
        public string DestinationDirectory { get; set; }

        /// <summary>
        /// Gets or sets the width of the resized image.
        /// </summary>
        public double? Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the resized image.
        /// </summary>
        public double? Height { get; set; }

        /// <summary>
        /// Gets or sets the resize unit (Pixel, Percent, Inch, Centimeter).
        /// </summary>
        public ResizeUnit? Unit { get; set; }

        /// <summary>
        /// Gets or sets the resize fit mode (Fill, Fit, Stretch).
        /// </summary>
        public ResizeFit? Fit { get; set; }

        /// <summary>
        /// Gets or sets the index of the preset size to use.
        /// </summary>
        public int? SizeIndex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to only shrink images (not enlarge).
        /// </summary>
        public bool? ShrinkOnly { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to replace the original file.
        /// </summary>
        public bool? Replace { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to ignore orientation when resizing.
        /// </summary>
        public bool? IgnoreOrientation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to remove metadata from the resized image.
        /// </summary>
        public bool? RemoveMetadata { get; set; }

        /// <summary>
        /// Gets or sets the JPEG quality level (1-100).
        /// </summary>
        public int? JpegQualityLevel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to keep the date modified.
        /// </summary>
        public bool? KeepDateModified { get; set; }

        /// <summary>
        /// Gets or sets the output filename format.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use line-based progress output for screen reader accessibility.
        /// </summary>
        public bool? ProgressLines { get; set; }

        /// <summary>
        /// Gets the list of files to process.
        /// </summary>
        public ICollection<string> Files { get; } = new List<string>();

        /// <summary>
        /// Gets or sets the pipe name for receiving file list.
        /// </summary>
        public string PipeName { get; set; }

        /// <summary>
        /// Gets parse/validation errors produced by System.CommandLine.
        /// </summary>
        public IReadOnlyList<string> ParseErrors { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// Converts a boolean value to nullable bool (true -> true, false -> null).
        /// </summary>
        private static bool? ToBoolOrNull(bool value) => value ? true : null;

        /// <summary>
        /// Parses command-line arguments into CliOptions using System.CommandLine.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>A CliOptions instance with parsed values.</returns>
        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            var cmd = new ImageResizerRootCommand();

            // Parse using System.CommandLine
            var parseResult = new Parser(cmd).Parse(args);

            if (parseResult.Errors.Count > 0)
            {
                var errors = new List<string>(parseResult.Errors.Count);
                foreach (var error in parseResult.Errors)
                {
                    errors.Add(error.Message);
                }

                options.ParseErrors = new ReadOnlyCollection<string>(errors);
            }

            // Extract values from parse result using strongly typed options
            options.ShowHelp = parseResult.GetValueForOption(cmd.HelpOption);
            options.ShowConfig = parseResult.GetValueForOption(cmd.ShowConfigOption);
            options.DestinationDirectory = parseResult.GetValueForOption(cmd.DestinationOption);
            options.Width = parseResult.GetValueForOption(cmd.WidthOption);
            options.Height = parseResult.GetValueForOption(cmd.HeightOption);
            options.Unit = parseResult.GetValueForOption(cmd.UnitOption);
            options.Fit = parseResult.GetValueForOption(cmd.FitOption);
            options.SizeIndex = parseResult.GetValueForOption(cmd.SizeOption);

            // Convert bool to nullable bool (true -> true, false -> null)
            options.ShrinkOnly = ToBoolOrNull(parseResult.GetValueForOption(cmd.ShrinkOnlyOption));
            options.Replace = ToBoolOrNull(parseResult.GetValueForOption(cmd.ReplaceOption));
            options.IgnoreOrientation = ToBoolOrNull(parseResult.GetValueForOption(cmd.IgnoreOrientationOption));
            options.RemoveMetadata = ToBoolOrNull(parseResult.GetValueForOption(cmd.RemoveMetadataOption));
            options.KeepDateModified = ToBoolOrNull(parseResult.GetValueForOption(cmd.KeepDateModifiedOption));
            options.ProgressLines = ToBoolOrNull(parseResult.GetValueForOption(cmd.ProgressLinesOption));

            options.JpegQualityLevel = parseResult.GetValueForOption(cmd.QualityOption);

            options.FileName = parseResult.GetValueForOption(cmd.FileNameOption);

            // Get files from arguments
            var files = parseResult.GetValueForArgument(cmd.FilesArgument);
            if (files != null)
            {
                const string pipeNamePrefix = "\\\\.\\pipe\\";
                foreach (var file in files)
                {
                    // Check for pipe name (must be at the start of the path)
                    if (file.StartsWith(pipeNamePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        options.PipeName = file.Substring(pipeNamePrefix.Length);
                    }
                    else
                    {
                        options.Files.Add(file);
                    }
                }
            }

            return options;
        }

        /// <summary>
        /// Prints current configuration to the console.
        /// </summary>
        /// <param name="settings">The settings to display.</param>
        public static void PrintConfig(ImageResizer.Properties.Settings settings)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(Properties.Resources.CLI_ConfigTitle);
            Console.WriteLine();
            Console.WriteLine(Properties.Resources.CLI_ConfigGeneralSettings);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigShrinkOnly, settings.ShrinkOnly));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigReplaceOriginal, settings.Replace));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigIgnoreOrientation, settings.IgnoreOrientation));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigRemoveMetadata, settings.RemoveMetadata));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigKeepDateModified, settings.KeepDateModified));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigJpegQuality, settings.JpegQualityLevel));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigPngInterlace, settings.PngInterlaceOption));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigTiffCompress, settings.TiffCompressOption));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigFilenameFormat, settings.FileName));
            Console.WriteLine();
            Console.WriteLine(Properties.Resources.CLI_ConfigCustomSize);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigWidth, settings.CustomSize.Width, settings.CustomSize.Unit));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigHeight, settings.CustomSize.Height, settings.CustomSize.Unit));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigFitMode, settings.CustomSize.Fit));
            Console.WriteLine();
            Console.WriteLine(Properties.Resources.CLI_ConfigPresetSizes);
            for (int i = 0; i < settings.Sizes.Count; i++)
            {
                var size = settings.Sizes[i];
                var selected = i == settings.SelectedSizeIndex ? "*" : " ";
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigPresetSizeFormat, i, selected, size.Name, size.Width, size.Height, size.Unit, size.Fit));
            }

            if (settings.SelectedSizeIndex >= settings.Sizes.Count)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ConfigCustomSelected, settings.CustomSize.Width, settings.CustomSize.Height, settings.CustomSize.Unit, settings.CustomSize.Fit));
            }
        }

        /// <summary>
        /// Prints usage information to the console.
        /// </summary>
        public static void PrintUsage()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(Properties.Resources.CLI_UsageTitle);
            Console.WriteLine();

            var cmd = new ImageResizerRootCommand();

            // Print usage line
            Console.WriteLine(Properties.Resources.CLI_UsageLine);
            Console.WriteLine();

            // Print options from the command definition
            Console.WriteLine(Properties.Resources.CLI_UsageOptions);
            foreach (var option in cmd.Options)
            {
                var aliases = string.Join(", ", option.Aliases);
                var description = option.Description ?? string.Empty;
                Console.WriteLine($"  {aliases,-30} {description}");
            }

            Console.WriteLine();
            Console.WriteLine(Properties.Resources.CLI_UsageExamples);
            Console.WriteLine(Properties.Resources.CLI_UsageExampleHelp);
            Console.WriteLine(Properties.Resources.CLI_UsageExampleDimensions);
            Console.WriteLine(Properties.Resources.CLI_UsageExamplePercent);
            Console.WriteLine(Properties.Resources.CLI_UsageExamplePreset);
        }
    }
}
