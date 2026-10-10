// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>
/// Supplies the catalog's independently cacheable sources and reports when the configured source set changes.
/// </summary>
/// <remarks>
/// Returned source instances have stable identity and are owned by the consuming catalog. Providers own only the
/// configuration subscription used to construct and retain those instances.
/// </remarks>
internal interface IAppSourceProvider : IDisposable
{
    /// <summary>Raised after the provider atomically replaces its current source snapshot.</summary>
    event EventHandler? Changed;

    /// <summary>Gets an atomic snapshot of the currently configured sources.</summary>
    IReadOnlyList<IAppSource> GetSources();
}
