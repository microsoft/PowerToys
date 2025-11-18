// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace AdvancedPaste.Services;

public interface IPromptModerationService
{
    Task ValidateAsync(string fullPrompt, CancellationToken cancellationToken);
}
