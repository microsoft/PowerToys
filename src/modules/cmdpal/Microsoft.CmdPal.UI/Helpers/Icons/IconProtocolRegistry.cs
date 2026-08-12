// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal static class IconProtocolRegistry
{
    // This is deliberately immutable after type initialization. Protocol lookup is
    // used from the WinUI STA and loader workers, so it must not acquire a registry lock.
    // Explicit construction also keeps the registry visible to Native AOT without reflection.
    private static readonly IIconProtocolProcessor[] Processors = [];

    static IconProtocolRegistry()
    {
        ValidateProcessors(Processors);
    }

    public static IIconProtocolProcessor? Find(string? value) => Find(value, Processors);

    internal static IIconProtocolProcessor? Find(
        string? value,
        ReadOnlySpan<IIconProtocolProcessor> processors)
    {
        // Every registered protocol starts with '|'. This leaves ordinary glyphs and
        // paths—the overwhelmingly common inputs—at one predictable character check.
        if (string.IsNullOrEmpty(value) || value[0] != '|')
        {
            return null;
        }

        foreach (var processor in processors)
        {
            foreach (var prefix in processor.ProtocolPrefixes)
            {
                if (value.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return processor;
                }
            }
        }

        return null;
    }

    internal static void ValidateProcessors(ReadOnlySpan<IIconProtocolProcessor> processors)
    {
        List<string> declaredPrefixes = [];

        for (var processorIndex = 0; processorIndex < processors.Length; processorIndex++)
        {
            var prefixes = processors[processorIndex].ProtocolPrefixes;
            if (prefixes.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Icon protocol processor {processorIndex} declares no prefixes.");
            }

            for (var prefixIndex = 0; prefixIndex < prefixes.Length; prefixIndex++)
            {
                var prefix = prefixes[prefixIndex];
                if (string.IsNullOrEmpty(prefix) || prefix[0] != '|')
                {
                    throw new InvalidOperationException(
                        $"Icon protocol prefix {prefixIndex} on processor {processorIndex} must be non-empty and start with '|'.");
                }

                foreach (var declaredPrefix in declaredPrefixes)
                {
                    if (prefix.StartsWith(declaredPrefix, StringComparison.Ordinal) ||
                        declaredPrefix.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Icon protocol prefixes '{declaredPrefix}' and '{prefix}' overlap; routing would depend on declaration order.");
                    }
                }

                declaredPrefixes.Add(prefix);
            }
        }
    }
}
