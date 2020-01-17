// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Common.ComInterlop;
using Microsoft.Win32;

namespace Common
{
    /// <summary>
    /// Preview Handler base class implmenenting interfaces required by Preview Handler.
    /// </summary>
    public abstract class PreviewHandlerBase : IPreviewHandler, IOleWindow, IObjectWithSite, IPreviewHandlerVisuals
    {
        /// <summary>
        /// An instance of Preview Control Used by the Handler.
        /// </summary>
        private IPreviewHandlerControl previewControl;

        /// <summary>
        /// Hold reference for the window handle.
        /// </summary>
        private IntPtr parentHwnd;

        /// <summary>
        /// Hold the bounds of the window.
        /// </summary>
        private Rectangle windowBounds;

        /// <summary>
        /// Holds the site pointer.
        /// </summary>
        private object unkSite;

        /// <summary>
        /// Holds reference for the IPreviewHandlerFrame.
        /// </summary>
        private IPreviewHandlerFrame frame;

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewHandlerBase"/> class.
        /// </summary>
        public PreviewHandlerBase()
        {
            this.previewControl = this.CreatePreviewHandlerControl();
        }

        /// <summary>
        /// Do the registeration of preview handler.
        /// </summary>
        /// <param name="t">Type of the class to register.</param>
        [ComRegisterFunction]
        public static void Register(Type t)
        {
            if (t != null && t.IsSubclassOf(typeof(PreviewHandlerBase)))
            {
                object[] attrs = (object[])t.GetCustomAttributes(typeof(PreviewHandlerAttribute), true);
                if (attrs != null && attrs.Length == 1)
                {
                    PreviewHandlerAttribute attr = attrs[0] as PreviewHandlerAttribute;
                    RegisterPreviewHandler(attr.Name, attr.Extension, t.GUID.ToString("B"), attr.AppId);
                }
            }
        }

        /// <summary>
        /// Do the unregisteration of preview handler.
        /// </summary>
        /// <param name="t">Type of the class to register.</param>
        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            if (t != null && t.IsSubclassOf(typeof(PreviewHandlerBase)))
            {
                object[] attrs = (object[])t.GetCustomAttributes(typeof(PreviewHandlerAttribute), true);
                if (attrs != null && attrs.Length == 1)
                {
                    PreviewHandlerAttribute attr = attrs[0] as PreviewHandlerAttribute;
                    UnregisterPreviewHandler(attr.Extension, t.GUID.ToString("B"), attr.AppId);
                }
            }
        }

        /// <inheritdoc />
        public abstract void DoPreview();

        /// <inheritdoc />
        public void SetWindow(IntPtr hwnd, ref RECT rect)
        {
            this.parentHwnd = hwnd;
            this.windowBounds = rect.ToRectangle();
            this.previewControl.SetWindow(hwnd, this.windowBounds);
        }

        /// <inheritdoc />
        public void SetRect(ref RECT rect)
        {
            this.windowBounds = rect.ToRectangle();
            this.previewControl.SetRect(this.windowBounds);
        }

        /// <inheritdoc />
        public void Unload()
        {
            this.previewControl.Unload();
        }

        /// <inheritdoc />
        public void SetFocus()
        {
            this.previewControl.SetFocus();
        }

        /// <inheritdoc />
        public void QueryFocus(out IntPtr phwnd)
        {
            this.previewControl.QueryFocus(out IntPtr result);
            phwnd = result;
            if (phwnd == IntPtr.Zero)
            {
                throw new Win32Exception();
            }
        }

        /// <inheritdoc />
        public uint TranslateAccelerator(ref MSG pmsg)
        {
            // Current implementation simply directs all Keystrokes to IPreviewHandlerFrame. This is the recommended approach to handle keystokes for all low-integrity preview handlers.
            // Source: https://docs.microsoft.com/en-us/windows/win32/shell/building-preview-handlers#ipreviewhandlertranslateaccelerator
            if (this.frame != null)
            {
                return this.frame.TranslateAccelerator(ref pmsg);
            }

            const uint S_FALSE = 1;
            return S_FALSE;
        }

