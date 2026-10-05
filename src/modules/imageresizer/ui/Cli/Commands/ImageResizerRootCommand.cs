// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

using ImageResizer.Cli.Options;

namespace ImageResizer.Cli.Commands
{
    /// <summary>
    /// Root command for the ImageResizer CLI.
    /// </summary>
    public sealed class ImageResizerRootCommand : RootCommand
    {
        public ImageResizerRootCommand()
            : base("PowerToys Image Resizer - Resize images from command line")
        {
            // This CLI has its own help option and uses -h for --height, so drop RootCommand's built-ins.
            Options.Clear();

            HelpOption = new HelpOption();
            ShowConfigOption = new ShowConfigOption();
            DestinationOption = new DestinationOption();
            WidthOption = new WidthOption();
            HeightOption = new HeightOption();
            UnitOption = new UnitOption();
            FitOption = new FitOption();
            SizeOption = new SizeOption();
            ShrinkOnlyOption = new ShrinkOnlyOption();
            ReplaceOption = new ReplaceOption();
            IgnoreOrientationOption = new IgnoreOrientationOption();
            RemoveMetadataOption = new RemoveMetadataOption();
            QualityOption = new QualityOption();
            KeepDateModifiedOption = new KeepDateModifiedOption();
            FileNameOption = new FileNameOption();
            ProgressLinesOption = new ProgressLinesOption();
            FilesArgument = new FilesArgument();

            Options.Add(HelpOption);
            Options.Add(ShowConfigOption);
            Options.Add(DestinationOption);
            Options.Add(WidthOption);
            Options.Add(HeightOption);
            Options.Add(UnitOption);
            Options.Add(FitOption);
            Options.Add(SizeOption);
            Options.Add(ShrinkOnlyOption);
            Options.Add(ReplaceOption);
            Options.Add(IgnoreOrientationOption);
            Options.Add(RemoveMetadataOption);
            Options.Add(QualityOption);
            Options.Add(KeepDateModifiedOption);
            Options.Add(FileNameOption);
            Options.Add(ProgressLinesOption);
            Arguments.Add(FilesArgument);
        }

        public HelpOption HelpOption { get; }

        public ShowConfigOption ShowConfigOption { get; }

        public DestinationOption DestinationOption { get; }

        public WidthOption WidthOption { get; }

        public HeightOption HeightOption { get; }

        public UnitOption UnitOption { get; }

        public FitOption FitOption { get; }

        public SizeOption SizeOption { get; }

        public ShrinkOnlyOption ShrinkOnlyOption { get; }

        public ReplaceOption ReplaceOption { get; }

        public IgnoreOrientationOption IgnoreOrientationOption { get; }

        public RemoveMetadataOption RemoveMetadataOption { get; }

        public QualityOption QualityOption { get; }

        public KeepDateModifiedOption KeepDateModifiedOption { get; }

        public FileNameOption FileNameOption { get; }

        public ProgressLinesOption ProgressLinesOption { get; }

        public FilesArgument FilesArgument { get; }
    }
}
