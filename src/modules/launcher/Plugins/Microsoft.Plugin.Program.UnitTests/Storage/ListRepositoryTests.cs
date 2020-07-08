using Microsoft.Plugin.Program.Storage;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Capture;
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
            IRepository<string> repository = new ListRepository<string>() { itemName };

            //Act
            var result = repository.Contains(itemName);

            //Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void Contains_ShouldReturnTrue_WhenListIsUpdatedWithAdd()
        {
            //Arrange
            IRepository<string> repository = new ListRepository<string>();

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
            IRepository<string> repository = new ListRepository<string>() { itemName };

            //Act
            repository.Remove(itemName);
            var result = repository.Contains(itemName);

            //Assert
            Assert.IsFalse(result);
        }

        [Test]
        public async Task Add_ShouldNotThrow_WhenBeingIterated()
        {
            //Arrange
            ListRepository<string> repository = new ListRepository<string>();
            var numItems = 1000; 
            for(var i=0; i<numItems;++i)
            {
                repository.Add($"OriginalItem_{i}");
            }

            //Act - Begin iterating on one thread
            var iterationTask = Task.Run(() =>
            {
                var remainingIterations = 10000;
                while (remainingIterations > 0)
                {
                    foreach (var item in repository)
                    {
                        //keep iterating

                    }
                    --remainingIterations;
                }

            });

            //Act - Insert on another thread
            var addTask =  Task.Run(() =>
            {
                for (var i = 0; i < numItems; ++i)
                {
                    repository.Add($"NewItem_{i}");
                }
            });

            //Assert that this does not throw.  Collections that aren't syncronized will throw an invalidoperatioexception if the list is modified while enumerating
            Assert.DoesNotThrowAsync(async () => 
            {
                await Task.WhenAll(new Task[] { iterationTask, addTask });
            });
        }

        [Test]
        public async Task Remove_ShouldNotThrow_WhenBeingIterated()
        {
            //Arrange
            ListRepository<string> repository = new ListRepository<string>();
            var numItems = 1000;
            for (var i = 0; i < numItems; ++i)
            {
                repository.Add($"OriginalItem_{i}");
            }

            //Act - Begin iterating on one thread
            var iterationTask = Task.Run(() =>
            {
                var remainingIterations = 10000;
                while (remainingIterations > 0)
                {
                    foreach (var item in repository)
                    {
                        //keep iterating

                    }
                    --remainingIterations;
                }

            });

            //Act - Remove on another thread
            var addTask = Task.Run(() =>
            {
                for (var i = 0; i < numItems; ++i)
                {
                    repository.Remove($"OriginalItem_{i}");
                }
            });

            //Assert that this does not throw.  Collections that aren't syncronized will throw an invalidoperatioexception if the list is modified while enumerating
            Assert.DoesNotThrowAsync(async () =>
            {
                await Task.WhenAll(new Task[] { iterationTask, addTask });
            });
        }
    }
}
