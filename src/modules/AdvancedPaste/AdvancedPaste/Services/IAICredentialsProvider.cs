// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.Services;

public interface IAICredentialsProvider
{
    bool IsConfigured { get; }

    string Key { get; }

    bool Refresh();
}
