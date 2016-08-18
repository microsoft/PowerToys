using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage.Streams;
using AppxPackaing;
using Shell;
using Wox.Infrastructure.Logger;
using IStream = AppxPackaing.IStream;

namespace Wox.Plugin.Program.ProgramSources
{
    public class UWPApp
    {
        public string Name { get; }
        public string FullName { get; }
        public string FamilyName { get; }


        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Logo { get; set; } //todo
        public string PublisherDisplayName { get; set; }
        public string Location { get; set; }

        public Application[] Apps { get; set; }

        public Package Package { get; }

        public int Score { get; set; }



        public UWPApp(Package package)
        {
            Package = package;
            Name = Package.Id.Name;
            FullName = Package.Id.FullName;
            FamilyName = Package.Id.FamilyName;
            Location = Package.InstalledLocation.Path;

            InitializeAppDisplayInfo(package);
            InitializeAppInfo(package);
            Apps = Apps.Where(a =>
            {
                var valid = !string.IsNullOrEmpty(a.Executable) &&
                            !string.IsNullOrEmpty(a.UserModelId) &&
                            !string.IsNullOrEmpty(a.DisplayName);
                return valid;
            }).ToArray();
        }

        private void InitializeAppInfo(Package package)
        {
            var manifestPath = Path.Combine(Location, "AppxManifest.xml");
            var appxFactory = new AppxFactory();
            IStream manifestStream;
            var result = SHCreateStreamOnFileEx(
                manifestPath,
                Stgm.Read | Stgm.ShareExclusive,
                0,
                false,
                null,
                out manifestStream
            );

            if (result == Hresult.Ok)
            {
                var reader = appxFactory.CreateManifestReader(manifestStream);

                var properties = reader.GetProperties();
                Logo = properties.GetStringValue("Logo");
                Logo = Path.Combine(Location, Logo);
                PublisherDisplayName = properties.GetStringValue("PublisherDisplayName");
                DisplayName = properties.GetStringValue("DisplayName");
                Description = properties.GetStringValue("Description");

                var apps = reader.GetApplications();
                int i = 0;
                while (apps.GetHasCurrent() != 0 && i <= Apps.Length)
                {
                    var currentApp = apps.GetCurrent();
                    var userModelId = currentApp.GetAppUserModelId();
                    var executable = currentApp.GetStringValue("Executable");

                    Apps[i].Executable = executable;
                    Apps[i].UserModelId = userModelId;

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
                LogoStream = a.DisplayInfo.GetLogo(new Size(150, 150))
            }).ToArray();
        }

        public static List<UWPApp> All()
        {
            var packages = CurrentUserPackages();
            var uwps = new List<UWPApp>();
            Parallel.ForEach(packages, p =>
            {
                try
                {
                    var u = new UWPApp(p);
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
            var uwp = obj as UWPApp;
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

            // todo: wrap with try exception
            public void Launch()
            {
                var appManager = new ApplicationActivationManager();
                uint unusedPid;
                const string noArgs = "";
                const ACTIVATEOPTIONS noFlags = ACTIVATEOPTIONS.AO_NONE;
                appManager.ActivateApplication(UserModelId, noArgs, noFlags, out unusedPid);
            }


            public BitmapImage Logo()
            {
                IRandomAccessStreamWithContentType stream;
                try
                {
                    stream = LogoStream.OpenReadAsync().AsTask().Result;
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
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                // magic! don't know why UI thread can't own it
                image.Freeze();
                return image;
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
