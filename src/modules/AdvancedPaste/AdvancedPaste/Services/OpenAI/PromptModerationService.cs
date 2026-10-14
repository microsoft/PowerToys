// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ClientModel;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services;
using ManagedCommon;
using OpenAI.Moderations;

namespace AdvancedPaste.Services.OpenAI;

public sealed class PromptModerationService(IAICredentialsProvider aiCredentialsProvider, Func<bool> isModerationAllowed = null) : IPromptModerationService
{
    private const string ModelName = "omni-moderation-latest";

    private readonly IAICredentialsProvider _aiCredentialsProvider = aiCredentialsProvider;
    private readonly Func<bool> _isModerationAllowed = isModerationAllowed ?? (() => AdvancedPastePolicy.IsOpenAIModerationAllowed);

    public async Task ValidateAsync(string fullPrompt, CancellationToken cancellationToken)
    {
        if (!_isModerationAllowed())
        {
            Logger.LogWarning("Skipping OpenAI moderation because OpenAI or online AI is disabled by policy.");
            return;
        }

        try
        {
            _aiCredentialsProvider.Refresh();
            var apiKey = _aiCredentialsProvider.GetKey()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(apiKey))
            {
                Logger.LogWarning("Skipping OpenAI moderation because no credential is configured.");
                return;
            }

            ModerationClient moderationClient = new(ModelName, apiKey);
            var moderationClientResult = await moderationClient.ClassifyTextAsync(fullPrompt, cancellationToken);
            var moderationResult = moderationClientResult.Value;

            Logger.LogDebug($"{nameof(PromptModerationService)}.{nameof(ValidateAsync)} complete; {nameof(moderationResult.Flagged)}={moderationResult.Flagged}");

            if (moderationResult.Flagged)
            {
                throw new PasteActionModeratedException();
            }
        }
        catch (ClientResultException ex)
        {
            throw new PasteActionException(ErrorHelpers.TranslateErrorText(ex.Status), ex);
        }
    }
}
