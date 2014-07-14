# -*- coding: utf-8 -*-
"""Beautiful Soup bonus library: Unicode, Dammit

This library converts a bytestream to Unicode through any means
necessary. It is heavily based on code from Mark Pilgrim's Universal
Feed Parser. It works best on XML and XML, but it does not rewrite the
XML or HTML to reflect a new encoding; that's the tree builder's job.
"""

import codecs
from htmlentitydefs import codepoint2name
import re
import logging
import string

# Import a library to autodetect character encodings.
chardet_type = None
try:
    # First try the fast C implementation.
    #  PyPI package: cchardet
    import cchardet
    def chardet_dammit(s):
        return cchardet.detect(s)['encoding']
except ImportError:
    try:
        # Fall back to the pure Python implementation
        #  Debian package: python-chardet
        #  PyPI package: chardet
        import chardet
        def chardet_dammit(s):
            return chardet.detect(s)['encoding']
        #import chardet.constants
        #chardet.constants._debug = 1
    except ImportError:
        # No chardet available.
        def chardet_dammit(s):
            return None

# Available from http://cjkpython.i18n.org/.
try:
    import iconv_codec
except ImportError:
    pass

xml_encoding_re = re.compile(
    '^<\?.*encoding=[\'"](.*?)[\'"].*\?>'.encode(), re.I)
html_meta_re = re.compile(
    '<\s*meta[^>]+charset\s*=\s*["\']?([^>]*?)[ /;\'">]'.encode(), re.I)

