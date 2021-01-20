namespace Mages.Core.Source
{
    using System;
    using System.IO;

    sealed class StringScanner : BaseScanner, IScanner
    {
        #region Fields

        private readonly StringReader _source;

        private Int32 _current;
        private Int32 _previous;
        private Boolean _skip;
        private Boolean _swap;

        #endregion

        #region ctor

        public StringScanner(String source)
        {
            _source = new StringReader(source);
            _previous = CharacterTable.NullPtr;
            _current = CharacterTable.NullPtr;
        }

        #endregion

        #region Properties

        public Int32 Current
        {
            get { return _current; }
        }

        #endregion

        #region Methods

        public TextPosition GetPositionAt(Int32 index)
        {
            return new TextPosition(0, 0, index + 1);
        }

        public Boolean MoveNext()
        {
            Advance();
            return _current != CharacterTable.End;
        }

        public Boolean MoveBack()
        {
            Retreat();
            return _current != CharacterTable.End;
        }

        #endregion

        #region Helpers

        protected override void Cleanup()
        {
            _source.Dispose();
        }

        private void Retreat()
        {
            if (!_swap)
            {
                Swap();
            }
        }

        private void Advance()
        {
            if (_swap)
            {
                Swap();
            }
            else if (_current != CharacterTable.End)
            {
                if (_current == CharacterTable.LineFeed)
                {
                    NextRow();
                }
                else
                {
                    NextColumn();
                }

                _previous = _current;
                _current = Read();
            }
        }

        private Int32 Read()
        {
            var current = _source.Read();

            if (current == CharacterTable.CarriageReturn)
            {
                current = CharacterTable.LineFeed;
                _skip = true;
            }
            else if (_skip && _current == CharacterTable.LineFeed)
            {
                _skip = false;
                return Read();
            }
            else
            {
                _skip = false;
            }

            return current;
        }

        private void Swap()
        {
            var tmp = _current;
            _current = _previous;
            _previous = tmp;
            _swap = !_swap;
        }

        #endregion
    }
}
