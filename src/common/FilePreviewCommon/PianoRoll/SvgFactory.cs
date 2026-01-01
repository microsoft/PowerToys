// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Microsoft.PowerToys.FilePreviewCommon.PianoRoll
{
    internal sealed class SvgFactory
    {
        private string[] noteColors = new string[]
        {
            "#1F77B4",
            "#ff7f0e",
            "#2ca02c",
            "#d62728",
            "#9467bd",
            "#8c564b",
            "#e377c2",
            "#7f7f7f",
            "#bcbd22",
            "#17becf",
        };

        private List<Rectangle[]> tracksRectangles = new List<Rectangle[]>();

        internal string Style { get; set; } = string.Empty;

        public required CoordinateHelper CoordinateHelper { get; set; }

        private int noteRound = 4;

        public void ApplyStyle(int trackCount)
        {
            StringBuilder styleBuilder = new StringBuilder();
            for (int trackId = 0; trackId < trackCount; trackId++)
            {
                var trackStyle = $@".note{trackId.ToString(CultureInfo.InvariantCulture)} {{
    fill: {noteColors[trackId % noteColors.Length]}
}}
";
                styleBuilder.Append(trackStyle);
            }

            Style = styleBuilder.ToString();
        }

        public void DrawTrack(Track track)
        {
            tracksRectangles.Add(track.NoteList.Select(DrawNote).ToArray());
        }

        public Rectangle DrawNote(Note note)
        {
            var parameters = CoordinateHelper.GetNotePositionParameters(note);
            var rect = new Rectangle
            {
                X = parameters.Point1.Item1,
                Y = parameters.Point1.Item2,
                Width = parameters.Point2.Item1 - parameters.Point1.Item1,
                Height = parameters.Point2.Item2 - parameters.Point1.Item2,
                R = noteRound,
            };
            return rect;
        }

        public XmlDocument ToXmlDocument()
        {
            var svgFile = new XmlDocument();
            var root = svgFile.CreateElement("svg", "http://www.w3.org/2000/svg");
            root.SetAttribute("width", CoordinateHelper.GetSize().Item1.ToString(CultureInfo.InvariantCulture));
            root.SetAttribute("height", CoordinateHelper.GetSize().Item2.ToString(CultureInfo.InvariantCulture));
            svgFile.AppendChild(root);
            var style = svgFile.CreateElement("style", svgFile.DocumentElement?.NamespaceURI);
            style.InnerText = Style;
            root.AppendChild(style);
            for (int trackId = 0; trackId < tracksRectangles.Count; trackId++)
            {
                var trackRectangles = tracksRectangles[trackId];
                foreach (var rectangleElement in trackRectangles)
                {
                    var rectangle = svgFile.CreateElement("rect", svgFile.DocumentElement?.NamespaceURI);
                    rectangle.SetAttribute("class", $"note{trackId}");
                    rectangle.SetAttribute("x", rectangleElement.X.ToString(CultureInfo.InvariantCulture));
                    rectangle.SetAttribute("y", rectangleElement.Y.ToString(CultureInfo.InvariantCulture));
                    rectangle.SetAttribute("width", rectangleElement.Width.ToString(CultureInfo.InvariantCulture));
                    rectangle.SetAttribute("height", rectangleElement.Height.ToString(CultureInfo.InvariantCulture));
                    rectangle.SetAttribute("rx", rectangleElement.R.ToString(CultureInfo.InvariantCulture));
                    rectangle.SetAttribute("ry", rectangleElement.R.ToString(CultureInfo.InvariantCulture));
                    root.AppendChild(rectangle);
                }
            }

            return svgFile;
        }

        public string Dumps()
        {
            StringWriter stringWriter = new StringWriter();
            using (XmlWriter writer = XmlWriter.Create(stringWriter))
            {
                ToXmlDocument().WriteTo(writer);
                writer.Flush();
            }

            return stringWriter.ToString();
        }

        public void Write(string path)
        {
            using (XmlWriter writer = XmlWriter.Create(path))
            {
                ToXmlDocument().WriteTo(writer);
                writer.Flush();
            }
        }
    }
}
