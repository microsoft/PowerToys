using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using Wox.Plugin;
using Wox.ViewModel;

namespace Wox.Test
{
    [TestFixture]
    class Wox
    {
        [Test]
        public void ContextMenuItemCommand_MustCallOnPropertyChanged_WhenSet()
        {
            // Arrange
            var mockBaseModel = new Mock<IBaseModel>();

            // Act
            ContextMenuItemViewModel item = new ContextMenuItemViewModel(mockBaseModel.Object);
            item.Command = new RelayCommand(null);

            // Assert
            mockBaseModel.Verify(m => m.OnPropertyChanged("Command"));

        }

    }
}
