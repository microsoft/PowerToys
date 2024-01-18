// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditor.Controls
{
#pragma warning disable SA1622 // Generic type parameter documentation should have text
    /// <summary>
    /// Represents a method that handles general events.
    /// </summary>
    /// <typeparam name="TSender"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="sender">The event source.</param>
    /// <param name="args">The event data. If there is no event data, this parameter will be null.</param>
    public delegate void TypedEventHandler<TSender, TResult>(TSender sender, TResult args);
#pragma warning restore SA1622 // Generic type parameter documentation should have text
}
