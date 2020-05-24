// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace FancyZonesEditor.Models
{
    // CanvasLayoutModel
    //  Free form Layout Model, which specifies independent zone rects
    public class CanvasLayoutModel : LayoutModel
    {
        public CanvasLayoutModel(string uuid, string name, LayoutType type, IList<Int32Rect> zones, int workAreaWidth, int workAreaHeight)
            : base(uuid, name, type)
        {
            lastWorkAreaWidth = workAreaWidth;
            lastWorkAreaHeight = workAreaHeight;

            if (ShouldScaleLayout())
            {
                ScaleLayout(zones);
            }
            else
            {
                Zones = zones;
            }
        }

        public CanvasLayoutModel(string name, LayoutType type)
        : base(name, type)
        {
        }

        // Zones - the list of all zones in this layout, described as independent rectangles
        public IList<Int32Rect> Zones { get; private set; } = new List<Int32Rect>();

        private int lastWorkAreaWidth = (int)Settings.WorkArea.Width;

        private int lastWorkAreaHeight = (int)Settings.WorkArea.Height;

        // RemoveZoneAt
        //  Removes the specified index from the Zones list, and fires a property changed notification for the Zones property
        public void RemoveZoneAt(int index)
        {
            Zones.RemoveAt(index);
            FirePropertyChanged("Zones");
        }

        // AddZone
        //  Adds the specified Zone to the end of the Zones list, and fires a property changed notification for the Zones property
        public void AddZone(Int32Rect zone)
        {
            Zones.Add(zone);
            FirePropertyChanged("Zones");
        }

        // Clone
        //  Implements the LayoutModel.Clone abstract method
        //  Clones the data from this CanvasLayoutModel to a new CanvasLayoutModel
        public override LayoutModel Clone()
        {
            CanvasLayoutModel layout = new CanvasLayoutModel(Name, Type);

            foreach (Int32Rect zone in Zones)
            {
                layout.Zones.Add(zone);
            }

            return layout;
        }

        public void RestoreTo(CanvasLayoutModel other)
        {
            other.Zones.Clear();
            foreach (Int32Rect zone in Zones)
            {
                other.Zones.Add(zone);
            }
        }

        private bool ShouldScaleLayout()
        {
            // Scale if:
            // - at least one dimension changed
            // - orientation remained the same
            return (lastWorkAreaHeight != Settings.WorkArea.Height || lastWorkAreaWidth != Settings.WorkArea.Width) &&
                ((lastWorkAreaHeight > lastWorkAreaWidth && Settings.WorkArea.Height > Settings.WorkArea.Width) ||
                  (lastWorkAreaWidth > lastWorkAreaHeight && Settings.WorkArea.Width > Settings.WorkArea.Height));
        }

        private void ScaleLayout(IList<Int32Rect> zones)
        {
            foreach (Int32Rect zone in zones)
            {
                double widthFactor = (double)Settings.WorkArea.Width / lastWorkAreaWidth;
                double heightFactor = (double)Settings.WorkArea.Height / lastWorkAreaHeight;
                int scaledX = (int)(zone.X * widthFactor);
                int scaledY = (int)(zone.Y * heightFactor);
                int scaledWidth = (int)(zone.Width * widthFactor);
                int scaledHeight = (int)(zone.Height * heightFactor);
                Zones.Add(new Int32Rect(scaledX, scaledY, scaledWidth, scaledHeight));
            }

            lastWorkAreaHeight = (int)Settings.WorkArea.Height;
            lastWorkAreaWidth = (int)Settings.WorkArea.Width;
        }

        private struct Zone
        {
            public int X { get; set; }

            public int Y { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }
        }

        private struct CanvasLayoutInfo
        {
            public int RefWidth { get; set; }

            public int RefHeight { get; set; }

            public Zone[] Zones { get; set; }
        }

        private struct CanvasLayoutJson
        {
            public string Uuid { get; set; }

            public string Name { get; set; }

            public string Type { get; set; }

            public CanvasLayoutInfo Info { get; set; }
        }

        // PersistData
        // Implements the LayoutModel.PersistData abstract method
        protected override void PersistData()
        {
            CanvasLayoutInfo layoutInfo = new CanvasLayoutInfo
            {
                RefWidth = lastWorkAreaWidth,
                RefHeight = lastWorkAreaHeight,

                Zones = new Zone[Zones.Count],
            };
            for (int i = 0; i < Zones.Count; ++i)
            {
                Zone zone = new Zone
                {
                    X = Zones[i].X,
                    Y = Zones[i].Y,
                    Width = Zones[i].Width,
                    Height = Zones[i].Height,
                };

                layoutInfo.Zones[i] = zone;
            }

            CanvasLayoutJson jsonObj = new CanvasLayoutJson
            {
                Uuid = "{" + Guid.ToString().ToUpper() + "}",
                Name = Name,
                Type = "canvas",
                Info = layoutInfo,
            };

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new DashCaseNamingPolicy(),
            };

            try
            {
                string jsonString = JsonSerializer.Serialize(jsonObj, options);
                File.WriteAllText(Settings.AppliedZoneSetTmpFile, jsonString);
            }
            catch (Exception ex)
            {
                ShowExceptionMessageBox("Error persisting canvas layout", ex);
            }
        }
    }
}