class EntitySubstitution(object):

    """Substitute XML or HTML entities for the corresponding characters."""

    def _populate_class_variables():
        lookup = {}
        reverse_lookup = {}
        characters_for_re = []
        for codepoint, name in list(codepoint2name.items()):
            character = unichr(codepoint)
            if codepoint != 34:
                # There's no point in turning the quotation mark into
                # &quot;, unless it happens within an attribute value, which
                # is handled elsewhere.
                characters_for_re.append(character)
                lookup[character] = name
            # But we do want to turn &quot; into the quotation mark.
            reverse_lookup[name] = character
        re_definition = "[%s]" % "".join(characters_for_re)
        return lookup, reverse_lookup, re.compile(re_definition)
    (CHARACTER_TO_HTML_ENTITY, HTML_ENTITY_TO_CHARACTER,
     CHARACTER_TO_HTML_ENTITY_RE) = _populate_class_variables()

    CHARACTER_TO_XML_ENTITY = {
        "'": "apos",
        '"': "quot",
        "&": "amp",
        "<": "lt",
        ">": "gt",
        }

    BARE_AMPERSAND_OR_BRACKET = re.compile("([<>]|"
                                           "&(?!#\d+;|#x[0-9a-fA-F]+;|\w+;)"
                                           ")")

    AMPERSAND_OR_BRACKET = re.compile("([<>&])")

    @classmethod
    def _substitute_html_entity(cls, matchobj):
        entity = cls.CHARACTER_TO_HTML_ENTITY.get(matchobj.group(0))
        return "&%s;" % entity

    @classmethod
    def _substitute_xml_entity(cls, matchobj):
        """Used with a regular expression to substitute the
        appropriate XML entity for an XML special character."""
        entity = cls.CHARACTER_TO_XML_ENTITY[matchobj.group(0)]
        return "&%s;" % entity

    @classmethod
    def quoted_attribute_value(self, value):
        """Make a value into a quoted XML attribute, possibly escaping it.

         Most strings will be quoted using double quotes.

          Bob's Bar -> "Bob's Bar"

         If a string contains double quotes, it will be quoted using
         single quotes.

          Welcome to "my bar" -> 'Welcome to "my bar"'

         If a string contains both single and double quotes, the
         double quotes will be escaped, and the string will be quoted
         using double quotes.

          Welcome to "Bob's Bar" -> "Welcome to &quot;Bob's bar&quot;
        """
        quote_with = '"'
        if '"' in value:
            if "'" in value:
                # The string contains both single and double
                # quotes.  Turn the double quotes into
                # entities. We quote the double quotes rather than
                # the single quotes because the entity name is
                # "&quot;" whether this is HTML or XML.  If we
                # quoted the single quotes, we'd have to decide
                # between &apos; and &squot;.
                replace_with = "&quot;"
                value = value.replace('"', replace_with)
            else:
                # There are double quotes but no single quotes.
                # We can use single quotes to quote the attribute.
                quote_with = "'"
        return quote_with + value + quote_with

    @classmethod
    def substitute_xml(cls, value, make_quoted_attribute=False):
        """Substitute XML entities for special XML characters.

        :param value: A string to be substituted. The less-than sign
          will become &lt;, the greater-than sign will become &gt;,
          and any ampersands will become &amp;. If you want ampersands
          that appear to be part of an entity definition to be left
          alone, use substitute_xml_containing_entities() instead.

        :param make_quoted_attribute: If True, then the string will be
         quoted, as befits an attribute value.
        """
        # Escape angle brackets and ampersands.
        value = cls.AMPERSAND_OR_BRACKET.sub(
            cls._substitute_xml_entity, value)

        if make_quoted_attribute:
            value = cls.quoted_attribute_value(value)
        return value

    @classmethod
    def substitute_xml_containing_entities(
        cls, value, make_quoted_attribute=False):
        """Substitute XML entities for special XML characters.

        :param value: A string to be substituted. The less-than sign will
          become &lt;, the greater-than sign will become &gt;, and any
          ampersands that are not part of an entity defition will
          become &amp;.

        :param make_quoted_attribute: If True, then the string will be
         quoted, as befits an attribute value.
        """
        # Escape angle brackets, and ampersands that aren't part of
        # entities.
        value = cls.BARE_AMPERSAND_OR_BRACKET.sub(
            cls._substitute_xml_entity, value)

        if make_quoted_attribute:
            value = cls.quoted_attribute_value(value)
        return value

    @classmethod
    def substitute_html(cls, s):
        """Replace certain Unicode characters with named HTML entities.

        This differs from data.encode(encoding, 'xmlcharrefreplace')
        in that the goal is to make the result more readable (to those
        with ASCII displays) rather than to recover from
        errors. There's absolutely nothing wrong with a UTF-8 string
        containg a LATIN SMALL LETTER E WITH ACUTE, but replacing that
        character with "&eacute;" will make it more readable to some
        people.
        """
        return cls.CHARACTER_TO_HTML_ENTITY_RE.sub(
            cls._substitute_html_entity, s)


