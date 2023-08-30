export function gitignoreDefinition() {
    return {
        defaultToken: 'invalid',

        tokenizer: {
            root: [
                [/^#.*$/, 'comment'],
                [/^\s*!.*/, 'invalid'],
                [/^\s*[^#]+/, "tag"]
            ]
        }
    };
}