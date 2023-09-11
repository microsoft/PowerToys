// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Peek.FilePreviewer.Previewers;
using Peek.FilePreviewer.Previewers.Archives;
using Peek.FilePreviewer.Previewers.Archives.Models;

namespace Peek.FilePreviewer.Controls
{
    public sealed partial class ArchiveControl : UserControl
    {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(ObservableCollection<ArchiveItem>),
            typeof(ArchivePreviewer),
            new PropertyMetadata(null));

        public static readonly DependencyProperty LoadingStateProperty = DependencyProperty.Register(
            nameof(LoadingState),
            typeof(PreviewState),
            typeof(ArchivePreviewer),
            new PropertyMetadata(PreviewState.Uninitialized));

        public static readonly DependencyProperty DirectoryCountProperty = DependencyProperty.Register(
            nameof(DirectoryCount),
            typeof(string),
            typeof(ArchivePreviewer),
            new PropertyMetadata(null));

        public static readonly DependencyProperty FileCountProperty = DependencyProperty.Register(
            nameof(FileCount),
            typeof(string),
            typeof(ArchivePreviewer),
            new PropertyMetadata(null));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size),
            typeof(string),
            typeof(ArchivePreviewer),
            new PropertyMetadata(null));

        public ObservableCollection<ArchiveItem>? Source
        {
            get { return (ObservableCollection<ArchiveItem>)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public PreviewState? LoadingState
        {
            get { return (PreviewState)GetValue(LoadingStateProperty); }
            set { SetValue(LoadingStateProperty, value); }
        }

        public string? DirectoryCount
        {
            get { return (string)GetValue(DirectoryCountProperty); }
            set { SetValue(DirectoryCountProperty, value); }
        }

        public string? FileCount
        {
            get { return (string)GetValue(FileCountProperty); }
            set { SetValue(FileCountProperty, value); }
        }

        public string? Size
        {
            get { return (string)GetValue(SizeProperty); }
            set { SetValue(SizeProperty, value); }
        }

        public ArchiveControl()
        {
            this.InitializeComponent();
        }
    }
}
