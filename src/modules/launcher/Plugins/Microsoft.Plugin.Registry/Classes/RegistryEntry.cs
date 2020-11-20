// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Win32;

namespace Microsoft.Plugin.Registry.Classes
{
    /// <summary>
    /// A entry of the registry (can be a key or a value inside a key)
    /// </summary>
    internal class RegistryEntry
    {
        /// <summary>
        /// Gets the full path to a registry key
        /// </summary>
        internal string KeyPath { get; }

        /// <summary>
        /// Gets the registry key for this entry
        /// </summary>
        internal RegistryKey? Key { get; }

        /// <summary>
        /// Gets a possible exception that was occured when try to open this registry key (e.g. <see cref="UnauthorizedAccessException"/>)
        /// </summary>
        internal Exception? Exception { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryEntry"/> class.
        /// </summary>
        /// <param name="keyPath">The full path to the registry key for this entry</param>
        /// <param name="key">The <see cref="RegistryKey"/> for this entry</param>
        /// <param name="exception">(optional) A possible exception that was occured when try to access this registry key</param>
        public RegistryEntry(string keyPath, RegistryKey? key, Exception? exception = null)
        {
            KeyPath = keyPath;
            Key = key;
            Exception = exception;
        }
    }
}
