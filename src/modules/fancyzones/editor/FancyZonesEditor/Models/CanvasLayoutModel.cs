using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Converters;
using System.Windows.Documents;

namespace FancyZonesEditor.Models
{
    // CanvasLayoutModel 
    //  Free form Layout Model, which specifies independent zone rects
    public class CanvasLayoutModel : LayoutModel
    {
        public CanvasLayoutModel(ushort version, string name, ushort id, byte[] data) : base(name, id)
        {
            if (version == c_latestVersion)
            {
                Load(data);
            }
        }

        public CanvasLayoutModel(string name, ushort id, int referenceWidth, int referenceHeight) : base(name, id)
        {
            // Initialize Reference Size
            _referenceWidth = referenceWidth;
            _referenceHeight = referenceHeight;
        }

        public CanvasLayoutModel(string name, ushort id) : base(name, id) { }
        public CanvasLayoutModel(string name) : base(name) { }
        public CanvasLayoutModel() : base() { }

        // ReferenceWidth - the reference width for the layout rect that all Zones are relative to
        public int ReferenceWidth
        {
            get { return _referenceWidth; }
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
            get { return _referenceHeight; }
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
        public IList<Int32Rect> Zones { get { return _zones; } }
        private IList<Int32Rect> _zones = new List<Int32Rect>();

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

        private void Load(byte[] data)
        {
            // Initialize this CanvasLayoutModel based on the given persistence data
            // Skip version (2 bytes), id (2 bytes), and type (1 bytes)
            int i = 5;
            _referenceWidth = data[i++] * 256 + data[i++];
            _referenceHeight = data[i++] * 256 + data[i++];

            int count = data[i++];

            while (count-- > 0)
            {
                _zones.Add(new Int32Rect(
                    data[i++] * 256 + data[i++],
                    data[i++] * 256 + data[i++],
                    data[i++] * 256 + data[i++],
                    data[i++] * 256 + data[i++]));
            }
        }

        // Clone
        //  Implements the LayoutModel.Clone abstract method
        //  Clones the data from this CanvasLayoutModel to a new CanvasLayoutModel
        public override LayoutModel Clone()
        {
            CanvasLayoutModel layout = new CanvasLayoutModel(Name);
            layout.ReferenceHeight = ReferenceHeight;
            layout.ReferenceWidth = ReferenceWidth;

            foreach(Int32Rect zone in Zones)
            {
                layout.Zones.Add(zone);
            }

            return layout;
        }

        // GetPersistData
        //  Implements the LayoutModel.GetPersistData abstract method
        //  Returns the state of this GridLayoutModel in persisted format
        protected override byte[] GetPersistData()
        {
            byte[] data = new byte[10 + (_zones.Count * 8)];
            int i = 0;

            // Common persisted values between all layout types
            data[i++] = (byte)(c_latestVersion / 256);
            data[i++] = (byte)(c_latestVersion % 256);
            data[i++] = 1; // LayoutModelType: 1 == CanvasLayoutModel
            data[i++] = (byte)(Id / 256);
            data[i++] = (byte)(Id % 256);
            // End common

            data[i++] = (byte)(_referenceWidth / 256);
            data[i++] = (byte)(_referenceWidth % 256);
            data[i++] = (byte)(_referenceHeight / 256);
            data[i++] = (byte)(_referenceHeight % 256);
            data[i++] = (byte)_zones.Count;

            foreach (Int32Rect rect in _zones)
            {
                data[i++] = (byte)(rect.X / 256);
                data[i++] = (byte)(rect.X % 256);

                data[i++] = (byte)(rect.Y / 256);
                data[i++] = (byte)(rect.Y % 256);

                data[i++] = (byte)(rect.Width / 256);
                data[i++] = (byte)(rect.Width % 256);

                data[i++] = (byte)(rect.Height / 256);
                data[i++] = (byte)(rect.Height % 256);
            }
            return data;
        }

        private static ushort c_latestVersion = 0;
    }
}
