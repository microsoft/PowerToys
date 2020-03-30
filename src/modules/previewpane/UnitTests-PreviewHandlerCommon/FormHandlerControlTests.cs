// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UnitTests_PreviewHandlerCommon
{
    [TestClass]
    public class FormHandlerControlTests
    {
        private class TestFormControl : FormHandlerControl
        { }

        [TestMethod]
        public void FormHandlerControl_ShouldCreateHandle_OnIntialization()
        {
            // Arrange and act
            var testFormHandlerControl = new TestFormControl();

            // Assert
            Assert.IsTrue(testFormHandlerControl.IsHandleCreated);
        }

        [TestMethod]
        public void FormHandlerControl_ShouldSetVisibleFalse_OnIntialization()
        {
            // Arrange and act
            var testFormHandlerControl = new TestFormControl();

            // Assert
            Assert.IsFalse(testFormHandlerControl.Visible);
        }

        [TestMethod]
        public void FormHandlerControl_ShouldSetFormBorderStyle_OnIntialization()
        {
            // Arrange and act
            var testFormHandlerControl = new TestFormControl();

            // Assert
            Assert.AreEqual(FormBorderStyle.None, testFormHandlerControl.FormBorderStyle);
        }

        [TestMethod]
        public void FormHandlerControl_ShouldReturnValidHandle_WhenGetHandleCalled()
        {
            // Arrange
            var testFormHandlerControl = new TestFormControl();

            // Act
            var handle = testFormHandlerControl.GetHandle();

            // Assert
            Assert.AreEqual(testFormHandlerControl.Handle, handle);
        }

        [TestMethod]
        public void FormHandlerControl_ShouldSetBackgroundColor_WhenSetBackgroundColorCalled()
        {
            // Arrange
            var testFormHandlerControl = new TestFormControl();
            var color = Color.Navy;

            // Act
            testFormHandlerControl.SetBackgroundColor(color);

            // Assert
            Assert.AreEqual(color, testFormHandlerControl.BackColor);
        }

        [TestMethod]
        public void FormHandlerControl_ShouldSetFont_WhenSetFontCalled()
        {
            // Arrange
            var testFormHandlerControl = new TestFormControl();
            var font = new Font("Arial", 20);

            // Act
            testFormHandlerControl.SetFont(font);

            // Assert
            Assert.AreEqual(font, testFormHandlerControl.Font);
        }

        [TestMethod]
        public void FormHandlerControl_ShouldUpdateBounds_WhenSetRectCalled()
        {
            // Arrange
            var testFormHandlerControl = new TestFormControl();
            var bounds = new Rectangle(2, 2, 4, 4);

            // Act
            testFormHandlerControl.SetRect(bounds);

            // Assert
            Assert.AreEqual(bounds, testFormHandlerControl.Bounds);
        }

        [TestMethod]
        public void FormHandlerControl_ShouldSetTextColor_WhenSetTextColorCalled()
        {
            // Arrange
            var testFormHandlerControl = new TestFormControl();
            var color = Color.Navy;

            // Act
            testFormHandlerControl.SetTextColor(color);

            // Assert
            Assert.AreEqual(color, testFormHandlerControl.ForeColor);
        }

        [TestMethod]
        public void FormHandlerControl_ShouldClearAllControls_WhenUnloadCalled()
        {
            // Arrange
            var testFormHandlerControl = new TestFormControl();
            testFormHandlerControl.Controls.Add(new TextBox());
            testFormHandlerControl.Controls.Add(new RichTextBox());

            // Act
            testFormHandlerControl.Unload();

            // Assert
            Assert.AreEqual(0, testFormHandlerControl.Controls.Count);
        }

        [TestMethod]
        public void FormHandlerControl_ShouldSetVisibleFalse_WhenUnloadCalled()
        {
            // Arrange
            var testFormHandlerControl = new TestFormControl();

            // Act
            testFormHandlerControl.Unload();

            // Assert
            Assert.IsFalse(testFormHandlerControl.Visible);
        }

        [TestMethod]
        public void FormHandlerControl_ShouldSetVisibletrue_WhenDoPreviewCalled()
        {
            // Arrange
            var testFormHandlerControl = new TestFormControl();

            // Act
            testFormHandlerControl.DoPreview("valid-path");

            // Assert
            Assert.IsTrue(testFormHandlerControl.Visible);
        }

        [TestMethod]
        public void FormHandlerControl_ShouldSetParentHandle_WhenSetWindowCalled()
        {
            // Arrange
            var testFormHandlerControl = new TestFormControl();
            var parentFormWindow = new UserControl();
            var parentHwnd = parentFormWindow.Handle;
            var rect = new Rectangle(2, 2, 4, 4);

            // Act
            testFormHandlerControl.SetWindow(parentHwnd, rect);
            var actualParentHwnd = GetAncestor(testFormHandlerControl.Handle, 1); // GA_PARENT 1

            // Assert
            Assert.AreEqual(parentHwnd, actualParentHwnd);
        }

        // Gets the ancestor window: https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getancestor
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
    }
}
