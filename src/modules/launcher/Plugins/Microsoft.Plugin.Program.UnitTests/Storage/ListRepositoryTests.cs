using Microsoft.Plugin.Program.Storage;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using Wox.Infrastructure.Storage;

namespace Microsoft.Plugin.Program.UnitTests.Storage
{
    [TestFixture]
    class ListRepositoryTests
    {

        [Test]
        public void Contains_ShouldReturnTrue_WhenListIsInitializedWithItem()
        {
            //Arrange
            var itemName = "originalItem1";
            var mockStorage = new Mock<IStorage<IList<string>>>();
            IRepository<string> repository = new ListRepository<string>(mockStorage.Object) { itemName };

            //Act
            var result = repository.Contains(itemName);

            //Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void Contains_ShouldReturnTrue_WhenListIsUpdatedWithAdd()
        {
            //Arrange
            var mockStorage = new Mock<IStorage<IList<string>>>();
            IRepository<string> repository = new ListRepository<string>(mockStorage.Object);

            //Act
            var itemName = "newItem";
            repository.Add(itemName);
            var result = repository.Contains(itemName);

            //Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void Contains_ShouldReturnFalse_WhenListIsUpdatedWithRemove()
        {
            //Arrange
            var itemName = "originalItem1";
            var mockStorage = new Mock<IStorage<IList<string>>>();
            IRepository<string> repository = new ListRepository<string>(mockStorage.Object) { itemName };

            //Act
            repository.Remove(itemName);
            var result = repository.Contains(itemName);

            //Assert
            Assert.IsFalse(result);
        }
    }
}
