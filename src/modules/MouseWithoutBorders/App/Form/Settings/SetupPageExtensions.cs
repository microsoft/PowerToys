using System;
using System.Windows.Forms;
using MouseWithoutBorders.Class;

namespace MouseWithoutBorders.Form.Settings
{
    internal static class SetupPageExtensions
    {
        public static TabPage CreateDiscoveryTab()
        {
            var tab = new TabPage("Discover Devices")
            {
                Name = "tabDiscovery",
                UseVisualStyleBackColor = true
            };

            var discoveryControl = new DeviceDiscoveryControl
            {
                Dock = DockStyle.Fill
            };

            var instructionLabel = new Label
            {
                Text = "Devices running Mouse Without Borders on your network will appear below.\n" +
                       "Double-click a device to connect automatically.",
                Dock = DockStyle.Top,
                Height = 50,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(10)
            };

            tab.Controls.Add(discoveryControl);
            tab.Controls.Add(instructionLabel);

            return tab;
        }

        public static void HandleDeviceSelection(DiscoveredDevice device, Form parentForm)
        {
            var result = MessageBox.Show(
                $"Connect to {device.DeviceName}?\n\n" +
                $"IP: {device.IpAddress}\n" +
                $"Version: {device.Version}\n\n" +
                "The device will be added to your configuration.",
                "Confirm Connection",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    Setting.Values.MyKey = Common.GenerateSecurityKey();
                    
                    var machineIndex = FindAvailableMachineSlot();
                    if (machineIndex >= 0)
                    {
                        Setting.Values.MachineMatrix[machineIndex].Name = device.DeviceName;
                        Setting.Values.MachineMatrix[machineIndex].IP = device.IpAddress;
                        Setting.Values.Save();
                        
                        MessageBox.Show(
                            $"Added {device.DeviceName} successfully!\n\n" +
                            $"Your security key: {Setting.Values.MyKey}\n\n" +
                            "Enter this key on the other device to complete setup.",
                            "Connection Info",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        
                        parentForm?.Close();
                    }
                    else
                    {
                        MessageBox.Show(
                            "Maximum number of devices reached (4).\n" +
                            "Remove a device before adding new ones.",
                            "Device Limit",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to connect: {ex.Message}",
                        "Connection Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Common.Log($"Device connection error: {ex}");
                }
            }
        }

        private static int FindAvailableMachineSlot()
        {
            for (int i = 0; i < Setting.Values.MachineMatrix.Length; i++)
            {
                if (string.IsNullOrEmpty(Setting.Values.MachineMatrix[i].Name))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
