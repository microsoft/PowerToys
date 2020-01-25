// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Common
{
    /// <summary>
    /// Attribute class for Preview Handler used for registration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class PreviewHandlerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewHandlerAttribute"/> class.
        /// </summary>
        /// <param name="name">Name of the Preview Handler.</param>
        /// <param name="extension">File extension type.</param>
        /// <param name="appId">AppId Guid used for the process in which handler is created.</param>
        public PreviewHandlerAttribute(string name, string extension, string appId)
        {
            this.Name = name ?? throw new ArgumentNullException("name");
            this.Extension = extension ?? throw new ArgumentNullException("extension");
            this.AppId = appId ?? throw new ArgumentNullException("appId");
        }

        /// <summary>
        /// Gets the Name of the handler.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the Extension type of the handler.
        /// </summary>
        public string Extension { get; private set; }

        /// <summary>
        /// Gets the App Id for the Preview Handler.
        /// </summary>
        public string AppId { get; private set; }
    }
}
