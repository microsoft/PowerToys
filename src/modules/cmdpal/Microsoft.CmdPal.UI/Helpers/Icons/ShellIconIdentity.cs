// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal readonly record struct ShellIconIdentity(
    ShellIconIdentityKind Kind,
    int SystemImageListIndex,
    string? ItemPath,
    bool Jumbo,
    int CacheGeneration)
{
    public static ShellIconIdentity FromSystemImageList(int index, bool jumbo, int cacheGeneration = 0) =>
        new(ShellIconIdentityKind.SystemImageList, index, null, jumbo, cacheGeneration);

    public static ShellIconIdentity FromItemThumbnail(string itemPath, bool jumbo, int cacheGeneration = 0) =>
        new(ShellIconIdentityKind.ItemThumbnail, 0, itemPath, jumbo, cacheGeneration);

    public static ShellIconIdentity FromItemPath(string itemPath, bool jumbo, int cacheGeneration = 0) =>
        new(ShellIconIdentityKind.ItemPath, 0, itemPath, jumbo, cacheGeneration);

    public ShellIconIdentity WithCacheGeneration(int cacheGeneration) =>
        this with { CacheGeneration = cacheGeneration };
}
