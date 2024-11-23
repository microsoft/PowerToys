// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace AdvancedPaste.Models;

public class PasteActionException(string message, Exception innerException, string aiServiceMessage = null) : Exception(message, innerException)
{
    public string AIServiceMessage { get; } = aiServiceMessage;
}
