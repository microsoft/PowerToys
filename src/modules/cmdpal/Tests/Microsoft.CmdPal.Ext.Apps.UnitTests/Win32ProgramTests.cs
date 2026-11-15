// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class Win32ProgramTests
{
    [TestMethod]
    [DataRow(null, 0)]
    [DataRow("%SystemRoot%\\System32\\shell32.dll", 0)]
    [DataRow("%SystemRoot%\\System32\\shell32.dll", 3)]
    [DataRow("%SystemRoot%\\System32\\shell32.dll", -4)]
    [DataRow("C:\\Icons, custom\\APP.ICO", 0)]
    public void Shortcut_UsesIconLocationWithoutChangingLaunchPath(string iconPath, int iconIndex)
    {
        var shortcutPath = Path.Combine(Path.GetTempPath(), $"CmdPal-icon-{Guid.NewGuid():N}.lnk");
        var targetPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        try
        {
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = "/c echo shortcut";
            shortcut.WorkingDirectory = Environment.SystemDirectory;
            shortcut.Description = "Shortcut description";
            if (iconPath is not null)
            {
                shortcut.IconLocation = FormattableString.Invariant($"{iconPath},{iconIndex}");
            }

            shortcut.Save();

            var program = Win32Program.GetAppFromPath(shortcutPath);
            Assert.IsNotNull(program);
            Assert.IsTrue(program.Valid);
            Assert.AreEqual("Shortcut description", program.Description);
            var expectedIcon = iconPath is null
                ? targetPath
                : FormattableString.Invariant($"{Environment.ExpandEnvironmentVariables(iconPath)},{iconIndex}");
            Assert.IsTrue(string.Equals(expectedIcon, program.IcoPath, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(string.Equals(targetPath, program.FullPath, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(shortcutPath, program.LnkFilePath);

            Assert.AreEqual("/c echo shortcut", program.Arguments);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
            File.Delete(shortcutPath);
        }
    }
}
