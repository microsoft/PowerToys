namespace Mages.Core.Tokens
{
    using Mages.Core.Source;
    using System;
    using System.Collections.Generic;

    sealed class NumberTokenizer : ITokenizer
    {
        #region Methods

        public IToken Next(IScanner scanner)
        {
            var state = new NumberState(scanner);
            var current = scanner.Current;

            if (current == CharacterTable.FullStop)
            {
                return state.Dot();
            }
            else if (current == CharacterTable.Zero)
            {
                return state.Zero();
            }

            return state.Digit();
        }

        #endregion

        #region Helpers

        private static Boolean IsDotOperator(Int32 current)
        {
            return current == CharacterTable.SingleQuotationMark ||
                current == CharacterTable.CircumflexAccent || current == CharacterTable.Asterisk ||
                current == CharacterTable.Slash || current == CharacterTable.Backslash;
        }

        #endregion

        #region State

        struct NumberState
        {
            private readonly IScanner _scanner;
            private readonly TextPosition _start;

            private List<ParseError> _errors;
            private UInt64 _value;
            private UInt16 _digits;
            private Int32 _powers;

            public NumberState(IScanner scanner)
            {
                _scanner = scanner;
                _start = scanner.Position;
                _errors = null;
                _value = 0;
                _digits = 0;
                _powers = 0;
            }

            public Double Number
            {
                get { return _value * Math.Pow(10.0, -_digits) * Math.Pow(10.0, _powers); }
            }

            public IToken Zero()
            {
                if (!_scanner.MoveNext())
                {
                    return new NumberToken(0.0, _errors, _start, _scanner.Position);
                }

                var current = _scanner.Current;

                if (current == CharacterTable.SmallX)
                {
                    return Hex();
                }
                else if (current == CharacterTable.SmallB)
                {
                    return Binary();
                }
                else if (current.IsDigit() || current == CharacterTable.FullStop)
                {
                    return Digit();
                }

                return Final();
            }

            public IToken Dot()
            {
                if (_scanner.MoveNext())
                {
                    if (_scanner.Current.IsDigit())
                    {
                        return Decimal();
                    }

                    _scanner.MoveBack();
                }

                return new OperatorToken(TokenType.Dot, ".", _start);
            }

            public IToken Digit()
            {
                while (_scanner.Current.IsDigit())
                {
                    _value = _value * 10UL + (UInt64)(_scanner.Current - CharacterTable.Zero);

                    if (!_scanner.MoveNext())
                    {
                        break;
                    }
                }

                if (_scanner.Current == CharacterTable.FullStop && _scanner.MoveNext())
                {
                    return Decimal();
                }
                else if (_scanner.Current == CharacterTable.SmallE || _scanner.Current == CharacterTable.BigE)
                {
                    return Exponent();
                }

                return Final();
            }

            private IToken Binary()
            {
                var numbers = new List<Int32>();
                var weight = 1;

                while (_scanner.MoveNext() && _scanner.Current.IsInRange(CharacterTable.Zero, CharacterTable.One))
                {
                    numbers.Add(_scanner.Current - CharacterTable.Zero);
                }

                for (var i = numbers.Count - 1; i >= 0; --i)
                {
                    _value += (UInt64)(numbers[i] * weight);
                    weight *= 2;
                }

                return Final();
            }

            private IToken Hex()
            {
                var numbers = new List<Int32>();
                var weight = 1;

                while (_scanner.MoveNext() && _scanner.Current.IsHex())
                {
                    numbers.Add(_scanner.Current.FromHex());
                }

                for (var i = numbers.Count - 1; i >= 0; --i)
                {
                    _value += (UInt64)(numbers[i] * weight);
                    weight *= 16;
                }

                return Final();
            }

            private IToken Decimal()
            {
                while (_scanner.Current.IsDigit())
                {
                    _value = _value * 10UL + (UInt64)(_scanner.Current - CharacterTable.Zero);
                    _digits++;

                    if (!_scanner.MoveNext())
                    {
                        break;
                    }
                }

                if (_digits == 0)
                {
                    if (IsDotOperator(_scanner.Current))
                    {
                        _scanner.MoveBack();
                        return Final();
                    }

                    AddError(ErrorCode.FloatingMismatch, _scanner.Position.From(_start));
                }

                if (_scanner.Current == CharacterTable.SmallE || _scanner.Current == CharacterTable.BigE)
                {
                    return Exponent();
                }

                return Final();
            }

            private IToken Exponent()
            {
                var sign = 1;
                var num = 0;

                if (_scanner.MoveNext())
                {
                    if (_scanner.Current == CharacterTable.Minus)
                    {
                        _scanner.MoveNext();
                        sign = -1;
                    }
                    else if (_scanner.Current == CharacterTable.Plus)
                    {
                        _scanner.MoveNext();
                    }

                    while (_scanner.Current.IsDigit())
                    {
                        num++;
                        _powers = _powers * 10 + _scanner.Current - CharacterTable.Zero;

                        if (!_scanner.MoveNext())
                        {
                            break;
                        }
                    }
                }

                if (num == 0)
                {
                    AddError(ErrorCode.ScientificMismatch, _scanner.Position.From(_start));
                }

                _powers = sign * _powers;
                return Final();
            }

            private IToken Final()
            {
                _scanner.MoveBack();
                return new NumberToken(Number, _errors, _start, _scanner.Position);
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
