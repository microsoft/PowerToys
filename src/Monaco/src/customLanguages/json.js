// JSON (with comments) highlighting for the PowerToys Monaco bundle. monaco-editor's only JSON support is
// its JSON language service (a worker plus validation), which also loads every editor feature. This grammar
// emits the same token names as that service's tokenizer, so the built-in vs / vs-dark JSON colors apply.
export const conf = {
    wordPattern: /(-?\d*\.\d\w*)|([^[{\]}:",\s]+)/g,
    comments: {
        lineComment: '//',
        blockComment: ['/*', '*/'],
    },
    brackets: [
        ['{', '}'],
        ['[', ']'],
    ],
    autoClosingPairs: [
        { open: '{', close: '}', notIn: ['string'] },
        { open: '[', close: ']', notIn: ['string'] },
        { open: '"', close: '"', notIn: ['string'] },
    ],
};

export const language = {
    defaultToken: '',
    tokenPostfix: '.json',
    tokenizer: {
        root: [
            { include: '@whitespace' },
            [/"(?:[^"\\]|\\.)*"(?=\s*:)/, 'string.key'],
            [/"(?:[^"\\]|\\.)*"?/, 'string.value'],
            [/-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?/, 'number'],
            [/\b(?:true|false|null)\b/, 'keyword'],
            [/[{}]/, 'delimiter.bracket'],
            [/[[\]]/, 'delimiter.array'],
            [/:/, 'delimiter.colon'],
            [/,/, 'delimiter.comma'],
        ],
        whitespace: [
            [/[ \t\r\n]+/, ''],
            [/\/\*/, 'comment.block', '@comment'],
            [/\/\/.*$/, 'comment.line'],
        ],
        comment: [
            [/[^/*]+/, 'comment.block'],
            [/\*\//, 'comment.block', '@pop'],
            [/[/*]/, 'comment.block'],
        ],
    },
};
