using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Wox.Plugin.Everything.Everything.Exceptions;

namespace Wox.Plugin.Everything.Everything
{
    public sealed class EverythingAPI
    {
        #region DllImport
        [DllImport(EVERYTHING_DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern int Everything_SetSearchW(string lpSearchString);
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern void Everything_SetMatchPath(bool bEnable);
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern void Everything_SetMatchCase(bool bEnable);
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern void Everything_SetMatchWholeWord(bool bEnable);
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern void Everything_SetRegex(bool bEnable);
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern void Everything_SetMax(int dwMax);
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern void Everything_SetOffset(int dwOffset);

        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern bool Everything_GetMatchPath();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern bool Everything_GetMatchCase();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern bool Everything_GetMatchWholeWord();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern bool Everything_GetRegex();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern UInt32 Everything_GetMax();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern UInt32 Everything_GetOffset();
        [DllImport(EVERYTHING_DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern string Everything_GetSearchW();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern StateCode Everything_GetLastError();

        [DllImport(EVERYTHING_DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern bool Everything_QueryW(bool bWait);

        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern void Everything_SortResultsByPath();

        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern int Everything_GetNumFileResults();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern int Everything_GetNumFolderResults();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern int Everything_GetNumResults();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern int Everything_GetTotFileResults();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern int Everything_GetTotFolderResults();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern int Everything_GetTotResults();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern bool Everything_IsVolumeResult(int nIndex);
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern bool Everything_IsFolderResult(int nIndex);
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern bool Everything_IsFileResult(int nIndex);
        [DllImport(EVERYTHING_DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern void Everything_GetResultFullPathNameW(int nIndex, StringBuilder lpString, int nMaxCount);
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern void Everything_Reset();
        #endregion

        const string EVERYTHING_DLL_NAME = "Everything.dll";
        enum StateCode
        {
            OK,
            MemoryError,
            IPCError,
            RegisterClassExError,
            CreateWindowError,
            CreateThreadError,
            InvalidIndexError,
            InvalidCallError
        }

        /// <summary>
        /// Gets or sets a value indicating whether [match path].
        /// </summary>
        /// <value><c>true</c> if [match path]; otherwise, <c>false</c>.</value>
        public Boolean MatchPath
        {
            get
            {
                return Everything_GetMatchPath();
            }
            set
            {
                Everything_SetMatchPath(value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [match case].
        /// </summary>
        /// <value><c>true</c> if [match case]; otherwise, <c>false</c>.</value>
        public Boolean MatchCase
        {
            get
            {
                return Everything_GetMatchCase();
            }
            set
            {
                Everything_SetMatchCase(value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [match whole word].
        /// </summary>
        /// <value><c>true</c> if [match whole word]; otherwise, <c>false</c>.</value>
        public Boolean MatchWholeWord
        {
            get
            {
                return Everything_GetMatchWholeWord();
            }
            set
            {
                Everything_SetMatchWholeWord(value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [enable regex].
        /// </summary>
        /// <value><c>true</c> if [enable regex]; otherwise, <c>false</c>.</value>
        public Boolean EnableRegex
        {
            get
            {
                return Everything_GetRegex();
            }
            set
            {
                Everything_SetRegex(value);
            }
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public void Reset()
        {
            Everything_Reset();
        }

        private void no()
        {
            switch (Everything_GetLastError())
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

        /// <summary>
        /// Searches the specified key word.
        /// </summary>
        /// <param name="keyWord">The key word.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="maxCount">The max count.</param>
        /// <returns></returns>
        public IEnumerable<SearchResult> Search(string keyWord, int offset = 0, int maxCount = 100)
        {
            if (string.IsNullOrEmpty(keyWord))
                throw new ArgumentNullException("keyWord");

            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");

            if (maxCount < 0)
                throw new ArgumentOutOfRangeException("maxCount");

            if (keyWord.StartsWith("@"))
            {
                Everything_SetRegex(true);
                keyWord = keyWord.Substring(1);
            }
            Everything_SetSearchW(keyWord);
            Everything_SetOffset(offset);
            Everything_SetMax(maxCount);


            if (!Everything_QueryW(true))
            {
                switch (Everything_GetLastError())
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
                yield break;
            }

            const int bufferSize = 4096;
            StringBuilder buffer = new StringBuilder(bufferSize);
            for (int idx = 0; idx < Everything_GetNumResults(); ++idx)
            {
                Everything_GetResultFullPathNameW(idx, buffer, bufferSize);

                var result = new SearchResult { FullPath = buffer.ToString() };
                if (Everything_IsFolderResult(idx))
                    result.Type = ResultType.Folder;
                else if (Everything_IsFileResult(idx))
                    result.Type = ResultType.File;

                yield return result;
            }
        }
    }
}
