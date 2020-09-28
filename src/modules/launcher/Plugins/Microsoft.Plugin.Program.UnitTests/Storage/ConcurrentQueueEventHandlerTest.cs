// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Microsoft.Plugin.Program.Storage;
using NUnit.Framework;

namespace Microsoft.Plugin.Program.UnitTests.Storage
{
    [TestFixture]
    public class ConcurrentQueueEventHandlerTest
    {
        [TestCase]
        public void EventHandlerMustReturnEmptyPathForEmptyQueue()
        {
            // Arrange
            int dequeueDelay = 0;
            ConcurrentQueue<string> eventHandlingQueue = new ConcurrentQueue<string>();

            // Act
            string appPath = EventHandler.GetAppPathFromQueue(eventHandlingQueue, dequeueDelay);

            // Assert
            Assert.IsEmpty(appPath);
        }

        [TestCase(1)]
        [TestCase(10)]
        public void EventHandlerMustReturnPathForConcurrentQueueWithSameFilePaths(int itemCount)
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
            string pathFromQueue = EventHandler.GetAppPathFromQueue(eventHandlingQueue, dequeueDelay);

            // Assert
            Assert.AreEqual(pathFromQueue, appPath);
            Assert.AreEqual(eventHandlingQueue.Count, 0);
        }

        [TestCase(5)]
        public void EventHandlerMustReturnPathAndRetainDifferentFilePathsInQueue(int itemCount)
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
            string pathFromQueue = EventHandler.GetAppPathFromQueue(eventHandlingQueue, dequeueDelay);

            // Assert
            Assert.AreEqual(pathFromQueue, firstAppPath);
            Assert.AreEqual(eventHandlingQueue.Count, itemCount);
        }

        [TestCase(5)]
        public void EventHandlerMustReturnPathAndRetainAllPathsAfterEncounteringADifferentPath(int itemCount)
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
            string pathFromQueue = EventHandler.GetAppPathFromQueue(eventHandlingQueue, dequeueDelay);

            // Assert
            Assert.AreEqual(pathFromQueue, firstAppPath);
            Assert.AreEqual(eventHandlingQueue.Count, itemCount * 2);
        }
    }
}
