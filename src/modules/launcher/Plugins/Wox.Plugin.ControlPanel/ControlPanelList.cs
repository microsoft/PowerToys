using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Wox.Plugin.ControlPanel
{
    //from:https://raw.githubusercontent.com/CoenraadS/Windows-Control-Panel-Items
    public static class ControlPanelList
    {
        private const uint GROUP_ICON = 14;
        private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        private const string CONTROL = @"%SystemRoot%\System32\control.exe";

        private delegate bool EnumResNameDelegate(
        IntPtr hModule,
        IntPtr lpszType,
        IntPtr lpszName,
        IntPtr lParam);

        [DllImport("kernel32.dll", EntryPoint = "EnumResourceNamesW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool EnumResourceNamesWithID(IntPtr hModule, uint lpszType, EnumResNameDelegate lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int LoadString(IntPtr hInstance, uint uID, StringBuilder lpBuffer, int nBufferMax);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr LoadImage(IntPtr hinst, IntPtr lpszName, uint uType,
        int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        [DllImport("kernel32.dll")]
        static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

        static IntPtr defaultIconPtr;


        static RegistryKey nameSpace = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\ControlPanel\\NameSpace");
        static RegistryKey clsid = Registry.ClassesRoot.OpenSubKey("CLSID");

        public static List<ControlPanelItem> Create(uint iconSize)
        {
            int size = (int)iconSize;
            RegistryKey currentKey;
            ProcessStartInfo executablePath;
            List<ControlPanelItem> controlPanelItems = new List<ControlPanelItem>();
            string localizedString;
            string infoTip;
            Icon myIcon;

            foreach (string key in nameSpace.GetSubKeyNames())
            {
                currentKey = clsid.OpenSubKey(key);
                if (currentKey != null)
                {
                    executablePath = getExecutablePath(currentKey);

                    if (!(executablePath == null)) //Cannot have item without executable path
                    {
                        localizedString = getLocalizedString(currentKey);

                        if (!string.IsNullOrEmpty(localizedString))//Cannot have item without Title
                        {
                            infoTip = getInfoTip(currentKey);

                            myIcon = getIcon(currentKey, size);

                            controlPanelItems.Add(new ControlPanelItem(localizedString, infoTip, key, executablePath, myIcon));
                        }
                    }
                }
            }

            return controlPanelItems;
        }

        private static ProcessStartInfo getExecutablePath(RegistryKey currentKey)
        {
            ProcessStartInfo executablePath = new ProcessStartInfo();
            string applicationName;

            if (currentKey.GetValue("System.ApplicationName") != null)
            {
                //CPL Files (usually native MS items)
                applicationName = currentKey.GetValue("System.ApplicationName").ToString();
                executablePath.FileName = Environment.ExpandEnvironmentVariables(CONTROL);
                executablePath.Arguments = "-name " + applicationName;
            }
            else if (currentKey.OpenSubKey("Shell\\Open\\Command") != null && currentKey.OpenSubKey("Shell\\Open\\Command").GetValue(null) != null)
            {
                //Other files (usually third party items)
                string input = "\"" + Environment.ExpandEnvironmentVariables(currentKey.OpenSubKey("Shell\\Open\\Command").GetValue(null).ToString()) + "\"";
                executablePath.FileName = "cmd.exe";
                executablePath.Arguments = "/C " + input;
                executablePath.WindowStyle = ProcessWindowStyle.Hidden;   
            }
            else
            {
                return null;
            }

            return executablePath;
        }

        private static string getLocalizedString(RegistryKey currentKey)
        {
            IntPtr dataFilePointer;
            string[] localizedStringRaw;
            uint stringTableIndex;
            StringBuilder resource;
            string localizedString;

            if (currentKey.GetValue("LocalizedString") != null)
            {
                localizedStringRaw = currentKey.GetValue("LocalizedString").ToString().Split(new[] { ",-" }, StringSplitOptions.None);

                if (localizedStringRaw.Length > 1)
                {
                    if (localizedStringRaw[0][0] == '@')
                    {
                        localizedStringRaw[0] = localizedStringRaw[0].Substring(1);
                    }

                    localizedStringRaw[0] = Environment.ExpandEnvironmentVariables(localizedStringRaw[0]);

                    dataFilePointer = LoadLibraryEx(localizedStringRaw[0], IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE); //Load file with strings

                    stringTableIndex = sanitizeUint(localizedStringRaw[1]);

                    resource = new StringBuilder(255);
                    LoadString(dataFilePointer, stringTableIndex, resource, resource.Capacity + 1); //Extract needed string
                    FreeLibrary(dataFilePointer);

                    localizedString = resource.ToString();

                    //Some apps don't return a string, although they do have a stringIndex. Use Default value.

                    if (String.IsNullOrEmpty(localizedString))
                    {
                        if (currentKey.GetValue(null) != null)
                        {
                            localizedString = currentKey.GetValue(null).ToString();
                        }
                        else
                        {
                            return null; //Cannot have item without title.
                        }
                    }
                }
                else
                {
                    localizedString = localizedStringRaw[0];
                }
            }
            else if (currentKey.GetValue(null) != null)
            {
                localizedString = currentKey.GetValue(null).ToString();
            }
            else
            {
                return null; //Cannot have item without title.
            }
            return localizedString;
        }

        private static string getInfoTip(RegistryKey currentKey)
        {
            IntPtr dataFilePointer;
            string[] infoTipRaw;
            uint stringTableIndex;
            StringBuilder resource;
            string infoTip = "";

            if (currentKey.GetValue("InfoTip") != null)
            {
                infoTipRaw = currentKey.GetValue("InfoTip").ToString().Split(new[] { ",-" }, StringSplitOptions.None);

                if (infoTipRaw.Length == 2)
                {
                    if (infoTipRaw[0][0] == '@')
                    {
                        infoTipRaw[0] = infoTipRaw[0].Substring(1);
                    }
                    infoTipRaw[0] = Environment.ExpandEnvironmentVariables(infoTipRaw[0]);

                    dataFilePointer = LoadLibraryEx(infoTipRaw[0], IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE); //Load file with strings

                    stringTableIndex = sanitizeUint(infoTipRaw[1]);

                    resource = new StringBuilder(255);
                    LoadString(dataFilePointer, stringTableIndex, resource, resource.Capacity + 1); //Extract needed string
                    FreeLibrary(dataFilePointer);

                    infoTip = resource.ToString();
                }
                else
                {
                    infoTip = currentKey.GetValue("InfoTip").ToString();
                }
            }
            else
            {
                infoTip = "";
            }

            return infoTip;
        }

        private static Icon getIcon(RegistryKey currentKey, int iconSize)
        {
            IntPtr iconPtr = IntPtr.Zero;
            List<string> iconString;
            IntPtr dataFilePointer;
            IntPtr iconIndex;
            Icon myIcon = null;

            if (currentKey.OpenSubKey("DefaultIcon") != null)
            {
                if (currentKey.OpenSubKey("DefaultIcon").GetValue(null) != null)
                {
                    iconString = new List<string>(currentKey.OpenSubKey("DefaultIcon").GetValue(null).ToString().Split(new[] { ',' }, 2));
                    if (string.IsNullOrEmpty(iconString[0]))
                    {
                        // fallback to default icon
                        return null;
                    }

                    if (iconString[0][0] == '@')
                    {
                        iconString[0] = iconString[0].Substring(1);
                    }

                    dataFilePointer = LoadLibraryEx(iconString[0], IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);

                    if (iconString.Count == 2)
                    {
                        iconIndex = (IntPtr)sanitizeUint(iconString[1]);

                        iconPtr = LoadImage(dataFilePointer, iconIndex, 1, iconSize, iconSize, 0);
                    }

                    if (iconPtr == IntPtr.Zero)
                    {
                        defaultIconPtr = IntPtr.Zero;
                        EnumResourceNamesWithID(dataFilePointer, GROUP_ICON, EnumRes, IntPtr.Zero); //Iterate through resources. 

                        iconPtr = LoadImage(dataFilePointer, defaultIconPtr, 1, iconSize, iconSize, 0);
                    }

                    FreeLibrary(dataFilePointer);

                    if (iconPtr != IntPtr.Zero)
                    {
                        try
                        {
                            myIcon = Icon.FromHandle(iconPtr);
                            myIcon = (Icon)myIcon.Clone(); //Remove pointer dependancy.
                        }
                        catch
                        {
                            //Silently fail for now.
                        }
                    }
                }
            }

            if (iconPtr != IntPtr.Zero)
            {
                DestroyIcon(iconPtr);
            }
            return myIcon;
        }

        private static uint sanitizeUint(string args) //Remove all chars before and after first set of digits.
        {
            int x = 0;

            while (x < args.Length && !Char.IsDigit(args[x]))
            {
                args = args.Substring(1);
            }

            x = 0;

            while (x < args.Length && Char.IsDigit(args[x]))
            {
                x++;
            }

            if (x < args.Length)
            {
                args = args.Remove(x);
            }

            /*If the logic is correct, this should never through an exception.
             * If there is an exception, then need to analyze what the input is.
             * Returning the wrong number will cause more errors */
            return Convert.ToUInt32(args);
        }

        private static bool IS_INTRESOURCE(IntPtr value)
        {
            if (((uint)value) > ushort.MaxValue)
                return false;
            return true;
        }

        private static uint GET_RESOURCE_ID(IntPtr value)
        {
            if (IS_INTRESOURCE(value))
                return (uint)value;
            throw new NotSupportedException("value is not an ID!");
        }

        private static string GET_RESOURCE_NAME(IntPtr value)
        {
            if (IS_INTRESOURCE(value))
                return value.ToString();
            return Marshal.PtrToStringUni(value);
        }

        private static bool EnumRes(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam)
        {
            defaultIconPtr = lpszName;
            return false;
        }
    }
}
