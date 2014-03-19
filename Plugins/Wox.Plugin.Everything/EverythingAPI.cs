using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Wox.Plugin.Everything
{
    public sealed class EverythingAPI
    {

        #region Const
        const string EVERYTHING_DLL_NAME = "Everything.dll";
        #endregion

        #region DllImport
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern int Everything_SetSearch(string lpSearchString);
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
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern string Everything_GetSearch();
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern StateCode Everything_GetLastError();

        [DllImport(EVERYTHING_DLL_NAME, EntryPoint = "Everything_QueryW")]
        private static extern bool Everything_Query(bool bWait);

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
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern void Everything_GetResultFullPathName(int nIndex, StringBuilder lpString, int nMaxCount);
        [DllImport(EVERYTHING_DLL_NAME)]
        private static extern void Everything_Reset();
        #endregion

        #region Enum
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
        #endregion

        #region Property

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
        #endregion

        #region Public Method
        /// <summary>
        /// Resets this instance.
        /// </summary>
        public void Reset()
        {
            Everything_Reset();
        }

        /// <summary>
        /// Searches the specified key word.
        /// </summary>
        /// <param name="keyWord">The key word.</param>
        /// <returns></returns>
        public IEnumerable<SearchResult> Search(string keyWord)
        {
            return Search(keyWord, 0, int.MaxValue);
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
        public IEnumerable<SearchResult> Search(string keyWord, int offset, int maxCount)
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
            Everything_SetSearch(keyWord);
            Everything_SetOffset(offset);
            Everything_SetMax(maxCount);


            if (!Everything_Query(true))
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

            Everything_SortResultsByPath();

            const int bufferSize = 4096;
            StringBuilder buffer = new StringBuilder(bufferSize);
            for (int idx = 0; idx < Everything_GetNumResults(); ++idx)
            {
                Everything_GetResultFullPathName(idx, buffer, bufferSize);

                var result = new SearchResult() { FullPath = buffer.ToString() };
                if (Everything_IsFolderResult(idx))
                    result.Type = ResultType.Folder;
                else if (Everything_IsFileResult(idx))
                    result.Type = ResultType.File;

                yield return result;
            }
        }
        #endregion
    }

    public enum ResultType
    {
        Volume,
        Folder,
        File
    }

    public class SearchResult
    {
        public string FullPath { get; set; }
        public ResultType Type { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class MemoryErrorException : ApplicationException
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public class IPCErrorException : ApplicationException
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public class RegisterClassExException : ApplicationException
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public class CreateWindowException : ApplicationException
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public class CreateThreadException : ApplicationException
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public class InvalidIndexException : ApplicationException
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public class InvalidCallException : ApplicationException
    {
    }
}
