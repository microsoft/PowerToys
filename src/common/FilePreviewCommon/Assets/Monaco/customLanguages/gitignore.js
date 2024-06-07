export function gitignoreDefinition() {
    return {
        defaultToken: 'invalid',

        tokenizer: {
            root: [
                [/^#.*$/, 'comment'],
                [/(^!\s*(?:\\\s|\S)+)((?:\s+(?:\\\s|\S)+)*)/, ['custom-gitignore.negation', 'invalid']],
                [/.*((?<!(^|\/))\*\*.*|\*\*(?!(\/|$))).*/, 'invalid'],
                [/(^\s*(?:\\\s|\S)+)((?:\s+(?:\\\s|\S)+)*)/, ['tag', 'invalid']],
            ]
        }
    };
}