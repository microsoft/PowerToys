#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageResizer.Models;
using ImageResizer.Properties;
using Windows.Graphics.Imaging;

namespace ImageResizer.ViewModels
{
    public class AdvancedViewModel : ObservableObject
    {
        private static Dictionary<Guid, string> InitEncoderMap()
        {
            return new Dictionary<Guid, string>
            {
                [BitmapEncoder.BmpEncoderId] = "BMP Encoder",
                [BitmapEncoder.GifEncoderId] = "GIF Encoder",
                [BitmapEncoder.JpegEncoderId] = "JPEG Encoder",
                [BitmapEncoder.PngEncoderId] = "PNG Encoder",
                [BitmapEncoder.TiffEncoderId] = "TIFF Encoder",
                [BitmapEncoder.JpegXREncoderId] = "JPEG XR Encoder",
            };
        }

        public AdvancedViewModel(Settings settings)
        {
            RemoveSizeCommand = new RelayCommand<ResizeSize>(RemoveSize);
            AddSizeCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(AddSize);
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
