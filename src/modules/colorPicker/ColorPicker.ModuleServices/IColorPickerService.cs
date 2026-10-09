// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.ModuleContracts;

namespace ColorPicker.ModuleServices;

public interface IColorPickerService : IModuleService
{
    Task<OperationResult> OpenPickerAsync(CancellationToken cancellationToken = default);

    Task<OperationResult<IReadOnlyList<SavedColor>>> GetSavedColorsAsync(CancellationToken cancellationToken = default);
}
