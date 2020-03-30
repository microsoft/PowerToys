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
        public CanvasLayoutModel(string uuid, string name, LayoutType type, int referenceWidth, int referenceHeight, IList<Int32Rect> zones)
            : base(uuid, name, type)
        {
            _referenceWidth = referenceWidth;
            _referenceHeight = referenceHeight;
            Zones = zones;
        }

        public CanvasLayoutModel(string name, LayoutType type, int referenceWidth, int referenceHeight)
        : base(name, type)
        {
            // Initialize Reference Size
            _referenceWidth = referenceWidth;
            _referenceHeight = referenceHeight;
        }

        public CanvasLayoutModel(string name)
            : base(name)
        {
        }

        // ReferenceWidth - the reference width for the layout rect that all Zones are relative to
        public int ReferenceWidth
        {
            get
            {
                return _referenceWidth;
            }

            set
            {
                if (_referenceWidth != value)
                {
                    _referenceWidth = value;
                    FirePropertyChanged("ReferenceWidth");
                }
            }
        }

        private int _referenceWidth;

        // ReferenceHeight - the reference height for the layout rect that all Zones are relative to
        public int ReferenceHeight
        {
            get
            {
                return _referenceHeight;
            }

            set
            {
                if (_referenceHeight != value)
                {
                    _referenceHeight = value;
                    FirePropertyChanged("ReferenceHeight");
                }
            }
        }

        private int _referenceHeight;

        // Zones - the list of all zones in this layout, described as independent rectangles
        public IList<Int32Rect> Zones { get; } = new List<Int32Rect>();

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
            CanvasLayoutModel layout = new CanvasLayoutModel(Name)
            {
                ReferenceHeight = ReferenceHeight,
                ReferenceWidth = ReferenceWidth,
            };

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
                RefWidth = _referenceWidth,
                RefHeight = _referenceHeight,
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
