// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Awake.Core.Models
{
    public enum MessageFilterInfo : uint
    {
        None = 0,
        AlreadyAllowed = 1,
        AlreadyDisallowed = 2,
        AllowedHigher = 3,
    }
}
