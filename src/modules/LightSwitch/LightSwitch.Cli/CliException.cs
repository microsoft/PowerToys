// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace LightSwitch.Cli;

internal sealed class CliException : Exception
{
    internal CliException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    internal string Code { get; }
}
