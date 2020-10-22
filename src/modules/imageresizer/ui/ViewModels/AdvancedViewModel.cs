// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Properties;

namespace ImageResizer.ViewModels
{
    public class AdvancedViewModel : Observable
    {
        private static Dictionary<Guid, string> InitEncoderMap()
        {
            var bmpCodec = new BmpBitmapEncoder().CodecInfo;
            var gifCodec = new GifBitmapEncoder().CodecInfo;
            var jpegCodec = new JpegBitmapEncoder().CodecInfo;
            var pngCodec = new PngBitmapEncoder().CodecInfo;
            var tiffCodec = new TiffBitmapEncoder().CodecInfo;
            var wmpCodec = new WmpBitmapEncoder().CodecInfo;

            return new Dictionary<Guid, string>
            {
                [bmpCodec.ContainerFormat] = bmpCodec.FriendlyName,
                [gifCodec.ContainerFormat] = gifCodec.FriendlyName,
                [jpegCodec.ContainerFormat] = jpegCodec.FriendlyName,
                [pngCodec.ContainerFormat] = pngCodec.FriendlyName,
                [tiffCodec.ContainerFormat] = tiffCodec.FriendlyName,
                [wmpCodec.ContainerFormat] = wmpCodec.FriendlyName,
            };
        }

        public AdvancedViewModel(Settings settings)
        {
            RemoveSizeCommand = new RelayCommand<ResizeSize>(RemoveSize);
            AddSizeCommand = new RelayCommand(AddSize);
            Settings = settings;
        }

        public static IDictionary<Guid, string> EncoderMap { get; } = InitEncoderMap();

        public Settings Settings { get; }

        public static string Version
            => typeof(AdvancedViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

        public static IEnumerable<Guid> Encoders => EncoderMap.Keys;

        public ICommand RemoveSizeCommand { get; }

        public ICommand AddSizeCommand { get; }

        public void RemoveSize(ResizeSize size)
            => Settings.Sizes.Remove(size);

        public void AddSize()
            => Settings.Sizes.Add(new ResizeSize());

        public void Close(bool accepted)
        {
            if (accepted)
            {
                Settings.Save();

                return;
            }

            var selectedSizeIndex = Settings.SelectedSizeIndex;
            var shrinkOnly = Settings.ShrinkOnly;
            var replace = Settings.Replace;
            var ignoreOrientation = Settings.IgnoreOrientation;

            Settings.Reload();
            Settings.SelectedSizeIndex = selectedSizeIndex;
            Settings.ShrinkOnly = shrinkOnly;
            Settings.Replace = replace;
            Settings.IgnoreOrientation = ignoreOrientation;
        }
    }
}
