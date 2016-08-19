using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.ApplicationModel.Core;
using Windows.Storage.Streams;
using AppxPackaing;
using Shell;
using Wox.Infrastructure.Logger;
using IStream = AppxPackaing.IStream;
using Size = Windows.Foundation.Size;

namespace Wox.Plugin.Program.ProgramSources
{
    public class UWP
    {
        public string Name { get; }
        public string FullName { get; }
        public string FamilyName { get; }


        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string PublisherDisplayName { get; set; }
        public string Location { get; set; }

        public Application[] Apps { get; set; }

        public Package Package { get; }

        public int Score { get; set; }

        public UWP(Package package)
        {
            Package = package;
            Name = Package.Id.Name;
            FullName = Package.Id.FullName;
            FamilyName = Package.Id.FamilyName;
            Location = Package.InstalledLocation.Path;

            InitializeAppDisplayInfo(package);
            InitializeAppInfo();
            Apps = Apps.Where(a =>
            {
                var valid = !string.IsNullOrEmpty(a.Executable) &&
                            !string.IsNullOrEmpty(a.UserModelId) &&
                            !string.IsNullOrEmpty(a.DisplayName);
                return valid;
            }).ToArray();
        }

        private void InitializeAppInfo()
        {
            var path = Path.Combine(Location, "AppxManifest.xml");
            var appx = new AppxFactory();
            IStream stream;
            const uint noAttribute = 0x80;
            const Stgm exclusiveRead = Stgm.Read | Stgm.ShareExclusive;
            var result = SHCreateStreamOnFileEx(path, exclusiveRead, noAttribute, false, null, out stream);

            if (result == Hresult.Ok)
            {
                var reader = appx.CreateManifestReader(stream);

                var properties = reader.GetProperties();
                PublisherDisplayName = properties.GetStringValue("PublisherDisplayName");
                DisplayName = properties.GetStringValue("DisplayName");
                Description = properties.GetStringValue("Description");

                var apps = reader.GetApplications();
                int i = 0;
                while (apps.GetHasCurrent() != 0 && i <= Apps.Length)
                {
                    var currentApp = apps.GetCurrent();
                    Apps[i].UserModelId = currentApp.GetAppUserModelId();
                    Apps[i].Executable = currentApp.GetStringValue("Executable");
                    Apps[i].BackgroundColor = currentApp.GetStringValue("BackgroundColor");
                    Apps[i].LogoPath = Path.Combine(Location, currentApp.GetStringValue("Square44x44Logo"));

                    apps.MoveNext();
                    i++;
                }
                if (i != Apps.Length)
                {
                    var message = $"Wrong application number - {Name}: {i}";
                    Console.WriteLine(message);
                }
            }
        }


        private void InitializeAppDisplayInfo(Package package)
        {
            IReadOnlyList<AppListEntry> apps;
            try
            {
                apps = package.GetAppListEntriesAsync().AsTask().Result;
            }
            catch (Exception e)
            {
                var message = $"{e.Message} @ {Name}";
                Console.WriteLine(message);
                return;
            }

            Apps = apps.Select(a => new Application
            {
                DisplayName = a.DisplayInfo.DisplayName,
                Description = a.DisplayInfo.Description,
                // todo: which size is valid?
                LogoStream = a.DisplayInfo.GetLogo(new Size(44, 44))
            }).ToArray();
        }

        public static List<UWP> All()
        {
            var packages = CurrentUserPackages();
            var uwps = new List<UWP>();
            Parallel.ForEach(packages, p =>
            {
                try
                {
                    var u = new UWP(p);
                    if (u.Apps.Length > 0)
                    {
                        uwps.Add(u);
                    }
                }
                catch (Exception e)
                {
                    // if there are errors, just ignore it and continue
                    var message = $"Can't parse {p.Id.Name}: {e.Message}";
                    Log.Error(message);
                }
            });
            return uwps;
        }

        private static IEnumerable<Package> CurrentUserPackages()
        {
            var user = WindowsIdentity.GetCurrent()?.User;

            if (user != null)
            {
                var userSecurityId = user.Value;
                var packageManager = new PackageManager();
                var packages = packageManager.FindPackagesForUser(userSecurityId);
                // cw5n1h2txyewy is PublisherID for unnormal package
                // e.g. ShellExperienceHost
                // but WindowsFeedBack is flitered
                // tested with windows 10 1511  
                const string filteredPublisherID = "cw5n1h2txyewy";
                packages = packages.Where(
                    p => !p.IsFramework && p.Id.PublisherId != filteredPublisherID
                );
                return packages;
            }
            else
            {
                return new Package[] { };
            }
        }

