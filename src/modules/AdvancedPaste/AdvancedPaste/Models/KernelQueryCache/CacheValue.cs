// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace AdvancedPaste.Models.KernelQueryCache;

public record class CacheValue(List<ActionChainItem> ActionChain);
