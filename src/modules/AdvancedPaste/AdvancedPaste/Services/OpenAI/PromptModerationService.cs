// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ClientModel;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using ManagedCommon;
using OpenAI.Moderations;

namespace AdvancedPaste.Services.OpenAI;

public sealed class PromptModerationService(IAICredentialsProvider aiCredentialsProvider) : IPromptModerationService
{
    private const string ModelName = "omni-moderation-latest";

    private readonly IAICredentialsProvider _aiCredentialsProvider = aiCredentialsProvider;

    public async Task ValidateAsync(string fullPrompt, CancellationToken cancellationToken)
    {
        try
        {
            ModerationClient moderationClient = new(ModelName, _aiCredentialsProvider.Key);
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
