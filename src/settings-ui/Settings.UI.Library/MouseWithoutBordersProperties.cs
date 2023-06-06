// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
#pragma warning disable SA1649 // File name should match first type name
    public struct ConnectionRequest
#pragma warning restore SA1649 // File name should match first type name
    {
        public string PCName;
        public string SecurityKey;
    }

    public struct NewKeyGenerationRequest
    {
    }

    public class MouseWithoutBordersProperties : ICloneable
    {
        public StringProperty SecurityKey { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool UseService { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ShowOriginalUI { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool WrapMouse { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ShareClipboard { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool TransferFile { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool HideMouseAtScreenEdge { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool DrawMouseCursor { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ValidateRemoteMachineIP { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool SameSubnetOnly { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool BlockScreenSaverOnOtherMachines { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool MoveMouseRelatively { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool BlockMouseAtScreenCorners { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ShowClipboardAndNetworkStatusMessages { get; set; }

        public List<string> MachineMatrixString { get; set; }

        public StringProperty MachinePool { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool MatrixOneRow { get; set; }

        public IntProperty EasyMouse { get; set; }

        public IntProperty MachineID { get; set; }

        public IntProperty LastX { get; set; }

        public IntProperty LastY { get; set; }

        public IntProperty PackageID { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool FirstRun { get; set; }

        public IntProperty HotKeySwitchMachine { get; set; }

        public IntProperty HotKeyToggleEasyMouse { get; set; }

        public IntProperty HotKeyLockMachine { get; set; }

        public IntProperty HotKeyReconnect { get; set; }

        public IntProperty HotKeySwitch2AllPC { get; set; }

        public IntProperty TCPPort { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool DrawMouseEx { get; set; }

        public StringProperty Name2IP { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool FirstCtrlShiftS { get; set; }

        public StringProperty DeviceID { get; set; }

        public MouseWithoutBordersProperties()
        {
            SecurityKey = new StringProperty(string.Empty);
            WrapMouse = true;
            ShareClipboard = true;
            TransferFile = true;
            HideMouseAtScreenEdge = true;
            DrawMouseCursor = true;
            ValidateRemoteMachineIP = false;
            SameSubnetOnly = false;
            BlockScreenSaverOnOtherMachines = true;
            MoveMouseRelatively = false;
            BlockMouseAtScreenCorners = false;
            ShowClipboardAndNetworkStatusMessages = false;
            EasyMouse = new IntProperty(1);
            MachineMatrixString = new List<string>();
            DeviceID = new StringProperty(string.Empty);
            ShowOriginalUI = false;
            UseService = false;

            HotKeySwitchMachine = new IntProperty(0x70); // VK.F1
            HotKeyToggleEasyMouse = new IntProperty(0x45); // VK.E
            HotKeyLockMachine = new IntProperty(0x4C); // VK.L
            HotKeyReconnect = new IntProperty(0x52); // VK.R
            HotKeySwitch2AllPC = new IntProperty(0); // Disabled

            // These are internal, i.e. cannot be edited directly from UI
            MachinePool = ":,:,:,:";
            MatrixOneRow = true;
            MachineID = new IntProperty(0);
            LastX = new IntProperty(0);
            LastY = new IntProperty(0);
            PackageID = new IntProperty(0);
            FirstRun = false;
            TCPPort = new IntProperty(15100);
            DrawMouseEx = true;
            Name2IP = new StringProperty(string.Empty);
            FirstCtrlShiftS = false;
        }

        public object Clone()
        {
            var clone = new MouseWithoutBordersProperties();
            clone = this;
            return clone;
        }
    }
}
