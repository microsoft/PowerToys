// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Common;

namespace PreviewHandlerCommon
{
    /// <summary>
    /// Customized the WebBrowser to get control over what it downloads, displays and executes.
    /// </summary>
    public class WebBrowserExt : WebBrowser
    {
        /// <inheritdoc/>
        protected override WebBrowserSiteBase CreateWebBrowserSiteBase()
        {
            // Returns instance of WebBrowserSiteExt.
            return new WebBrowserSiteExt(this);
        }

        /// <summary>
        /// Extend the WebBrowserSite with IDispatch implementation to handle the DISPID_AMBIENT_DLCONTROL.
        /// More details: https://docs.microsoft.com/en-us/previous-versions/windows/internet-explorer/ie-developer/platform-apis/aa770041(v=vs.85)?redirectedfrom=MSDN#controlling-download-and-execution.
        /// </summary>
        protected class WebBrowserSiteExt : WebBrowserSite, IReflect
        {
            // Dispid of DISPID_AMBIENT_DLCONTROL is defined in MsHtmdid.h header file in distributed Windows Sdk component.
            private const string DISPIDAMBIENTDLCONTROL = "[DISPID=-5512]";
            private WebBrowserExt browserExtControl;

            /// <summary>
            /// Initializes a new instance of the <see cref="WebBrowserSiteExt"/> class.
            /// </summary>
            /// <param name="browserControl">Browser Control Instance pass to the site.</param>
            public WebBrowserSiteExt(WebBrowserExt browserControl)
                : base(browserControl)
            {
                this.browserExtControl = browserControl;
            }

            /// <inheritdoc/>
            public Type UnderlyingSystemType
            {
                get { return this.GetType(); }
            }

            /// <inheritdoc/>
            public object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters)
            {
                object result;

                if (name != null && name.Equals(DISPIDAMBIENTDLCONTROL, StringComparison.Ordinal))
                {
                    // Using InvariantCulture since this is used for web browser configurations
                    result = Convert.ToInt32(
                        WebBrowserDownloadControlFlags.DLIMAGES |
                        WebBrowserDownloadControlFlags.PRAGMA_NO_CACHE |
                        WebBrowserDownloadControlFlags.FORCEOFFLINE |
                        WebBrowserDownloadControlFlags.NO_CLIENTPULL |
                        WebBrowserDownloadControlFlags.NO_SCRIPTS |
                        WebBrowserDownloadControlFlags.NO_JAVA |
                        WebBrowserDownloadControlFlags.NO_FRAMEDOWNLOAD |
                        WebBrowserDownloadControlFlags.NOFRAMES |
                        WebBrowserDownloadControlFlags.NO_DLACTIVEXCTLS |
                        WebBrowserDownloadControlFlags.NO_RUNACTIVEXCTLS |
                        WebBrowserDownloadControlFlags.NO_BEHAVIORS |
                        WebBrowserDownloadControlFlags.SILENT, CultureInfo.InvariantCulture);
                }
                else
                {
                    result = GetType().InvokeMember(name, invokeAttr, binder, target, args, modifiers, culture, namedParameters);
                }

                return result;
            }

            /// <inheritdoc/>
            public FieldInfo[] GetFields(BindingFlags bindingAttr)
            {
                return this.GetType().GetFields(bindingAttr);
            }

            /// <inheritdoc/>
            public MethodInfo[] GetMethods(BindingFlags bindingAttr)
            {
                return this.GetType().GetMethods(bindingAttr);
            }

            /// <inheritdoc/>
            public PropertyInfo[] GetProperties(BindingFlags bindingAttr)
            {
                return this.GetType().GetProperties(bindingAttr);
            }

            /// <inheritdoc/>
            public FieldInfo GetField(string name, BindingFlags bindingAttr)
            {
                return this.GetType().GetField(name, bindingAttr);
            }

            /// <inheritdoc/>
            public MemberInfo[] GetMember(string name, BindingFlags bindingAttr)
            {
                return this.GetType().GetMember(name, bindingAttr);
            }

            /// <inheritdoc/>
            public MemberInfo[] GetMembers(BindingFlags bindingAttr)
            {
                return this.GetType().GetMembers(bindingAttr);
            }

            /// <inheritdoc/>
            public MethodInfo GetMethod(string name, BindingFlags bindingAttr)
            {
                return this.GetType().GetMethod(name, bindingAttr);
            }

            /// <inheritdoc/>
            public MethodInfo GetMethod(string name, BindingFlags bindingAttr, Binder binder, Type[] types, ParameterModifier[] modifiers)
            {
                return this.GetType().GetMethod(name, bindingAttr, binder, types, modifiers);
            }

            /// <inheritdoc/>
            public PropertyInfo GetProperty(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
            {
                return this.GetType().GetProperty(name, bindingAttr, binder, returnType, types, modifiers);
            }

            /// <inheritdoc/>
            public PropertyInfo GetProperty(string name, BindingFlags bindingAttr)
            {
                return this.GetType().GetProperty(name, bindingAttr);
            }
        }
    }
}
