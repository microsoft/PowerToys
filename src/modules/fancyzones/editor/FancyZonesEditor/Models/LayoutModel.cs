// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FancyZonesEditor.Models
{
    // Base LayoutModel
    //  Manages common properties and base persistence
    public abstract class LayoutModel : INotifyPropertyChanged
    {
        protected LayoutModel() { }

        protected LayoutModel(string name) : this()
        {
            Name = name;
        }

        protected LayoutModel(string name, ushort id) : this(name)
        {
            _id = id;
        }

        // Name - the display name for this layout model - is also used as the key in the registry
        public string Name
        {
            get
            {
                return _name;
            }

            set
            {
                if (_name != value)
                {
                    _name = value;
                    FirePropertyChanged("Name");
                }
            }
        }

        private string _name;

        // Id - the unique ID for this layout model - is used to connect fancy zones' ZonesSets with the editor's Layouts
        //    - note: 0 means this is a new layout, which means it will have its ID auto-assigned on persist
        public ushort Id
        {
            get
            {
                if (_id == 0)
                {
                    _id = ++s_maxId;
                }

                return _id;
            }
        }

        private ushort _id = 0;

        // IsSelected (not-persisted) - tracks whether or not this LayoutModel is selected in the picker
        // TODO: once we switch to a picker per monitor, we need to move this state to the view
        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }

            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    FirePropertyChanged("IsSelected");
                }
            }
        }

        private bool _isSelected;

        // implementation of INotifyProeprtyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        // FirePropertyChanged -- wrapper that calls INPC.PropertyChanged
        protected virtual void FirePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Removes this Layout from the registry and the loaded CustomModels list
        public void Delete()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(_registryPath, true);
            if (key != null)
            {
                key.DeleteValue(Name);
            }

            int i = s_customModels.IndexOf(this);
            if (i != -1)
            {
                s_customModels.RemoveAt(i);
            }
        }

        // Loads all the Layouts persisted under the Layouts key in the registry
        public static ObservableCollection<LayoutModel> LoadCustomModels()
        {
            s_customModels = new ObservableCollection<LayoutModel>();

            RegistryKey key = Registry.CurrentUser.OpenSubKey(_registryPath);
            if (key != null)
            {
                foreach (string name in key.GetValueNames())
                {
                    LayoutModel model = null;
                    byte[] data = (byte[])Registry.GetValue(_fullRegistryPath, name, null);

                    ushort version = (ushort)((data[0] * 256) + data[1]);
                    byte type = data[2];
                    ushort id = (ushort)((data[3] * 256) + data[4]);

                    switch (type)
                    {
                        case 0: model = new GridLayoutModel(version, name, id, data); break;
                        case 1: model = new CanvasLayoutModel(version, name, id, data); break;
                    }

                    if (model != null)
                    {
                        if (s_maxId < id)
                        {
                            s_maxId = id;
                        }

                        s_customModels.Add(model);
                    }
                }
            }

            return s_customModels;
        }

        private static ObservableCollection<LayoutModel> s_customModels = null;

        private static ushort s_maxId = 0;

        // Callbacks that the base LayoutModel makes to derived types
        protected abstract byte[] GetPersistData();

        public abstract LayoutModel Clone();

        // PInvokes to handshake with fancyzones backend
        internal static class Native
        {
            [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
            public static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)]string lpFileName);

            [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

            internal delegate int PersistZoneSet(
                [MarshalAs(UnmanagedType.LPWStr)] string activeKey,
                [MarshalAs(UnmanagedType.LPWStr)] string resolutionKey,
                uint monitor,
                ushort layoutId,
                int zoneCount,
                [MarshalAs(UnmanagedType.LPArray)] int[] zoneArray);
        }

        public void Persist(System.Windows.Int32Rect[] zones)
        {
            // Persist the editor data
            Registry.SetValue(_fullRegistryPath, Name, GetPersistData(), Microsoft.Win32.RegistryValueKind.Binary);
            Apply(zones);
        }

        public void Apply(System.Windows.Int32Rect[] zones)
        {
            // Persist the zone data back into FZ
            var module = Native.LoadLibrary("fancyzones.dll");
            if (module == IntPtr.Zero)
            {
                return;
            }

            var pfn = Native.GetProcAddress(module, "PersistZoneSet");
            if (pfn == IntPtr.Zero)
            {
                return;
            }

            // Scale all the zones to the DPI and then pack them up to be marshalled.
            int zoneCount = zones.Length;
            var zoneArray = new int[zoneCount * 4];
            for (int i = 0; i < zones.Length; i++)
            {
                var left = (int)(zones[i].X * Settings.Dpi);
                var top = (int)(zones[i].Y * Settings.Dpi);
                var right = left + (int)(zones[i].Width * Settings.Dpi);
                var bottom = top + (int)(zones[i].Height * Settings.Dpi);

                var index = i * 4;
                zoneArray[index] = left;
                zoneArray[index + 1] = top;
                zoneArray[index + 2] = right;
                zoneArray[index + 3] = bottom;
            }

            var persistZoneSet = Marshal.GetDelegateForFunctionPointer<Native.PersistZoneSet>(pfn);
            persistZoneSet(Settings.UniqueKey, Settings.WorkAreaKey, Settings.Monitor, _id, zoneCount, zoneArray);
        }

        private static readonly string _registryPath = Settings.RegistryPath + "\\Layouts";
        private static readonly string _fullRegistryPath = Settings.FullRegistryPath + "\\Layouts";
    }
}
