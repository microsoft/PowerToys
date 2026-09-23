// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace RobocopyUI.Helpers
{
    internal sealed class RCJParser
    {
        public sealed record RCJCommand(string Command, string? Argument);

        private readonly string _input;

        private int _position;

        internal RCJParser(string input)
        {
            _input = input;
            _position = 0;
        }

        public RCJCommand[] Parse()
        {
            var commands = new List<RCJCommand>();

            var currentArgument = new StringBuilder();
            var currentArgumentValue = new StringBuilder();
            bool inArgument = false;
            bool inArgumentValue = false;

            void AddCommand()
            {
                if (inArgument)
                {
                    if (inArgumentValue)
                    {
                        string currentArgumentValueStr = currentArgumentValue.ToString();
                        commands.Add(new RCJCommand(currentArgument.ToString(), string.IsNullOrEmpty(currentArgumentValueStr) ? null : currentArgumentValueStr));
                        currentArgument.Clear();
                        currentArgumentValue.Clear();
                    }
                    else
                    {
                        commands.Add(new RCJCommand(currentArgument.ToString(), null));
                        currentArgument.Clear();
                    }
                }
            }

            while (_position < _input.Length)
            {
                if (char.IsWhiteSpace(_input[_position]) || _input[_position] == '\r' || _input[_position] == '\n')
                {
                    _position++;
                    AddCommand();
                    inArgument = false;
                    inArgumentValue = false;
                    continue;
                }

                if (_input[_position] == ':' && _position + 1 < _input.Length && _input[_position + 1] == ':')
                {
                    while (_position < _input.Length && _input[_position] != '\n')
                    {
                        AddCommand();

                        _position++;
                        inArgument = false;
                        inArgumentValue = false;
                    }

                    continue;
                }

                if (_input[_position] == '/')
                {
                    AddCommand();

                    _position++;
                    inArgument = true;
                    inArgumentValue = false;
                    continue;
                }

                if (_input[_position] == ':' && inArgument && !inArgumentValue)
                {
                    inArgumentValue = true;
                    _position++;
                    continue;
                }

                if (inArgument)
                {
                    if (inArgumentValue)
                    {
                        currentArgumentValue.Append(_input[_position]);
                    }
                    else
                    {
                        currentArgument.Append(_input[_position]);
                    }
                }

                _position++;
            }

            AddCommand();

            return [.. commands];
        }
    }
}
