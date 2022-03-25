// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Media;

namespace ColorPicker.ViewModelContracts
{
    public interface IMainViewModel
    {
        /// <summary>
        /// Gets the text representation of the selected color value
        /// </summary>
        string ColorText { get; }

        /// <summary>
        /// Gets the current selected color as a <see cref="Brush"/>
        /// </summary>
        Brush ColorBrush { get; }

        /// <summary>
        /// Gets the color name
        /// </summary>
        string ColorName { get; }

        /// <summary>
        /// Gets a value indicating whether gets the show color name
        /// </summary>
        bool ShowColorName { get; }

        void RegisterWindowHandle(System.Windows.Interop.HwndSource hwndSource);
    }
}
