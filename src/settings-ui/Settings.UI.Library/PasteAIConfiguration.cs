// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Legacy name retained for Advanced Paste settings JSON compatibility during migration.
    /// </summary>
    [Obsolete("Use AIConfiguration instead.")]
    public class PasteAIConfiguration : AIConfiguration
    {
    }
}
