/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
define('vs/basic-languages/shell/shell',["require", "exports"], function (require, exports) {
    "use strict";
    Object.defineProperty(exports, "__esModule", { value: true });
    exports.language = exports.conf = void 0;
    exports.conf = {
        comments: {
            lineComment: '#'
        },
        brackets: [
            ['{', '}'],
            ['[', ']'],
            ['(', ')']
        ],
        autoClosingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '"', close: '"' },
            { open: "'", close: "'" },
            { open: '`', close: '`' }
        ],
        surroundingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '"', close: '"' },
            { open: "'", close: "'" },
            { open: '`', close: '`' }
        ]
    };
    exports.language = {
        defaultToken: '',
        ignoreCase: true,
        tokenPostfix: '.shell',
        brackets: [
            { token: 'delimiter.bracket', open: '{', close: '}' },
            { token: 'delimiter.parenthesis', open: '(', close: ')' },
            { token: 'delimiter.square', open: '[', close: ']' }
        ],
        keywords: [
            'if',
            'then',
            'do',
            'else',
            'elif',
            'while',
            'until',
            'for',
            'in',
            'esac',
            'fi',
            'fin',
            'fil',
            'done',
            'exit',
            'set',
            'unset',
            'export',
            'function'
        ],
        builtins: [
            'ab',
            'awk',
            'bash',
            'beep',
            'cat',
            'cc',
            'cd',
            'chown',
            'chmod',
            'chroot',
            'clear',
            'cp',
            'curl',
            'cut',
            'diff',
            'echo',
            'find',
            'gawk',
            'gcc',
            'get',
            'git',
            'grep',
            'hg',
            'kill',
            'killall',
            'ln',
            'ls',
            'make',
            'mkdir',
            'openssl',
            'mv',
            'nc',
            'node',
            'npm',
            'ping',
            'ps',
            'restart',
            'rm',
            'rmdir',
            'sed',
            'service',
            'sh',
            'shopt',
            'shred',
            'source',
            'sort',
            'sleep',
            'ssh',
            'start',
            'stop',
            'su',
            'sudo',
            'svn',
            'tee',
            'telnet',
            'top',
            'touch',
            'vi',
            'vim',
            'wall',
            'wc',
            'wget',
            'who',
            'write',
            'yes',
            'zsh'
        ],
        // we include these common regular expressions
        symbols: /[=><!~?&|+\-*\/\^;\.,]+/,
        // The main tokenizer for our languages
        tokenizer: {
            root: [
                { include: '@whitespace' },
                [
                    /[a-zA-Z]\w*/,
                    {
                        cases: {
                            '@keywords': 'keyword',
                            '@builtins': 'type.identifier',
                            '@default': ''
                        }
                    }
                ],
                { include: '@strings' },
                { include: '@parameters' },
                { include: '@heredoc' },
                [/[{}\[\]()]/, '@brackets'],
                [/-+\w+/, 'attribute.name'],
                [/@symbols/, 'delimiter'],
                { include: '@numbers' },
                [/[,;]/, 'delimiter']
            ],
            whitespace: [
                [/\s+/, 'white'],
                [/(^#!.*$)/, 'metatag'],
                [/(^#.*$)/, 'comment']
            ],
            numbers: [
                [/\d*\.\d+([eE][\-+]?\d+)?/, 'number.float'],
                [/0[xX][0-9a-fA-F_]*[0-9a-fA-F]/, 'number.hex'],
                [/\d+/, 'number']
            ],
            // Recognize strings, including those broken across lines
            strings: [
                [/'/, 'string', '@stringBody'],
                [/"/, 'string', '@dblStringBody']
            ],
            stringBody: [
                [/'/, 'string', '@popall'],
                [/./, 'string']
            ],
            dblStringBody: [
                [/"/, 'string', '@popall'],
                [/./, 'string']
            ],
            heredoc: [
                [
                    /(<<[-<]?)(\s*)(['"`]?)([\w\-]+)(['"`]?)/,
                    [
                        'constants',
                        'white',
                        'string.heredoc.delimiter',
                        'string.heredoc',
                        'string.heredoc.delimiter'
                    ]
                ]
            ],
            parameters: [
                [/\$\d+/, 'variable.predefined'],
                [/\$\w+/, 'variable'],
                [/\$[*@#?\-$!0_]/, 'variable'],
                [/\$'/, 'variable', '@parameterBodyQuote'],
                [/\$"/, 'variable', '@parameterBodyDoubleQuote'],
                [/\$\(/, 'variable', '@parameterBodyParen'],
                [/\$\{/, 'variable', '@parameterBodyCurlyBrace']
            ],
            parameterBodyQuote: [
                [/[^#:%*@\-!_']+/, 'variable'],
                [/[#:%*@\-!_]/, 'delimiter'],
                [/[']/, 'variable', '@pop']
            ],
            parameterBodyDoubleQuote: [
                [/[^#:%*@\-!_"]+/, 'variable'],
                [/[#:%*@\-!_]/, 'delimiter'],
                [/["]/, 'variable', '@pop']
            ],
            parameterBodyParen: [
                [/[^#:%*@\-!_)]+/, 'variable'],
                [/[#:%*@\-!_]/, 'delimiter'],
                [/[)]/, 'variable', '@pop']
            ],
            parameterBodyCurlyBrace: [
                [/[^#:%*@\-!_}]+/, 'variable'],
                [/[#:%*@\-!_]/, 'delimiter'],
                [/[}]/, 'variable', '@pop']
            ]
        }
    };
});

