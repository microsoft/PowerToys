/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
define('vs/basic-languages/cameligo/cameligo',["require", "exports"], function (require, exports) {
    "use strict";
    Object.defineProperty(exports, "__esModule", { value: true });
    exports.language = exports.conf = void 0;
    exports.conf = {
        comments: {
            lineComment: '//',
            blockComment: ['(*', '*)']
        },
        brackets: [
            ['{', '}'],
            ['[', ']'],
            ['(', ')'],
            ['<', '>']
        ],
        autoClosingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '<', close: '>' },
            { open: "'", close: "'" }
        ],
        surroundingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '<', close: '>' },
            { open: "'", close: "'" }
        ]
    };
    exports.language = {
        defaultToken: '',
        tokenPostfix: '.cameligo',
        ignoreCase: true,
        brackets: [
            { open: '{', close: '}', token: 'delimiter.curly' },
            { open: '[', close: ']', token: 'delimiter.square' },
            { open: '(', close: ')', token: 'delimiter.parenthesis' },
            { open: '<', close: '>', token: 'delimiter.angle' }
        ],
        keywords: [
            'abs',
            'begin',
            'Bytes',
            'Crypto',
            'Current',
            'else',
            'end',
            'failwith',
            'false',
            'fun',
            'if',
            'in',
            'let',
            'let%entry',
            'let%init',
            'List',
            'list',
            'Map',
            'map',
            'match',
            'match%nat',
            'mod',
            'not',
            'operation',
            'Operation',
            'of',
            'Set',
            'set',
            'sender',
            'source',
            'String',
            'then',
            'true',
            'type',
            'with'
        ],
        typeKeywords: ['int', 'unit', 'string', 'tz'],
        operators: [
            '=',
            '>',
            '<',
            '<=',
            '>=',
            '<>',
            ':',
            ':=',
            'and',
            'mod',
            'or',
            '+',
            '-',
            '*',
            '/',
            '@',
            '&',
            '^',
            '%',
            '->',
            '<-'
        ],
        // we include these common regular expressions
        symbols: /[=><:@\^&|+\-*\/\^%]+/,
        // The main tokenizer for our languages
        tokenizer: {
            root: [
                // identifiers and keywords
                [
                    /[a-zA-Z_][\w]*/,
                    {
                        cases: {
                            '@keywords': { token: 'keyword.$0' },
                            '@default': 'identifier'
                        }
                    }
                ],
                // whitespace
                { include: '@whitespace' },
                // delimiters and operators
                [/[{}()\[\]]/, '@brackets'],
                [/[<>](?!@symbols)/, '@brackets'],
                [
                    /@symbols/,
                    {
                        cases: {
                            '@operators': 'delimiter',
                            '@default': ''
                        }
                    }
                ],
                // numbers
                [/\d*\.\d+([eE][\-+]?\d+)?/, 'number.float'],
                [/\$[0-9a-fA-F]{1,16}/, 'number.hex'],
                [/\d+/, 'number'],
                // delimiter: after number because of .\d floats
                [/[;,.]/, 'delimiter'],
                // strings
                [/'([^'\\]|\\.)*$/, 'string.invalid'],
                [/'/, 'string', '@string'],
                // characters
                [/'[^\\']'/, 'string'],
                [/'/, 'string.invalid'],
                [/\#\d+/, 'string']
            ],
            /* */
            comment: [
                [/[^\(\*]+/, 'comment'],
                //[/\(\*/,    'comment', '@push' ],    // nested comment  not allowed :-(
                [/\*\)/, 'comment', '@pop'],
                [/\(\*/, 'comment']
            ],
            string: [
                [/[^\\']+/, 'string'],
                [/\\./, 'string.escape.invalid'],
                [/'/, { token: 'string.quote', bracket: '@close', next: '@pop' }]
            ],
            whitespace: [
                [/[ \t\r\n]+/, 'white'],
                [/\(\*/, 'comment', '@comment'],
                [/\/\/.*$/, 'comment']
            ]
        }
    };
});

