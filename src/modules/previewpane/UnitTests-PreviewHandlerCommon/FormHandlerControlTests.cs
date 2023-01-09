// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using System.Windows.Forms;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PreviewHandlerCommonUnitTests
{
    [TestClass]
    public class FormHandlerControlTests
    {
        private class TestFormControl : FormHandlerControl
        {
        }

        [TestMethod]
        public void FormHandlerControlShouldCreateHandleOnInitialization()
        {
            // Arrange and act
            using (var testFormHandlerControl = new TestFormControl())
            {
                // Assert
                Assert.IsTrue(testFormHandlerControl.IsHandleCreated);
            }
        }

        [TestMethod]
        public void FormHandlerControlShouldSetVisibleFalseOnInitialization()
        {
            // Arrange and act
            using (var testFormHandlerControl = new TestFormControl())
            {
                // Assert
                Assert.IsFalse(testFormHandlerControl.Visible);
            }
        }

        [TestMethod]
        public void FormHandlerControlShouldSetFormBorderStyleOnInitialization()
        {
            // Arrange and act
            using (var testFormHandlerControl = new TestFormControl())
            {
                // Assert
                Assert.AreEqual(FormBorderStyle.None, testFormHandlerControl.FormBorderStyle);
            }
        }

        [TestMethod]
        public void FormHandlerControlShouldReturnValidHandleWhenGetHandleCalled()
        {
            // Arrange
            using (var testFormHandlerControl = new TestFormControl())
            {
                // Act
                var handle = testFormHandlerControl.Handle;

                // Assert
                Assert.AreEqual(testFormHandlerControl.Handle, handle);
            }
        }

        [TestMethod]
        public void FormHandlerControlShouldSetBackgroundColorWhenSetBackgroundColorCalled()
        {
            // Arrange
            using (var testFormHandlerControl = new TestFormControl())
            {
                var color = Color.Navy;

                // Act
                testFormHandlerControl.SetBackgroundColor(color);

                // Assert
                Assert.AreEqual(color, testFormHandlerControl.BackColor);
            }
        }

        [TestMethod]
        public void FormHandlerControlShouldSetFontWhenSetFontCalled()
        {
            // Arrange
            using (var testFormHandlerControl = new TestFormControl())
            {
                using (var font = new Font("Arial", 20))
                {
                    // Act
                    testFormHandlerControl.SetFont(font);

                    // Assert
                    Assert.AreEqual(font, testFormHandlerControl.Font);
                }
            }
        }

        [TestMethod]
        public void FormHandlerControlShouldSetTextColorWhenSetTextColorCalled()
        {
            // Arrange
            using (var testFormHandlerControl = new TestFormControl())
            {
                var color = Color.Navy;

                // Act
                testFormHandlerControl.SetTextColor(color);

                // Assert
                Assert.AreEqual(color, testFormHandlerControl.ForeColor);
            }
        }

        [TestMethod]
        public void FormHandlerControlShouldClearAllControlsWhenUnloadCalled()
        {
            // Arrange
            using (var testFormHandlerControl = new TestFormControl())
            {
                testFormHandlerControl.Controls.Add(new TextBox());
                testFormHandlerControl.Controls.Add(new RichTextBox());

                // Act
                testFormHandlerControl.Unload();

                // Assert
                Assert.AreEqual(0, testFormHandlerControl.Controls.Count);
            }
        }

        [TestMethod]
        public void FormHandlerControlShouldSetVisibleFalseWhenUnloadCalled()
        {
            // Arrange
            using (var testFormHandlerControl = new TestFormControl())
            {
                // Act
                testFormHandlerControl.Unload();

                // Assert
                Assert.IsFalse(testFormHandlerControl.Visible);
            }
        }

        [TestMethod]
        public void FormHandlerControlShouldSetVisibletrueWhenDoPreviewCalled()
        {
            // Arrange
            using (var testFormHandlerControl = new TestFormControl())
            {
                // Act
                testFormHandlerControl.DoPreview("valid-path");

                // Assert
                Assert.IsTrue(testFormHandlerControl.Visible);
            }
        }

        [TestMethod]
        public void FormHandlerControlShouldSetParentHandleWhenSetWindowCalled()
        {
            // Arrange
            using (var testFormHandlerControl = new TestFormControl())
            {
                using (var parentFormWindow = new UserControl())
                {
                    var parentHwnd = parentFormWindow.Handle;
                    var rect = new Rectangle(2, 2, 4, 4);

                    // Act
                    testFormHandlerControl.SetWindow(parentHwnd, rect);
                    var actualParentHwnd = NativeMethods.GetAncestor(testFormHandlerControl.Handle, 1); // GA_PARENT 1

                    // Assert
                    Assert.AreEqual(parentHwnd, actualParentHwnd);
                }
            }
        }
    }
}
