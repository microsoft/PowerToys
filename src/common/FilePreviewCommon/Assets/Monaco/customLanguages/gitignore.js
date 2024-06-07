export function gitignoreDefinition() {
    return {
        defaultToken: 'invalid',

        tokenizer: {
            root: [
                [/^#.*$/, 'comment'],
                [/ahoj/, 'custom-gitignore.negation'],
                [/.*((?<!(^|\/))\*\*.*|\*\*(?!(\/|$))).*/, 'invalid'],
                [/(^\s*(?:\\\s|\S)+)((?:\s+(?:\\\s|\S)+)*)/, ['tag', 'invalid']],
            ]
        }
    };
}