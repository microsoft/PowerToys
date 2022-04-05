// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for GridResizer.xaml
    /// </summary>
    public partial class GridResizer : Thumb
    {
        private static readonly RotateTransform _rotateTransform = new RotateTransform(90, 24, 24);

        public int LeftReferenceZone { get; set; }

        public int RightReferenceZone { get; set; }

        public int TopReferenceZone { get; set; }

        public int BottomReferenceZone { get; set; }

        public LayoutModel Model { get; set; }

        private Orientation _orientation;

        public GridResizer()
        {
            InitializeComponent();
        }

        public Orientation Orientation
        {
            get
            {
                return _orientation;
            }

            set
            {
                _orientation = value;
                ApplyTemplate();
                Border body = (Border)Template.FindName("Body", this);
                if (value == Orientation.Vertical)
                {
                    body.RenderTransform = null;
                    body.Cursor = Cursors.SizeWE;
                }
                else
                {
                    body.RenderTransform = _rotateTransform;
                    body.Cursor = Cursors.SizeNS;
                }
            }
        }
    }
}
