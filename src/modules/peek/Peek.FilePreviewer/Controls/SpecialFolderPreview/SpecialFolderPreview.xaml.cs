// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Peek.Common.Converters;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers;

namespace Peek.FilePreviewer.Controls;

[INotifyPropertyChanged]
public sealed partial class SpecialFolderPreview : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(SpecialFolderPreviewData),
            typeof(SpecialFolderPreview),
            new PropertyMetadata(null));

    public static readonly DependencyProperty LoadingStateProperty = DependencyProperty.Register(
        nameof(LoadingState),
        typeof(PreviewState),
        typeof(SpecialFolderPreview),
        new PropertyMetadata(PreviewState.Uninitialized));

    public SpecialFolderPreviewData? Source
    {
        get { return (SpecialFolderPreviewData)GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    public PreviewState? LoadingState
    {
        get { return (PreviewState)GetValue(LoadingStateProperty); }
        set { SetValue(LoadingStateProperty, value); }
    }

    public SpecialFolderPreview()
    {
        InitializeComponent();
    }

    public Visibility IsVisibleIfStatesMatch(PreviewState? a, PreviewState? b) => VisibilityConverter.Convert(a == b);
}
