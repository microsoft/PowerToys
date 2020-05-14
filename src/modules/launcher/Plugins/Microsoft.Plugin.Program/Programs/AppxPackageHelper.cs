using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Windows.Storage;

namespace Microsoft.Plugin.Program.Programs
{
    public class AppxPackageHelper
    {
        // This function returns a list of attributes of applications
        public List<IAppxManifestApplication> getAppsFromManifest(IStream stream)
        {
            List<IAppxManifestApplication> apps = new List<IAppxManifestApplication>();
            var appxFactory = new AppxFactory();
            var reader = ((IAppxFactory)appxFactory).CreateManifestReader(stream);
            var manifestApps = reader.GetApplications();
            while (manifestApps.GetHasCurrent())
            {
                string appListEntry;
                var manifestApp = manifestApps.GetCurrent();
                manifestApp.GetStringValue("AppListEntry", out appListEntry);
                if (appListEntry != "none")
                {
                    apps.Add(manifestApp);
                }
                manifestApps.MoveNext();
            }
            return apps;
        }

        // Reference : https://stackoverflow.com/questions/32122679/getting-icon-of-modern-windows-app-from-a-desktop-application
        [Guid("5842a140-ff9f-4166-8f5c-62f5b7b0c781"), ComImport]
        public class AppxFactory
        {
        }

        [Guid("BEB94909-E451-438B-B5A7-D79E767B75D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAppxFactory
        {
            void _VtblGap0_2(); // skip 2 methods
            IAppxManifestReader CreateManifestReader(IStream inputStream);
        }

        [Guid("4E1BD148-55A0-4480-A3D1-15544710637C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAppxManifestReader
        {
            void _VtblGap0_1(); // skip 1 method
            IAppxManifestProperties GetProperties();
            void _VtblGap1_5(); // skip 5 methods
            IAppxManifestApplicationsEnumerator GetApplications();
        }

        [Guid("9EB8A55A-F04B-4D0D-808D-686185D4847A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAppxManifestApplicationsEnumerator
        {
            IAppxManifestApplication GetCurrent();
            bool GetHasCurrent();
            bool MoveNext();
        }

        [Guid("5DA89BF4-3773-46BE-B650-7E744863B7E8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAppxManifestApplication
        {
            [PreserveSig]
            int GetStringValue([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] out string value);

            [PreserveSig]
            int GetAppUserModelId([MarshalAs(UnmanagedType.LPWStr)] out string value);
        }

        [Guid("03FAF64D-F26F-4B2C-AAF7-8FE7789B8BCA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAppxManifestProperties
        {
            [PreserveSig]
            int GetBoolValue([MarshalAs(UnmanagedType.LPWStr)]string name, out bool value);
            [PreserveSig]
            int GetStringValue([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] out string value);
        }
    }
}
