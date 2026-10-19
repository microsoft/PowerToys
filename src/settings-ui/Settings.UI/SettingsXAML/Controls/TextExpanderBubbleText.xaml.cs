// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// Shows a snippet's text with a bubble for each value that is only known when it runs: the
    /// clipboard, an answer, or a date or time, shown as it is now. The snippet editor's preview
    /// and the snippet list both use it, so a bubble looks the same everywhere.
    /// </summary>
    public sealed partial class TextExpanderBubbleText : UserControl
    {
        public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
            nameof(Segments),
            typeof(object),
            typeof(TextExpanderBubbleText),
            new PropertyMetadata(null, (d, _) => ((TextExpanderBubbleText)d).OnSegmentsChanged()));

        private readonly TextExpanderTrimFallback _trimFallback = new();

        private bool _showBreaksAsMarkers;

        private bool _textOnly;

        private bool _textOnlyWhenTrimmed;

        private bool _hasElements;

        public TextExpanderBubbleText()
        {
            InitializeComponent();
            Block.IsTextTrimmedChanged += (_, _) => CheckTrimmed();
            SizeChanged += (_, e) =>
            {
                if (_textOnlyWhenTrimmed && _trimFallback.OnWidthChanged(e.NewSize.Width))
                {
                    Render();
                }
            };
        }

        /// <summary>Gets or sets the text to show, as built by <see cref="TextExpanderSnippetPreview.Build"/>.</summary>
        public object Segments
        {
            get => GetValue(SegmentsProperty);
            set => SetValue(SegmentsProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether line breaks and tabs show as ↵ and → markers,
        /// keeping a snippet on one line in the list.
        /// </summary>
        public bool ShowBreaksAsMarkers
        {
            get => _showBreaksAsMarkers;
            set
            {
                _showBreaksAsMarkers = value;
                Render();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether everything shows as text: each value as its icon
        /// and label without the bubble's box, and the cursor and markers as characters. A line
        /// that's trimmed needs this, because WinUI draws an element that falls past the ellipsis
        /// at the start of the line, over the text. The line is then spoken as the bubbles were.
        /// </summary>
        public bool TextOnly
        {
            get => _textOnly;
            set
            {
                _textOnly = value;
                Render();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the line shows bubbles until WinUI trims it, and
        /// then shows as text, as with <see cref="TextOnly"/>, until it has clearly more room.
        /// </summary>
        public bool TextOnlyWhenTrimmed
        {
            get => _textOnlyWhenTrimmed;
            set
            {
                _textOnlyWhenTrimmed = value;
                _trimFallback.Reset();
                Render();
            }
        }

        public int MaxLines
        {
            get => Block.MaxLines;
            set => Block.MaxLines = value;
        }

        public TextTrimming TextTrimming
        {
            get => Block.TextTrimming;
            set => Block.TextTrimming = value;
        }

        public TextWrapping TextWrapping
        {
            get => Block.TextWrapping;
            set => Block.TextWrapping = value;
        }

        /// <summary>Gets or sets the text's name for screen readers, which may not reach the bubbles themselves.</summary>
        public string AccessibleName
        {
            get => AutomationProperties.GetName(Block);
            set => AutomationProperties.SetName(Block, value);
        }

        /// <summary>Describes a bubble for screen readers, for example "Clipboard, filled in when the snippet runs".</summary>
        public static string DescribeBubble(TextExpanderPreviewSegment segment)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            return segment.Kind switch
            {
                TextExpanderPreviewSegmentKind.Clipboard => resourceLoader.GetString("TextExpander_PreviewClipboard_Description"),
                TextExpanderPreviewSegmentKind.Prompt => string.Format(CultureInfo.CurrentCulture, resourceLoader.GetString("TextExpander_PreviewPrompt_Description"), PromptLabel(segment)),
                TextExpanderPreviewSegmentKind.Cursor => resourceLoader.GetString("TextExpander_PreviewCursor_Description"),
                _ => string.Format(CultureInfo.CurrentCulture, resourceLoader.GetString("TextExpander_PreviewValue_Description"), segment.Value),
            };
        }

        private static string PromptLabel(TextExpanderPreviewSegment segment)
            => TextExpanderInlines.ValueText(segment, Resource).Label;

        private static string Resource(string key) => ResourceLoaderInstance.ResourceLoader.GetString(key);

        private static FontFamily ThemeFont(string key, string fallback)
            => Application.Current.Resources.TryGetValue(key, out object value) && value is FontFamily font ? font : new FontFamily(fallback);

        private void OnSegmentsChanged()
        {
            // New content gets its bubbles back first.
            _trimFallback.Reset();
            Render();
        }

        private void CheckTrimmed()
        {
            if (_textOnlyWhenTrimmed && _trimFallback.OnTrimmed(Block.IsTextTrimmed, _hasElements, ActualWidth))
            {
                Render();
            }
        }

        private void Render()
        {
            if (Block == null)
            {
                return;
            }

            Block.Blocks.Clear();
            var segments = Segments as IReadOnlyList<TextExpanderPreviewSegment>;
            bool textOnly = _textOnly || _trimFallback.ShowsText;
            if (textOnly)
            {
                // The icons are characters that mean nothing read aloud, so the line says what the bubbles would have.
                AutomationProperties.SetName(Block, segments == null ? string.Empty : TextExpanderSnippetPreview.Describe(segments, DescribeBubble));
            }
            else if (_textOnlyWhenTrimmed)
            {
                Block.ClearValue(AutomationProperties.NameProperty);
            }

            _hasElements = false;
            if (segments == null || segments.Count == 0)
            {
                return;
            }

            var paragraph = new Paragraph();
            foreach (TextExpanderInline piece in TextExpanderInlines.Plan(segments, _showBreaksAsMarkers, textOnly, Resource))
            {
                Inline inline = piece.Kind switch
                {
                    TextExpanderInlineKind.LineBreak => new LineBreak(),
                    TextExpanderInlineKind.Bubble => new InlineUIContainer { Child = CreateBubble(piece.Segment) },
                    TextExpanderInlineKind.CursorBar => new InlineUIContainer { Child = CreateCursorMarker() },
                    TextExpanderInlineKind.BreakMarker => CreateMarker(piece.Text),
                    TextExpanderInlineKind.ValueIcon => new Run { Text = piece.Text, FontFamily = ThemeFont("SymbolThemeFontFamily", "Segoe Fluent Icons, Segoe MDL2 Assets") },
                    TextExpanderInlineKind.ValueLabel => new Run { Text = piece.Text, FontFamily = ThemeFont("ContentControlThemeFontFamily", "Segoe UI Variable, Segoe UI") },
                    TextExpanderInlineKind.CursorText => CreateCursorText(piece.Text),
                    _ => new Run { Text = piece.Text },
                };
                paragraph.Inlines.Add(inline);
                _hasElements |= piece.IsElement;
            }

            Block.Blocks.Add(paragraph);

            // A line already too narrow raises no trimmed change, so check once it's laid out.
            if (_textOnlyWhenTrimmed && _hasElements)
            {
                DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, CheckTrimmed);
            }
        }

        private Border CreateBubble(TextExpanderPreviewSegment segment)
        {
            (string glyph, string text) = TextExpanderInlines.ValueText(segment, Resource);

            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            content.Children.Add(new FontIcon { Glyph = glyph, Style = (Style)Resources["BubbleIconStyle"] });
            content.Children.Add(new TextBlock { Text = text, Style = (Style)Resources["BubbleTextStyle"] });

            var bubble = new Border { Child = content, Style = (Style)Resources["BubbleStyle"] };
            AutomationProperties.SetName(bubble, DescribeBubble(segment));
            return bubble;
        }

        private Border CreateCursorMarker()
        {
            var marker = new Border { Style = (Style)Resources["CursorStyle"] };
            AutomationProperties.SetName(marker, DescribeBubble(new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Cursor)));
            return marker;
        }

        private InlineUIContainer CreateMarker(string glyph)
            => new() { Child = new TextBlock { Text = glyph, Style = (Style)Resources["MarkerStyle"] } };

        /// <summary>The cursor as a bar character in the cursor bar's accent color, which follows the theme.</summary>
        private Run CreateCursorText(string text)
        {
            var run = new Run { Text = text, FontFamily = ThemeFont("ContentControlThemeFontFamily", "Segoe UI Variable, Segoe UI"), FontWeight = FontWeights.SemiBold };
            BindingOperations.SetBinding(run, TextElement.ForegroundProperty, new Binding { Source = CursorTextColor, Path = new PropertyPath(nameof(Border.Background)) });
            return run;
        }
    }
}
