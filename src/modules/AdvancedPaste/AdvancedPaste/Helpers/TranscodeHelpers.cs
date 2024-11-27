// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;

using ManagedCommon;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace AdvancedPaste.Helpers;

internal static class TranscodeHelpers
{
    public static async Task<DataPackage> TranscodeToMp3Async(DataPackageView clipboardData, IProgress<double> progress)
    {
        return await TranscodeMediaAsync(clipboardData, MediaEncodingProfile.CreateMp3(AudioEncodingQuality.High), ".mp3", progress);
    }

    public static async Task<DataPackage> TranscodeToMp4Async(DataPackageView clipboardData, IProgress<double> progress)
    {
        return await TranscodeMediaAsync(clipboardData, MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p), ".mp4", progress);
    }

    private static async Task<DataPackage> TranscodeMediaAsync(DataPackageView clipboardData, MediaEncodingProfile profile, string extension, IProgress<double> progress)
    {
        Logger.LogTrace();

        var sourceFiles = await clipboardData.GetStorageItemsAsync();

        if (sourceFiles.Count != 1)
        {
            throw new InvalidOperationException($"{nameof(TranscodeMediaAsync)} does not support multiple files");
        }

        var sourceFile = sourceFiles[0] as StorageFile;
        var sourcePath = sourceFile.Path;
        var sourceNameWithoutExtension = Path.GetFileNameWithoutExtension(sourcePath);

        var destinationFolder = await Task.Run(() => Directory.CreateTempSubdirectory("PowerToys_AdvancedPaste_"));
        var destinationName = StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(sourcePath), extension) ? sourceNameWithoutExtension + "_1" : sourceNameWithoutExtension;
        var destinationPath = Path.Combine(destinationFolder.FullName, Path.ChangeExtension(destinationName, extension));
        await File.WriteAllBytesAsync(destinationPath, []);

        var destinationFile = await StorageFile.GetFileFromPathAsync(destinationPath);
        await TranscodeMediaAsync(sourceFile, destinationFile, profile, progress);

        return await DataPackageHelpers.CreateFromFileAsync(destinationPath);
    }

    private static async Task TranscodeMediaAsync(StorageFile sourceFile, StorageFile destinationFile, MediaEncodingProfile profile, IProgress<double> progress)
    {
        var prepareOp = await new MediaTranscoder().PrepareFileTranscodeAsync(sourceFile, destinationFile, profile);

        if (!prepareOp.CanTranscode)
        {
            throw new InvalidOperationException($"Error transcoding; Reason={prepareOp.FailureReason}");
        }

        var transcodeOp = prepareOp.TranscodeAsync();
        transcodeOp.Progress = (_, args) => progress.Report(args);

        await transcodeOp;
    }
}
