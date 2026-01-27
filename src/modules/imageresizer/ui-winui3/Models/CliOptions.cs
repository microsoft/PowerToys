// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.CommandLine.Parsing;
using System.Globalization;
using ImageResizer.Cli.Commands;
using ImageResizer.Helpers;

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type

namespace ImageResizer.Models
{
    /// <summary>
    /// Represents the command-line options for ImageResizer CLI mode.
    /// </summary>
    public class CliOptions
    {
        public bool ShowHelp { get; set; }

        public bool ShowConfig { get; set; }

        public string DestinationDirectory { get; set; }

        public double? Width { get; set; }

        public double? Height { get; set; }

        public ResizeUnit? Unit { get; set; }

        public ResizeFit? Fit { get; set; }

        public int? SizeIndex { get; set; }

        public bool? ShrinkOnly { get; set; }

        public bool? Replace { get; set; }

        public bool? IgnoreOrientation { get; set; }

        public bool? RemoveMetadata { get; set; }

        public int? JpegQualityLevel { get; set; }

        public bool? KeepDateModified { get; set; }

        public string FileName { get; set; }

        public bool? ProgressLines { get; set; }

        public ICollection<string> Files { get; } = new List<string>();

        public string PipeName { get; set; }

        public IReadOnlyList<string> ParseErrors { get; private set; } = Array.Empty<string>();

        private static bool? ToBoolOrNull(bool value) => value ? true : null;

        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            var cmd = new ImageResizerRootCommand();

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

            options.ShowHelp = parseResult.GetValueForOption(cmd.HelpOption);
            options.ShowConfig = parseResult.GetValueForOption(cmd.ShowConfigOption);
            options.DestinationDirectory = parseResult.GetValueForOption(cmd.DestinationOption);
            options.Width = parseResult.GetValueForOption(cmd.WidthOption);
            options.Height = parseResult.GetValueForOption(cmd.HeightOption);
            options.Unit = parseResult.GetValueForOption(cmd.UnitOption);
            options.Fit = parseResult.GetValueForOption(cmd.FitOption);
            options.SizeIndex = parseResult.GetValueForOption(cmd.SizeOption);

            options.ShrinkOnly = ToBoolOrNull(parseResult.GetValueForOption(cmd.ShrinkOnlyOption));
            options.Replace = ToBoolOrNull(parseResult.GetValueForOption(cmd.ReplaceOption));
            options.IgnoreOrientation = ToBoolOrNull(parseResult.GetValueForOption(cmd.IgnoreOrientationOption));
            options.RemoveMetadata = ToBoolOrNull(parseResult.GetValueForOption(cmd.RemoveMetadataOption));
            options.KeepDateModified = ToBoolOrNull(parseResult.GetValueForOption(cmd.KeepDateModifiedOption));
            options.ProgressLines = ToBoolOrNull(parseResult.GetValueForOption(cmd.ProgressLinesOption));

            options.JpegQualityLevel = parseResult.GetValueForOption(cmd.QualityOption);

            options.FileName = parseResult.GetValueForOption(cmd.FileNameOption);

            var files = parseResult.GetValueForArgument(cmd.FilesArgument);
            if (files != null)
            {
                const string pipeNamePrefix = "\\\\.\\pipe\\";
                foreach (var file in files)
                {
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

        public static void PrintConfig(ImageResizer.Properties.Settings settings)
        {
            var loader = ResourceLoaderInstance.ResourceLoader;
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(loader.GetString("CLI_ConfigTitle"));
            Console.WriteLine();
            Console.WriteLine(loader.GetString("CLI_ConfigGeneralSettings"));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigShrinkOnly"), settings.ShrinkOnly));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigReplaceOriginal"), settings.Replace));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigIgnoreOrientation"), settings.IgnoreOrientation));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigRemoveMetadata"), settings.RemoveMetadata));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigKeepDateModified"), settings.KeepDateModified));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigJpegQuality"), settings.JpegQualityLevel));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigPngInterlace"), settings.PngInterlaceOption));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigTiffCompress"), settings.TiffCompressOption));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigFilenameFormat"), settings.FileName));
            Console.WriteLine();
            Console.WriteLine(loader.GetString("CLI_ConfigCustomSize"));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigWidth"), settings.CustomSize.Width, settings.CustomSize.Unit));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigHeight"), settings.CustomSize.Height, settings.CustomSize.Unit));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigFitMode"), settings.CustomSize.Fit));
            Console.WriteLine();
            Console.WriteLine(loader.GetString("CLI_ConfigPresetSizes"));
            for (int i = 0; i < settings.Sizes.Count; i++)
            {
                var size = settings.Sizes[i];
                var selected = i == settings.SelectedSizeIndex ? "*" : " ";
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigPresetSizeFormat"), i, selected, size.Name, size.Width, size.Height, size.Unit, size.Fit));
            }

            if (settings.SelectedSizeIndex >= settings.Sizes.Count)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, loader.GetString("CLI_ConfigCustomSelected"), settings.CustomSize.Width, settings.CustomSize.Height, settings.CustomSize.Unit, settings.CustomSize.Fit));
            }
        }

        public static void PrintUsage()
        {
            var loader = ResourceLoaderInstance.ResourceLoader;
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(loader.GetString("CLI_UsageTitle"));
            Console.WriteLine();

            var cmd = new ImageResizerRootCommand();

            Console.WriteLine(loader.GetString("CLI_UsageLine"));
            Console.WriteLine();

            Console.WriteLine(loader.GetString("CLI_UsageOptions"));
            foreach (var option in cmd.Options)
            {
                var aliases = string.Join(", ", option.Aliases);
                var description = option.Description ?? string.Empty;
                Console.WriteLine($"  {aliases,-30} {description}");
            }

            Console.WriteLine();
            Console.WriteLine(loader.GetString("CLI_UsageExamples"));
            Console.WriteLine(loader.GetString("CLI_UsageExampleHelp"));
            Console.WriteLine(loader.GetString("CLI_UsageExampleDimensions"));
            Console.WriteLine(loader.GetString("CLI_UsageExamplePercent"));
            Console.WriteLine(loader.GetString("CLI_UsageExamplePreset"));
        }
    }
}
