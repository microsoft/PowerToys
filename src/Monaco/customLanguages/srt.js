export function srtDefinition() {
    return {
        tokenizer: {
            root: [
                [/\s*\d+/, 'number', '@block']
            ],
            
            block: [
                [/^\d{2}:\d{2}:\d{2},\d{3} --> \d{2}:\d{2}:\d{2},\d{3}/, {
                    cases: {
                        '@eos': {token: 'type.identifier', next: '@subtitle'},
                        '@default': {token: 'type.identifier', next: '@ignore'}
                    }
                }],
                [/^$/, 'string', '@pop']
            ],
            
            ignore: [
                [/.+$/, '', '@subtitle']
            ],
            
            subtitle: [
                [/^$/, 'string', '@popall'],
                [/<\/?(?:[ibu]|font(?:\s+color="[^"]+"\s*)?)>/, 'tag'],
                [/./, 'string']
            ]
        }
    };
}