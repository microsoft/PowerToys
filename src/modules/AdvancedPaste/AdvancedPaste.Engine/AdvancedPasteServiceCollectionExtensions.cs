// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;

using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedPaste.Services;

public static class AdvancedPasteServiceCollectionExtensions
{
    public static IServiceCollection AddAdvancedPasteEngine(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IUserSettings, UserSettings>();
        services.AddSingleton<IAICredentialsProvider, EnhancedVaultCredentialsProvider>();
        services.AddSingleton<IPromptModerationService, OpenAI.PromptModerationService>();
        services.AddSingleton<IKernelQueryCacheService, CustomActionKernelQueryCacheService>();
        services.AddSingleton<IPasteAIProviderFactory, PasteAIProviderFactory>();
        services.AddSingleton<ICustomActionTransformService, CustomActionTransformService>();
        services.AddSingleton<IKernelService, AdvancedAIKernelService>();
        services.AddSingleton<IPasteFormatExecutor, PasteFormatExecutor>();
        return services;
    }
}
