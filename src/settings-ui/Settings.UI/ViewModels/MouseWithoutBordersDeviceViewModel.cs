// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    [WinRT.GeneratedBindableCustomProperty]
    public sealed partial class MouseWithoutBordersDeviceViewModel : Observable
    {
        private int _index;

        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged(nameof(Index));
                }
            }
        }

        public string Name { get; set; } = string.Empty;

        public bool CanDragDrop { get; set; }

        private Brush _statusBrush = new SolidColorBrush(Colors.Transparent);

        public Brush StatusBrush
        {
            get => _statusBrush;
            set
            {
                if (_statusBrush != value)
                {
                    _statusBrush = value;
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        }
    }
}
