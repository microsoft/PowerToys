// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Dedicated ResourceDictionary for dynamic overrides that win over base theme resources. Since
/// we can't use a key or name to identify the dictionary in Application resources, we use a dedicated type.
/// </summary>
internal sealed partial class MutableOverridesDictionary : ResourceDictionary;
