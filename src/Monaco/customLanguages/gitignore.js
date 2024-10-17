export function gitignoreDefinition() {
    return {
        defaultToken: 'invalid',

        tokenizer: {
            root: [
                [/^#.*$/, 'comment'],
                [/.*((?<!(^|\/))\*\*.*|\*\*(?!(\/|$))).*/, 'invalid'],
                [/((?:^!\s*(?:\\\s|\S)+)?)((?:^\s*(?:\\\s|\S)+)?)((?:\s+(?:\\\s|\S)+)*)/, ['custom-gitignore.negation', 'tag', 'invalid']]
            ]
        }
    };
}