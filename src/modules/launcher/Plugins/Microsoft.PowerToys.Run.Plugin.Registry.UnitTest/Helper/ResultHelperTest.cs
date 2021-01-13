// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Run.Plugin.Registry.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.Registry.UnitTest.Helper
{
    [TestClass]
    public sealed class ResultHelperTest
    {
        [TestMethod]
        [DataRow(@"HKEY_CLASSES_ROOT\*\OpenWithList", @"HKEY_CLASSES_ROOT\*\OpenWithList")]
        [DataRow(@"HKEY_CURRENT_USER\Control Panel\Accessibility", @"HKEY_CURRENT_USER\Control Panel\Accessibility")]
        [DataRow(@"HKEY_LOCAL_MACHINE\HARDWARE\UEFI", @"HKEY_LOCAL_MACHINE\HARDWARE\UEFI")]
        [DataRow(@"HKEY_USERS\.DEFAULT\Environment", @"HKEY_USERS\.DEFAULT\Environment")]
        [DataRow(@"HKCC\System\CurrentControlSet\Control", @"HKEY_CURRENT_CONFIG\System\CurrentControlSet\Control")]
        [DataRow(@"HKEY_PERFORMANCE_DATA\???", @"HKEY_PERFORMANCE_DATA\???")]
        [DataRow(@"HKCR\*\shell\Open with VS Code\command", @"HKEY_CLASSES_ROOT\*\shell\Open with VS Code\command")]
        [DataRow(@"...ndows\CurrentVersion\Explorer\StartupApproved", @"HKEY_CURRENT_USER\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved")]
        [DataRow(@"...p\Upgrade\NetworkDriverBackup\Control\Network", @"HKEY_LOCAL_MACHINE\SYSTEM\Setup\Upgrade\NetworkDriverBackup\Control\Network")]
        [DataRow(@"...anel\International\User Profile System Backup", @"HKEY_USERS\.DEFAULT\Control Panel\International\User Profile System Backup")]
        [DataRow(@"...stem\CurrentControlSet\Control\Print\Printers", @"HKEY_CURRENT_CONFIG\System\CurrentControlSet\Control\Print\Printers")]
        public void GetTruncatedTextTest(string registryKeyShort, string registryKeyFull)
        {
            Assert.AreEqual(registryKeyShort, ResultHelper.GetTruncatedText(registryKeyFull, 45));
        }
    }
}