        public override string ToString()
        {
            return FamilyName;
        }

        public override bool Equals(object obj)
        {
            var uwp = obj as UWP;
            if (uwp != null)
            {
                return FamilyName.Equals(uwp.FamilyName);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return FamilyName.GetHashCode();
        }


        public class Application
        {
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public RandomAccessStreamReference LogoStream { get; set; }
            public string UserModelId { get; set; }
            public string Executable { get; set; }
            public string PublisherDisplayName { get; set; }
            public string BackgroundColor { get; set; }
            public string LogoPath { get; set; }

            // todo: wrap with try exception
            public void Launch()
            {
                var appManager = new ApplicationActivationManager();
                uint unusedPid;
                const string noArgs = "";
                const ACTIVATEOPTIONS noFlags = ACTIVATEOPTIONS.AO_NONE;
                appManager.ActivateApplication(UserModelId, noArgs, noFlags, out unusedPid);
            }


            public ImageSource Logo()
            {
                var logo = !string.IsNullOrEmpty(LogoPath) ? ImageFromPath(LogoPath) : ImageFromStream(LogoStream);

                var validBaground = !string.IsNullOrEmpty(BackgroundColor) && BackgroundColor != "transparent";
                var plated = validBaground ? PlatedImage(logo) : logo;

                // todo magic! temp fix for cross thread object
                plated.Freeze();
                return plated;
            }

            private BitmapImage ImageFromPath(string path)
            {
                // https://msdn.microsoft.com/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets
                var extension = path.Substring(path.Length - 4);
                var filename = path.Substring(0, path.Length - 4);
                // todo: remove hard cod scale
                var path1 = $"{filename}.scale-200{extension}";
                var path2 = $"{filename}.scale-100{extension}";
                var uri = File.Exists(path1) ? new Uri(path1) : new Uri(path2);
                var image = new BitmapImage(uri);
                return image;
            }

            private BitmapImage ImageFromStream(RandomAccessStreamReference reference)
            {
                IRandomAccessStreamWithContentType stream;
                try
                {
                    stream = reference.OpenReadAsync().AsTask().Result;
                }
                catch (Exception e)
                {
                    var message = $"{e.Message} @ {DisplayName}";
                    Log.Error(message);
                    throw;
                }

                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = stream.AsStream();
                image.EndInit();
                return image;
            }

            private ImageSource PlatedImage(BitmapImage image)
            {
                var width = image.Width;
                var height = image.Height;
                var x = 0;
                var y = 0;

                var group = new DrawingGroup();

                var converted = ColorConverter.ConvertFromString(BackgroundColor);
                if (converted != null)
                {
                    var color = (Color)converted;
                    var brush = new SolidColorBrush(color);
                    var pen = new Pen(brush, 1);
                    var backgroundArea = new Rect(0, 0, width, width);
                    var rectabgle = new RectangleGeometry(backgroundArea);
                    var rectDrawing = new GeometryDrawing(brush, pen, rectabgle);
                    group.Children.Add(rectDrawing);

                    var imageArea = new Rect(x, y, image.Width, image.Height);
                    var imageDrawing = new ImageDrawing(image, imageArea);
                    group.Children.Add(imageDrawing);

                    // http://stackoverflow.com/questions/6676072/get-system-drawing-bitmap-of-a-wpf-area-using-visualbrush
                    var visual = new DrawingVisual();
                    var context = visual.RenderOpen();
                    context.DrawDrawing(group);
                    context.Close();
                    const int dpiScale100 = 96;
                    var bitmap = new RenderTargetBitmap(
                        Convert.ToInt32(width), Convert.ToInt32(height),
                        dpiScale100, dpiScale100,
                        PixelFormats.Pbgra32
                    );
                    bitmap.Render(visual);
                    return bitmap;
                }
                else
                {
                    var bitmap = new BitmapImage();
                    return bitmap;
                }
            }

            public override string ToString()
            {
                return $"{DisplayName}: {Description}";
            }
        }

        [Flags]
        private enum Stgm : uint
        {
            Read = 0x0,
            ShareExclusive = 0x10,
        }

        private enum Hresult : uint
        {
            Ok = 0x0000,
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern Hresult SHCreateStreamOnFileEx(string fileName, Stgm grfMode, uint attributes, bool create, IStream reserved, out IStream stream);
    }
}
