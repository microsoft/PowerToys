"""Python Character Mapping Codec cp720 generated on Windows:
Vista 6.0.6002 SP2 Multiprocessor Free with the command:
  python Tools/unicode/genwincodec.py 720
"""#"


import codecs

### Codec APIs

class Codec(codecs.Codec):

    def encode(self,input,errors='strict'):
        return codecs.charmap_encode(input,errors,encoding_table)

    def decode(self,input,errors='strict'):
        return codecs.charmap_decode(input,errors,decoding_table)

class IncrementalEncoder(codecs.IncrementalEncoder):
    def encode(self, input, final=False):
        return codecs.charmap_encode(input,self.errors,encoding_table)[0]

class IncrementalDecoder(codecs.IncrementalDecoder):
    def decode(self, input, final=False):
        return codecs.charmap_decode(input,self.errors,decoding_table)[0]

class StreamWriter(Codec,codecs.StreamWriter):
    pass

class StreamReader(Codec,codecs.StreamReader):
    pass

### encodings module API

def getregentry():
    return codecs.CodecInfo(
        name='cp720',
        encode=Codec().encode,
        decode=Codec().decode,
        incrementalencoder=IncrementalEncoder,
        incrementaldecoder=IncrementalDecoder,
        streamreader=StreamReader,
        streamwriter=StreamWriter,
    )


### Decoding Table

