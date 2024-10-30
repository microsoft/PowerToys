export function srtDefinition() {
    return {
        tokenizer: {
            root: [
                [/\s*\d+/, 'number', '@block']
            ],
            
            block: [
                [/^\d{2}:\d{2}:\d{2},\d{3} --> \d{2}:\d{2}:\d{2},\d{3}/, {
                    cases: {
                        '@eos': {token: 'tag', next: '@subtitle'},
                        '@default': {token: 'tag', next: '@ignore'}
                    }
                }],
                [/^$/, 'string', '@pop']
            ],
            
            ignore: [
                [/.+$/, '', '@subtitle']
            ],
            
            tags: [
                [/^$/, 'string', '@popall'],
                [/<b>/, 'string.bold', '@bold'],
                [/<i>/, 'string.emphasis', '@italic'],
                [/<u>/, 'string.underline', '@underline']
            ],
            
            subtitle: [
                {include: '@tags'},
                [/./, 'string']
            ],
            
            bold: [
                [/<\/b>/, 'string.bold', '@pop'],
                {include: '@tags'},
                [/./, 'string.bold'],
            ],
            
            italic: [
                [/<\/i>/, 'string.emphasis', '@pop'],
                {include: '@tags'},
                [/./, 'string.emphasis'],
            ],
            
            underline: [
                [/<\/u>/, 'string.underline', '@pop'],
                {include: '@tags'},
                [/./, 'string.underline'],
            ],
        }
    };
}