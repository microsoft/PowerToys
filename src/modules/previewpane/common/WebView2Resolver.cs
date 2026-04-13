// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Microsoft.PowerToys.PreviewHandler
{
    /// <summary>
    /// Resolves the managed Microsoft.Web.WebView2.Core assembly for preview handlers.
    ///
    /// After flattening the WinUI3Apps subfolder, the native (WinRT) and managed versions of
    /// Microsoft.Web.WebView2.Core.dll share the same output folder. The native version (needed
    /// by WinUI3 apps like Peek) overwrites the managed version (needed by WPF/WinForms preview
    /// handlers). This resolver loads the managed copy shipped as a renamed file.
    /// </summary>
    public static class WebView2Resolver
    {
        private static bool _registered;

        /// <summary>
        /// Registers the assembly resolver. Must be called at the very start of Program.Main(),
        /// before any WebView2 types are accessed.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Register()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            AssemblyLoadContext.Default.Resolving += OnResolving;
        }

        private static Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name)
        {
            if (name.Name == "Microsoft.Web.WebView2.Core")
            {
                string managedPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Microsoft.Web.WebView2.Core.Managed.dll");

                if (File.Exists(managedPath))
                {
                    return context.LoadFromAssemblyPath(managedPath);
                }
            }

            return null;
        }
    }
}