class EncodingDetector:
    """Suggests a number of possible encodings for a bytestring.

    Order of precedence:

    1. Encodings you specifically tell EncodingDetector to try first
    (the override_encodings argument to the constructor).

    2. An encoding declared within the bytestring itself, either in an
    XML declaration (if the bytestring is to be interpreted as an XML
    document), or in a <meta> tag (if the bytestring is to be
    interpreted as an HTML document.)

    3. An encoding detected through textual analysis by chardet,
    cchardet, or a similar external library.

    4. UTF-8.

    5. Windows-1252.
    """
    def __init__(self, markup, override_encodings=None, is_html=False):
        self.override_encodings = override_encodings or []
        self.chardet_encoding = None
        self.is_html = is_html
        self.declared_encoding = None

        # First order of business: strip a byte-order mark.
        self.markup, self.sniffed_encoding = self.strip_byte_order_mark(markup)

    def _usable(self, encoding, tried):
        if encoding is not None:
            encoding = encoding.lower()
            if encoding not in tried:
                tried.add(encoding)
                return True
        return False

    @property
    def encodings(self):
        """Yield a number of encodings that might work for this markup."""
        tried = set()
        for e in self.override_encodings:
            if self._usable(e, tried):
                yield e

        # Did the document originally start with a byte-order mark
        # that indicated its encoding?
        if self._usable(self.sniffed_encoding, tried):
            yield self.sniffed_encoding

        # Look within the document for an XML or HTML encoding
        # declaration.
        if self.declared_encoding is None:
            self.declared_encoding = self.find_declared_encoding(
                self.markup, self.is_html)
        if self._usable(self.declared_encoding, tried):
            yield self.declared_encoding

        # Use third-party character set detection to guess at the
        # encoding.
        if self.chardet_encoding is None:
            self.chardet_encoding = chardet_dammit(self.markup)
        if self._usable(self.chardet_encoding, tried):
            yield self.chardet_encoding

        # As a last-ditch effort, try utf-8 and windows-1252.
        for e in ('utf-8', 'windows-1252'):
            if self._usable(e, tried):
                yield e

    @classmethod
    def strip_byte_order_mark(cls, data):
        """If a byte-order mark is present, strip it and return the encoding it implies."""
        encoding = None
        if (len(data) >= 4) and (data[:2] == b'\xfe\xff') \
               and (data[2:4] != '\x00\x00'):
            encoding = 'utf-16be'
            data = data[2:]
        elif (len(data) >= 4) and (data[:2] == b'\xff\xfe') \
                 and (data[2:4] != '\x00\x00'):
            encoding = 'utf-16le'
            data = data[2:]
        elif data[:3] == b'\xef\xbb\xbf':
            encoding = 'utf-8'
            data = data[3:]
        elif data[:4] == b'\x00\x00\xfe\xff':
            encoding = 'utf-32be'
            data = data[4:]
        elif data[:4] == b'\xff\xfe\x00\x00':
            encoding = 'utf-32le'
            data = data[4:]
        return data, encoding

    @classmethod
    def find_declared_encoding(cls, markup, is_html=False, search_entire_document=False):
        """Given a document, tries to find its declared encoding.

        An XML encoding is declared at the beginning of the document.

        An HTML encoding is declared in a <meta> tag, hopefully near the
        beginning of the document.
        """
        if search_entire_document:
            xml_endpos = html_endpos = len(markup)
        else:
            xml_endpos = 1024
            html_endpos = max(2048, int(len(markup) * 0.05))
            
        declared_encoding = None
        declared_encoding_match = xml_encoding_re.search(markup, endpos=xml_endpos)
        if not declared_encoding_match and is_html:
            declared_encoding_match = html_meta_re.search(markup, endpos=html_endpos)
        if declared_encoding_match is not None:
            declared_encoding = declared_encoding_match.groups()[0].decode(
                'ascii')
        if declared_encoding:
            return declared_encoding.lower()
        return None

