// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

internal sealed class CustomDirectoryAppSource : DirectoryWin32ProgramSource
{
    private readonly ProgramSource _source;

    public CustomDirectoryAppSource(ProgramSource source, IReadOnlyList<string> suffixes)
        : base([source.Location], suffixes)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public override string Id => string.IsNullOrWhiteSpace(_source.UniqueIdentifier)
        ? $"custom:{_source.Location}"
        : $"custom:{_source.UniqueIdentifier}";

    public override int Priority => 0;

    public override bool IsEnabled => _source.Enabled;

    public override bool IncludeNonApps => true;
}
