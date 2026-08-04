// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.Helpers;
using FancyZonesEditor.Models;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace FancyZonesEditor
{
    /// <summary>
    /// The draggable pill that sits between two grid zones and resizes them.
    /// WinUI 3 seals <see cref="Thumb"/>, so this hosts one and forwards its drag events
    /// instead of deriving from it.
    /// </summary>
    public sealed partial class GridResizer : UserControl
    {
        private Orientation _orientation;

        public GridResizer()
        {
            InitializeComponent();

            AutomationProperties.SetName(this, ResourceLoaderInstance.GetString("Resizer_Thumb_Announce"));

            ResizerThumb.DragStarted += (s, e) => DragStarted?.Invoke(this, e);
            ResizerThumb.DragDelta += (s, e) => DragDelta?.Invoke(this, e);
            ResizerThumb.DragCompleted += (s, e) => DragCompleted?.Invoke(this, e);

            GotFocus += (_, _) => FocusRing.Opacity = 1;
            LostFocus += (_, _) => FocusRing.Opacity = 0;
        }

        public event DragStartedEventHandler DragStarted;

        public event DragDeltaEventHandler DragDelta;

        public event DragCompletedEventHandler DragCompleted;

        public int LeftReferenceZone { get; set; }

        public int RightReferenceZone { get; set; }

        public int TopReferenceZone { get; set; }

        public int BottomReferenceZone { get; set; }

        public LayoutModel Model { get; set; }

        public Orientation Orientation
        {
            get
            {
                return _orientation;
            }

            set
            {
                _orientation = value;

                if (value == Orientation.Vertical)
                {
                    Body.RenderTransform = null;
                    ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
                }
                else
                {
                    Body.RenderTransform = new RotateTransform { Angle = 90, CenterX = 24, CenterY = 24 };
                    ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
                }
            }
        }
    }
}
