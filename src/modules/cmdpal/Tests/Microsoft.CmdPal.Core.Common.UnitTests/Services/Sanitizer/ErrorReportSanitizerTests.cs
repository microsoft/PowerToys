// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services.Sanitizer;

namespace Microsoft.CmdPal.Common.UnitTests.Services.Sanitizer;

[TestClass]
public partial class ErrorReportSanitizerTests
{
    [TestMethod]
    public void Sanitize_ShouldMaskPiiInErrorReport()
    {
        // Arrange
        var reportSanitizer = new ErrorReportSanitizer();
        var input = TestData.Input;

        // Act
        var result = reportSanitizer.Sanitize(input);

        // Assert
        Assert.AreEqual(TestData.Expected, result);
    }

    [TestMethod]
    public void Sanitize_ShouldNotMaskTooMuchPiiInErrorReport()
    {
        // Arrange
        var reportSanitizer = new ErrorReportSanitizer();
        var input = TestData.Input2;

        // Act
        var result = reportSanitizer.Sanitize(input);

        // Assert
        Assert.AreEqual(TestData.Expected2, result);
    }
}
