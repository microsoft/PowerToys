// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using System.Threading.Tasks;
using UtfUnknown;

namespace Peek.FilePreviewer.Previewers
{
    public static class ReadHelper
    {
        public static async Task<string> Read(string path)
        {
            DetectionResult result = CharsetDetector.DetectFromFile(path);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using var fs = OpenReadOnly(path);
            using var sr = new StreamReader(fs, result.Detected.Encoding);

            string content = await sr.ReadToEndAsync();
            return content;
        }

        public static FileStream OpenReadOnly(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
    }
}
