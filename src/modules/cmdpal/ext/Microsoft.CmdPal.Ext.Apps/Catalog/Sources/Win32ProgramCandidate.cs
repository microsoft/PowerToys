// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>Pairs a discovery path with its scan-local search metadata.</summary>
internal readonly record struct Win32ProgramCandidate(string Path, IReadOnlyList<string> MatchTerms);
