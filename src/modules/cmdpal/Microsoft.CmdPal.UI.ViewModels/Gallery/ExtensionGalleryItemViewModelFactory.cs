// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.UI.ViewModels.Gallery;

public sealed class ExtensionGalleryItemViewModelFactory
{
    private readonly ILogger<ExtensionGalleryItemViewModel> _logger;
    private readonly IWinGetPackageManagerService? _winGetPackageManagerService;
    private readonly IWinGetOperationTrackerService? _winGetOperationTrackerService;
    private readonly IWinGetPackageStatusService? _winGetPackageStatusService;
    private readonly IJsExtensionInstaller? _jsExtensionInstaller;

    public ExtensionGalleryItemViewModelFactory(
        ILogger<ExtensionGalleryItemViewModel> logger,
        IWinGetPackageManagerService? winGetPackageManagerService = null,
        IWinGetPackageStatusService? winGetPackageStatusService = null,
        IWinGetOperationTrackerService? winGetOperationTrackerService = null,
        IJsExtensionInstaller? jsExtensionInstaller = null)
    {
        _logger = logger;
        _winGetPackageManagerService = winGetPackageManagerService;
        _winGetPackageStatusService = winGetPackageStatusService;
        _winGetOperationTrackerService = winGetOperationTrackerService;
        _jsExtensionInstaller = jsExtensionInstaller;
    }

    public ExtensionGalleryItemViewModel Create(GalleryExtensionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ExtensionGalleryItemViewModel(
            entry,
            _logger,
            _winGetPackageManagerService,
            _winGetPackageStatusService,
            _winGetOperationTrackerService,
            _jsExtensionInstaller);
    }
}
