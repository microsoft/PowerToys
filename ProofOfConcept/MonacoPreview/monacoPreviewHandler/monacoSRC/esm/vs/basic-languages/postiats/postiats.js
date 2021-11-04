/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Artyom Shalkhakov. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *
 *  Based on the ATS/Postiats lexer by Hongwei Xi.
 *--------------------------------------------------------------------------------------------*/
export var conf = {
    comments: {
        lineComment: '//',
        blockComment: ['(*', '*)']
    },
    brackets: [
        ['{', '}'],
        ['[', ']'],
        ['(', ')'],
        ['<', '>']
    ],
    autoClosingPairs: [
        { open: '"', close: '"', notIn: ['string', 'comment'] },
        { open: '{', close: '}', notIn: ['string', 'comment'] },
        { open: '[', close: ']', notIn: ['string', 'comment'] },
        { open: '(', close: ')', notIn: ['string', 'comment'] }
    ]
};
export var language = {
    tokenPostfix: '.pats',
    // TODO: staload and dynload are followed by a special kind of string literals
    // with {$IDENTIFER} variables, and it also may make sense to highlight
    // the punctuation (. and / and \) differently.
    // Set defaultToken to invalid to see what you do not tokenize yet
    defaultToken: 'invalid',
    // keyword reference: https://github.com/githwxi/ATS-Postiats/blob/master/src/pats_lexing_token.dats
    keywords: [
        //
        'abstype',
        'abst0ype',
        'absprop',
        'absview',
        'absvtype',
        'absviewtype',
        'absvt0ype',
        'absviewt0ype',
        //
        'as',
        //
        'and',
        //
        'assume',
        //
        'begin',
        //
        /*
                "case", // CASE
        */
        //
        'classdec',
        //
        'datasort',
        //
        'datatype',
        'dataprop',
        'dataview',
        'datavtype',
        'dataviewtype',
        //
        'do',
        //
        'end',
        //
        'extern',
        'extype',
        'extvar',
        //
        'exception',
        //
        'fn',
        'fnx',
        'fun',
        //
        'prfn',
        'prfun',
        //
        'praxi',
        'castfn',
        //
        'if',
        'then',
        'else',
        //
        'ifcase',
        //
        'in',
        //
        'infix',
        'infixl',
        'infixr',
        'prefix',
        'postfix',
        //
        'implmnt',
        'implement',
        //
        'primplmnt',
        'primplement',
        //
        'import',
        //
        /*
                "lam", // LAM
                "llam", // LLAM
                "fix", // FIX
        */
        //
        'let',
        //
        'local',
        //
        'macdef',
        'macrodef',
        //
        'nonfix',
        //
        'symelim',
        'symintr',
        'overload',
        //
        'of',
        'op',
        //
        'rec',
        //
        'sif',
        'scase',
        //
        'sortdef',
        /*
        // HX: [sta] is now deprecated
        */
        'sta',
        'stacst',
        'stadef',
        'static',
        /*
                "stavar", // T_STAVAR
        */
        //
        'staload',
        'dynload',
        //
        'try',
        //
        'tkindef',
        //
        /*
                "type", // TYPE
        */
        'typedef',
        'propdef',
        'viewdef',
        'vtypedef',
        'viewtypedef',
        //
        /*
                "val", // VAL
        */
        'prval',
        //
        'var',
        'prvar',
        //
        'when',
        'where',
        //
        /*
                "for", // T_FOR
                "while", // T_WHILE
        */
        //
        'with',
        //
        'withtype',
        'withprop',
        'withview',
        'withvtype',
        'withviewtype' // WITHVIEWTYPE
        //
    ],
    keywords_dlr: [
        '$delay',
        '$ldelay',
        //
        '$arrpsz',
        '$arrptrsize',
        //
        '$d2ctype',
        //
        '$effmask',
        '$effmask_ntm',
        '$effmask_exn',
        '$effmask_ref',
        '$effmask_wrt',
        '$effmask_all',
        //
        '$extern',
        '$extkind',
        '$extype',
        '$extype_struct',
        //
        '$extval',
        '$extfcall',
        '$extmcall',
        //
        '$literal',
        //
        '$myfilename',
        '$mylocation',
        '$myfunction',
        //
        '$lst',
        '$lst_t',
        '$lst_vt',
        '$list',
        '$list_t',
        '$list_vt',
        //
        '$rec',
        '$rec_t',
        '$rec_vt',
        '$record',
        '$record_t',
        '$record_vt',
        //
        '$tup',
        '$tup_t',
        '$tup_vt',
        '$tuple',
        '$tuple_t',
        '$tuple_vt',
        //
        '$break',
        '$continue',
        //
        '$raise',
        //
        '$showtype',
        //
        '$vcopyenv_v',
        '$vcopyenv_vt',
        //
        '$tempenver',
        //
        '$solver_assert',
        '$solver_verify' // T_DLRSOLVERIFY
    ],
    keywords_srp: [
        //
        '#if',
        '#ifdef',
        '#ifndef',
        //
        '#then',
        //
        '#elif',
        '#elifdef',
        '#elifndef',
        //
        '#else',
        '#endif',
        //
        '#error',
        //
        '#prerr',
        '#print',
        //
        '#assert',
        //
        '#undef',
        '#define',
        //
        '#include',
        '#require',
        //
        '#pragma',
        '#codegen2',
        '#codegen3' // T_SRPCODEGEN3 // for level-3 codegen
        //
        // HX: end of special tokens
        //
    ],
    irregular_keyword_list: [
        'val+',
        'val-',
        'val',
        'case+',
        'case-',
        'case',
        'addr@',
        'addr',
        'fold@',
        'free@',
        'fix@',
        'fix',
        'lam@',
        'lam',
        'llam@',
        'llam',
        'viewt@ype+',
        'viewt@ype-',
        'viewt@ype',
        'viewtype+',
        'viewtype-',
        'viewtype',
        'view+',
        'view-',
        'view@',
        'view',
        'type+',
        'type-',
        'type',
        'vtype+',
        'vtype-',
        'vtype',
        'vt@ype+',
        'vt@ype-',
        'vt@ype',
        'viewt@ype+',
        'viewt@ype-',
        'viewt@ype',
        'viewtype+',
        'viewtype-',
        'viewtype',
        'prop+',
        'prop-',
        'prop',
        'type+',
        'type-',
        'type',
        't@ype',
        't@ype+',
        't@ype-',
        'abst@ype',
        'abstype',
        'absviewt@ype',
        'absvt@ype',
        'for*',
        'for',
        'while*',
        'while'
    ],
    keywords_types: [
        'bool',
        'double',
        'byte',
        'int',
        'short',
        'char',
        'void',
        'unit',
        'long',
        'float',
        'string',
        'strptr'
    ],
    // TODO: reference for this?
    keywords_effects: [
        '0',
        'fun',
        'clo',
        'prf',
        'funclo',
        'cloptr',
        'cloref',
        'ref',
        'ntm',
        '1' // all effects
    ],
    operators: [
        '@',
        '!',
        '|',
        '`',
        ':',
        '$',
        '.',
        '=',
        '#',
        '~',
        //
        '..',
        '...',
        //
        '=>',
        // "=<", // T_EQLT
        '=<>',
        '=/=>',
        '=>>',
        '=/=>>',
        //
        '<',
        '>',
        //
        '><',
        //
        '.<',
        '>.',
        //
        '.<>.',
        //
        '->',
        //"-<", // T_MINUSLT
        '-<>' // T_MINUSLTGT
        //
        /*
                ":<", // T_COLONLT
        */
    ],
    brackets: [
        { open: ',(', close: ')', token: 'delimiter.parenthesis' },
        { open: '`(', close: ')', token: 'delimiter.parenthesis' },
        { open: '%(', close: ')', token: 'delimiter.parenthesis' },
        { open: "'(", close: ')', token: 'delimiter.parenthesis' },
        { open: "'{", close: '}', token: 'delimiter.parenthesis' },
        { open: '@(', close: ')', token: 'delimiter.parenthesis' },
        { open: '@{', close: '}', token: 'delimiter.brace' },
        { open: '@[', close: ']', token: 'delimiter.square' },
        { open: '#[', close: ']', token: 'delimiter.square' },
        { open: '{', close: '}', token: 'delimiter.curly' },
        { open: '[', close: ']', token: 'delimiter.square' },
        { open: '(', close: ')', token: 'delimiter.parenthesis' },
        { open: '<', close: '>', token: 'delimiter.angle' }
    ],
    // we include these common regular expressions
    symbols: /[=><!~?:&|+\-*\/\^%]+/,
    IDENTFST: /[a-zA-Z_]/,
    IDENTRST: /[a-zA-Z0-9_'$]/,
    symbolic: /[%&+-./:=@~`^|*!$#?<>]/,
    digit: /[0-9]/,
    digitseq0: /@digit*/,
    xdigit: /[0-9A-Za-z]/,
    xdigitseq0: /@xdigit*/,
    INTSP: /[lLuU]/,
    FLOATSP: /[fFlL]/,
    fexponent: /[eE][+-]?[0-9]+/,
    fexponent_bin: /[pP][+-]?[0-9]+/,
    deciexp: /\.[0-9]*@fexponent?/,
    hexiexp: /\.[0-9a-zA-Z]*@fexponent_bin?/,
    irregular_keywords: /val[+-]?|case[+-]?|addr\@?|fold\@|free\@|fix\@?|lam\@?|llam\@?|prop[+-]?|type[+-]?|view[+-@]?|viewt@?ype[+-]?|t@?ype[+-]?|v(iew)?t@?ype[+-]?|abst@?ype|absv(iew)?t@?ype|for\*?|while\*?/,
    ESCHAR: /[ntvbrfa\\\?'"\(\[\{]/,
    start: 'root',
    // The main tokenizer for ATS/Postiats
    // reference: https://github.com/githwxi/ATS-Postiats/blob/master/src/pats_lexing.dats
    tokenizer: {
        root: [
            // lexing_blankseq0
            { regex: /[ \t\r\n]+/, action: { token: '' } },
            // NOTE: (*) is an invalid ML-like comment!
            { regex: /\(\*\)/, action: { token: 'invalid' } },
            {
                regex: /\(\*/,
                action: { token: 'comment', next: 'lexing_COMMENT_block_ml' }
            },
            {
                regex: /\(/,
                action: '@brackets' /*{ token: 'delimiter.parenthesis' }*/
            },
            {
                regex: /\)/,
                action: '@brackets' /*{ token: 'delimiter.parenthesis' }*/
            },
            {
                regex: /\[/,
                action: '@brackets' /*{ token: 'delimiter.bracket' }*/
            },
            {
                regex: /\]/,
                action: '@brackets' /*{ token: 'delimiter.bracket' }*/
            },
            {
                regex: /\{/,
                action: '@brackets' /*{ token: 'delimiter.brace' }*/
            },
            {
                regex: /\}/,
                action: '@brackets' /*{ token: 'delimiter.brace' }*/
            },
            // lexing_COMMA
            {
                regex: /,\(/,
                action: '@brackets' /*{ token: 'delimiter.parenthesis' }*/
            },
            { regex: /,/, action: { token: 'delimiter.comma' } },
            { regex: /;/, action: { token: 'delimiter.semicolon' } },
            // lexing_AT
            {
                regex: /@\(/,
                action: '@brackets' /* { token: 'delimiter.parenthesis' }*/
            },
            {
                regex: /@\[/,
                action: '@brackets' /* { token: 'delimiter.bracket' }*/
            },
            {
                regex: /@\{/,
                action: '@brackets' /*{ token: 'delimiter.brace' }*/
            },
            // lexing_COLON
            {
                regex: /:</,
                action: { token: 'keyword', next: '@lexing_EFFECT_commaseq0' }
            },
            /*
            lexing_DOT:

            . // SYMBOLIC => lexing_IDENT_sym
            . FLOATDOT => lexing_FLOAT_deciexp
            . DIGIT => T_DOTINT
            */
            { regex: /\.@symbolic+/, action: { token: 'identifier.sym' } },
            // FLOATDOT case
            {
                regex: /\.@digit*@fexponent@FLOATSP*/,
                action: { token: 'number.float' }
            },
            { regex: /\.@digit+/, action: { token: 'number.float' } },
            // lexing_DOLLAR:
            // '$' IDENTFST IDENTRST* => lexing_IDENT_dlr, _ => lexing_IDENT_sym
            {
                regex: /\$@IDENTFST@IDENTRST*/,
                action: {
                    cases: {
                        '@keywords_dlr': { token: 'keyword.dlr' },
                        '@default': { token: 'namespace' } // most likely a module qualifier
                    }
                }
            },
            // lexing_SHARP:
            // '#' IDENTFST IDENTRST* => lexing_ident_srp, _ => lexing_IDENT_sym
            {
                regex: /\#@IDENTFST@IDENTRST*/,
                action: {
                    cases: {
                        '@keywords_srp': { token: 'keyword.srp' },
                        '@default': { token: 'identifier' }
                    }
                }
            },
            // lexing_PERCENT:
            { regex: /%\(/, action: { token: 'delimiter.parenthesis' } },
            {
                regex: /^%{(#|\^|\$)?/,
                action: {
                    token: 'keyword',
                    next: '@lexing_EXTCODE',
                    nextEmbedded: 'text/javascript'
                }
            },
            { regex: /^%}/, action: { token: 'keyword' } },
            // lexing_QUOTE
            { regex: /'\(/, action: { token: 'delimiter.parenthesis' } },
            { regex: /'\[/, action: { token: 'delimiter.bracket' } },
            { regex: /'\{/, action: { token: 'delimiter.brace' } },
            [/(')(\\@ESCHAR|\\[xX]@xdigit+|\\@digit+)(')/, ['string', 'string.escape', 'string']],
            [/'[^\\']'/, 'string'],
            // lexing_DQUOTE
            [/"/, 'string.quote', '@lexing_DQUOTE'],
            // lexing_BQUOTE
            {
                regex: /`\(/,
                action: '@brackets' /* { token: 'delimiter.parenthesis' }*/
            },
            // TODO: otherwise, try lexing_IDENT_sym
            { regex: /\\/, action: { token: 'punctuation' } },
            // lexing_IDENT_alp:
            // NOTE: (?!regex) is syntax for "not-followed-by" regex
            // to resolve ambiguity such as foreach$fwork being incorrectly lexed as [for] [each$fwork]!
            {
                regex: /@irregular_keywords(?!@IDENTRST)/,
                action: { token: 'keyword' }
            },
            {
                regex: /@IDENTFST@IDENTRST*[<!\[]?/,
                action: {
                    cases: {
                        // TODO: dynload and staload should be specially parsed
                        // dynload whitespace+ "special_string"
                        // this special string is really:
                        //  '/' '\\' '.' => punctuation
                        // ({\$)([a-zA-Z_][a-zA-Z_0-9]*)(}) => punctuation,keyword,punctuation
                        // [^"] => identifier/literal
                        '@keywords': { token: 'keyword' },
                        '@keywords_types': { token: 'type' },
                        '@default': { token: 'identifier' }
                    }
                }
            },
            // lexing_IDENT_sym:
            {
                regex: /\/\/\/\//,
                action: { token: 'comment', next: '@lexing_COMMENT_rest' }
            },
            { regex: /\/\/.*$/, action: { token: 'comment' } },
            {
                regex: /\/\*/,
                action: { token: 'comment', next: '@lexing_COMMENT_block_c' }
            },
            // AS-20160627: specifically for effect annotations
            {
                regex: /-<|=</,
                action: { token: 'keyword', next: '@lexing_EFFECT_commaseq0' }
            },
            {
                regex: /@symbolic+/,
                action: {
                    cases: {
                        '@operators': 'keyword',
                        '@default': 'operator'
                    }
                }
            },
            // lexing_ZERO:
            // FIXME: this one is quite messy/unfinished yet
            // TODO: lexing_INT_hex
            // - testing_hexiexp => lexing_FLOAT_hexiexp
            // - testing_fexponent_bin => lexing_FLOAT_hexiexp
            // - testing_intspseq0 => T_INT_hex
            // lexing_INT_hex:
            {
                regex: /0[xX]@xdigit+(@hexiexp|@fexponent_bin)@FLOATSP*/,
                action: { token: 'number.float' }
            },
            { regex: /0[xX]@xdigit+@INTSP*/, action: { token: 'number.hex' } },
            {
                regex: /0[0-7]+(?![0-9])@INTSP*/,
                action: { token: 'number.octal' }
            },
            //{regex: /0/, action: { token: 'number' } }, // INTZERO
            // lexing_INT_dec:
            // - testing_deciexp => lexing_FLOAT_deciexp
            // - testing_fexponent => lexing_FLOAT_deciexp
            // - otherwise => intspseq0 ([0-9]*[lLuU]?)
            {
                regex: /@digit+(@fexponent|@deciexp)@FLOATSP*/,
                action: { token: 'number.float' }
            },
            {
                regex: /@digit@digitseq0@INTSP*/,
                action: { token: 'number.decimal' }
            },
            // DIGIT, if followed by digitseq0, is lexing_INT_dec
            { regex: /@digit+@INTSP*/, action: { token: 'number' } }
        ],
        lexing_COMMENT_block_ml: [
            [/[^\(\*]+/, 'comment'],
            [/\(\*/, 'comment', '@push'],
            [/\(\*/, 'comment.invalid'],
            [/\*\)/, 'comment', '@pop'],
            [/\*/, 'comment']
        ],
        lexing_COMMENT_block_c: [
            [/[^\/*]+/, 'comment'],
            // [/\/\*/, 'comment', '@push' ],    // nested C-style block comments not allowed
            // [/\/\*/,    'comment.invalid' ],	// NOTE: this breaks block comments in the shape of /* //*/
            [/\*\//, 'comment', '@pop'],
            [/[\/*]/, 'comment']
        ],
        lexing_COMMENT_rest: [
            [/$/, 'comment', '@pop'],
            [/.*/, 'comment']
        ],
        // NOTE: added by AS, specifically for highlighting
        lexing_EFFECT_commaseq0: [
            {
                regex: /@IDENTFST@IDENTRST+|@digit+/,
                action: {
                    cases: {
                        '@keywords_effects': { token: 'type.effect' },
                        '@default': { token: 'identifier' }
                    }
                }
            },
            { regex: /,/, action: { token: 'punctuation' } },
            { regex: />/, action: { token: '@rematch', next: '@pop' } }
        ],
        lexing_EXTCODE: [
            {
                regex: /^%}/,
                action: {
                    token: '@rematch',
                    next: '@pop',
                    nextEmbedded: '@pop'
                }
            },
            { regex: /[^%]+/, action: '' }
        ],
        lexing_DQUOTE: [
            { regex: /"/, action: { token: 'string.quote', next: '@pop' } },
            // AS-20160628: additional hi-lighting for variables in staload/dynload strings
            {
                regex: /(\{\$)(@IDENTFST@IDENTRST*)(\})/,
                action: [
                    { token: 'string.escape' },
                    { token: 'identifier' },
                    { token: 'string.escape' }
                ]
            },
            { regex: /\\$/, action: { token: 'string.escape' } },
            {
                regex: /\\(@ESCHAR|[xX]@xdigit+|@digit+)/,
                action: { token: 'string.escape' }
            },
            { regex: /[^\\"]+/, action: { token: 'string' } }
        ]
    }
};
