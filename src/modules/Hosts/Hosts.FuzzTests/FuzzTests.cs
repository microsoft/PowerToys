// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.IO;
using System.IO.Abstractions;
using HostsUILib.Helpers;
using HostsUILib.Models;
using HostsUILib.Settings;
using Moq;
using Windows.ApplicationModel.DataTransfer;

namespace Hosts.FuzzTests
{
    public class FuzzTests
    {
        // Case1： Fuzzing method for ValidIPv4
        public static void FuzzValidIPv4(ReadOnlySpan<byte> input)
        {
            try
            {
                string address = System.Text.Encoding.UTF8.GetString(input);
                bool isValid = ValidationHelper.ValidIPv4(address);
            }
            catch (Exception ex) when (ex is OutOfMemoryException)
            {
                throw;
            }
        }

        // Case2: fuzzing method for ValidIPv6
        public static void FuzzValidIPv6(ReadOnlySpan<byte> input)
        {
            try
            {
                string address = System.Text.Encoding.UTF8.GetString(input);
                bool isValid = ValidationHelper.ValidIPv6(address);
            }
            catch (Exception ex) when (ex is OutOfMemoryException)
            {
                throw;
            }
        }

        // Case3: fuzzing method for ValidHosts
        public static void FuzzValidHosts(ReadOnlySpan<byte> input)
        {
            try
            {
                string hosts = System.Text.Encoding.UTF8.GetString(input);
                bool isValid = ValidationHelper.ValidHosts(hosts, true);
            }
            catch (Exception ex) when (ex is OutOfMemoryException)
            {
                // This is an example. It's important to filter out any *expected* exceptions from our code here.
                // However, catching all exceptions is considered an anti-pattern because it may suppress legitimate
                // issues, such as a NullReferenceException thrown by our code. In this case, we still re-throw
                // the exception, as the ToJsonFromXmlOrCsvAsync method is not expected to throw any exceptions.
                throw;
            }
        }

        // Case4 fuzzing method for WriteAsync;
        public static void FuzzWriteAsync(ReadOnlySpan<byte> data)
        {
            try
            {
                // mock ElevationHelper
                var mockElevationHelper = new Mock<IElevationHelper>();
                mockElevationHelper.Setup(eh => eh.IsElevated).Returns(true);

                // mock userSetting
                var mockUserSettings = new Mock<IUserSettings>();
                mockUserSettings.Setup(us => us.AdditionalLinesPosition).Returns(HostsAdditionalLinesPosition.Top);
                mockUserSettings.Setup(us => us.Encoding).Returns(HostsEncoding.Utf8);

                // Mock FileInfo.New(HostsFilePath)
                var mockFileInfo = new Mock<IFileInfo>();
                mockFileInfo.Setup(f => f.IsReadOnly).Returns(true); // Simulate a non-read-only file

                // mock FileSystem
                var mockFileSystem = new Mock<IFileSystem>();

                // Mocking the FileSystem methods
                var mockFileSystemWatcher = new Mock<IFileSystemWatcher>();
                mockFileSystem.Setup(fs => fs.FileSystemWatcher.New()).Returns(mockFileSystemWatcher.Object);
                var mockPath = new Mock<IPath>();
                mockFileSystem.Setup(fs => fs.Path).Returns(mockPath.Object);
                mockPath.Setup(p => p.GetDirectoryName(It.IsAny<string>())).Returns(@"C:\Windows\System32\drivers\etc");
                mockPath.Setup(p => p.GetFileName(It.IsAny<string>())).Returns("hosts");

                // Set up the mock FileSystem to return the mock file info
                mockFileSystem.Setup(fs => fs.FileInfo.New(It.IsAny<string>())).Returns(mockFileInfo.Object);

                HostsService hostsService = new HostsService(mockFileSystem.Object, mockUserSettings.Object, mockElevationHelper.Object);

                // fuzzing input
                string input = System.Text.Encoding.UTF8.GetString(data);
                string additionalLines = " ";
                if (input.Length <= 2)
                {
                    return;
                }

                var parts = SplitStringRandomly(input);
                string hosts = parts[0];
                string address = parts[1];
                string comments = parts[2];
                var entries = new List<Entry>
                {
                    new Entry(1, hosts, address, comments, true),
                };

                // fuzzing WriteAsync
                _ = Task.Run(async () => await hostsService.WriteAsync(additionalLines, entries));
            }
            catch (Exception ex) when (ex is ArgumentException)
            {
                throw;
            }
        }

        public static string[] SplitStringRandomly(string input)
        {
            Random rand = new Random();
            int length = input.Length;

            // Ensure the split points are valid
            int firstSplit = rand.Next(1, length - 1);  // Between 1 and length-1
            int secondSplit = rand.Next(firstSplit + 1, length);  // Between firstSplit+1 and length

            // Split the string into three parts using the split points
            string part1 = input.Substring(0, firstSplit);
            string part2 = input.Substring(firstSplit, secondSplit - firstSplit);
            string part3 = input.Substring(secondSplit);

            return new string[] { part1, part2, part3 };
        }
    }
}
