// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.MacroEngine;

public interface IAppScopeChecker
{
    bool IsForegroundAppMatch(string processName);
}
