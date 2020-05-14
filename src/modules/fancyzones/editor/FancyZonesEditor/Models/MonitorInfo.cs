using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FancyZonesEditor
{
    public class MonitorInfo
    {
        public MonitorInfo(int id, string name, int height, int width, bool selected = false)
        {
            Id = id;
            Name = name;
            Height = height;
            Width = width;
            Selected = selected;
            Fill = Selected ? "DeepSkyBlue" : "Gray";
        }

        private int id;

        public int Id
        {
            get { return id; }
            set { id = value; }
        }

        private string name;

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        private int height;

        public int Height
        {
            get { return height; }
            set { height = value; }
        }

        private int width;

        public int Width
        {
            get { return width; }
            set { width = value; }
        }

        private string fill;

        public string Fill
        {
            get { return fill; }
            set { fill = value; }
        }

        private bool selected;

        public bool Selected
        {
            get { return selected; }
            set { selected = value; }
        }
    }
}
