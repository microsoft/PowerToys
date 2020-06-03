using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Wox.Infrastructure;
using Microsoft.Plugin.Program.Logger;
using Rect = System.Windows.Rect;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wox.Plugin;
using System.Windows.Input;
using System.Runtime.InteropServices.ComTypes;
using Wox.Plugin.SharedCommands;
using System.Reflection;

namespace Microsoft.Plugin.Program.Programs
{
    [Serializable]
    public class UWP
    {
        public string Name { get; }
        public string FullName { get; }
        public string FamilyName { get; }
        public string Location { get; set; }

        public Application[] Apps { get; set; }

        public PackageVersion Version { get; set; }

        public UWP(Package package)
        {
            Location = package.InstalledLocation.Path;
            Name = package.Id.Name;
            FullName = package.Id.FullName;
            FamilyName = package.Id.FamilyName;
            InitializeAppInfo();
            Apps = Apps.Where(a =>
            {
                var valid =
                    !string.IsNullOrEmpty(a.UserModelId) &&
                    !string.IsNullOrEmpty(a.DisplayName);
                return valid;
            }).ToArray();
        }

        private void InitializeAppInfo()
        {
            AppxPackageHelper _helper = new AppxPackageHelper();
            var path = Path.Combine(Location, "AppxManifest.xml");

            var namespaces = XmlNamespaces(path);
            InitPackageVersion(namespaces);

            IStream stream;
            const uint noAttribute = 0x80;
            const Stgm exclusiveRead = Stgm.Read | Stgm.ShareExclusive;
            var hResult = SHCreateStreamOnFileEx(path, exclusiveRead, noAttribute, false, null, out stream);

            if (hResult == Hresult.Ok)
            {
                var apps = new List<Application>();
             
                List<AppxPackageHelper.IAppxManifestApplication> _apps = _helper.getAppsFromManifest(stream);
                foreach(var _app in _apps)
                {
                    var app = new Application(_app, this);
                    apps.Add(app);
                }
                
                Apps = apps.Where(a => a.AppListEntry != "none").ToArray();
            }
            else
            {
                var e = Marshal.GetExceptionForHR((int)hResult);
                ProgramLogger.LogException($"|UWP|InitializeAppInfo|{path}" +
                                                "|Error caused while trying to get the details of the UWP program", e);

                Apps = new List<Application>().ToArray();
            }
        }



        /// http://www.hanselman.com/blog/GetNamespacesFromAnXMLDocumentWithXPathDocumentAndLINQToXML.aspx
        private string[] XmlNamespaces(string path)
        {
            XDocument z = XDocument.Load(path);
            if (z.Root != null)
            {
                var namespaces = z.Root.Attributes().
                    Where(a => a.IsNamespaceDeclaration).
                    GroupBy(
                        a => a.Name.Namespace == XNamespace.None ? string.Empty : a.Name.LocalName,
                        a => XNamespace.Get(a.Value)
                    ).Select(
                        g => g.First().ToString()
                    ).ToArray();
                return namespaces;
            }
            else
            {
                ProgramLogger.LogException($"|UWP|XmlNamespaces|{path}" +
                                                $"|Error occured while trying to get the XML from {path}", new ArgumentNullException());

                return new string[] { };
            }
        }

        private void InitPackageVersion(string[] namespaces)
        {
            var versionFromNamespace = new Dictionary<string, PackageVersion>
            {
                {"http://schemas.microsoft.com/appx/manifest/foundation/windows10", PackageVersion.Windows10},
                {"http://schemas.microsoft.com/appx/2013/manifest", PackageVersion.Windows81},
                {"http://schemas.microsoft.com/appx/2010/manifest", PackageVersion.Windows8},
            };

            foreach (var n in versionFromNamespace.Keys)
            {
                if (namespaces.Contains(n))
                {
                    Version = versionFromNamespace[n];
                    return;
                }
            }

            ProgramLogger.LogException($"|UWP|XmlNamespaces|{Location}" +
                                                "|Trying to get the package version of the UWP program, but a unknown UWP appmanifest version  "
                                                + $"{FullName} from location {Location} is returned.", new FormatException());

            Version = PackageVersion.Unknown;
        }

