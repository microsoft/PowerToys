// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public record MouseJumpThumbnailSize : INotifyPropertyChanged, ICmdLineRepresentable
    {
        private int _width;
        private int _height;

        [JsonPropertyName("width")]
        public int Width
        {
            get
            {
                return _width;
            }

            set
            {
                var newWidth = Math.Max(0, value);
                if (newWidth != _width)
                {
                    _width = newWidth;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("height")]
        public int Height
        {
            get
            {
                return _height;
            }

            set
            {
                var newHeight = Math.Max(0, value);
                if (newHeight != _height)
                {
                    _height = newHeight;
                    OnPropertyChanged();
                }
            }
        }

        public MouseJumpThumbnailSize()
        {
            Width = 1600;
            Height = 1200;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static bool TryParseFromCmd(string cmd, out object result)
        {
            result = null;

            var parts = cmd.Split('x');
            if (parts.Length != 2)
            {
                return false;
            }

            if (int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
            {
                result = new MouseJumpThumbnailSize { Width = width, Height = height };
                return true;
            }

            return false;
        }

        public bool TryToCmdRepresentable(out string result)
        {
            result = $"{Width}x{Height}";
            return true;
        }
    }
}
