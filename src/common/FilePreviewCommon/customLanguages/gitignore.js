export function gitignoreDefinition() {
    return {
        defaultToken: 'invalid',

        tokenizer: {
            root: [
                [/^#.*$/, 'comment'],
                [/^\s*!/, 'negation'],
                [/^\s*[^#]+/, "tag"]
            ]
        }
    };
}