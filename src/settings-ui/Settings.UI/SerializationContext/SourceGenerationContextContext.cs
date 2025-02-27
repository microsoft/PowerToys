// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.SerializationContext;

[JsonSerializable(typeof(WINDOWPLACEMENT))]
[JsonSerializable(typeof(AdvancedPasteSettings))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
[JsonSerializable(typeof(AlwaysOnTopSettings))]
[JsonSerializable(typeof(ColorPickerSettings))]
[JsonSerializable(typeof(CropAndLockSettings))]
[JsonSerializable(typeof(FileLocksmithSettings))]
[JsonSerializable(typeof(MeasureToolSettings))]
[JsonSerializable(typeof(MouseWithoutBordersSettings))]
[JsonSerializable(typeof(NewPlusSettings))]
[JsonSerializable(typeof(PeekSettings))]
[JsonSerializable(typeof(PowerLauncherSettings))]
[JsonSerializable(typeof(PowerOcrSettings))]
[JsonSerializable(typeof(RegistryPreviewSettings))]
[JsonSerializable(typeof(WorkspacesSettings))]
[JsonSerializable(typeof(IList<PowerToysReleaseInfo>))]
[JsonSerializable(typeof(ActionMessage))]
public sealed partial class SourceGenerationContextContext : JsonSerializerContext
{
}
