// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Text;

public struct FuzzyTargetCache
{
    private string? _lastRaw;
    private uint _schemaId;
    private FuzzyTarget _target;

    public FuzzyTarget GetOrUpdate(IPrecomputedFuzzyMatcher matcher, string? raw)
    {
        raw ??= string.Empty;

        if (_schemaId == matcher.SchemaId && string.Equals(_lastRaw, raw, StringComparison.Ordinal))
        {
            return _target;
        }

        _target = matcher.PrecomputeTarget(raw);
        _schemaId = matcher.SchemaId;
        _lastRaw = raw;
        return _target;
    }

    public void Invalidate()
    {
        _lastRaw = null;
        _target = default;
        _schemaId = 0;
    }
}
