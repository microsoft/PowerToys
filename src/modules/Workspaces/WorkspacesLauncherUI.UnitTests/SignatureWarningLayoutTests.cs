// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Models;
using WorkspacesLauncherUI.Properties;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class SignatureWarningLayoutTests
    {
        [DataTestMethod]
        [DataRow("en-US", FlowDirection.LeftToRight)]
        [DataRow("fr-FR", FlowDirection.LeftToRight)]
        [DataRow("ar-SA", FlowDirection.RightToLeft)]
        public void WarningUsesUICultureAndPreservesRawDiagnosticFields(string cultureName, FlowDirection direction)
        {
            RunOnSta(() =>
            {
                using var culture = new CultureScope(cultureName);
                var request = CreateRequest();
                var window = new SignatureWarningWindow(request);
                try
                {
                    var root = LayoutContent(window, 600);
                    var details = VisualDescendants<Expander>(root).Single();
                    details.IsExpanded = true;
                    LayoutContent(window, 600);

                    Assert.AreEqual(cultureName, window.Language.IetfLanguageTag, ignoreCase: true, CultureInfo.InvariantCulture);
                    Assert.AreEqual(direction, window.FlowDirection);
                    Assert.AreEqual(direction, root.FlowDirection);
                    Assert.AreEqual(request.Title, window.Title);
                    AssertRawField(root, "SignatureWarningPath", request.Path);
                    AssertRawField(root, "SignatureWarningArguments", request.Arguments);
                    AssertRawField(root, "SignatureWarningStatus", request.Status);

                    var app = FindTextField(root, "SignatureWarningApp");
                    Assert.AreEqual(request.AppName, app.Text);
                    Assert.AreEqual(direction, app.FlowDirection);
                    Assert.IsTrue(app.ActualWidth > 0);
                    Assert.AreEqual(0, VisualDescendants<TextBox>(root).Count(), "Read-only details must not look like editable inputs.");
                    AssertStaticTextPeer(app, request.AppLabel, request.AppName);

                    var copy = VisualDescendants<Button>(root).Single(button => AutomationProperties.GetAutomationId(button) == "CopySignatureDetails");
                    Assert.AreEqual(request.CopyDetailsButtonText, copy.Content);
                    Assert.IsTrue(copy.IsEnabled);
                    Assert.IsFalse(copy.IsDefault);
                    Assert.IsFalse(copy.IsCancel);
                }
                finally
                {
                    window.DismissWithoutResponse();
                }
            });
        }

        [DataTestMethod]
        [DataRow("en-US")]
        [DataRow("ar-SA")]
        public void WarningAndAutomationExposeEscapedDisplayValues(string cultureName)
        {
            RunOnSta(() =>
            {
                using var culture = new CultureScope(cultureName);
                var request = new SignatureWarningRequest
                {
                    AppName = "Example\r\nPublisher: Microsoft",
                    Path = "C:\\Apps\\file\u202Etxt.exe",
                    Arguments = "--name\tvalue\u2028another line",
                    Status = "0x800B0100\u2066hidden\u2069",
                    Reason = "unsigned",
                };
                var window = new SignatureWarningWindow(request);
                try
                {
                    var root = LayoutContent(window, 600);
                    VisualDescendants<Expander>(root).Single().IsExpanded = true;
                    LayoutContent(window, 600);

                    var app = FindTextField(root, "SignatureWarningApp");
                    Assert.AreEqual("Example\\r\\nPublisher: Microsoft", app.Text);
                    AssertStaticTextPeer(app, request.AppLabel, request.DisplayAppName);
                    AssertRawField(root, "SignatureWarningPath", "C:\\Apps\\file\\u202Etxt.exe");
                    AssertRawField(root, "SignatureWarningArguments", "--name\\tvalue\\u2028another line");
                    AssertRawField(root, "SignatureWarningStatus", "0x800B0100\\u2066hidden\\u2069");
                    var explanation = FindTextField(root, "SignatureWarningEscapedDisplay");
                    Assert.AreEqual(Visibility.Visible, explanation.Visibility);
                    Assert.AreEqual(request.EscapedDisplayExplanation, explanation.Text);
                    Assert.IsTrue(explanation.ActualWidth > 0 && explanation.ActualHeight > 0);
                    StringAssert.Contains(request.DetailsText, request.Path);
                    StringAssert.Contains(request.DetailsText, request.Arguments);
                }
                finally
                {
                    window.DismissWithoutResponse();
                }
            });
        }

        [DataTestMethod]
        [DataRow("fr-FR", 600)]
        [DataRow("fr-FR", 360)]
        [DataRow("ar-SA", 600)]
        [DataRow("ar-SA", 360)]
        public void ExpandedActionsWrapAndFitMeasuredContentBounds(string cultureName, int width)
        {
            RunOnSta(() =>
            {
                using var culture = new CultureScope(cultureName);
                var window = new SignatureWarningWindow(CreateRequest());
                try
                {
                    var root = LayoutContent(window, width);
                    var actions = (FrameworkElement)window.FindName("WarningActions");
                    var skip = (Button)window.FindName("SkipButton");
                    var run = (Button)window.FindName("RunAnywayButton");
                    Assert.IsNotNull(actions);
                    Assert.IsNotNull(skip);
                    Assert.IsNotNull(run);

                    AssertFitsInside(root, actions);
                    AssertWrappedButtonFits(root, actions, skip, Resources.SignatureWarningSkip);
                    AssertWrappedButtonFits(root, actions, run, Resources.SignatureWarningRunAnyway);
                    var skipBounds = BoundsIn(root, skip);
                    var runBounds = BoundsIn(root, run);
                    Assert.IsFalse(skipBounds.IntersectsWith(runBounds), "Action buttons must not overlap.");

                    Assert.IsTrue(skip.IsDefault);
                    Assert.IsTrue(skip.IsCancel);
                    Assert.IsTrue(skip.IsEnabled);
                    Assert.IsFalse(run.IsDefault);
                    Assert.IsFalse(run.IsCancel);
                    Assert.IsFalse(run.IsEnabled);

                    window.EnableRunChoice();
                    Assert.IsTrue(run.IsEnabled);
                    Assert.IsTrue(skip.IsDefault);
                    Assert.IsTrue(skip.IsCancel);
                    Assert.IsFalse(run.IsDefault);
                    Assert.IsFalse(run.IsCancel);
                }
                finally
                {
                    window.DismissWithoutResponse();
                }
            });
        }

        private static SignatureWarningRequest CreateRequest()
        {
            return new SignatureWarningRequest
            {
                AppName = "[TEST] Example app",
                Path = @"C:\[TEST]\Example app.exe",
                Arguments = "--test \"[TEST] value\"",
                Status = "0x800B0100",
                Reason = "unsigned",
            };
        }

        private static FrameworkElement LayoutContent(SignatureWarningWindow window, double width)
        {
            window.Width = width;
            var root = (FrameworkElement)window.Content;

            // Lay out the real XAML content without showing or creating a window handle.
            var available = new Size(width, window.MaxHeight);
            root.Measure(available);
            root.Arrange(new Rect(available));
            root.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            root.Measure(available);
            root.Arrange(new Rect(available));
            root.UpdateLayout();

            Assert.IsTrue(root.ActualWidth > 0 && root.ActualHeight > 0, "The real content must have completed layout.");
            Assert.IsTrue(root.ActualWidth <= width);
            Assert.IsFalse(window.IsVisible, "Unit tests must never display the warning.");
            return root;
        }

        private static void AssertRawField(FrameworkElement root, string automationId, string expected)
        {
            var field = FindTextField(root, automationId);
            Assert.AreEqual(expected, field.Text, automationId);
            Assert.AreEqual(FlowDirection.LeftToRight, field.FlowDirection, automationId);
            Assert.IsTrue(field.ActualWidth > 0 && field.ActualHeight > 0, automationId);
            Assert.AreEqual(TextWrapping.Wrap, field.TextWrapping);
            var label = (TextBlock)AutomationProperties.GetLabeledBy(field);
            Assert.IsNotNull(label, automationId);
            AssertStaticTextPeer(field, label.Text, expected);
        }

        private static TextBlock FindTextField(DependencyObject root, string automationId)
        {
            return VisualDescendants<TextBlock>(root).Single(field => AutomationProperties.GetAutomationId(field) == automationId);
        }

        private static void AssertStaticTextPeer(TextBlock field, string label, string value)
        {
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(field);
            Assert.IsNotNull(peer);
            Assert.AreEqual(AutomationControlType.Text, peer.GetAutomationControlType());
            Assert.AreEqual(value, peer.GetName());
            var labelElement = AutomationProperties.GetLabeledBy(field);
            Assert.IsNotNull(labelElement);
            var labelPeer = FrameworkElementAutomationPeer.CreatePeerForElement(labelElement);
            Assert.IsNotNull(labelPeer);
            Assert.AreSame(labelPeer, peer.GetLabeledBy());
            Assert.AreEqual(label, labelPeer.GetName());
            Assert.IsNull(peer.GetPattern(PatternInterface.Value), "Display text must not expose an editable value pattern.");
            Assert.IsFalse(field.Focusable, "Static details must not add unnecessary keyboard stops.");
        }

        private static void AssertWrappedButtonFits(FrameworkElement root, FrameworkElement actions, Button button, string expectedText)
        {
            Assert.IsInstanceOfType<string>(button.Content);
            Assert.AreEqual(expectedText, button.Content);
            StringAssert.StartsWith(expectedText, $"[TEST {CultureInfo.CurrentUICulture.Name}]");
            AssertFitsInside(root, button);
            AssertFitsInside(actions, button);

            var text = VisualDescendants<TextBlock>(button).Single(block => block.Text == expectedText);
            Assert.AreEqual(TextWrapping.Wrap, text.TextWrapping);
            Assert.AreEqual(TextTrimming.None, text.TextTrimming);
            Assert.IsTrue(text.ActualHeight > 2 * text.FontSize, "Expanded text must actually occupy multiple lines.");
            Assert.IsTrue(text.DesiredSize.Width <= text.ActualWidth + 0.5, "Text must not be horizontally clipped.");
            Assert.IsTrue(text.DesiredSize.Height <= text.ActualHeight + 0.5, "Text must not be vertically clipped.");
            AssertFitsInside(button, text);
            AssertFitsInside(root, text);
        }

        private static void AssertFitsInside(FrameworkElement ancestor, FrameworkElement element)
        {
            const double tolerance = 0.5;
            Assert.IsTrue(element.ActualWidth > 0 && element.ActualHeight > 0, $"{element.GetType().Name} has no measured content.");
            var bounds = BoundsIn(ancestor, element);
            Assert.IsTrue(
                bounds.Left >= -tolerance && bounds.Top >= -tolerance
                && bounds.Right <= ancestor.ActualWidth + tolerance && bounds.Bottom <= ancestor.ActualHeight + tolerance,
                $"{element.GetType().Name} bounds {bounds} exceed {ancestor.GetType().Name} content {ancestor.RenderSize}.");
        }

        private static Rect BoundsIn(FrameworkElement ancestor, FrameworkElement element)
        {
            return element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
        }

        private static IEnumerable<T> VisualDescendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var descendant in VisualDescendants<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private static void RunOnSta(Action test)
        {
            ExceptionDispatchInfo failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception exception)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "In-process WPF layout did not complete.");
            failure?.Throw();
        }
    }
}
