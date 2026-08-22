// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Globalization;

namespace PowerOCR.ViewModels;

[System.Diagnostics.CodeAnalysis.SuppressMessage("CommunityToolkit.Mvvm.SourceGenerators", "MVVMTK0045", Justification = "Field-based pattern used for current toolkit version.")]
public sealed partial class OverlaySessionViewModel : ObservableObject
{
    public ObservableCollection<Language> Languages { get; } = new();

    [ObservableProperty]
    private Language? selectedLanguage;

    [ObservableProperty]
    private bool isSingleLine;

    [ObservableProperty]
    private bool isTable;

    [ObservableProperty]
    private bool isProcessing;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool hasError;
}
