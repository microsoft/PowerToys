// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Linq;
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

        public IReadOnlyList<string> ParseErrors { get; private set; } = [];

        private static bool? ToBoolOrNull(bool value) => value ? true : null;

        public static CliOptions Parse(string[] args)
            => ParseCore(args, rejectOptionLikeFiles: false);

        internal static CliOptions ParseForCli(string[] args)
            => ParseCore(args, rejectOptionLikeFiles: true);

        private static CliOptions ParseCore(string[] args, bool rejectOptionLikeFiles)
        {
            var options = new CliOptions();
            var cmd = new ImageResizerRootCommand();

            var parseResult = new Parser(cmd).Parse(args);
            var errors = new List<string>(parseResult.Errors.Count + 1);

            foreach (var error in parseResult.Errors)
            {
                errors.Add(error.Message);
            }

            var files = parseResult.GetValueForArgument(cmd.FilesArgument);
            PopulateInputs(options, files);
            PopulateOptionValues(options, cmd, parseResult);

            if (errors.Count > 0)
            {
                options.ParseErrors = new ReadOnlyCollection<string>(errors);
                return options;
            }

            if ((options.Width.HasValue || options.Height.HasValue) &&
                (options.Width ?? 0) == 0 &&
                (options.Height ?? 0) == 0)
            {
                errors.Add(Properties.Resources.CLI_ErrorZeroDimensions);
                options.ParseErrors = new ReadOnlyCollection<string>(errors);
            }

            if (rejectOptionLikeFiles)
            {
                var validationArgs = ExpandTokensForValidation(args);
                AddOptionLikeFileErrors(validationArgs, parseResult, cmd.Options, options.Files, errors);
                if (errors.Count > 0)
                {
                    options.ParseErrors = new ReadOnlyCollection<string>(errors);
                }
            }

            return options;
        }

        private static void PopulateOptionValues(CliOptions options, ImageResizerRootCommand command, ParseResult parseResult)
        {
            options.ShowHelp = GetValidOptionValue(parseResult, command.HelpOption);
            options.ShowConfig = GetValidOptionValue(parseResult, command.ShowConfigOption);
            options.DestinationDirectory = GetValidOptionValue(parseResult, command.DestinationOption);
            options.Width = GetValidOptionValue(parseResult, command.WidthOption);
            options.Height = GetValidOptionValue(parseResult, command.HeightOption);
            options.Unit = GetValidOptionValue(parseResult, command.UnitOption);
            options.Fit = GetValidOptionValue(parseResult, command.FitOption);
            options.SizeIndex = GetValidOptionValue(parseResult, command.SizeOption);

            options.ShrinkOnly = ToBoolOrNull(GetValidOptionValue(parseResult, command.ShrinkOnlyOption));
            options.Replace = ToBoolOrNull(GetValidOptionValue(parseResult, command.ReplaceOption));
            options.IgnoreOrientation = ToBoolOrNull(GetValidOptionValue(parseResult, command.IgnoreOrientationOption));
            options.RemoveMetadata = ToBoolOrNull(GetValidOptionValue(parseResult, command.RemoveMetadataOption));
            options.KeepDateModified = ToBoolOrNull(GetValidOptionValue(parseResult, command.KeepDateModifiedOption));
            options.ProgressLines = ToBoolOrNull(GetValidOptionValue(parseResult, command.ProgressLinesOption));

            options.JpegQualityLevel = GetValidOptionValue(parseResult, command.QualityOption);
            options.FileName = GetValidOptionValue(parseResult, command.FileNameOption);
        }

        private static T GetValidOptionValue<T>(ParseResult parseResult, System.CommandLine.Option<T> option)
        {
            var optionResult = parseResult.FindResultFor(option);
            if (optionResult != null && !string.IsNullOrEmpty(optionResult.ErrorMessage))
            {
                return default;
            }

            try
            {
                return parseResult.GetValueForOption(option);
            }
            catch (InvalidOperationException)
            {
                return default;
            }
        }

        private static void AddOptionLikeFileErrors(
            IReadOnlyList<string> args,
            ParseResult parseResult,
            IReadOnlyList<System.CommandLine.Option> options,
            ICollection<string> files,
            ICollection<string> errors)
        {
            var escapedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var afterEndOfOptions = false;
            foreach (var token in parseResult.Tokens)
            {
                if (!afterEndOfOptions && token.Type == TokenType.DoubleDash)
                {
                    afterEndOfOptions = true;
                    continue;
                }

                if (afterEndOfOptions && LooksLikeOption(token.Value))
                {
                    escapedCounts.TryGetValue(token.Value, out var count);
                    escapedCounts[token.Value] = count + 1;
                }
            }

            var optionLikeErrors = new List<string>();
            foreach (var file in files.Reverse())
            {
                if (!LooksLikeOption(file))
                {
                    continue;
                }

                if (escapedCounts.TryGetValue(file, out var count) && count > 0)
                {
                    escapedCounts[file] = count - 1;
                    continue;
                }

                optionLikeErrors.Add(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ErrorUnknownOption, file));
            }

            optionLikeErrors.Reverse();
            foreach (var error in optionLikeErrors)
            {
                errors.Add(error);
            }

            AddInvalidBundleErrors(args, options, errors);
        }

        private static bool LooksLikeOption(string value)
            => value?.Length > 1 && value[0] == '-';

        private static void AddInvalidBundleErrors(
            IReadOnlyList<string> args,
            IReadOnlyList<System.CommandLine.Option> options,
            ICollection<string> errors)
        {
            var aliasMap = options
                .SelectMany(option => option.Aliases.Select(alias => (Alias: alias, Option: option)))
                .ToDictionary(item => item.Alias, item => item.Option, StringComparer.Ordinal);
            var shortOptions = aliasMap
                .Where(item => item.Key.Length == 2 && item.Key[0] == '-' && item.Key[1] != '-')
                .ToDictionary(item => item.Key[1], item => item.Value);

            for (var argumentIndex = 0; argumentIndex < args.Count; argumentIndex++)
            {
                var arg = args[argumentIndex];
                if (arg == "--")
                {
                    break;
                }

                if (aliasMap.TryGetValue(arg, out var exactOption))
                {
                    if (exactOption.ValueType != typeof(bool) && argumentIndex + 1 < args.Count)
                    {
                        argumentIndex++;
                    }

                    continue;
                }

                var handledSeparatedOption = false;
                foreach (var (alias, option) in aliasMap)
                {
                    if (arg.Length > alias.Length &&
                        arg.StartsWith(alias, StringComparison.Ordinal) &&
                        (arg[alias.Length] == '=' || arg[alias.Length] == ':'))
                    {
                        var hasAttachedValue = arg.Length > alias.Length + 1;
                        if (option.ValueType != typeof(bool) && !hasAttachedValue && argumentIndex + 1 < args.Count)
                        {
                            argumentIndex++;
                        }

                        handledSeparatedOption = true;
                        break;
                    }
                }

                if (handledSeparatedOption)
                {
                    continue;
                }

                if (arg.StartsWith("--", StringComparison.Ordinal) || arg.StartsWith('/'))
                {
                    continue;
                }

                if (arg.Length <= 2 || arg[0] != '-' || arg[1] == '-')
                {
                    continue;
                }

                var consumedFlag = false;
                for (var index = 1; index < arg.Length; index++)
                {
                    if (!shortOptions.TryGetValue(arg[index], out var option))
                    {
                        if (consumedFlag)
                        {
                            errors.Add(string.Format(CultureInfo.InvariantCulture, Properties.Resources.CLI_ErrorUnknownOption, arg));
                        }

                        break;
                    }

                    if (option.ValueType != typeof(bool))
                    {
                        var attachedValue = arg.Substring(index + 1);
                        if (attachedValue.Length == 0 && argumentIndex + 1 < args.Count)
                        {
                            argumentIndex++;
                        }

                        // The remainder, when present, is the attached value for this option.
                        break;
                    }

                    consumedFlag = true;
                    if (bool.TryParse(arg.AsSpan(index + 1), out _))
                    {
                        // System.CommandLine accepts an explicit boolean value attached to a
                        // short option (for example, -rtrue). The value consumes the remainder.
                        break;
                    }
                }
            }
        }

        private static IReadOnlyList<string> ExpandTokensForValidation(string[] args)
        {
            var tokenizerCommand = new System.CommandLine.RootCommand();
            var tokenizerArgument = new System.CommandLine.Argument<string[]>("tokens")
            {
                Arity = System.CommandLine.ArgumentArity.ZeroOrMore,
            };
            tokenizerCommand.AddArgument(tokenizerArgument);

            return new Parser(tokenizerCommand)
                .Parse(args)
                .Tokens
                .Select(token => token.Type == TokenType.DoubleDash ? "--" : token.Value)
                .ToList();
        }

        private static void PopulateInputs(CliOptions options, string[] files)
        {
            if (files == null)
            {
                return;
            }

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

        public static void PrintConfig(ImageResizer.Properties.Settings settings)
        {
            var getString = ResourceLoaderInstance.GetString;
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(getString("CLI_ConfigTitle"));
            Console.WriteLine();
            Console.WriteLine(getString("CLI_ConfigGeneralSettings"));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigShrinkOnly"), settings.ShrinkOnly));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigReplaceOriginal"), settings.Replace));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigIgnoreOrientation"), settings.IgnoreOrientation));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigRemoveMetadata"), settings.RemoveMetadata));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigKeepDateModified"), settings.KeepDateModified));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigJpegQuality"), settings.JpegQualityLevel));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigPngInterlace"), settings.PngInterlaceOption));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigTiffCompress"), settings.TiffCompressOption));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigFilenameFormat"), settings.FileName));
            Console.WriteLine();
            Console.WriteLine(getString("CLI_ConfigCustomSize"));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigWidth"), settings.CustomSize.Width, settings.CustomSize.Unit));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigHeight"), settings.CustomSize.Height, settings.CustomSize.Unit));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigFitMode"), settings.CustomSize.Fit));
            Console.WriteLine();
            Console.WriteLine(getString("CLI_ConfigPresetSizes"));
            for (int i = 0; i < settings.Sizes.Count; i++)
            {
                var size = settings.Sizes[i];
                var selected = i == settings.SelectedSizeIndex ? "*" : " ";
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigPresetSizeFormat"), i, selected, size.Name, size.Width, size.Height, size.Unit, size.Fit));
            }

            if (settings.SelectedSizeIndex >= settings.Sizes.Count)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, getString("CLI_ConfigCustomSelected"), settings.CustomSize.Width, settings.CustomSize.Height, settings.CustomSize.Unit, settings.CustomSize.Fit));
            }
        }

        public static void PrintUsage()
        {
            var getString = ResourceLoaderInstance.GetString;
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(getString("CLI_UsageTitle"));
            Console.WriteLine();

            var cmd = new ImageResizerRootCommand();

            Console.WriteLine(getString("CLI_UsageLine"));
            Console.WriteLine();

            Console.WriteLine(getString("CLI_UsageOptions"));
            foreach (var option in cmd.Options)
            {
                var aliases = string.Join(", ", option.Aliases);
                var description = option.Description ?? string.Empty;
                Console.WriteLine($"  {aliases,-30} {description}");
            }

            Console.WriteLine();
            Console.WriteLine(getString("CLI_UsageExamples"));
            Console.WriteLine(getString("CLI_UsageExampleHelp"));
            Console.WriteLine(getString("CLI_UsageExampleDimensions"));
            Console.WriteLine(getString("CLI_UsageExamplePercent"));
            Console.WriteLine(getString("CLI_UsageExamplePreset"));
        }
    }
}
