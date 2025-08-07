// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ClientModel;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Settings;
using ManagedCommon;
using OpenAI;
using OpenAI.Moderations;

namespace AdvancedPaste.Services.OpenAI;

public sealed class PromptModerationService(IUserSettings userSettings, IAICredentialsProvider aiCredentialsProvider) : IPromptModerationService
{
    private readonly IUserSettings _userSettings = userSettings;

    private const string ModelName = "omni-moderation-latest";

    private readonly IAICredentialsProvider _aiCredentialsProvider = aiCredentialsProvider;

    public async Task ValidateAsync(string fullPrompt, CancellationToken cancellationToken)
    {
        if (_userSettings.DisableModeration)
        {
            Logger.LogDebug($"{nameof(PromptModerationService)}.{nameof(ValidateAsync)} skipped; moderation is disabled");
            return;
        }

        try
        {
            OpenAIClientOptions clientOptions = new();
            if (!string.IsNullOrEmpty(_userSettings.CustomEndpoint))
            {
                clientOptions.Endpoint = new Uri(_userSettings.CustomEndpoint);
            }

            ModerationClient moderationClient = new(ModelName, new(_aiCredentialsProvider.Key), clientOptions);
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
