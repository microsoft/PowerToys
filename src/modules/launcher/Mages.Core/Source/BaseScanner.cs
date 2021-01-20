namespace Mages.Core.Source
{
    using System;
    using System.Collections.Generic;

    abstract class BaseScanner : IDisposable
    {
        #region Fields

        private readonly Stack<Int32> _columns;

        private Boolean _disposed;
        private Int32 _column;
        private Int32 _row;
        private Int32 _position;

        #endregion

        #region ctor

        public BaseScanner()
        {
            _columns = new Stack<Int32>();
            _row = 1;
            _column = 0;
            _position = 0;
        }

        #endregion

        #region Properties

        public TextPosition Position
        {
            get { return new TextPosition(_row, _column, _position); }
        }

        #endregion

        #region Methods

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _columns.Clear();
                Cleanup();
            }
        }

        protected abstract void Cleanup();

        protected void NextRow()
        {
            _columns.Push(_column);
            _column = 1;
            _row++;
            _position++;
        }

        protected void NextColumn()
        {
            _column++;
            _position++;
        }

        #endregion
    }
}
