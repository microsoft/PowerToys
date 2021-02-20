/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
export var conf = {
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
        { open: "'", close: "'" }
    ],
    surroundingPairs: [
        { open: '{', close: '}' },
        { open: '[', close: ']' },
        { open: '(', close: ')' },
        { open: '"', close: '"' },
        { open: "'", close: "'" }
    ]
};
export var language = {
    defaultToken: '',
    tokenPostfix: '.dockerfile',
    variable: /\${?[\w]+}?/,
    tokenizer: {
        root: [
            { include: '@whitespace' },
            { include: '@comment' },
            [/(ONBUILD)(\s+)/, ['keyword', '']],
            [/(ENV)(\s+)([\w]+)/, ['keyword', '', { token: 'variable', next: '@arguments' }]],
            [
                /(FROM|MAINTAINER|RUN|EXPOSE|ENV|ADD|ARG|VOLUME|LABEL|USER|WORKDIR|COPY|CMD|STOPSIGNAL|SHELL|HEALTHCHECK|ENTRYPOINT)/,
                { token: 'keyword', next: '@arguments' }
            ]
        ],
        arguments: [
            { include: '@whitespace' },
            { include: '@strings' },
            [
                /(@variable)/,
                {
                    cases: {
                        '@eos': { token: 'variable', next: '@popall' },
                        '@default': 'variable'
                    }
                }
            ],
            [
                /\\/,
                {
                    cases: {
                        '@eos': '',
                        '@default': ''
                    }
                }
            ],
            [
                /./,
                {
                    cases: {
                        '@eos': { token: '', next: '@popall' },
                        '@default': ''
                    }
                }
            ]
        ],
        // Deal with white space, including comments
        whitespace: [
            [
                /\s+/,
                {
                    cases: {
                        '@eos': { token: '', next: '@popall' },
                        '@default': ''
                    }
                }
            ]
        ],
        comment: [[/(^#.*$)/, 'comment', '@popall']],
        // Recognize strings, including those broken across lines with \ (but not without)
        strings: [
            [/\\'$/, '', '@popall'],
            [/\\'/, ''],
            [/'$/, 'string', '@popall'],
            [/'/, 'string', '@stringBody'],
            [/"$/, 'string', '@popall'],
            [/"/, 'string', '@dblStringBody']
        ],
        stringBody: [
            [
                /[^\\\$']/,
                {
                    cases: {
                        '@eos': { token: 'string', next: '@popall' },
                        '@default': 'string'
                    }
                }
            ],
            [/\\./, 'string.escape'],
            [/'$/, 'string', '@popall'],
            [/'/, 'string', '@pop'],
            [/(@variable)/, 'variable'],
            [/\\$/, 'string'],
            [/$/, 'string', '@popall']
        ],
        dblStringBody: [
            [
                /[^\\\$"]/,
                {
                    cases: {
                        '@eos': { token: 'string', next: '@popall' },
                        '@default': 'string'
                    }
                }
            ],
            [/\\./, 'string.escape'],
            [/"$/, 'string', '@popall'],
            [/"/, 'string', '@pop'],
            [/(@variable)/, 'variable'],
            [/\\$/, 'string'],
            [/$/, 'string', '@popall']
        ]
    }
};
