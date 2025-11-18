// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using ManagedCommon;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace AdvancedPaste.Helpers;

internal static class TranscodeHelpers
{
    public static async Task<DataPackage> TranscodeToMp3Async(DataPackageView clipboardData, CancellationToken cancellationToken, IProgress<double> progress) =>
        await TranscodeMediaAsync(clipboardData, MediaEncodingProfile.CreateMp3(AudioEncodingQuality.High), ".mp3", cancellationToken, progress);

    public static async Task<DataPackage> TranscodeToMp4Async(DataPackageView clipboardData, CancellationToken cancellationToken, IProgress<double> progress) =>
        await TranscodeMediaAsync(clipboardData, MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p), ".mp4", cancellationToken, progress);

    private static async Task<DataPackage> TranscodeMediaAsync(DataPackageView clipboardData, MediaEncodingProfile baseOutputProfile, string extension, CancellationToken cancellationToken, IProgress<double> progress)
    {
        Logger.LogTrace();

        var inputFiles = await clipboardData.GetStorageItemsAsync();

        if (inputFiles.Count != 1)
        {
            throw new InvalidOperationException($"{nameof(TranscodeMediaAsync)} does not support multiple files");
        }

        var inputFile = inputFiles.Single() as StorageFile ?? throw new InvalidOperationException($"{nameof(TranscodeMediaAsync)} only supports files");
        var inputFileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputFile.Path);

        var inputProfile = await MediaEncodingProfile.CreateFromFileAsync(inputFile);
        var outputProfile = CreateOutputProfile(inputProfile, baseOutputProfile);

#if DEBUG
        static string ProfileToString(MediaEncodingProfile profile) => System.Text.Json.JsonSerializer.Serialize(profile, options: new() { WriteIndented = true });
        Logger.LogDebug($"{nameof(inputProfile)}: {ProfileToString(inputProfile)}");
        Logger.LogDebug($"{nameof(outputProfile)}: {ProfileToString(outputProfile)}");
#endif

        var outputFolder = await Task.Run(() => Directory.CreateTempSubdirectory("PowerToys_AdvancedPaste_"), cancellationToken);
        var outputFileName = StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(inputFile.Path), extension) ? inputFileNameWithoutExtension + "_1" : inputFileNameWithoutExtension;
        var outputFilePath = Path.Combine(outputFolder.FullName, Path.ChangeExtension(outputFileName, extension));
        await File.WriteAllBytesAsync(outputFilePath, [], cancellationToken); // TranscodeAsync seems to require the output file to exist

        await TranscodeMediaAsync(inputFile, await StorageFile.GetFileFromPathAsync(outputFilePath), outputProfile, cancellationToken, progress);

        return await DataPackageHelpers.CreateFromFileAsync(outputFilePath);
    }

    private static MediaEncodingProfile CreateOutputProfile(MediaEncodingProfile inputProfile, MediaEncodingProfile baseOutputProfile)
    {
        MediaEncodingProfile outputProfile = new()
        {
            Video = null,
            Audio = null,
        };

        outputProfile.Container = baseOutputProfile.Container.Copy();

        if (inputProfile.Video != null && baseOutputProfile.Video != null)
        {
            outputProfile.Video = baseOutputProfile.Video.Copy();

            if (inputProfile.Video.Bitrate != 0)
            {
                outputProfile.Video.Bitrate = inputProfile.Video.Bitrate;
            }

            if (inputProfile.Video.FrameRate.Numerator != 0)
            {
                outputProfile.Video.FrameRate.Numerator = inputProfile.Video.FrameRate.Numerator;
            }

            if (inputProfile.Video.FrameRate.Denominator != 0)
            {
                outputProfile.Video.FrameRate.Denominator = inputProfile.Video.FrameRate.Denominator;
            }

            if (inputProfile.Video.PixelAspectRatio.Numerator != 0)
            {
                outputProfile.Video.PixelAspectRatio.Numerator = inputProfile.Video.PixelAspectRatio.Numerator;
            }

            if (inputProfile.Video.PixelAspectRatio.Denominator != 0)
            {
                outputProfile.Video.PixelAspectRatio.Denominator = inputProfile.Video.PixelAspectRatio.Denominator;
            }

            outputProfile.Video.Width = inputProfile.Video.Width;
            outputProfile.Video.Height = inputProfile.Video.Height;
        }

        if (inputProfile.Audio != null && baseOutputProfile.Audio != null)
        {
            outputProfile.Audio = baseOutputProfile.Audio.Copy();

            if (inputProfile.Audio.Bitrate != 0)
            {
                outputProfile.Audio.Bitrate = inputProfile.Audio.Bitrate;
            }

            if (inputProfile.Audio.BitsPerSample != 0)
            {
                outputProfile.Audio.BitsPerSample = inputProfile.Audio.BitsPerSample;
            }

            if (inputProfile.Audio.ChannelCount != 0)
            {
                outputProfile.Audio.ChannelCount = inputProfile.Audio.ChannelCount;
            }

            if (inputProfile.Audio.SampleRate != 0)
            {
                outputProfile.Audio.SampleRate = inputProfile.Audio.SampleRate;
            }
        }

        return outputProfile;
    }

    private static async Task TranscodeMediaAsync(StorageFile inputFile, StorageFile outputFile, MediaEncodingProfile outputProfile, CancellationToken cancellationToken, IProgress<double> progress)
    {
        if (outputProfile.Video == null && outputProfile.Audio == null)
        {
            throw new InvalidOperationException("Target profile does not contain media");
        }

        async Task<PrepareTranscodeResult> GetPrepareResult(bool hardwareAccelerationEnabled)
        {
            MediaTranscoder transcoder = new()
            {
                AlwaysReencode = false,
                HardwareAccelerationEnabled = hardwareAccelerationEnabled,
            };

            return await transcoder.PrepareFileTranscodeAsync(inputFile, outputFile, outputProfile);
        }

        var prepareResult = await GetPrepareResult(hardwareAccelerationEnabled: true);

        if (!prepareResult.CanTranscode)
        {
            Logger.LogWarning($"Unable to transcode with hardware acceleration enabled, falling back to software; {nameof(prepareResult.FailureReason)}={prepareResult.FailureReason}");

            prepareResult = await GetPrepareResult(hardwareAccelerationEnabled: false);
        }

        if (!prepareResult.CanTranscode)
        {
            var message = ResourceLoaderInstance.ResourceLoader.GetString(prepareResult.FailureReason == TranscodeFailureReason.CodecNotFound ? "TranscodeErrorUnsupportedCodec" : "TranscodeErrorGeneral");
            throw new PasteActionException(message, new InvalidOperationException($"Error transcoding; {nameof(prepareResult.FailureReason)}={prepareResult.FailureReason}"));
        }

        await prepareResult.TranscodeAsync().AsTask(cancellationToken, progress);
    }
}
