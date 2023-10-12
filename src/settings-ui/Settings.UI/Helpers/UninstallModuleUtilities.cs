// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Utility class for performing various file and string operations.
    /// </summary>
    internal sealed class UninstallModuleUtilities
    {
        /// <summary>
        /// Get a list of file names in a specified directory.
        /// </summary>
        /// <param name="dirPath">The path to the directory to retrieve file names from.</param>
        /// <returns>A list of file names in the specified directory or null if the directory does not exist.</returns>
        public static List<string> GetFilesNamesInDir(string dirPath)
        {
            try
            {
                if (Directory.Exists(dirPath))
                {
                    string[] filePaths = Directory.GetFiles(dirPath);

                    List<string> fileNames = new List<string>();
                    foreach (string filePath in filePaths)
                    {
                        fileNames.Add(Path.GetFileName(filePath));
                    }

                    return fileNames;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Read words from a text file.
        /// </summary>
        /// <param name="filePath">The path to the text file to read.</param>
        /// <param name="delimiter">The optional delimiter used to split words in the file.</param>
        /// <returns>A list of words read from the file or null if the file does not exist.</returns>
        public static List<string> ReadWordsFromFile(string filePath, string delimiter = " ")
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string fileContent = File.ReadAllText(filePath);

                    string[] words = fileContent.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);

                    return new List<string>(words);
                }
                else
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Write a string to a text file.
        /// </summary>
        /// <param name="word">The string to write to the file.</param>
        /// <param name="filePath">The path to the text file to write to.</param>
        /// <param name="delimiter">The optional delimiter to separate the strings in the file.</param>
        public static void WriteWordToFile(string word, string filePath, string delimiter = " ")
        {
            try
            {
                string content = string.Join(delimiter, word);

                File.WriteAllText(filePath, content);
            }
            catch (Exception)
            {
                // Handle any exceptions that may occur during the operation
            }
        }

        /// <summary>
        /// Find strings in a list that contain a specified word.
        /// </summary>
        /// <param name="inputList">The list of strings to search within.</param>
        /// <param name="targetWord">The word to search for within the strings.</param>
        /// <returns>A list of strings containing the target word.</returns>
        public static List<string> FindStringsContainingWord(List<string> inputList, string targetWord)
        {
            if (inputList == null || inputList.Count == 0)
            {
                return new List<string>();
            }

            if (string.IsNullOrEmpty(targetWord))
            {
                return new List<string>();
            }

            List<string> result = inputList.Where(item => item.Contains(targetWord)).ToList();
            return result;
        }

        /// <summary>
        /// Check if a list of strings contains a specific word.
        /// </summary>
        /// <param name="wordList">The list of strings to check.</param>
        /// <param name="targetWord">The word to search for in the list.</param>
        /// <returns>True if the list contains the target word; otherwise, false.</returns>
        public static bool DoesListContainWord(List<string> wordList, string targetWord)
        {
            if (wordList == null || wordList.Count == 0)
            {
                return false;
            }

            if (string.IsNullOrEmpty(targetWord))
            {
                return false;
            }

            return wordList.Contains(targetWord);
        }

        /// <summary>
        /// Delete a file.
        /// </summary>
        /// <param name="filePath">The path to the file to delete.</param>
        /// <returns>True if the file was successfully deleted; otherwise, false.</returns>
        public static bool DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle any exceptions that may occur during the operation
                return false;
            }
        }
    }
}
