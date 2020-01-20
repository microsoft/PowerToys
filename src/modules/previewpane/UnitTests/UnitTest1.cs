using System;
using Common;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [TestClass]
    public class FormHandlerUnitTest
    {
        private class TestFormHandler : FormHandlerControl
        {
        }

        [TestMethod]
        public void GetHandle_ShouldReturnFormHandle()
        {
            using (var testFormHandler = new TestFormHandler()) 
            {
                // Act
                var actualHandle = testFormHandler.GetHandle();

                // Assert
                actualHandle.Should().BeEq
            }
        }
    }
}
