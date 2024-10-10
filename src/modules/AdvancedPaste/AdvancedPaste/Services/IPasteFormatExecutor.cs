// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using AdvancedPaste.Models;

namespace AdvancedPaste.Services;

public interface IPasteFormatExecutor
{
    Task<string> ExecutePasteFormatAsync(PasteFormat pasteFormat, PasteActionSource source);
}
