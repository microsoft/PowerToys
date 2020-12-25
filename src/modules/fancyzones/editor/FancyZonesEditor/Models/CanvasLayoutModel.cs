// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;

namespace FancyZonesEditor.Models
{
    // CanvasLayoutModel
    //  Free form Layout Model, which specifies independent zone rects
    public class CanvasLayoutModel : LayoutModel
    {
        // Non-localizable strings
        public const string ModelTypeID = "canvas";

        public Rect CanvasRect { get; private set; }

        public CanvasLayoutModel(string uuid, string name, LayoutType type, IList<Int32Rect> zones, int width, int height)
            : base(uuid, name, type)
        {
            Zones = zones;
            CanvasRect = new Rect(new Size(width, height));
        }

        public CanvasLayoutModel(string name, LayoutType type)
        : base(name, type)
        {
        }

        public CanvasLayoutModel(string name)
        : base(name)
        {
        }

        // Zones - the list of all zones in this layout, described as independent rectangles
        public IList<Int32Rect> Zones { get; private set; } = new List<Int32Rect>();

        // RemoveZoneAt
        //  Removes the specified index from the Zones list, and fires a property changed notification for the Zones property
        public void RemoveZoneAt(int index)
        {
            Zones.RemoveAt(index);
            UpdateLayout();
        }

        // AddZone
        //  Adds the specified Zone to the end of the Zones list, and fires a property changed notification for the Zones property
        public void AddZone(Int32Rect zone)
        {
            Zones.Add(zone);
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            FirePropertyChanged();
        }

        // Clone
        //  Implements the LayoutModel.Clone abstract method
        //  Clones the data from this CanvasLayoutModel to a new CanvasLayoutModel
        public override LayoutModel Clone()
        {
            CanvasLayoutModel layout = new CanvasLayoutModel(Name);

            foreach (Int32Rect zone in Zones)
            {
                layout.Zones.Add(zone);
            }

            layout.SensitivityRadius = SensitivityRadius;
            return layout;
        }

        public void RestoreTo(CanvasLayoutModel other)
        {
            other.Zones.Clear();
            foreach (Int32Rect zone in Zones)
            {
                other.Zones.Add(zone);
            }

            other.SensitivityRadius = SensitivityRadius;
        }

        // PersistData
        // Implements the LayoutModel.PersistData abstract method
        protected override void PersistData()
        {
            AddCustomLayout(this);
        }
    }
}