class UnicodeDammit:
    """A class for detecting the encoding of a *ML document and
    converting it to a Unicode string. If the source encoding is
    windows-1252, can replace MS smart quotes with their HTML or XML
    equivalents."""

    # This dictionary maps commonly seen values for "charset" in HTML
    # meta tags to the corresponding Python codec names. It only covers
    # values that aren't in Python's aliases and can't be determined
    # by the heuristics in find_codec.
    CHARSET_ALIASES = {"macintosh": "mac-roman",
                       "x-sjis": "shift-jis"}

    ENCODINGS_WITH_SMART_QUOTES = [
        "windows-1252",
        "iso-8859-1",
        "iso-8859-2",
        ]

    def __init__(self, markup, override_encodings=[],
                 smart_quotes_to=None, is_html=False):
        self.smart_quotes_to = smart_quotes_to
        self.tried_encodings = []
        self.contains_replacement_characters = False
        self.is_html = is_html

        self.detector = EncodingDetector(markup, override_encodings, is_html)

        # Short-circuit if the data is in Unicode to begin with.
        if isinstance(markup, unicode) or markup == '':
            self.markup = markup
            self.unicode_markup = unicode(markup)
            self.original_encoding = None
            return

        # The encoding detector may have stripped a byte-order mark.
        # Use the stripped markup from this point on.
        self.markup = self.detector.markup

        u = None
        for encoding in self.detector.encodings:
            markup = self.detector.markup
            u = self._convert_from(encoding)
            if u is not None:
                break

        if not u:
            # None of the encodings worked. As an absolute last resort,
            # try them again with character replacement.

            for encoding in self.detector.encodings:
                if encoding != "ascii":
                    u = self._convert_from(encoding, "replace")
                if u is not None:
                    logging.warning(
                            "Some characters could not be decoded, and were "
                            "replaced with REPLACEMENT CHARACTER.")
                    self.contains_replacement_characters = True
                    break

        # If none of that worked, we could at this point force it to
        # ASCII, but that would destroy so much data that I think
        # giving up is better.
        self.unicode_markup = u
        if not u:
            self.original_encoding = None

    def _sub_ms_char(self, match):
        """Changes a MS smart quote character to an XML or HTML
        entity, or an ASCII character."""
        orig = match.group(1)
        if self.smart_quotes_to == 'ascii':
            sub = self.MS_CHARS_TO_ASCII.get(orig).encode()
        else:
            sub = self.MS_CHARS.get(orig)
            if type(sub) == tuple:
                if self.smart_quotes_to == 'xml':
                    sub = '&#x'.encode() + sub[1].encode() + ';'.encode()
                else:
                    sub = '&'.encode() + sub[0].encode() + ';'.encode()
            else:
                sub = sub.encode()
        return sub

    def _convert_from(self, proposed, errors="strict"):
        proposed = self.find_codec(proposed)
        if not proposed or (proposed, errors) in self.tried_encodings:
            return None
        self.tried_encodings.append((proposed, errors))
        markup = self.markup
        # Convert smart quotes to HTML if coming from an encoding
        # that might have them.
        if (self.smart_quotes_to is not None
            and proposed in self.ENCODINGS_WITH_SMART_QUOTES):
            smart_quotes_re = b"([\x80-\x9f])"
            smart_quotes_compiled = re.compile(smart_quotes_re)
            markup = smart_quotes_compiled.sub(self._sub_ms_char, markup)

        try:
            #print "Trying to convert document to %s (errors=%s)" % (
            #    proposed, errors)
            u = self._to_unicode(markup, proposed, errors)
            self.markup = u
            self.original_encoding = proposed
        except Exception as e:
            #print "That didn't work!"
            #print e
            return None
        #print "Correct encoding: %s" % proposed
        return self.markup

    def _to_unicode(self, data, encoding, errors="strict"):
        '''Given a string and its encoding, decodes the string into Unicode.
        %encoding is a string recognized by encodings.aliases'''
        return unicode(data, encoding, errors)

    @property
    def declared_html_encoding(self):
        if not self.is_html:
            return None
        return self.detector.declared_encoding

    def find_codec(self, charset):
        value = (self._codec(self.CHARSET_ALIASES.get(charset, charset))
               or (charset and self._codec(charset.replace("-", "")))
               or (charset and self._codec(charset.replace("-", "_")))
               or (charset and charset.lower())
               or charset
                )
        if value:
            return value.lower()
        return None

    def _codec(self, charset):
        if not charset:
            return charset
        codec = None
        try:
            codecs.lookup(charset)
            codec = charset
        except (LookupError, ValueError):
            pass
        return codec


    # A partial mapping of ISO-Latin-1 to HTML entities/XML numeric entities.
    MS_CHARS = {b'\x80': ('euro', '20AC'),
                b'\x81': ' ',
                b'\x82': ('sbquo', '201A'),
                b'\x83': ('fnof', '192'),
                b'\x84': ('bdquo', '201E'),
                b'\x85': ('hellip', '2026'),
                b'\x86': ('dagger', '2020'),
                b'\x87': ('Dagger', '2021'),
                b'\x88': ('circ', '2C6'),
                b'\x89': ('permil', '2030'),
                b'\x8A': ('Scaron', '160'),
                b'\x8B': ('lsaquo', '2039'),
                b'\x8C': ('OElig', '152'),
                b'\x8D': '?',
                b'\x8E': ('#x17D', '17D'),
                b'\x8F': '?',
                b'\x90': '?',
                b'\x91': ('lsquo', '2018'),
                b'\x92': ('rsquo', '2019'),
                b'\x93': ('ldquo', '201C'),
                b'\x94': ('rdquo', '201D'),
                b'\x95': ('bull', '2022'),
                b'\x96': ('ndash', '2013'),
                b'\x97': ('mdash', '2014'),
                b'\x98': ('tilde', '2DC'),
                b'\x99': ('trade', '2122'),
                b'\x9a': ('scaron', '161'),
                b'\x9b': ('rsaquo', '203A'),
                b'\x9c': ('oelig', '153'),
                b'\x9d': '?',
                b'\x9e': ('#x17E', '17E'),
                b'\x9f': ('Yuml', ''),}

    # A parochial partial mapping of ISO-Latin-1 to ASCII. Contains
    # horrors like stripping diacritical marks to turn á into a, but also
    # contains non-horrors like turning “ into ".
    MS_CHARS_TO_ASCII = {
        b'\x80' : 'EUR',
        b'\x81' : ' ',
        b'\x82' : ',',
        b'\x83' : 'f',
        b'\x84' : ',,',
        b'\x85' : '...',
        b'\x86' : '+',
        b'\x87' : '++',
        b'\x88' : '^',
        b'\x89' : '%',
        b'\x8a' : 'S',
        b'\x8b' : '<',
        b'\x8c' : 'OE',
        b'\x8d' : '?',
        b'\x8e' : 'Z',
        b'\x8f' : '?',
        b'\x90' : '?',
        b'\x91' : "'",
        b'\x92' : "'",
        b'\x93' : '"',
        b'\x94' : '"',
        b'\x95' : '*',
        b'\x96' : '-',
        b'\x97' : '--',
        b'\x98' : '~',
        b'\x99' : '(TM)',
        b'\x9a' : 's',
        b'\x9b' : '>',
        b'\x9c' : 'oe',
        b'\x9d' : '?',
        b'\x9e' : 'z',
        b'\x9f' : 'Y',
        b'\xa0' : ' ',
        b'\xa1' : '!',
        b'\xa2' : 'c',
        b'\xa3' : 'GBP',
        b'\xa4' : '$', #This approximation is especially parochial--this is the
                       #generic currency symbol.
        b'\xa5' : 'YEN',
        b'\xa6' : '|',
        b'\xa7' : 'S',
        b'\xa8' : '..',
        b'\xa9' : '',
        b'\xaa' : '(th)',
        b'\xab' : '<<',
        b'\xac' : '!',
        b'\xad' : ' ',
        b'\xae' : '(R)',
        b'\xaf' : '-',
        b'\xb0' : 'o',
        b'\xb1' : '+-',
        b'\xb2' : '2',
        b'\xb3' : '3',
        b'\xb4' : ("'", 'acute'),
        b'\xb5' : 'u',
        b'\xb6' : 'P',
        b'\xb7' : '*',
        b'\xb8' : ',',
        b'\xb9' : '1',
        b'\xba' : '(th)',
        b'\xbb' : '>>',
        b'\xbc' : '1/4',
        b'\xbd' : '1/2',
        b'\xbe' : '3/4',
        b'\xbf' : '?',
        b'\xc0' : 'A',
        b'\xc1' : 'A',
        b'\xc2' : 'A',
        b'\xc3' : 'A',
        b'\xc4' : 'A',
        b'\xc5' : 'A',
        b'\xc6' : 'AE',
        b'\xc7' : 'C',
        b'\xc8' : 'E',
        b'\xc9' : 'E',
        b'\xca' : 'E',
        b'\xcb' : 'E',
        b'\xcc' : 'I',
        b'\xcd' : 'I',
        b'\xce' : 'I',
        b'\xcf' : 'I',
        b'\xd0' : 'D',
        b'\xd1' : 'N',
        b'\xd2' : 'O',
        b'\xd3' : 'O',
        b'\xd4' : 'O',
        b'\xd5' : 'O',
        b'\xd6' : 'O',
        b'\xd7' : '*',
        b'\xd8' : 'O',
        b'\xd9' : 'U',
        b'\xda' : 'U',
        b'\xdb' : 'U',
        b'\xdc' : 'U',
        b'\xdd' : 'Y',
        b'\xde' : 'b',
        b'\xdf' : 'B',
        b'\xe0' : 'a',
        b'\xe1' : 'a',
        b'\xe2' : 'a',
        b'\xe3' : 'a',
        b'\xe4' : 'a',
        b'\xe5' : 'a',
        b'\xe6' : 'ae',
        b'\xe7' : 'c',
        b'\xe8' : 'e',
        b'\xe9' : 'e',
        b'\xea' : 'e',
        b'\xeb' : 'e',
        b'\xec' : 'i',
        b'\xed' : 'i',
        b'\xee' : 'i',
        b'\xef' : 'i',
        b'\xf0' : 'o',
        b'\xf1' : 'n',
        b'\xf2' : 'o',
        b'\xf3' : 'o',
        b'\xf4' : 'o',
        b'\xf5' : 'o',
        b'\xf6' : 'o',
        b'\xf7' : '/',
        b'\xf8' : 'o',
        b'\xf9' : 'u',
        b'\xfa' : 'u',
        b'\xfb' : 'u',
        b'\xfc' : 'u',
        b'\xfd' : 'y',
        b'\xfe' : 'b',
        b'\xff' : 'y',
        }

    # A map used when removing rogue Windows-1252/ISO-8859-1
    # characters in otherwise UTF-8 documents.
    #
    # Note that \x81, \x8d, \x8f, \x90, and \x9d are undefined in
    # Windows-1252.
    WINDOWS_1252_TO_UTF8 = {
        0x80 : b'\xe2\x82\xac', # €
        0x82 : b'\xe2\x80\x9a', # ‚
        0x83 : b'\xc6\x92',     # ƒ
        0x84 : b'\xe2\x80\x9e', # „
        0x85 : b'\xe2\x80\xa6', # …
        0x86 : b'\xe2\x80\xa0', # †
        0x87 : b'\xe2\x80\xa1', # ‡
        0x88 : b'\xcb\x86',     # ˆ
        0x89 : b'\xe2\x80\xb0', # ‰
        0x8a : b'\xc5\xa0',     # Š
        0x8b : b'\xe2\x80\xb9', # ‹
        0x8c : b'\xc5\x92',     # Œ
        0x8e : b'\xc5\xbd',     # Ž
        0x91 : b'\xe2\x80\x98', # ‘
        0x92 : b'\xe2\x80\x99', # ’
        0x93 : b'\xe2\x80\x9c', # “
        0x94 : b'\xe2\x80\x9d', # ”
        0x95 : b'\xe2\x80\xa2', # •
        0x96 : b'\xe2\x80\x93', # –
        0x97 : b'\xe2\x80\x94', # —
        0x98 : b'\xcb\x9c',     # ˜
        0x99 : b'\xe2\x84\xa2', # ™
        0x9a : b'\xc5\xa1',     # š
        0x9b : b'\xe2\x80\xba', # ›
        0x9c : b'\xc5\x93',     # œ
        0x9e : b'\xc5\xbe',     # ž
        0x9f : b'\xc5\xb8',     # Ÿ
        0xa0 : b'\xc2\xa0',     #  
        0xa1 : b'\xc2\xa1',     # ¡
        0xa2 : b'\xc2\xa2',     # ¢
        0xa3 : b'\xc2\xa3',     # £
        0xa4 : b'\xc2\xa4',     # ¤
        0xa5 : b'\xc2\xa5',     # ¥
        0xa6 : b'\xc2\xa6',     # ¦
        0xa7 : b'\xc2\xa7',     # §
        0xa8 : b'\xc2\xa8',     # ¨
        0xa9 : b'\xc2\xa9',     # ©
        0xaa : b'\xc2\xaa',     # ª
        0xab : b'\xc2\xab',     # «
        0xac : b'\xc2\xac',     # ¬
        0xad : b'\xc2\xad',     # ­
        0xae : b'\xc2\xae',     # ®
        0xaf : b'\xc2\xaf',     # ¯
        0xb0 : b'\xc2\xb0',     # °
        0xb1 : b'\xc2\xb1',     # ±
        0xb2 : b'\xc2\xb2',     # ²
        0xb3 : b'\xc2\xb3',     # ³
        0xb4 : b'\xc2\xb4',     # ´
        0xb5 : b'\xc2\xb5',     # µ
        0xb6 : b'\xc2\xb6',     # ¶
        0xb7 : b'\xc2\xb7',     # ·
        0xb8 : b'\xc2\xb8',     # ¸
        0xb9 : b'\xc2\xb9',     # ¹
        0xba : b'\xc2\xba',     # º
        0xbb : b'\xc2\xbb',     # »
        0xbc : b'\xc2\xbc',     # ¼
        0xbd : b'\xc2\xbd',     # ½
        0xbe : b'\xc2\xbe',     # ¾
        0xbf : b'\xc2\xbf',     # ¿
        0xc0 : b'\xc3\x80',     # À
        0xc1 : b'\xc3\x81',     # Á
        0xc2 : b'\xc3\x82',     # Â
        0xc3 : b'\xc3\x83',     # Ã
        0xc4 : b'\xc3\x84',     # Ä
        0xc5 : b'\xc3\x85',     # Å
        0xc6 : b'\xc3\x86',     # Æ
        0xc7 : b'\xc3\x87',     # Ç
        0xc8 : b'\xc3\x88',     # È
        0xc9 : b'\xc3\x89',     # É
        0xca : b'\xc3\x8a',     # Ê
        0xcb : b'\xc3\x8b',     # Ë
        0xcc : b'\xc3\x8c',     # Ì
        0xcd : b'\xc3\x8d',     # Í
        0xce : b'\xc3\x8e',     # Î
        0xcf : b'\xc3\x8f',     # Ï
        0xd0 : b'\xc3\x90',     # Ð
        0xd1 : b'\xc3\x91',     # Ñ
        0xd2 : b'\xc3\x92',     # Ò
        0xd3 : b'\xc3\x93',     # Ó
        0xd4 : b'\xc3\x94',     # Ô
        0xd5 : b'\xc3\x95',     # Õ
        0xd6 : b'\xc3\x96',     # Ö
        0xd7 : b'\xc3\x97',     # ×
        0xd8 : b'\xc3\x98',     # Ø
        0xd9 : b'\xc3\x99',     # Ù
        0xda : b'\xc3\x9a',     # Ú
        0xdb : b'\xc3\x9b',     # Û
        0xdc : b'\xc3\x9c',     # Ü
        0xdd : b'\xc3\x9d',     # Ý
        0xde : b'\xc3\x9e',     # Þ
        0xdf : b'\xc3\x9f',     # ß
        0xe0 : b'\xc3\xa0',     # à
        0xe1 : b'\xa1',     # á
        0xe2 : b'\xc3\xa2',     # â
        0xe3 : b'\xc3\xa3',     # ã
        0xe4 : b'\xc3\xa4',     # ä
        0xe5 : b'\xc3\xa5',     # å
        0xe6 : b'\xc3\xa6',     # æ
        0xe7 : b'\xc3\xa7',     # ç
        0xe8 : b'\xc3\xa8',     # è
        0xe9 : b'\xc3\xa9',     # é
        0xea : b'\xc3\xaa',     # ê
        0xeb : b'\xc3\xab',     # ë
        0xec : b'\xc3\xac',     # ì
        0xed : b'\xc3\xad',     # í
        0xee : b'\xc3\xae',     # î
        0xef : b'\xc3\xaf',     # ï
        0xf0 : b'\xc3\xb0',     # ð
        0xf1 : b'\xc3\xb1',     # ñ
        0xf2 : b'\xc3\xb2',     # ò
        0xf3 : b'\xc3\xb3',     # ó
        0xf4 : b'\xc3\xb4',     # ô
        0xf5 : b'\xc3\xb5',     # õ
        0xf6 : b'\xc3\xb6',     # ö
        0xf7 : b'\xc3\xb7',     # ÷
        0xf8 : b'\xc3\xb8',     # ø
        0xf9 : b'\xc3\xb9',     # ù
        0xfa : b'\xc3\xba',     # ú
        0xfb : b'\xc3\xbb',     # û
        0xfc : b'\xc3\xbc',     # ü
        0xfd : b'\xc3\xbd',     # ý
        0xfe : b'\xc3\xbe',     # þ
        }

    MULTIBYTE_MARKERS_AND_SIZES = [
        (0xc2, 0xdf, 2), # 2-byte characters start with a byte C2-DF
        (0xe0, 0xef, 3), # 3-byte characters start with E0-EF
        (0xf0, 0xf4, 4), # 4-byte characters start with F0-F4
        ]

    FIRST_MULTIBYTE_MARKER = MULTIBYTE_MARKERS_AND_SIZES[0][0]
    LAST_MULTIBYTE_MARKER = MULTIBYTE_MARKERS_AND_SIZES[-1][1]

    @classmethod
    def detwingle(cls, in_bytes, main_encoding="utf8",
                  embedded_encoding="windows-1252"):
        """Fix characters from one encoding embedded in some other encoding.

        Currently the only situation supported is Windows-1252 (or its
        subset ISO-8859-1), embedded in UTF-8.

        The input must be a bytestring. If you've already converted
        the document to Unicode, you're too late.

        The output is a bytestring in which `embedded_encoding`
        characters have been converted to their `main_encoding`
        equivalents.
        """
        if embedded_encoding.replace('_', '-').lower() not in (
            'windows-1252', 'windows_1252'):
            raise NotImplementedError(
                "Windows-1252 and ISO-8859-1 are the only currently supported "
                "embedded encodings.")

        if main_encoding.lower() not in ('utf8', 'utf-8'):
            raise NotImplementedError(
                "UTF-8 is the only currently supported main encoding.")

        byte_chunks = []

        chunk_start = 0
        pos = 0
        while pos < len(in_bytes):
            byte = in_bytes[pos]
            if not isinstance(byte, int):
                # Python 2.x
                byte = ord(byte)
            if (byte >= cls.FIRST_MULTIBYTE_MARKER
                and byte <= cls.LAST_MULTIBYTE_MARKER):
                # This is the start of a UTF-8 multibyte character. Skip
                # to the end.
                for start, end, size in cls.MULTIBYTE_MARKERS_AND_SIZES:
                    if byte >= start and byte <= end:
                        pos += size
                        break
            elif byte >= 0x80 and byte in cls.WINDOWS_1252_TO_UTF8:
                # We found a Windows-1252 character!
                # Save the string up to this point as a chunk.
                byte_chunks.append(in_bytes[chunk_start:pos])

                # Now translate the Windows-1252 character into UTF-8
                # and add it as another, one-byte chunk.
                byte_chunks.append(cls.WINDOWS_1252_TO_UTF8[byte])
                pos += 1
                chunk_start = pos
            else:
                # Go on to the next character.
                pos += 1
        if chunk_start == 0:
            # The string is unchanged.
            return in_bytes
        else:
            # Store the final chunk.
            byte_chunks.append(in_bytes[chunk_start:])
        return b''.join(byte_chunks)

