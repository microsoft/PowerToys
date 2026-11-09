// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Xml.Linq;

namespace Microsoft.MouseWithoutBorders.UITests;

internal static class SandboxConfiguration
{
    public static (string HostPath, string GuestPath, bool ReadOnly)[] EndpointMappings(EndpointChannel guest) =>
    [
        (guest.InputRoot, @"C:\MwbInput", true),
        (guest.OutputRoot, @"C:\MwbOutput", false),
    ];

    public static XDocument Create(
        string productArchive,
        string payloadRoot,
        string toolsRoot,
        EndpointChannel guest,
        bool legacyBootstrap)
    {
        static XElement Mapping(string source, string target, bool readOnly) =>
            new("MappedFolder", new XElement("HostFolder", source), new XElement("SandboxFolder", target), new XElement("ReadOnly", readOnly));

        var mappings = new XElement("MappedFolders");
        if (legacyBootstrap)
        {
            mappings.Add(
                Mapping(Path.GetDirectoryName(productArchive)!, @"C:\MwbArchive", true),
                Mapping(payloadRoot, @"C:\MwbPayload", true),
                Mapping(toolsRoot, @"C:\MwbTools", true));
            mappings.Add(EndpointMappings(guest).Select(item => Mapping(item.HostPath, item.GuestPath, item.ReadOnly)));
        }

        var configuration = new XElement(
            "Configuration",
            new XElement("VGpu", "Disable"),
            new XElement("MemoryInMB", 4096),
            new XElement("Networking", "Enable"),
            new XElement("ClipboardRedirection", "Disable"),
            new XElement("AudioInput", "Disable"),
            new XElement("VideoInput", "Disable"),
            new XElement("PrinterRedirection", "Disable"),
            mappings);
        if (legacyBootstrap)
        {
            var command = @"powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File C:\MwbPayload\EndpointWorker.ps1 -InputRoot C:\MwbInput -OutputRoot C:\MwbOutput -ProductRoot C:\MwbProduct -WinApp C:\MwbTools\winapp.exe -ProductArchive " +
                "\"C:\\MwbArchive\\" + Path.GetFileName(productArchive) + "\"";
            configuration.Add(new XElement("LogonCommand", new XElement("Command", command)));
        }

        return new XDocument(configuration);
    }
}
