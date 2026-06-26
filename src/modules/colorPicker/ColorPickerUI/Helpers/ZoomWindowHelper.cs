// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.ViewModelContracts;

using Point = Windows.Foundation.Point;

namespace ColorPicker.Helpers
{
    /// <summary>
    /// 7e-1 placeholder for the zoom magnifier. The real implementation (GDI screen capture,
    /// SoftwareBitmapSource, and the Win2D pixel-grid overlay) lands in 7e-2 / sub-project C
    /// step 8. The DI dependencies match the real helper so the App wiring stays stable; the
    /// methods are no-ops for now so the picking flow compiles and runs without the magnifier.
    /// </summary>
    public class ZoomWindowHelper
    {
        private readonly IZoomViewModel _zoomViewModel;
        private readonly AppStateHandler _appStateHandler;

        public ZoomWindowHelper(IZoomViewModel zoomViewModel, AppStateHandler appStateHandler)
        {
            _zoomViewModel = zoomViewModel;
            _appStateHandler = appStateHandler;
        }

        public void Zoom(Point position, bool zoomIn)
        {
            // No-op until the Win2D magnifier is ported (7e-2).
        }

        public void CloseZoomWindow()
        {
            // No-op until the Win2D magnifier is ported (7e-2).
        }
    }
}
