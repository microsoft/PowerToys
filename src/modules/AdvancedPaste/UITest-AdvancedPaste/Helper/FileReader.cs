// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Microsoft.AdvancedPaste.UITests.Helper;

public class FileReader
{
    public static string ReadContent(string filePath)
    {
        try
        {
            return File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read file: {ex.Message}", ex);
        }
    }

    public static string ReadRTFPlainText(string filePath)
    {
        try
        {
            using (var rtb = new System.Windows.Forms.RichTextBox())
            {
                rtb.Rtf = File.ReadAllText(filePath);
                return rtb.Text;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read plain text from file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Compares the contents of two RTF files to check if they are consistent.
    /// </summary>
    /// <param name="firstFilePath">Path to the first RTF file</param>
    /// <param name="secondFilePath">Path to the second RTF file</param>
    /// <param name="compareFormatting">If true, compares the raw RTF content (including formatting).
    /// If false, compares only the plain text content.</param>
    /// <returns>
    /// A tuple containing: (bool isConsistent, string firstContent, string secondContent)
    /// - isConsistent: true if the files are consistent according to the comparison method
    /// - firstContent: the content of the first file
    /// - secondContent: the content of the second file
    /// </returns>
    public static (bool IsConsistent, string FirstContent, string SecondContent) CompareRtfFiles(
        string firstFilePath,
        string secondFilePath,
        bool compareFormatting = false)
    {
        try
        {
            string firstContent, secondContent;

            if (compareFormatting)
            {
                // Compare raw RTF content (including formatting)
                firstContent = ReadContent(firstFilePath);
                secondContent = ReadContent(secondFilePath);
            }
            else
            {
                // Compare only the plain text content
                firstContent = ReadRTFPlainText(firstFilePath);
                secondContent = ReadRTFPlainText(secondFilePath);
            }

            bool isConsistent = string.Equals(firstContent, secondContent, StringComparison.Ordinal);
            return (isConsistent, firstContent, secondContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to compare RTF files: {ex.Message}", ex);
        }
    }
}
