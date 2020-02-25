using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Wox.Infrastructure.Logger;
using Wox.Plugin.Everything.Everything.Exceptions;

namespace Wox.Plugin.Everything.Everything
{
    public interface IEverythingApi
    {
        /// <summary>
        /// Searches the specified key word.
        /// </summary>
        /// <param name="keyWord">The key word.</param>
        /// <param name="token">token that allow cancellation</param>
        /// <param name="offset">The offset.</param>
        /// <param name="maxCount">The max count.</param>
        /// <returns></returns>
        List<SearchResult> Search(string keyWord, CancellationToken token, int offset = 0, int maxCount = 100);

        void Load(string sdkPath);
    }

    public sealed class EverythingApi : IEverythingApi
    {
        private const int BufferSize = 4096;

        private readonly object _syncObject = new object();
        // cached buffer to remove redundant allocations.
        private readonly StringBuilder _buffer = new StringBuilder(BufferSize);

        public enum StateCode
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
        /// Resets this instance.
        /// </summary>
        public void Reset()
        {
            lock (_syncObject)
            {
                EverythingApiDllImport.Everything_Reset();
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
        public List<SearchResult> Search(string keyWord, CancellationToken token, int offset = 0, int maxCount = 100)
        {
            if (string.IsNullOrEmpty(keyWord))
                throw new ArgumentNullException(nameof(keyWord));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (maxCount < 0)
                throw new ArgumentOutOfRangeException(nameof(maxCount));

            lock (_syncObject)
            {
                if (keyWord.StartsWith("@"))
                {
                    EverythingApiDllImport.Everything_SetRegex(true);
                    keyWord = keyWord.Substring(1);
                }

                EverythingApiDllImport.Everything_SetSearchW(keyWord);
                EverythingApiDllImport.Everything_SetOffset(offset);
                EverythingApiDllImport.Everything_SetMax(maxCount);

                if (token.IsCancellationRequested)
                {
                    return null;
                }


                if (!EverythingApiDllImport.Everything_QueryW(true))
                {
                    CheckAndThrowExceptionOnError();
                    return null;
                }

                var results = new List<SearchResult>();
                for (int idx = 0; idx < EverythingApiDllImport.Everything_GetNumResults(); ++idx)
                {
                    if (token.IsCancellationRequested)
                    {
                        return null;
                    }

                    EverythingApiDllImport.Everything_GetResultFullPathNameW(idx, _buffer, BufferSize);

                    var result = new SearchResult { FullPath = _buffer.ToString() };
                    if (EverythingApiDllImport.Everything_IsFolderResult(idx))
                        result.Type = ResultType.Folder;
                    else if (EverythingApiDllImport.Everything_IsFileResult(idx))
                        result.Type = ResultType.File;

                    results.Add(result);
                }

                Reset();

                return results;
            }
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
