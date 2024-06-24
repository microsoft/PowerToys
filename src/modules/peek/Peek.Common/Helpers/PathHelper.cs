// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Peek.Common.Helpers;

public static class PathHelper
{
    public static bool IsUncPath(string path) => Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && uri != null && uri.IsUnc;
}