        public static Application[] All()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;
            if (support)
            {
                var applications = CurrentUserPackages().AsParallel().SelectMany(p =>
                {
                    UWP u;
                    try
                    {
                        u = new UWP(p);
                    }
#if !DEBUG
                    catch (Exception e)
                    {
                        ProgramLogger.LogException($"|UWP|All|{p.InstalledLocation}|An unexpected error occured and "
                                                        + $"unable to convert Package to UWP for {p.Id.FullName}", e);
                        return new Application[] { };
                    }
#endif
#if DEBUG //make developer aware and implement handling
                    catch
                    {
                        throw;
                    }
#endif
                    return u.Apps;
                }).ToArray();

                var updatedListWithoutDisabledApps = applications
                                                        .Where(t1 => !Main._settings.DisabledProgramSources
                                                                        .Any(x => x.UniqueIdentifier == t1.UniqueIdentifier))
                                                        .Select(x => x);

                return updatedListWithoutDisabledApps.ToArray();
            }
            else
            {
                return new Application[] { };
            }
        }

        private static IEnumerable<Package> CurrentUserPackages()
        {
            var u = WindowsIdentity.GetCurrent().User;

            if (u != null)
            {
                var id = u.Value;
                var m = new PackageManager();
                var ps = m.FindPackagesForUser(id);
                ps = ps.Where(p =>
                {
                    bool valid;
                    try
                    {
                        var f = p.IsFramework;
                        var d = p.IsDevelopmentMode;
                        var path = p.InstalledLocation.Path;
                        valid = !f && !d && !string.IsNullOrEmpty(path);
                    }
                    catch (Exception e)
                    {
                        ProgramLogger.LogException("UWP" ,"CurrentUserPackages", $"id","An unexpected error occured and "
                                                   + $"unable to verify if package is valid", e);
                        return false;
                    }
                    
                    
                    return valid;
                });
                return ps;
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
            if (obj is UWP uwp)
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

        [Serializable]
        public class Application : IProgram
        {
            public string AppListEntry { get; set; }
            public string UniqueIdentifier { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string UserModelId { get; set; }
            public string BackgroundColor { get; set; }

            public string EntryPoint { get; set; }
            public string Name => DisplayName;
            public string Location => Package.Location;
            public bool Enabled { get; set; }
            public bool CanRunElevated {get;set;}

            public string LogoUri { get; set; }
            public string LogoPath { get; set; }
            public UWP Package { get; set; }

            private int Score(string query)
            {
                var displayNameMatch = StringMatcher.FuzzySearch(query, DisplayName);
                var descriptionMatch = StringMatcher.FuzzySearch(query, Description);
                var score = new[] { displayNameMatch.Score, descriptionMatch.Score }.Max();
                return score;
            }

            public Result Result(string query, IPublicAPI api)
            {
                var score = Score(query);
                if (score <= 0)
                { // no need to create result if score is 0
                    return null;
                }

                var result = new Result
                {
                    SubTitle = "Packaged application",
                    Icon = Logo,
                    Score = score,
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
                    result.TitleHighlightData = StringMatcher.FuzzySearch(query, Description).MatchData;
                }
                else
                {
                    result.Title = DisplayName;
                    result.TitleHighlightData = StringMatcher.FuzzySearch(query, DisplayName).MatchData;
                }
                return result;
            }

            public List<ContextMenuResult> ContextMenus(IPublicAPI api)
            {
                var contextMenus = new List<ContextMenuResult>();

                if (CanRunElevated)
                {
                    contextMenus.Add(
                            new ContextMenuResult
                            {
                                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                                Title = api.GetTranslation("wox_plugin_program_run_as_administrator"),
                                Glyph = "\xE7EF",
                                FontFamily = "Segoe MDL2 Assets",
                                AcceleratorKey = Key.Enter,
                                AcceleratorModifiers = (ModifierKeys.Control | ModifierKeys.Shift),
                                Action = _ =>
                                {
                                    string command = "shell:AppsFolder\\" + UniqueIdentifier;
                                    command.Trim();
                                    command = Environment.ExpandEnvironmentVariables(command);

                                    var info = ShellCommand.SetProcessStartInfo(command, verb: "runas");
                                    info.UseShellExecute = true;

                                    Process.Start(info);
                                    return true;
                                }
                            }
                        );
                }
               contextMenus.Add(
                   new ContextMenuResult
                   {
                       PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                       Title = api.GetTranslation("wox_plugin_program_open_containing_folder"),
                       Glyph = "\xE838",
                       FontFamily = "Segoe MDL2 Assets",
                       AcceleratorKey = Key.E,
                       AcceleratorModifiers = (ModifierKeys.Control | ModifierKeys.Shift),
                       Action = _ =>
                       {
                           Main.StartProcess(Process.Start, new ProcessStartInfo("explorer", Package.Location));

                           return true;
                       }
                   });
                
                return contextMenus;
            }

            private async void Launch(IPublicAPI api)
            {
                var appManager = new ApplicationActivationHelper.ApplicationActivationManager();
                uint unusedPid;
                const string noArgs = "";
                const ApplicationActivationHelper.ActivateOptions noFlags = ApplicationActivationHelper.ActivateOptions.None;
                await Task.Run(() =>
                {
                    try
                    {
                        appManager.ActivateApplication(UserModelId, noArgs, noFlags, out unusedPid);
                    }
                    catch (Exception)
                    {
                        var name = "Plugin: Program";
                        var message = $"Can't start UWP: {DisplayName}";
                        api.ShowMsg(name, message, string.Empty);
                    }
                });
            }

            public Application(AppxPackageHelper.IAppxManifestApplication manifestApp, UWP package)
            {
                // This is done because we cannot use the keyword 'out' along with a property
                string tmpUserModelId;
                string tmpUniqueIdentifier;
                string tmpDisplayName;
                string tmpDescription;
                string tmpBackgroundColor;
                string tmpEntryPoint;

                manifestApp.GetAppUserModelId(out tmpUserModelId);
                manifestApp.GetAppUserModelId(out tmpUniqueIdentifier);
                manifestApp.GetStringValue("DisplayName", out tmpDisplayName);
                manifestApp.GetStringValue("Description", out tmpDescription);
                manifestApp.GetStringValue("BackgroundColor", out tmpBackgroundColor);
                manifestApp.GetStringValue("EntryPoint", out tmpEntryPoint);

                UserModelId = tmpUserModelId;
                UniqueIdentifier = tmpUniqueIdentifier;
                DisplayName = tmpDisplayName;
                Description = tmpDescription;
                BackgroundColor = tmpBackgroundColor;
                EntryPoint = tmpEntryPoint;

                Package = package;

                DisplayName = ResourceFromPri(package.FullName, DisplayName);
                Description = ResourceFromPri(package.FullName, Description);
                LogoUri = LogoUriFromManifest(manifestApp);
                LogoPath = LogoPathFromUri(LogoUri);

                Enabled = true;
                CanRunElevated = IfApplicationcanRunElevated();
            }

            private bool IfApplicationcanRunElevated()
            {
                if (EntryPoint == "Windows.FullTrustApplication")
                {
                    return true;
                }
                else
                {
                    var manifest = Package.Location + "\\AppxManifest.xml";
                    if (File.Exists(manifest))
                    {
                        var file = File.ReadAllText(manifest);
                        if(file.Contains("TrustLevel=\"mediumIL\"", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            internal string ResourceFromPri(string packageFullName, string resourceReference)
            {
                const string prefix = "ms-resource:";
                if (!string.IsNullOrWhiteSpace(resourceReference) && resourceReference.StartsWith(prefix))
                {
                    // magic comes from @talynone
                    // https://github.com/talynone/Wox.Plugin.WindowsUniversalAppLauncher/blob/master/StoreAppLauncher/Helpers/NativeApiHelper.cs#L139-L153
                    string key = resourceReference.Substring(prefix.Length);
                    string parsed;
                    if (key.StartsWith("//"))
                    {
                        parsed = prefix + key;
                    }
                    else if (key.StartsWith("/"))
                    {
                        parsed = prefix + "//" + key;
                    }
                    else if (key.Contains("resources", StringComparison.OrdinalIgnoreCase))
                    {
                        parsed = prefix + key;
                    }
                    else
                    {
                        parsed = prefix + "///resources/" + key;
                    }

                    var outBuffer = new StringBuilder(128);
                    string source = $"@{{{packageFullName}? {parsed}}}";
                    var capacity = (uint)outBuffer.Capacity;
                    var hResult = SHLoadIndirectString(source, outBuffer, capacity, IntPtr.Zero);
                    if (hResult == Hresult.Ok)
                    {
                        var loaded = outBuffer.ToString();
                        if (!string.IsNullOrEmpty(loaded))
                        {
                            return loaded;
                        }
                        else
                        {
                            ProgramLogger.LogException($"|UWP|ResourceFromPri|{Package.Location}|Can't load null or empty result "
                                                        + $"pri {source} in uwp location {Package.Location}", new NullReferenceException());
                            return string.Empty;
                        }
                    }
                    else
                    {
                        // https://github.com/Wox-launcher/Wox/issues/964
                        // known hresult 2147942522:
                        // 'Microsoft Corporation' violates pattern constraint of '\bms-resource:.{1,256}'.
                        // for
                        // Microsoft.MicrosoftOfficeHub_17.7608.23501.0_x64__8wekyb3d8bbwe: ms-resource://Microsoft.MicrosoftOfficeHub/officehubintl/AppManifest_GetOffice_Description
                        // Microsoft.BingFoodAndDrink_3.0.4.336_x64__8wekyb3d8bbwe: ms-resource:AppDescription
                        var e = Marshal.GetExceptionForHR((int)hResult);
                        ProgramLogger.LogException($"|UWP|ResourceFromPri|{Package.Location}|Load pri failed {source} with HResult {hResult} and location {Package.Location}", e);
                        return string.Empty;
                    }
                }
                else
                {
                    return resourceReference;
                }
            }


            internal string LogoUriFromManifest(AppxPackageHelper.IAppxManifestApplication app)
            {
                var logoKeyFromVersion = new Dictionary<PackageVersion, string>
                {
                    { PackageVersion.Windows10, "Square44x44Logo" },
                    { PackageVersion.Windows81, "Square30x30Logo" },
                    { PackageVersion.Windows8, "SmallLogo" },
                };
                if (logoKeyFromVersion.ContainsKey(Package.Version))
                {
                    string logoUri;
                    var key = logoKeyFromVersion[Package.Version];
                    app.GetStringValue(key, out logoUri);
                    return logoUri;
                }
                else
                {
                    return string.Empty;
                }
            }

            internal string LogoPathFromUri(string uri)
            {
                // all https://msdn.microsoft.com/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets
                // windows 10 https://msdn.microsoft.com/en-us/library/windows/apps/dn934817.aspx
                // windows 8.1 https://msdn.microsoft.com/en-us/library/windows/apps/hh965372.aspx#target_size
                // windows 8 https://msdn.microsoft.com/en-us/library/windows/apps/br211475.aspx

                string path;
                if (uri.Contains("\\"))
                {
                    path = Path.Combine(Package.Location, uri);
                }
                else
                {
                    // for C:\Windows\MiracastView etc
                    path = Path.Combine(Package.Location, "Assets", uri);
                }

                var extension = Path.GetExtension(path);
                if (extension != null)
                {
                    var end = path.Length - extension.Length;
                    var prefix = path.Substring(0, end);
                    var paths = new List<string> { path };

                    // TODO: This value must be set in accordance to the WPF theme (work in progress).
                    // Must be set to `contrast-white` for light theme and to `contrast-black` for dark theme to get an icon of the contrasting color.
                    var theme = "contrast-black";

                    var scaleFactors = new Dictionary<PackageVersion, List<int>>
                    {
                        // scale factors on win10: https://docs.microsoft.com/en-us/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets#asset-size-tables,
                        { PackageVersion.Windows10, new List<int> { 100, 125, 150, 200, 400 } },
                        { PackageVersion.Windows81, new List<int> { 100, 120, 140, 160, 180 } },
                        { PackageVersion.Windows8, new List<int> { 100 } }
                    };

                    if (scaleFactors.ContainsKey(Package.Version))
                    {
                        foreach (var factor in scaleFactors[Package.Version])
                        {
                            paths.Add($"{prefix}.scale-{factor}{extension}");
                            paths.Add($"{prefix}.scale-{factor}_{theme}{extension}");
                            paths.Add($"{prefix}.{theme}_scale-{factor}{extension}");
                        }
                    }

                    var selected = paths.FirstOrDefault(File.Exists);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        return selected;
                    }
                    else
                    {
                        int appIconSize = 36;
                        var targetSizes = new List<int>{16, 24, 30, 36, 44, 60, 72, 96, 128, 180, 256}.AsParallel();
                        Dictionary<string, int> pathFactorPairs = new Dictionary<string, int>();

                        foreach (var factor in targetSizes)
                        {
                            string simplePath = $"{prefix}.targetsize-{factor}{extension}";
                            string suffixThemePath = $"{prefix}.targetsize-{factor}_{theme}{extension}";
                            string prefixThemePath = $"{prefix}.{theme}_targetsize-{factor}{extension}";

                            paths.Add(simplePath);
                            paths.Add(suffixThemePath);
                            paths.Add(prefixThemePath);

                            pathFactorPairs.Add(simplePath, factor);
                            pathFactorPairs.Add(suffixThemePath, factor);
                            pathFactorPairs.Add(prefixThemePath, factor);
                        }

                        var selectedIconPath = paths.OrderBy(x => Math.Abs(pathFactorPairs.GetValueOrDefault(x) - appIconSize)).FirstOrDefault(File.Exists);
                        if (!string.IsNullOrEmpty(selectedIconPath))
                        {
                            return selectedIconPath;
                        }
                        else
                        {
                            ProgramLogger.LogException($"|UWP|LogoPathFromUri|{Package.Location}" +
                                $"|{UserModelId} can't find logo uri for {uri} in package location: {Package.Location}", new FileNotFoundException());
                            return string.Empty;
                        }
                    }
                }
                else
                {
                    ProgramLogger.LogException($"|UWP|LogoPathFromUri|{Package.Location}" +
                                                    $"|Unable to find extension from {uri} for {UserModelId} " +
                                                    $"in package location {Package.Location}", new FileNotFoundException());
                    return string.Empty;
                }
            }

            public ImageSource Logo()
            {
                var logo = ImageFromPath(LogoPath);
                return logo;
            }


            private BitmapImage ImageFromPath(string path)
            {
                if (File.Exists(path))
                {
                    var image = new BitmapImage(new Uri(path));
                    return image;
                }
                else
                {
                    ProgramLogger.LogException($"|UWP|ImageFromPath|{path}" +
                                                    $"|Unable to get logo for {UserModelId} from {path} and" +
                                                    $" located in {Package.Location}", new FileNotFoundException());
                    return new BitmapImage(new Uri(Constant.ErrorIcon));
                }
            }

            public override string ToString()
            {
                return $"{DisplayName}: {Description}";
            }
        }

        public enum PackageVersion
        {
            Windows10,
            Windows81,
            Windows8,
            Unknown
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
        private static extern Hresult SHCreateStreamOnFileEx(string fileName, Stgm grfMode, uint attributes, bool create,
            IStream reserved, out IStream stream);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern Hresult SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf,
            IntPtr ppvReserved);
    }
}