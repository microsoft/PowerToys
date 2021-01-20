namespace Mages.Core.Tokens
{
    using Mages.Core.Source;
    using System;
    using System.Collections.Generic;
    using System.Text;

    sealed class StringTokenizer : ITokenizer
    {
        #region Fields

        private static readonly Int32[] DigitWeights = new[] { 4096, 256, 16, 1 };

        #endregion

        #region Methods

        public IToken Next(IScanner scanner)
        {
            var literal = scanner.Current == CharacterTable.At;
            var state = new StringState(scanner, literal);

            if (literal)
            {
                scanner.MoveNext();
            }

            if (scanner.MoveNext())
            {
                return state.Normal();
            }
            
            return state.Error();
        }

        #endregion

        #region State

        struct StringState
        {
            private readonly IScanner _scanner;
            private readonly TextPosition _start;
            private readonly Boolean _literal;

            private StringBuilder _buffer;
            private List<ParseError> _errors;

            public StringState(IScanner scanner, Boolean literal)
            {
                _buffer = StringBuilderPool.Pull();
                _scanner = scanner;
                _start = scanner.Position;
                _errors = null;
                _literal = literal;
            }

            public IToken Normal()
            {
                do
                {
                    var current = _scanner.Current;

                    if (current == CharacterTable.DoubleQuotationMark && (!_literal || !_scanner.PeekMoveNext(CharacterTable.DoubleQuotationMark)))
                    {
                        return Emit();
                    }
                    else if (!_literal && current == CharacterTable.Backslash)
                    {
                        return Escaped();
                    }
                    else
                    {
                        _buffer.Append(Char.ConvertFromUtf32(current));
                    }
                }
                while (_scanner.MoveNext());

                return Error();
            }

            public IToken Error()
            {
                AddError(ErrorCode.StringMismatch, _scanner.Position.ToRange());
                return Emit();
            }

            private IToken Escaped()
            {
                if (_scanner.MoveNext())
                {
                    switch (_scanner.Current)
                    {
                        case CharacterTable.SmallA: _buffer.Append('\a'); _scanner.MoveNext(); break;
                        case CharacterTable.SmallB: _buffer.Append('\b'); _scanner.MoveNext(); break;
                        case CharacterTable.SmallF: _buffer.Append('\f'); _scanner.MoveNext(); break;
                        case CharacterTable.SmallR: _buffer.Append('\r'); _scanner.MoveNext(); break;
                        case CharacterTable.SmallT: _buffer.Append('\t'); _scanner.MoveNext(); break;
                        case CharacterTable.SmallV: _buffer.Append('\v'); _scanner.MoveNext(); break;
                        case CharacterTable.SmallN: _buffer.Append('\n'); _scanner.MoveNext(); break;
                        case CharacterTable.SmallX: _buffer.Append(GetHexAsciiCharacter()); break;
                        case CharacterTable.SmallU: _buffer.Append(GetUnicodeCharacter()); break;
                        case CharacterTable.Zero: _buffer.Append('\0'); _scanner.MoveNext(); break;
                        case CharacterTable.Backslash: _buffer.Append('\\'); _scanner.MoveNext(); break;
                        case CharacterTable.QuestionMark: _buffer.Append('?'); _scanner.MoveNext(); break;
                        case CharacterTable.SingleQuotationMark: _buffer.Append('\''); _scanner.MoveNext(); break;
                        case CharacterTable.DoubleQuotationMark: _buffer.Append('\"'); _scanner.MoveNext(); break;
                        default: AddError(ErrorCode.EscapeSequenceInvalid, _scanner.Position.ToRange()); break;
                    }

                    if (_scanner.Current != CharacterTable.End)
                    {
                        return Normal();
                    }
                }

                return Error();
            }

            private String GetHexAsciiCharacter()
            {
                var start = _scanner.Position;

                if (_scanner.MoveNext() && _scanner.Current.IsHex())
                {
                    var sum = 16 * _scanner.Current.FromHex();

                    if (_scanner.MoveNext() && _scanner.Current.IsHex())
                    {
                        sum += _scanner.Current.FromHex();
                        _scanner.MoveNext();
                        return Char.ConvertFromUtf32(sum);
                    }
                }

                AddError(ErrorCode.AsciiSequenceInvalid, _scanner.Position.From(start));
                return String.Empty;
            }

            private String GetUnicodeCharacter()
            {
                var start = _scanner.Position;
                var sum = 0;

                for (var i = 0; i < DigitWeights.Length; i++)
                {
                    if (!_scanner.MoveNext() || !_scanner.Current.IsHex())
                    {
                        AddError(ErrorCode.UnicodeSequenceInvalid, _scanner.Position.From(start));
                        return String.Empty;
                    }

                    sum += DigitWeights[i] * _scanner.Current.FromHex();
                }

                _scanner.MoveNext();
                return Char.ConvertFromUtf32(sum);
            }

            private IToken Emit()
            {
                var content = _buffer.Stringify();
                _buffer = null;
                return new StringToken(content, _errors, _start, _scanner.Position);
            }

            private void AddError(ErrorCode code, ITextRange range)
            {
                if (_errors == null)
                {
                    _errors = new List<ParseError>();
                }

                _errors.Add(new ParseError(code, range));
            }
        }

        #endregion
    }
}