decoding_table = (
    u'\x00'     #  0x00 -> CONTROL CHARACTER
    u'\x01'     #  0x01 -> CONTROL CHARACTER
    u'\x02'     #  0x02 -> CONTROL CHARACTER
    u'\x03'     #  0x03 -> CONTROL CHARACTER
    u'\x04'     #  0x04 -> CONTROL CHARACTER
    u'\x05'     #  0x05 -> CONTROL CHARACTER
    u'\x06'     #  0x06 -> CONTROL CHARACTER
    u'\x07'     #  0x07 -> CONTROL CHARACTER
    u'\x08'     #  0x08 -> CONTROL CHARACTER
    u'\t'       #  0x09 -> CONTROL CHARACTER
    u'\n'       #  0x0A -> CONTROL CHARACTER
    u'\x0b'     #  0x0B -> CONTROL CHARACTER
    u'\x0c'     #  0x0C -> CONTROL CHARACTER
    u'\r'       #  0x0D -> CONTROL CHARACTER
    u'\x0e'     #  0x0E -> CONTROL CHARACTER
    u'\x0f'     #  0x0F -> CONTROL CHARACTER
    u'\x10'     #  0x10 -> CONTROL CHARACTER
    u'\x11'     #  0x11 -> CONTROL CHARACTER
    u'\x12'     #  0x12 -> CONTROL CHARACTER
    u'\x13'     #  0x13 -> CONTROL CHARACTER
    u'\x14'     #  0x14 -> CONTROL CHARACTER
    u'\x15'     #  0x15 -> CONTROL CHARACTER
    u'\x16'     #  0x16 -> CONTROL CHARACTER
    u'\x17'     #  0x17 -> CONTROL CHARACTER
    u'\x18'     #  0x18 -> CONTROL CHARACTER
    u'\x19'     #  0x19 -> CONTROL CHARACTER
    u'\x1a'     #  0x1A -> CONTROL CHARACTER
    u'\x1b'     #  0x1B -> CONTROL CHARACTER
    u'\x1c'     #  0x1C -> CONTROL CHARACTER
    u'\x1d'     #  0x1D -> CONTROL CHARACTER
    u'\x1e'     #  0x1E -> CONTROL CHARACTER
    u'\x1f'     #  0x1F -> CONTROL CHARACTER
    u' '        #  0x20 -> SPACE
    u'!'        #  0x21 -> EXCLAMATION MARK
    u'"'        #  0x22 -> QUOTATION MARK
    u'#'        #  0x23 -> NUMBER SIGN
    u'$'        #  0x24 -> DOLLAR SIGN
    u'%'        #  0x25 -> PERCENT SIGN
    u'&'        #  0x26 -> AMPERSAND
    u"'"        #  0x27 -> APOSTROPHE
    u'('        #  0x28 -> LEFT PARENTHESIS
    u')'        #  0x29 -> RIGHT PARENTHESIS
    u'*'        #  0x2A -> ASTERISK
    u'+'        #  0x2B -> PLUS SIGN
    u','        #  0x2C -> COMMA
    u'-'        #  0x2D -> HYPHEN-MINUS
    u'.'        #  0x2E -> FULL STOP
    u'/'        #  0x2F -> SOLIDUS
    u'0'        #  0x30 -> DIGIT ZERO
    u'1'        #  0x31 -> DIGIT ONE
    u'2'        #  0x32 -> DIGIT TWO
    u'3'        #  0x33 -> DIGIT THREE
    u'4'        #  0x34 -> DIGIT FOUR
    u'5'        #  0x35 -> DIGIT FIVE
    u'6'        #  0x36 -> DIGIT SIX
    u'7'        #  0x37 -> DIGIT SEVEN
    u'8'        #  0x38 -> DIGIT EIGHT
    u'9'        #  0x39 -> DIGIT NINE
    u':'        #  0x3A -> COLON
    u';'        #  0x3B -> SEMICOLON
    u'<'        #  0x3C -> LESS-THAN SIGN
    u'='        #  0x3D -> EQUALS SIGN
    u'>'        #  0x3E -> GREATER-THAN SIGN
    u'?'        #  0x3F -> QUESTION MARK
    u'@'        #  0x40 -> COMMERCIAL AT
    u'A'        #  0x41 -> LATIN CAPITAL LETTER A
    u'B'        #  0x42 -> LATIN CAPITAL LETTER B
    u'C'        #  0x43 -> LATIN CAPITAL LETTER C
    u'D'        #  0x44 -> LATIN CAPITAL LETTER D
    u'E'        #  0x45 -> LATIN CAPITAL LETTER E
    u'F'        #  0x46 -> LATIN CAPITAL LETTER F
    u'G'        #  0x47 -> LATIN CAPITAL LETTER G
    u'H'        #  0x48 -> LATIN CAPITAL LETTER H
    u'I'        #  0x49 -> LATIN CAPITAL LETTER I
    u'J'        #  0x4A -> LATIN CAPITAL LETTER J
    u'K'        #  0x4B -> LATIN CAPITAL LETTER K
    u'L'        #  0x4C -> LATIN CAPITAL LETTER L
    u'M'        #  0x4D -> LATIN CAPITAL LETTER M
    u'N'        #  0x4E -> LATIN CAPITAL LETTER N
    u'O'        #  0x4F -> LATIN CAPITAL LETTER O
    u'P'        #  0x50 -> LATIN CAPITAL LETTER P
    u'Q'        #  0x51 -> LATIN CAPITAL LETTER Q
    u'R'        #  0x52 -> LATIN CAPITAL LETTER R
    u'S'        #  0x53 -> LATIN CAPITAL LETTER S
    u'T'        #  0x54 -> LATIN CAPITAL LETTER T
    u'U'        #  0x55 -> LATIN CAPITAL LETTER U
    u'V'        #  0x56 -> LATIN CAPITAL LETTER V
    u'W'        #  0x57 -> LATIN CAPITAL LETTER W
    u'X'        #  0x58 -> LATIN CAPITAL LETTER X
    u'Y'        #  0x59 -> LATIN CAPITAL LETTER Y
    u'Z'        #  0x5A -> LATIN CAPITAL LETTER Z
    u'['        #  0x5B -> LEFT SQUARE BRACKET
    u'\\'       #  0x5C -> REVERSE SOLIDUS
    u']'        #  0x5D -> RIGHT SQUARE BRACKET
    u'^'        #  0x5E -> CIRCUMFLEX ACCENT
    u'_'        #  0x5F -> LOW LINE
    u'`'        #  0x60 -> GRAVE ACCENT
    u'a'        #  0x61 -> LATIN SMALL LETTER A
    u'b'        #  0x62 -> LATIN SMALL LETTER B
    u'c'        #  0x63 -> LATIN SMALL LETTER C
    u'd'        #  0x64 -> LATIN SMALL LETTER D
    u'e'        #  0x65 -> LATIN SMALL LETTER E
    u'f'        #  0x66 -> LATIN SMALL LETTER F
    u'g'        #  0x67 -> LATIN SMALL LETTER G
    u'h'        #  0x68 -> LATIN SMALL LETTER H
    u'i'        #  0x69 -> LATIN SMALL LETTER I
    u'j'        #  0x6A -> LATIN SMALL LETTER J
    u'k'        #  0x6B -> LATIN SMALL LETTER K
    u'l'        #  0x6C -> LATIN SMALL LETTER L
    u'm'        #  0x6D -> LATIN SMALL LETTER M
    u'n'        #  0x6E -> LATIN SMALL LETTER N
    u'o'        #  0x6F -> LATIN SMALL LETTER O
    u'p'        #  0x70 -> LATIN SMALL LETTER P
    u'q'        #  0x71 -> LATIN SMALL LETTER Q
    u'r'        #  0x72 -> LATIN SMALL LETTER R
    u's'        #  0x73 -> LATIN SMALL LETTER S
    u't'        #  0x74 -> LATIN SMALL LETTER T
    u'u'        #  0x75 -> LATIN SMALL LETTER U
    u'v'        #  0x76 -> LATIN SMALL LETTER V
    u'w'        #  0x77 -> LATIN SMALL LETTER W
    u'x'        #  0x78 -> LATIN SMALL LETTER X
    u'y'        #  0x79 -> LATIN SMALL LETTER Y
    u'z'        #  0x7A -> LATIN SMALL LETTER Z
    u'{'        #  0x7B -> LEFT CURLY BRACKET
    u'|'        #  0x7C -> VERTICAL LINE
    u'}'        #  0x7D -> RIGHT CURLY BRACKET
    u'~'        #  0x7E -> TILDE
    u'\x7f'     #  0x7F -> CONTROL CHARACTER
    u'\x80'
    u'\x81'
    u'\xe9'     #  0x82 -> LATIN SMALL LETTER E WITH ACUTE
    u'\xe2'     #  0x83 -> LATIN SMALL LETTER A WITH CIRCUMFLEX
    u'\x84'
    u'\xe0'     #  0x85 -> LATIN SMALL LETTER A WITH GRAVE
    u'\x86'
    u'\xe7'     #  0x87 -> LATIN SMALL LETTER C WITH CEDILLA
    u'\xea'     #  0x88 -> LATIN SMALL LETTER E WITH CIRCUMFLEX
    u'\xeb'     #  0x89 -> LATIN SMALL LETTER E WITH DIAERESIS
    u'\xe8'     #  0x8A -> LATIN SMALL LETTER E WITH GRAVE
    u'\xef'     #  0x8B -> LATIN SMALL LETTER I WITH DIAERESIS
    u'\xee'     #  0x8C -> LATIN SMALL LETTER I WITH CIRCUMFLEX
    u'\x8d'
    u'\x8e'
    u'\x8f'
    u'\x90'
    u'\u0651'   #  0x91 -> ARABIC SHADDA
    u'\u0652'   #  0x92 -> ARABIC SUKUN
    u'\xf4'     #  0x93 -> LATIN SMALL LETTER O WITH CIRCUMFLEX
    u'\xa4'     #  0x94 -> CURRENCY SIGN
    u'\u0640'   #  0x95 -> ARABIC TATWEEL
    u'\xfb'     #  0x96 -> LATIN SMALL LETTER U WITH CIRCUMFLEX
    u'\xf9'     #  0x97 -> LATIN SMALL LETTER U WITH GRAVE
    u'\u0621'   #  0x98 -> ARABIC LETTER HAMZA
    u'\u0622'   #  0x99 -> ARABIC LETTER ALEF WITH MADDA ABOVE
    u'\u0623'   #  0x9A -> ARABIC LETTER ALEF WITH HAMZA ABOVE
    u'\u0624'   #  0x9B -> ARABIC LETTER WAW WITH HAMZA ABOVE
    u'\xa3'     #  0x9C -> POUND SIGN
    u'\u0625'   #  0x9D -> ARABIC LETTER ALEF WITH HAMZA BELOW
    u'\u0626'   #  0x9E -> ARABIC LETTER YEH WITH HAMZA ABOVE
    u'\u0627'   #  0x9F -> ARABIC LETTER ALEF
    u'\u0628'   #  0xA0 -> ARABIC LETTER BEH
    u'\u0629'   #  0xA1 -> ARABIC LETTER TEH MARBUTA
    u'\u062a'   #  0xA2 -> ARABIC LETTER TEH
    u'\u062b'   #  0xA3 -> ARABIC LETTER THEH
    u'\u062c'   #  0xA4 -> ARABIC LETTER JEEM
    u'\u062d'   #  0xA5 -> ARABIC LETTER HAH
    u'\u062e'   #  0xA6 -> ARABIC LETTER KHAH
    u'\u062f'   #  0xA7 -> ARABIC LETTER DAL
    u'\u0630'   #  0xA8 -> ARABIC LETTER THAL
    u'\u0631'   #  0xA9 -> ARABIC LETTER REH
    u'\u0632'   #  0xAA -> ARABIC LETTER ZAIN
    u'\u0633'   #  0xAB -> ARABIC LETTER SEEN
    u'\u0634'   #  0xAC -> ARABIC LETTER SHEEN
    u'\u0635'   #  0xAD -> ARABIC LETTER SAD
    u'\xab'     #  0xAE -> LEFT-POINTING DOUBLE ANGLE QUOTATION MARK
    u'\xbb'     #  0xAF -> RIGHT-POINTING DOUBLE ANGLE QUOTATION MARK
    u'\u2591'   #  0xB0 -> LIGHT SHADE
    u'\u2592'   #  0xB1 -> MEDIUM SHADE
    u'\u2593'   #  0xB2 -> DARK SHADE
    u'\u2502'   #  0xB3 -> BOX DRAWINGS LIGHT VERTICAL
    u'\u2524'   #  0xB4 -> BOX DRAWINGS LIGHT VERTICAL AND LEFT
    u'\u2561'   #  0xB5 -> BOX DRAWINGS VERTICAL SINGLE AND LEFT DOUBLE
    u'\u2562'   #  0xB6 -> BOX DRAWINGS VERTICAL DOUBLE AND LEFT SINGLE
    u'\u2556'   #  0xB7 -> BOX DRAWINGS DOWN DOUBLE AND LEFT SINGLE
    u'\u2555'   #  0xB8 -> BOX DRAWINGS DOWN SINGLE AND LEFT DOUBLE
    u'\u2563'   #  0xB9 -> BOX DRAWINGS DOUBLE VERTICAL AND LEFT
    u'\u2551'   #  0xBA -> BOX DRAWINGS DOUBLE VERTICAL
    u'\u2557'   #  0xBB -> BOX DRAWINGS DOUBLE DOWN AND LEFT
    u'\u255d'   #  0xBC -> BOX DRAWINGS DOUBLE UP AND LEFT
    u'\u255c'   #  0xBD -> BOX DRAWINGS UP DOUBLE AND LEFT SINGLE
    u'\u255b'   #  0xBE -> BOX DRAWINGS UP SINGLE AND LEFT DOUBLE
    u'\u2510'   #  0xBF -> BOX DRAWINGS LIGHT DOWN AND LEFT
    u'\u2514'   #  0xC0 -> BOX DRAWINGS LIGHT UP AND RIGHT
    u'\u2534'   #  0xC1 -> BOX DRAWINGS LIGHT UP AND HORIZONTAL
    u'\u252c'   #  0xC2 -> BOX DRAWINGS LIGHT DOWN AND HORIZONTAL
    u'\u251c'   #  0xC3 -> BOX DRAWINGS LIGHT VERTICAL AND RIGHT
    u'\u2500'   #  0xC4 -> BOX DRAWINGS LIGHT HORIZONTAL
    u'\u253c'   #  0xC5 -> BOX DRAWINGS LIGHT VERTICAL AND HORIZONTAL
    u'\u255e'   #  0xC6 -> BOX DRAWINGS VERTICAL SINGLE AND RIGHT DOUBLE
    u'\u255f'   #  0xC7 -> BOX DRAWINGS VERTICAL DOUBLE AND RIGHT SINGLE
    u'\u255a'   #  0xC8 -> BOX DRAWINGS DOUBLE UP AND RIGHT
    u'\u2554'   #  0xC9 -> BOX DRAWINGS DOUBLE DOWN AND RIGHT
    u'\u2569'   #  0xCA -> BOX DRAWINGS DOUBLE UP AND HORIZONTAL
    u'\u2566'   #  0xCB -> BOX DRAWINGS DOUBLE DOWN AND HORIZONTAL
    u'\u2560'   #  0xCC -> BOX DRAWINGS DOUBLE VERTICAL AND RIGHT
    u'\u2550'   #  0xCD -> BOX DRAWINGS DOUBLE HORIZONTAL
    u'\u256c'   #  0xCE -> BOX DRAWINGS DOUBLE VERTICAL AND HORIZONTAL
    u'\u2567'   #  0xCF -> BOX DRAWINGS UP SINGLE AND HORIZONTAL DOUBLE
    u'\u2568'   #  0xD0 -> BOX DRAWINGS UP DOUBLE AND HORIZONTAL SINGLE
    u'\u2564'   #  0xD1 -> BOX DRAWINGS DOWN SINGLE AND HORIZONTAL DOUBLE
    u'\u2565'   #  0xD2 -> BOX DRAWINGS DOWN DOUBLE AND HORIZONTAL SINGLE
    u'\u2559'   #  0xD3 -> BOX DRAWINGS UP DOUBLE AND RIGHT SINGLE
    u'\u2558'   #  0xD4 -> BOX DRAWINGS UP SINGLE AND RIGHT DOUBLE
    u'\u2552'   #  0xD5 -> BOX DRAWINGS DOWN SINGLE AND RIGHT DOUBLE
    u'\u2553'   #  0xD6 -> BOX DRAWINGS DOWN DOUBLE AND RIGHT SINGLE
    u'\u256b'   #  0xD7 -> BOX DRAWINGS VERTICAL DOUBLE AND HORIZONTAL SINGLE
    u'\u256a'   #  0xD8 -> BOX DRAWINGS VERTICAL SINGLE AND HORIZONTAL DOUBLE
    u'\u2518'   #  0xD9 -> BOX DRAWINGS LIGHT UP AND LEFT
    u'\u250c'   #  0xDA -> BOX DRAWINGS LIGHT DOWN AND RIGHT
    u'\u2588'   #  0xDB -> FULL BLOCK
    u'\u2584'   #  0xDC -> LOWER HALF BLOCK
    u'\u258c'   #  0xDD -> LEFT HALF BLOCK
    u'\u2590'   #  0xDE -> RIGHT HALF BLOCK
    u'\u2580'   #  0xDF -> UPPER HALF BLOCK
    u'\u0636'   #  0xE0 -> ARABIC LETTER DAD
    u'\u0637'   #  0xE1 -> ARABIC LETTER TAH
    u'\u0638'   #  0xE2 -> ARABIC LETTER ZAH
    u'\u0639'   #  0xE3 -> ARABIC LETTER AIN
    u'\u063a'   #  0xE4 -> ARABIC LETTER GHAIN
    u'\u0641'   #  0xE5 -> ARABIC LETTER FEH
    u'\xb5'     #  0xE6 -> MICRO SIGN
    u'\u0642'   #  0xE7 -> ARABIC LETTER QAF
    u'\u0643'   #  0xE8 -> ARABIC LETTER KAF
    u'\u0644'   #  0xE9 -> ARABIC LETTER LAM
    u'\u0645'   #  0xEA -> ARABIC LETTER MEEM
    u'\u0646'   #  0xEB -> ARABIC LETTER NOON
    u'\u0647'   #  0xEC -> ARABIC LETTER HEH
    u'\u0648'   #  0xED -> ARABIC LETTER WAW
    u'\u0649'   #  0xEE -> ARABIC LETTER ALEF MAKSURA
    u'\u064a'   #  0xEF -> ARABIC LETTER YEH
    u'\u2261'   #  0xF0 -> IDENTICAL TO
    u'\u064b'   #  0xF1 -> ARABIC FATHATAN
    u'\u064c'   #  0xF2 -> ARABIC DAMMATAN
    u'\u064d'   #  0xF3 -> ARABIC KASRATAN
    u'\u064e'   #  0xF4 -> ARABIC FATHA
    u'\u064f'   #  0xF5 -> ARABIC DAMMA
    u'\u0650'   #  0xF6 -> ARABIC KASRA
    u'\u2248'   #  0xF7 -> ALMOST EQUAL TO
    u'\xb0'     #  0xF8 -> DEGREE SIGN
    u'\u2219'   #  0xF9 -> BULLET OPERATOR
    u'\xb7'     #  0xFA -> MIDDLE DOT
    u'\u221a'   #  0xFB -> SQUARE ROOT
    u'\u207f'   #  0xFC -> SUPERSCRIPT LATIN SMALL LETTER N
    u'\xb2'     #  0xFD -> SUPERSCRIPT TWO
    u'\u25a0'   #  0xFE -> BLACK SQUARE
    u'\xa0'     #  0xFF -> NO-BREAK SPACE
)

### Encoding table
encoding_table=codecs.charmap_build(decoding_table)
