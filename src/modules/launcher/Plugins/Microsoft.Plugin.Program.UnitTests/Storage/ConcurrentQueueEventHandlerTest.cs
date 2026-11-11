// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Threading.Tasks;

using Microsoft.Plugin.Program.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Plugin.Program.UnitTests.Storage
{
    [OSCondition(OperatingSystems.Windows)]
    [TestClass]
    public class ConcurrentQueueEventHandlerTest
    {
        [TestMethod]
        [DataRow]
        public async Task EventHandlerMustReturnEmptyPathForEmptyQueueAsync()
        {
            // Arrange
            int dequeueDelay = 0;
            ConcurrentQueue<string> eventHandlingQueue = new ConcurrentQueue<string>();

            // Act
            string appPath = await EventHandler.GetAppPathFromQueueAsync(eventHandlingQueue, dequeueDelay).ConfigureAwait(false);

            // Assert
            Assert.IsTrue(string.IsNullOrEmpty(appPath));
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(10)]
        public async Task EventHandlerMustReturnPathForConcurrentQueueWithSameFilePathsAsync(int itemCount)
        {
            // Arrange
            int dequeueDelay = 0;
            string appPath = "appPath";
            ConcurrentQueue<string> eventHandlingQueue = new ConcurrentQueue<string>();
            for (int i = 0; i < itemCount; i++)
            {
                eventHandlingQueue.Enqueue(appPath);
            }

            // Act
            string pathFromQueue = await EventHandler.GetAppPathFromQueueAsync(eventHandlingQueue, dequeueDelay).ConfigureAwait(false);

            // Assert
            Assert.AreEqual(appPath, pathFromQueue);
            Assert.IsEmpty(eventHandlingQueue);
        }

        [TestMethod]
        [DataRow(5)]
        public async Task EventHandlerMustReturnPathAndRetainDifferentFilePathsInQueueAsync(int itemCount)
        {
            // Arrange
            int dequeueDelay = 0;
            string firstAppPath = "appPath1";
            string secondAppPath = "appPath2";
            ConcurrentQueue<string> eventHandlingQueue = new ConcurrentQueue<string>();
            for (int i = 0; i < itemCount; i++)
            {
                eventHandlingQueue.Enqueue(firstAppPath);
            }

            for (int i = 0; i < itemCount; i++)
            {
                eventHandlingQueue.Enqueue(secondAppPath);
            }

            // Act
            string pathFromQueue = await EventHandler.GetAppPathFromQueueAsync(eventHandlingQueue, dequeueDelay).ConfigureAwait(false);

            // Assert
            Assert.AreEqual(firstAppPath, pathFromQueue);
            Assert.HasCount(itemCount, eventHandlingQueue);
        }

        [TestMethod]
        [DataRow(5)]
        public async Task EventHandlerMustReturnPathAndRetainAllPathsAfterEncounteringADifferentPathAsync(int itemCount)
        {
            // Arrange
            int dequeueDelay = 0;
            string firstAppPath = "appPath1";
            string secondAppPath = "appPath2";
            ConcurrentQueue<string> eventHandlingQueue = new ConcurrentQueue<string>();
            for (int i = 0; i < itemCount; i++)
            {
                eventHandlingQueue.Enqueue(firstAppPath);
            }

            for (int i = 0; i < itemCount; i++)
            {
                eventHandlingQueue.Enqueue(secondAppPath);
            }

            for (int i = 0; i < itemCount; i++)
            {
                eventHandlingQueue.Enqueue(firstAppPath);
            }

            // Act
            string pathFromQueue = await EventHandler.GetAppPathFromQueueAsync(eventHandlingQueue, dequeueDelay).ConfigureAwait(false);

            // Assert
            Assert.AreEqual(firstAppPath, pathFromQueue);
            Assert.HasCount(itemCount * 2, eventHandlingQueue);
        }
    }
}