        /// <inheritdoc />
        public void GetWindow(out IntPtr phwnd)
        {
            phwnd = this.previewControl.GetHandle();
        }

        /// <inheritdoc />
        public void ContextSensitiveHelp(bool fEnterMode)
        {
            // Should always return NotImplementedException. Source: https://docs.microsoft.com/en-us/windows/win32/shell/building-preview-handlers#iolewindowcontextsensitivehelp
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void SetSite(object pUnkSite)
        {
            // Implementation logic details: https://docs.microsoft.com/en-us/windows/win32/shell/building-preview-handlers#iobjectwithsitesetsite
            this.unkSite = pUnkSite;
            this.frame = this.unkSite as IPreviewHandlerFrame;
        }

        /// <inheritdoc />
        public void GetSite(ref Guid riid, out object ppvSite)
        {
            ppvSite = this.unkSite;
        }

        /// <inheritdoc />
        public void SetBackgroundColor(COLORREF color)
        {
            this.previewControl.SetBackgroundColor(color.Color);
        }

        /// <inheritdoc />
        public void SetFont(ref LOGFONT plf)
        {
            this.previewControl.SetFont(Font.FromLogFont(plf));
        }

        /// <inheritdoc />
        public void SetTextColor(COLORREF color)
        {
            this.previewControl.SetTextColor(color.Color);
        }

        /// <summary>
        /// Provide instance of the implementation of <see cref="IPreviewHandlerControl"/>. Should be overide by the implementation class with control object to be used.
        /// </summary>
        /// <returns>Instance of the <see cref="IPreviewHandlerControl"/>.</returns>
        protected abstract IPreviewHandlerControl CreatePreviewHandlerControl();

        private static void RegisterPreviewHandler(string name, string extensions, string previewerGuid, string appId)
        {
            // Create a new prevhost AppID so that this always runs in its own isolated process
            using (RegistryKey appIdsKey = Registry.ClassesRoot.OpenSubKey("AppID", true))
            using (RegistryKey appIdKey = appIdsKey.CreateSubKey(appId))
            {
                appIdKey.SetValue("DllSurrogate", @"%SystemRoot%\system32\prevhost.exe", RegistryValueKind.ExpandString);
            }

            // Add preview handler to preview handler list
            using (RegistryKey handlersKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers", true))
            {
                handlersKey.SetValue(previewerGuid, name, RegistryValueKind.String);
            }

            // Modify preview handler registration
            using (RegistryKey clsidKey = Registry.ClassesRoot.OpenSubKey("CLSID"))
            using (RegistryKey idKey = clsidKey.OpenSubKey(previewerGuid, true))
            {
                idKey.SetValue("DisplayName", name, RegistryValueKind.String);
                idKey.SetValue("AppID", appId, RegistryValueKind.String);
                idKey.SetValue("DisableLowILProcessIsolation", 1, RegistryValueKind.DWord); // optional, depending on what preview handler needs to be able to do
            }

            foreach (string extension in extensions.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // Set preview handler for specific extension
                using (RegistryKey extensionKey = Registry.ClassesRoot.CreateSubKey(extension))
                using (RegistryKey shellexKey = extensionKey.CreateSubKey("shellex"))
                using (RegistryKey previewKey = shellexKey.CreateSubKey("{8895b1c6-b41f-4c1c-a562-0d564250836f}"))
                {
                    previewKey.SetValue(null, previewerGuid, RegistryValueKind.String);
                }
            }
        }

        private static void UnregisterPreviewHandler(string extensions, string previewerGuid, string appId)
        {
            foreach (string extension in extensions.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                using (RegistryKey shellexKey = Registry.ClassesRoot.OpenSubKey(extension + "\\shellex", true))
                {
                    shellexKey.DeleteSubKey("{8895b1c6-b41f-4c1c-a562-0d564250836f}");
                }
            }

            using (RegistryKey appIdsKey = Registry.ClassesRoot.OpenSubKey("AppID", true))
            {
                appIdsKey.DeleteSubKey(appId);
            }

            using (RegistryKey classesKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers", true))
            {
                classesKey.DeleteValue(previewerGuid);
            }
        }
    }
}
