// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

internal readonly record struct CachedIconSourceProviderDiagnostics(
    string Name,
    Size IconSize,
    AdaptiveCacheStatistics CacheStatistics);
