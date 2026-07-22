// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.IO.Compression;
using System.Xml.Linq;

using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Microsoft.PowerToys.ThumbnailHandler.ThreeMf
{
    internal static class ThreeMfModelLoader
    {
        private static readonly string[] ThumbnailExtensions = { ".png", ".jpg", ".jpeg" };

        public static System.Drawing.Bitmap TryLoadEmbeddedThumbnail(Stream stream, uint maxSize)
        {
            if (stream == null || !stream.CanRead)
            {
                return null;
            }

            try
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                var thumbnailEntry = FindThumbnailEntry(archive);
                if (thumbnailEntry == null)
                {
                    return null;
                }

                using var thumbnailStream = thumbnailEntry.Open();
                using var memoryStream = new MemoryStream();
                thumbnailStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                using var image = System.Drawing.Image.FromStream(memoryStream);
                return ThreeMfThumbnailProvider.ResizeImage(new System.Drawing.Bitmap(image), maxSize);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static Model3DGroup LoadModel(Stream stream, Color materialColor)
        {
            if (stream == null || !stream.CanRead)
            {
                return null;
            }

            try
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                var modelEntries = archive.Entries
                    .Where(entry => entry.FullName.EndsWith(".model", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (modelEntries.Count == 0)
                {
                    return null;
                }

                var modelGroup = new Model3DGroup();
                var material = new DiffuseMaterial(new SolidColorBrush(materialColor));

                foreach (var modelEntry in modelEntries)
                {
                    using var modelStream = modelEntry.Open();
                    var document = XDocument.Load(modelStream);
                    AppendModelMeshes(document, modelGroup, material);
                }

                return modelGroup.Children.Count > 0 ? modelGroup : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static ZipArchiveEntry FindThumbnailEntry(ZipArchive archive)
        {
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.Contains("Metadata/", StringComparison.OrdinalIgnoreCase) &&
                    ThumbnailExtensions.Any(ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    return entry;
                }

                if (name.EndsWith("thumbnail.png", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("thumbnail.jpg", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("thumbnail.jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            var relationshipTargets = GetThumbnailTargetsFromRelationships(archive);
            foreach (var target in relationshipTargets)
            {
                var entry = archive.GetEntry(target) ??
                            archive.Entries.FirstOrDefault(e =>
                                e.FullName.Replace('\\', '/').EndsWith(target.TrimStart('/'), StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    return entry;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetThumbnailTargetsFromRelationships(ZipArchive archive)
        {
            var targets = new List<string>();
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (!name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var relStream = entry.Open();
                var document = XDocument.Load(relStream);
                foreach (var relationship in document.Descendants().Where(element => element.Name.LocalName == "Relationship"))
                {
                    var type = relationship.Attribute("Type")?.Value ?? string.Empty;
                    if (!type.Contains("thumbnail", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var target = relationship.Attribute("Target")?.Value;
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        targets.Add(target.Replace('\\', '/'));
                    }
                }
            }

            return targets;
        }

        private static void AppendModelMeshes(XDocument document, Model3DGroup modelGroup, Material material)
        {
            var meshGeometries = new Dictionary<string, MeshGeometry3D>(StringComparer.Ordinal);
            foreach (var meshElement in document.Descendants().Where(element => element.Name.LocalName == "mesh"))
            {
                var parentObject = meshElement.Parent;
                while (parentObject != null && parentObject.Name.LocalName != "object")
                {
                    parentObject = parentObject.Parent;
                }

                var objectId = parentObject?.Attribute("id")?.Value ?? Guid.NewGuid().ToString();
                if (!meshGeometries.ContainsKey(objectId))
                {
                    meshGeometries[objectId] = CreateMeshGeometry(meshElement);
                }
            }

            var buildItems = document.Descendants().Where(element => element.Name.LocalName == "item");
            if (buildItems.Any())
            {
                foreach (var buildItem in buildItems)
                {
                    var objectId = buildItem.Attribute("objectid")?.Value;
                    if (string.IsNullOrWhiteSpace(objectId) || !meshGeometries.TryGetValue(objectId, out var geometry))
                    {
                        continue;
                    }

                    var transform = ParseTransform(buildItem.Attribute("transform")?.Value);
                    var transformedGeometry = transform == null ? geometry : ApplyTransform(geometry, transform.Value);
                    modelGroup.Children.Add(new GeometryModel3D(transformedGeometry, material));
                }
            }
            else
            {
                foreach (var geometry in meshGeometries.Values)
                {
                    modelGroup.Children.Add(new GeometryModel3D(geometry, material));
                }
            }
        }

        private static MeshGeometry3D CreateMeshGeometry(XElement meshElement)
        {
            var vertices = meshElement.Descendants()
                .Where(element => element.Name.LocalName == "vertex")
                .Select(element => new Point3D(
                    ParseDouble(element.Attribute("x")?.Value),
                    ParseDouble(element.Attribute("y")?.Value),
                    ParseDouble(element.Attribute("z")?.Value)))
                .ToList();

            var positions = new Point3DCollection();
            var triangleIndices = new Int32Collection();

            foreach (var triangle in meshElement.Descendants().Where(element => element.Name.LocalName == "triangle"))
            {
                var v1 = ParseInt(triangle.Attribute("v1")?.Value);
                var v2 = ParseInt(triangle.Attribute("v2")?.Value);
                var v3 = ParseInt(triangle.Attribute("v3")?.Value);

                if (v1 < 0 || v2 < 0 || v3 < 0 ||
                    v1 >= vertices.Count || v2 >= vertices.Count || v3 >= vertices.Count)
                {
                    continue;
                }

                triangleIndices.Add(positions.Count);
                positions.Add(vertices[v1]);
                triangleIndices.Add(positions.Count);
                positions.Add(vertices[v2]);
                triangleIndices.Add(positions.Count);
                positions.Add(vertices[v3]);
            }

            return new MeshGeometry3D
            {
                Positions = positions,
                TriangleIndices = triangleIndices,
            };
        }

        private static MeshGeometry3D ApplyTransform(MeshGeometry3D geometry, Matrix3D transform)
        {
            var transformedPositions = new Point3DCollection(geometry.Positions.Count);
            foreach (var position in geometry.Positions)
            {
                var point = transform.Transform(position);
                transformedPositions.Add(point);
            }

            return new MeshGeometry3D
            {
                Positions = transformedPositions,
                TriangleIndices = geometry.TriangleIndices,
            };
        }

        private static Matrix3D? ParseTransform(string transformValue)
        {
            if (string.IsNullOrWhiteSpace(transformValue))
            {
                return null;
            }

            var values = transformValue
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseDouble)
                .ToArray();

            if (values.Length != 12)
            {
                return null;
            }

            return new Matrix3D(
                values[0], values[1], values[2], 0,
                values[3], values[4], values[5], 0,
                values[6], values[7], values[8], 0,
                values[9], values[10], values[11], 1);
        }

        private static double ParseDouble(string value)
        {
            return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result)
                ? result
                : 0;
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var result)
                ? result
                : -1;
        }
    }
}
