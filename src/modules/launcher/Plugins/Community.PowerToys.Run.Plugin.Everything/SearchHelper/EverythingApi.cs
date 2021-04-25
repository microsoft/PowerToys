using Community.PowerToys.Run.Plugin.Everything.Exception;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Community.PowerToys.Run.Plugin.Everything.SearchHelper
{
    internal class EverythingApi
    {
        public enum StateCode
        {
            OK,
            MemoryError,
            IPCError,
            RegisterClassExError,
            CreateWindowError,
            CreateThreadError,
            InvalidIndexError,
            InvalidCallError,
        }

        public enum RequestFlag
        {
            FileName = 0x00000001,
            Path = 0x00000002,
            FullPathAndFileName = 0x00000004,
            Extension = 0x00000008,
            Size = 0x00000010,
            DateCreated = 0x00000020,
            DateModified = 0x00000040,
            DateAccessed = 0x00000080,
            Attributes = 0x00000100,
            FileListFileName = 0x00000200,
            RunCount = 0x00000400,
            DateRun = 0x00000800,
            DateRecentlyChanged = 0x00001000,
            HighlightedFileName = 0x00002000,
            HighlightedPath = 0x00004000,
            HighlightedFullPathAndFileName = 0x00008000,
        }
        /// <summary>
        /// Gets or sets a value indicating whether [match path].
        /// </summary>
        /// <value><c>true</c> if [match path]; otherwise, <c>false</c>.</value>
        public bool MatchPath
        {
            get
            {
                return EverythingApiDllImport.Everything_GetMatchPath();
            }
            set
            {
                EverythingApiDllImport.Everything_SetMatchPath(value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [match case].
        /// </summary>
        /// <value><c>true</c> if [match case]; otherwise, <c>false</c>.</value>
        public bool MatchCase
        {
            get
            {
                return EverythingApiDllImport.Everything_GetMatchCase();
            }
            set
            {
                EverythingApiDllImport.Everything_SetMatchCase(value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [match whole word].
        /// </summary>
        /// <value><c>true</c> if [match whole word]; otherwise, <c>false</c>.</value>
        public bool MatchWholeWord
        {
            get
            {
                return EverythingApiDllImport.Everything_GetMatchWholeWord();
            }
            set
            {
                EverythingApiDllImport.Everything_SetMatchWholeWord(value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [enable regex].
        /// </summary>
        /// <value><c>true</c> if [enable regex]; otherwise, <c>false</c>.</value>
        public bool EnableRegex
        {
            get
            {
                return EverythingApiDllImport.Everything_GetRegex();
            }
            set
            {
                EverythingApiDllImport.Everything_SetRegex(value);
            }
        }

        /// <summary>
        /// Searches the specified key word and reset the everything API afterwards
        /// </summary>
        /// <param name="keyWord">The key word.</param>
        /// <param name="token">when cancelled the current search will stop and exit (and would not reset)</param>
        /// <param name="offset">The offset.</param>
        /// <param name="maxCount">The max count.</param>
        /// <returns></returns>
        public List<SearchResult> Search(string keyWord, CancellationToken token, int maxCount)
        {
            var results = new List<SearchResult>();

            if (string.IsNullOrEmpty(keyWord))
                throw new ArgumentNullException(nameof(keyWord));
            if (maxCount < 0)
                throw new ArgumentOutOfRangeException(nameof(maxCount));

            if (token.IsCancellationRequested) { return results; }
            if (keyWord.StartsWith("@"))
            {
                EverythingApiDllImport.Everything_SetRegex(true);
                keyWord = keyWord.Substring(1);
            }
            else
            {
                EverythingApiDllImport.Everything_SetRegex(false);
            }

            if (token.IsCancellationRequested) { return results; }
            EverythingApiDllImport.Everything_SetRequestFlags(RequestFlag.HighlightedFileName | RequestFlag.HighlightedFullPathAndFileName);
            if (token.IsCancellationRequested) { return results; }
            EverythingApiDllImport.Everything_SetOffset(0);
            if (token.IsCancellationRequested) { return results; }
            EverythingApiDllImport.Everything_SetMax(maxCount);
            if (token.IsCancellationRequested) { return results; }
            EverythingApiDllImport.Everything_SetSearchW(keyWord);

            if (token.IsCancellationRequested) { return results; }
            if (!EverythingApiDllImport.Everything_QueryW(true))
            {
                CheckAndThrowExceptionOnError();
                return results;
            }

            if (token.IsCancellationRequested) { return results; }
            int count = EverythingApiDllImport.Everything_GetNumResults();
            for (int idx = 0; idx < count; ++idx)
            {
                if (token.IsCancellationRequested) { return results; }

                string fileNameHighted = Marshal.PtrToStringUni(EverythingApiDllImport.Everything_GetResultHighlightedFileNameW(idx));
                string fullPathHighted = Marshal.PtrToStringUni(EverythingApiDllImport.Everything_GetResultHighlightedFullPathAndFileNameW(idx));
                if (fileNameHighted == null | fullPathHighted == null)
                {
                    CheckAndThrowExceptionOnError();
                }
                if (token.IsCancellationRequested) { return results; }
                ConvertHighlightFormat(fileNameHighted, out List<int> fileNameHighlightData, out string fileName);
                if (token.IsCancellationRequested) { return results; }
                ConvertHighlightFormat(fullPathHighted, out List<int> fullPathHighlightData, out string fullPath);

                var result = new SearchResult
                {
                    FileName = fileName,
                    FileNameHightData = fileNameHighlightData,
                    FullPath = fullPath,
                    FullPathHightData = fullPathHighlightData,
                };

                if (token.IsCancellationRequested) { return results; }
                if (EverythingApiDllImport.Everything_IsFolderResult(idx))
                {
                    result.Type = ResultType.Folder;
                }
                else
                {
                    result.Type = ResultType.File;
                }

                results.Add(result);
            }

            return results;
        }

        private static void ConvertHighlightFormat(string contentHighlighted, out List<int> highlightData, out string fn)
        {
            highlightData = new List<int>();
            StringBuilder content = new StringBuilder();
            bool flag = false;
            char[] contentArray = contentHighlighted.ToCharArray();
            int count = 0;
            for (int i = 0; i < contentArray.Length; i++)
            {
                char current = contentHighlighted[i];
                if (current == '*')
                {
                    flag = !flag;
                    count = count + 1;
                }
                else
                {
                    if (flag)
                    {
                        highlightData.Add(i - count);
                    }
                    content.Append(current);
                }
            }
            fn = content.ToString();
        }

        [DllImport("kernel32.dll")]
        private static extern int LoadLibrary(string name);

        public void Load(string sdkPath)
        {
            LoadLibrary(sdkPath);
        }

        private static void CheckAndThrowExceptionOnError()
        {
            switch (EverythingApiDllImport.Everything_GetLastError())
            {
                case StateCode.CreateThreadError:
                    throw new CreateThreadException();
                case StateCode.CreateWindowError:
                    throw new CreateWindowException();
                case StateCode.InvalidCallError:
                    throw new InvalidCallException();
                case StateCode.InvalidIndexError:
                    throw new InvalidIndexException();
                case StateCode.IPCError:
                    throw new IPCErrorException();
                case StateCode.MemoryError:
                    throw new MemoryErrorException();
                case StateCode.RegisterClassExError:
                    throw new RegisterClassExException();
            }
        }
    }
}
