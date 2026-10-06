// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CmdPal.UI.Helpers;

internal interface IIconProtocolProcessor
{
    IconCachePartition CachePartition { get; }

    ReadOnlySpan<string> ProtocolPrefixes { get; }

    /// <summary>Gets the normalized identity used to share equivalent protocol requests in the icon cache.</summary>
    string GetCacheIdentity(string value);

    /// <summary>
    /// Returns the context affecting this processor's output, or default to share across context changes.
    /// Processors whose output depends on theme or contrast must override this method to prevent sharing cache entries across those contexts.
    /// </summary>
    IconRenderContext GetCacheContext(string value, IconRenderContext context)
    {
        return default;
    }

    /// <summary>Classifies a claimed request so icon work is sent to the appropriate loader lane.</summary>
    IconLoadInputKind ClassifyInput(string value);

    /// <summary>Attempts protocol conversion without queuing asynchronous protocol work.</summary>
    /// <param name="value">The protocol request claimed by this processor.</param>
    /// <param name="targetSize">The requested pixel size.</param>
    /// <param name="context">The captured surface theme and contrast.</param>
    /// <param name="preparedIcon">The caller-owned prepared descriptor when conversion succeeds.</param>
    /// <returns>True when conversion finished synchronously; false when no synchronous result is available.</returns>
    bool TryPrepareSynchronously(
        string value,
        int targetSize,
        IconRenderContext context,
        [MaybeNullWhen(false)] out IconPathConverter.PreparedIcon preparedIcon);

    /// <summary>Prepares protocol artwork or ordered fallback descriptors on an icon worker.</summary>
    /// <param name="value">The protocol request claimed by this processor.</param>
    /// <param name="targetSize">The requested pixel size.</param>
    /// <param name="context">The captured surface theme and contrast.</param>
    /// <returns>Prepared artwork, ordered fallbacks, or an empty result when the request cannot be resolved.</returns>
    ValueTask<IconProtocolProcessingResult> PrepareAsync(
        string value,
        int targetSize,
        IconRenderContext context);
}
