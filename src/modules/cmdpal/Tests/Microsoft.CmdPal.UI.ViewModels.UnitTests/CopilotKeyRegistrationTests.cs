// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class CopilotKeyRegistrationTests
{
    [TestMethod]
    public void Registration_SetsWindowMessageAndRemovesPropertyOnDispose()
    {
        var hwnd = CreateWindowExW(0, "STATIC", string.Empty, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.AreNotEqual(nint.Zero, hwnd);

        try
        {
            using var registration = new CopilotKeyRegistration(hwnd);
            var messageId = ReadRegistration(hwnd).MessageId;
            Assert.AreEqual(CopilotKeyRegistration.MessageId, messageId);
            Assert.IsTrue(messageId is >= 0x8000 and <= 0xBFFF);

            registration.Dispose();
            Assert.AreEqual(VARENUM.VT_EMPTY, ReadRegistration(hwnd).Type);

            registration.Dispose();
            Assert.AreEqual(VARENUM.VT_EMPTY, ReadRegistration(hwnd).Type);
        }
        finally
        {
            Assert.IsTrue(DestroyWindow(hwnd));
        }
    }

    [TestMethod]
    [Ignore("Suspended until property-write error assertions are portable across Windows builds.")]
    public void Registration_PropertyWriteFailureIsNotIgnored()
    {
        var hwnd = CreateWindowExW(0, "STATIC", string.Empty, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.AreNotEqual(nint.Zero, hwnd);

        try
        {
            var value = default(PROPVARIANT);
            value.Anonymous.Anonymous.vt = VARENUM.VT_ILLEGAL;
            var setter = typeof(CopilotKeyRegistration).GetMethod("SetWindowProperty", BindingFlags.Static | BindingFlags.NonPublic)!;

            var failure = Assert.ThrowsExactly<TargetInvocationException>(() => setter.Invoke(null, [hwnd, value]));

            Assert.IsInstanceOfType<COMException>(failure.InnerException);
            Assert.AreEqual(unchecked((int)0x80028CA0), failure.InnerException.HResult);
            Assert.AreEqual(VARENUM.VT_EMPTY, ReadRegistration(hwnd).Type);
        }
        finally
        {
            Assert.IsTrue(DestroyWindow(hwnd));
        }
    }

    [DataTestMethod]
    [DataRow("Package.appxmanifest")]
    [DataRow("Package-Dev.appxmanifest")]
    public void Manifest_SingleTapMatchesWindowMessageAndLaunchFallback(string manifestName)
    {
        var manifest = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Manifests", manifestName));
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
        XNamespace uap3 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/3";
        var extension = manifest.Descendants(uap3 + "AppExtension")
            .Single(element => (string?)element.Attribute("Name") == "com.microsoft.windows.copilotkeyprovider");
        var tap = extension.Element(uap3 + "Properties")!.Element(manifest.Root!.Name.Namespace + "SingleTap")!;

        Assert.AreEqual(CopilotKeyRegistration.SingleTap.ToString(CultureInfo.InvariantCulture), (string?)tap.Attribute("MessageWParam"));
        Assert.AreEqual("x-cmdpal://", tap.Value);
        var uri = new Uri(tap.Value);
        Assert.IsTrue(manifest.Descendants(uap + "Protocol").Any(element => (string?)element.Attribute("Name") == uri.Scheme));

        var protocolActivation = new CmdPalProtocolActivation(new SettingsLinkResolver());
        Assert.IsFalse(protocolActivation.TryParse(uri, out _), "The root URI must use the default summon path.");
    }

    private static unsafe (VARENUM Type, uint MessageId) ReadRegistration(nint hwnd)
    {
        var iid = IPropertyStore.IID_Guid;
        IPropertyStore* propertyStore = null;
        PInvoke.SHGetPropertyStoreForWindow((HWND)hwnd, &iid, (void**)&propertyStore).ThrowOnFailure();
        var value = default(PROPVARIANT);
        try
        {
            var key = new PROPERTYKEY
            {
                fmtid = new Guid("38652BCA-4329-4E74-86F9-39CF29345EEA"),
                pid = 2,
            };
            propertyStore->GetValue(&key, &value);

            // Windows can canonicalize the message ID to a string in the property store.
            Marshal.ThrowExceptionForHR(PropVariantToUInt32(&value, out var messageId));
            return (value.Anonymous.Anonymous.vt, messageId);
        }
        finally
        {
            _ = PropVariantClear(&value);
            propertyStore->Release();
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("propsys.dll", ExactSpelling = true)]
    private static extern unsafe int PropVariantToUInt32(PROPVARIANT* value, out uint result);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern unsafe int PropVariantClear(PROPVARIANT* value);
}
