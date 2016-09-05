using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Management.Deployment;
using Windows.Storage.Streams;
using AppxPackaing;
using Shell;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using IStream = AppxPackaing.IStream;
using Rect = System.Windows.Rect;

namespace Wox.Plugin.Program.Programs
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

        public UWP(Package package)
        {
            Package = package;
            Name = Package.Id.Name;
            FullName = Package.Id.FullName;
            FamilyName = Package.Id.FamilyName;
            Location = Package.InstalledLocation.Path;
            Apps = MergedApps();
        }

        private Application[] AppInfos()
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
                var parsedApps = new List<Application>();
                while (apps.GetHasCurrent() != 0)
                {
                    var current = apps.GetCurrent();
                    var appListEntry = current.GetStringValue("AppListEntry");
                    if (appListEntry != "none")
                    {
                        var app = new Application
                        {
                            UserModelId = current.GetAppUserModelId(),
                            BackgroundColor = current.GetStringValue("BackgroundColor") ?? string.Empty,
                            Location = Location,
                            LogoPath = Application.LogoFromManifest(current, Location),
                            Valid = true // useless for now
                        };

                        if (!string.IsNullOrEmpty(app.UserModelId))
                        {
                            parsedApps.Add(app);
                        }
                    }
                    apps.MoveNext();
                }

                return parsedApps.ToArray();
            }
            else
            {
                return new Application[] { };
            }
        }

        private Application[] AppDisplayInfos()
        {
            IReadOnlyList<AppListEntry> apps;
            try
            {
                apps = Package.GetAppListEntriesAsync().AsTask().Result;
            }
            catch (Exception e)
            {
                var message = $"{e.Message} @ {Name}";
                Console.WriteLine(message);
                return new Application[] { };
            }

            var displayinfos = apps.Select(a =>
            {
                RandomAccessStreamReference logo;
                try
                {
                    // todo: which size is valid?
                    logo = a.DisplayInfo.GetLogo(new Size(44, 44));
                }
                catch (Exception e)
                {
                    var message = $"Can't get logo for {Name}";
                    Log.Error(message);
                    Log.Exception(e);
                    logo = RandomAccessStreamReference.CreateFromUri(new Uri(Constant.ErrorIcon));
                }
                var parsed = new Application
                {
                    DisplayName = a.DisplayInfo.DisplayName,
                    Description = a.DisplayInfo.Description,
                    LogoStream = logo
                };
                return parsed;
            }).ToArray();

            return displayinfos;
        }

        private Application[] MergedApps()
        {
            // todo can't find api, so just hard code it
            if (Location.Contains("SystemApps") || Location.Contains("WindowsApps"))
            {
                var infos = AppInfos();
                if (infos.Length > 0)
                {
                    var displayInfos = AppDisplayInfos();
                    var apps = infos;
                    // todo: temp hack for multipla application mismatch problem
                    // e.g. mail and calendar, skype video and messaging
                    // https://github.com/Wox-launcher/Wox/issues/198#issuecomment-244778783
                    var length = infos.Length;
                    for (int i = 0; i < length; i++)
                    {
                        var j = length - i - 1;
                        apps[i].DisplayName = displayInfos[j].DisplayName;
                        apps[i].Description = displayInfos[j].Description;
                        apps[i].LogoStream = displayInfos[j].LogoStream;
                    }
                    return apps;
                }
            }
            return new Application[] { };
        }

        public static Application[] All()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;
            if (support)
            {
                var applications = CurrentUserPackages().AsParallel().SelectMany(p => new UWP(p).Apps);
                applications = applications.Where(a => a.Valid);
                return applications.ToArray();
            }
            else
            {
                return new Application[] { };
            }
        }

        private static IEnumerable<Package> CurrentUserPackages()
        {
            var user = WindowsIdentity.GetCurrent().User;

            if (user != null)
            {
                var userSecurityId = user.Value;
                var packageManager = new PackageManager();
                var packages = packageManager.FindPackagesForUser(userSecurityId);
                packages = packages.Where(p => !p.IsFramework && !p.IsDevelopmentMode && !string.IsNullOrEmpty(p.InstalledLocation.Path));
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


        public class Application : IProgram
        {
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public RandomAccessStreamReference LogoStream { get; set; }
            public string UserModelId { get; set; }
            public string PublisherDisplayName { get; set; }
            public string BackgroundColor { get; set; }
            public string LogoPath { get; set; }

            public string Location { get; set; }
            public bool Valid { get; set; }

            private int Score(string query)
            {
                var score1 = StringMatcher.Score(DisplayName, query);
                var score2 = StringMatcher.ScoreForPinyin(DisplayName, query);
                var score3 = StringMatcher.Score(Description, query);
                var score = new[] { score1, score2, score3 }.Max();
                return score;
            }

            public Result Result(string query, IPublicAPI api)
            {
                var result = new Result
                {
                    SubTitle = Location,
                    Icon = Logo,
                    Score = Score(query),
                    ContextData = this,
                    Action = e =>
                    {
                        Launch(api);
                        return true;
                    }
                };

                if (Description.Length >= DisplayName.Length &&
                    Description.Substring(0, DisplayName.Length) == DisplayName)
                {
                    result.Title = Description;
                }
                else if (!string.IsNullOrEmpty(Description))
                {
                    result.Title = $"{DisplayName}: {Description}";
                }
                else
                {
                    result.Title = DisplayName;
                }
                return result;
            }

            public List<Result> ContextMenus(IPublicAPI api)
            {
                var contextMenus = new List<Result>
                {
                    new Result
                    {
                        Title = api.GetTranslation("wox_plugin_program_open_containing_folder"),
                        Action = _ =>
                        {
                            var hide = Main.StartProcess(new ProcessStartInfo(Location));
                            return hide;
                        },
                        IcoPath = "Images/folder.png"
                    }
                };
                return contextMenus;
            }

            private void Launch(IPublicAPI api)
            {
                try
                {
                    var appManager = new ApplicationActivationManager();
                    uint unusedPid;
                    const string noArgs = "";
                    const ACTIVATEOPTIONS noFlags = ACTIVATEOPTIONS.AO_NONE;
                    appManager.ActivateApplication(UserModelId, noArgs, noFlags, out unusedPid);
                }
                catch (Exception)
                {
                    var name = "Plugin: Program";
                    var message = $"Can't start UWP: {DisplayName}";
                    api.ShowMsg(name, message, string.Empty);
                }
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

            internal static string LogoFromManifest(IAppxManifestApplication application, string location)
            {
                // todo use hidpi logo when use hidpi screen
                var path1 = Path.Combine(location, application.GetStringValue("Square44x44Logo"));
                path1 = LogoFromPath(path1);
                if (!string.IsNullOrEmpty(path1))
                {
                    return path1;
                }
                else
                {
                    var path2 = Path.Combine(location, application.GetStringValue("Square150x150Logo"));
                    path2 = LogoFromPath(path2);
                    if (!string.IsNullOrEmpty(path2))
                    {
                        return path2;
                    }
                    else
                    {
                        return Constant.ErrorIcon;
                    }
                }
            }

            private static string LogoFromPath(string path)
            {
                if (!File.Exists(path))
                {
                    // https://msdn.microsoft.com/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets
                    var extension = Path.GetExtension(path);
                    if (!string.IsNullOrEmpty(extension))
                    {
                        var paths = new List<string>();
                        var end = path.Length - extension.Length;
                        var prefix = path.Substring(0, end);
                        // todo: remove hard cod scale
                        paths.Add($"{prefix}.scale-200{extension}");
                        paths.Add($"{prefix}.scale-100{extension}");

                        // hack for C:\Windows\ImmersiveControlPanel
                        var directory = Directory.GetParent(path).FullName;
                        var filename = Path.GetFileNameWithoutExtension(path);
                        prefix = Path.Combine(directory, "images", filename);
                        paths.Add($"{prefix}.scale-200{extension}");
                        paths.Add($"{prefix}.scale-100{extension}");

                        foreach (var p in paths)
                        {
                            if (File.Exists(p))
                            {
                                return p;
                            }
                        }
                        return string.Empty;
                    }
                    return string.Empty;
                }
                else
                {
                    // for js based application, e.g cut the rope
                    return path;
                }
            }

            private BitmapImage ImageFromPath(string path)
            {
                if (!File.Exists(path))
                {
                    // https://msdn.microsoft.com/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets

                    var extension = Path.GetExtension(path);
                    if (!string.IsNullOrEmpty(extension))
                    {
                        var paths = new List<string>();
                        var prefix = path.Substring(0, extension.Length);
                        // todo: remove hard cod scale
                        paths.Add($"{prefix}.scale-200{extension}");
                        paths.Add($"{prefix}.scale-100{extension}");
                        // hack for C:\Windows\ImmersiveControlPanel
                        var directory = Directory.GetParent(path).FullName;
                        var filename = Path.GetFileNameWithoutExtension(path);
                        prefix = Path.Combine(directory, "images", filename);
                        paths.Add($"{prefix}.scale-200{extension}");
                        paths.Add($"{prefix}.scale-100{extension}");
                        foreach (var p in paths)
                        {
                            if (File.Exists(p))
                            {
                                return new BitmapImage(new Uri(p));
                            }
                        }
                        return new BitmapImage(new Uri(Constant.ErrorIcon));
                    }
                    else
                    {
                        return new BitmapImage(new Uri(Constant.ErrorIcon));
                    }
                }
                else
                {
                    // for js based application, e.g cut the rope
                    var image = new BitmapImage(new Uri(path));
                    return image;
                }
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
                    Log.Exception(e);
                    return new BitmapImage(new Uri(Constant.ErrorIcon));
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
                    return new BitmapImage(new Uri(Constant.ErrorIcon));
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
            ShareExclusive = 0x10
        }

        private enum Hresult : uint
        {
            Ok = 0x0000
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern Hresult SHCreateStreamOnFileEx(string fileName, Stgm grfMode, uint attributes, bool create, IStream reserved, out IStream stream);
    }
}
