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

            AddOption(HelpOption);
            AddOption(ShowConfigOption);
            AddOption(DestinationOption);
            AddOption(WidthOption);
            AddOption(HeightOption);
            AddOption(UnitOption);
            AddOption(FitOption);
            AddOption(SizeOption);
            AddOption(ShrinkOnlyOption);
            AddOption(ReplaceOption);
            AddOption(IgnoreOrientationOption);
            AddOption(RemoveMetadataOption);
            AddOption(QualityOption);
            AddOption(KeepDateModifiedOption);
            AddOption(FileNameOption);
            AddOption(ProgressLinesOption);
            AddArgument(FilesArgument);
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
