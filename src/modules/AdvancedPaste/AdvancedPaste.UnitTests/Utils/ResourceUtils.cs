// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace AdvancedPaste.UnitTests.Utils;

internal static class ResourceUtils
{
    internal static async Task<DataPackage> GetImageAssetAsDataPackageAsync(string resourceName)
    {
        var imageStreamRef = await ConvertToRandomAccessStreamReferenceAsync(GetImageResourceAsStream($"Assets/{resourceName}"));

        DataPackage package = new();
        package.SetBitmap(imageStreamRef);
        return package;
    }

    private static async Task<RandomAccessStreamReference> ConvertToRandomAccessStreamReferenceAsync(Stream stream)
    {
        InMemoryRandomAccessStream inMemoryStream = new();
        using var inputStream = stream.AsInputStream();
        await RandomAccessStream.CopyAsync(inputStream, inMemoryStream);

        inMemoryStream.Seek(0);
        return RandomAccessStreamReference.CreateFromStream(inMemoryStream);
    }

    private static Stream GetImageResourceAsStream(string filename)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyName = new AssemblyName(assembly.FullName ?? throw new InvalidOperationException());
        var resourceName = $"{assemblyName.Name}.{filename.Replace("/", ".")}";

        return assembly.GetManifestResourceNames().Contains(resourceName)
            ? assembly.GetManifestResourceStream(resourceName)
            : throw new InvalidOperationException($"Embedded resource '{resourceName}' does not exist.");
    }
}
