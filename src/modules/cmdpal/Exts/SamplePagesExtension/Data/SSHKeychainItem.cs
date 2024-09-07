// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSHKeychainExtension.Data;

public sealed class SSHKeychainItem
{
    internal string HostName { get; init; }

    internal string EscapedHost => JsonEncodedText.Encode(HostName).ToString();
}
