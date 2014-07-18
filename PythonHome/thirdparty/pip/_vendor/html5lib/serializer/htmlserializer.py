from __future__ import absolute_import, division, unicode_literals
from pip._vendor.six import text_type

import gettext
_ = gettext.gettext

try:
    from functools import reduce
except ImportError:
    pass

from ..constants import voidElements, booleanAttributes, spaceCharacters
from ..constants import rcdataElements, entities, xmlEntities
from .. import utils
from xml.sax.saxutils import escape

spaceCharacters = "".join(spaceCharacters)

try:
    from codecs import register_error, xmlcharrefreplace_errors
except ImportError:
    unicode_encode_errors = "strict"
else:
    unicode_encode_errors = "htmlentityreplace"

    encode_entity_map = {}
    is_ucs4 = len("\U0010FFFF") == 1
    for k, v in list(entities.items()):
        # skip multi-character entities
        if ((is_ucs4 and len(v) > 1) or
                (not is_ucs4 and len(v) > 2)):
            continue
        if v != "&":
            if len(v) == 2:
                v = utils.surrogatePairToCodepoint(v)
            else:
                v = ord(v)
            if not v in encode_entity_map or k.islower():
                # prefer &lt; over &LT; and similarly for &amp;, &gt;, etc.
                encode_entity_map[v] = k

    def htmlentityreplace_errors(exc):
        if isinstance(exc, (UnicodeEncodeError, UnicodeTranslateError)):
            res = []
            codepoints = []
            skip = False
            for i, c in enumerate(exc.object[exc.start:exc.end]):
                if skip:
                    skip = False
                    continue
                index = i + exc.start
                if utils.isSurrogatePair(exc.object[index:min([exc.end, index + 2])]):
                    codepoint = utils.surrogatePairToCodepoint(exc.object[index:index + 2])
                    skip = True
                else:
                    codepoint = ord(c)
                codepoints.append(codepoint)
            for cp in codepoints:
                e = encode_entity_map.get(cp)
                if e:
                    res.append("&")
                    res.append(e)
                    if not e.endswith(";"):
                        res.append(";")
                else:
                    res.append("&#x%s;" % (hex(cp)[2:]))
            return ("".join(res), exc.end)
        else:
            return xmlcharrefreplace_errors(exc)

    register_error(unicode_encode_errors, htmlentityreplace_errors)

    del register_error


class HTMLSerializer(object):

    # attribute quoting options
    quote_attr_values = False
    quote_char = '"'
    use_best_quote_char = True

    # tag syntax options
    omit_optional_tags = True
    minimize_boolean_attributes = True
    use_trailing_solidus = False
    space_before_trailing_solidus = True

    # escaping options
    escape_lt_in_attrs = False
    escape_rcdata = False
    resolve_entities = True

    # miscellaneous options
    alphabetical_attributes = False
    inject_meta_charset = True
    strip_whitespace = False
    sanitize = False

    options = ("quote_attr_values", "quote_char", "use_best_quote_char",
               "omit_optional_tags", "minimize_boolean_attributes",
               "use_trailing_solidus", "space_before_trailing_solidus",
               "escape_lt_in_attrs", "escape_rcdata", "resolve_entities",
               "alphabetical_attributes", "inject_meta_charset",
               "strip_whitespace", "sanitize")

    def __init__(self, **kwargs):
        """Initialize HTMLSerializer.

        Keyword options (default given first unless specified) include:

        inject_meta_charset=True|False
          Whether it insert a meta element to define the character set of the
          document.
        quote_attr_values=True|False
          Whether to quote attribute values that don't require quoting
          per HTML5 parsing rules.
        quote_char=u'"'|u"'"
          Use given quote character for attribute quoting. Default is to
          use double quote unless attribute value contains a double quote,
          in which case single quotes are used instead.
        escape_lt_in_attrs=False|True
          Whether to escape < in attribute values.
        escape_rcdata=False|True
          Whether to escape characters that need to be escaped within normal
          elements within rcdata elements such as style.
        resolve_entities=True|False
          Whether to resolve named character entities that appear in the
          source tree. The XML predefined entities &lt; &gt; &amp; &quot; &apos;
          are unaffected by this setting.
        strip_whitespace=False|True
          Whether to remove semantically meaningless whitespace. (This
          compresses all whitespace to a single space except within pre.)
        minimize_boolean_attributes=True|False
          Shortens boolean attributes to give just the attribute value,
          for example <input disabled="disabled"> becomes <input disabled>.
        use_trailing_solidus=False|True
          Includes a close-tag slash at the end of the start tag of void
          elements (empty elements whose end tag is forbidden). E.g. <hr/>.
        space_before_trailing_solidus=True|False
          Places a space immediately before the closing slash in a tag
          using a trailing solidus. E.g. <hr />. Requires use_trailing_solidus.
        sanitize=False|True
          Strip all unsafe or unknown constructs from output.
          See `html5lib user documentation`_
        omit_optional_tags=True|False
          Omit start/end tags that are optional.
        alphabetical_attributes=False|True
          Reorder attributes to be in alphabetical order.

        .. _html5lib user documentation: http://code.google.com/p/html5lib/wiki/UserDocumentation
        """
        if 'quote_char' in kwargs:
            self.use_best_quote_char = False
        for attr in self.options:
            setattr(self, attr, kwargs.get(attr, getattr(self, attr)))
        self.errors = []
        self.strict = False

    def encode(self, string):
        assert(isinstance(string, text_type))
        if self.encoding:
            return string.encode(self.encoding, unicode_encode_errors)
        else:
            return string

    def encodeStrict(self, string):
        assert(isinstance(string, text_type))
        if self.encoding:
            return string.encode(self.encoding, "strict")
        else:
            return string

    def serialize(self, treewalker, encoding=None):
        self.encoding = encoding
        in_cdata = False
        self.errors = []

        if encoding and self.inject_meta_charset:
            from ..filters.inject_meta_charset import Filter
            treewalker = Filter(treewalker, encoding)
        # WhitespaceFilter should be used before OptionalTagFilter
        # for maximum efficiently of this latter filter
        if self.strip_whitespace:
            from ..filters.whitespace import Filter
            treewalker = Filter(treewalker)
        if self.sanitize:
            from ..filters.sanitizer import Filter
            treewalker = Filter(treewalker)
        if self.omit_optional_tags:
            from ..filters.optionaltags import Filter
            treewalker = Filter(treewalker)
        # Alphabetical attributes must be last, as other filters
        # could add attributes and alter the order
        if self.alphabetical_attributes:
            from ..filters.alphabeticalattributes import Filter
            treewalker = Filter(treewalker)

        for token in treewalker:
            type = token["type"]
            if type == "Doctype":
                doctype = "<!DOCTYPE %s" % token["name"]

                if token["publicId"]:
                    doctype += ' PUBLIC "%s"' % token["publicId"]
                elif token["systemId"]:
                    doctype += " SYSTEM"
                if token["systemId"]:
                    if token["systemId"].find('"') >= 0:
                        if token["systemId"].find("'") >= 0:
                            self.serializeError(_("System identifer contains both single and double quote characters"))
                        quote_char = "'"
                    else:
                        quote_char = '"'
                    doctype += " %s%s%s" % (quote_char, token["systemId"], quote_char)

                doctype += ">"
                yield self.encodeStrict(doctype)

            elif type in ("Characters", "SpaceCharacters"):
                if type == "SpaceCharacters" or in_cdata:
                    if in_cdata and token["data"].find("</") >= 0:
                        self.serializeError(_("Unexpected </ in CDATA"))
                    yield self.encode(token["data"])
                else:
                    yield self.encode(escape(token["data"]))

            elif type in ("StartTag", "EmptyTag"):
                name = token["name"]
                yield self.encodeStrict("<%s" % name)
                if name in rcdataElements and not self.escape_rcdata:
                    in_cdata = True
                elif in_cdata:
                    self.serializeError(_("Unexpected child element of a CDATA element"))
                for (attr_namespace, attr_name), attr_value in token["data"].items():
                    # TODO: Add namespace support here
                    k = attr_name
                    v = attr_value
                    yield self.encodeStrict(' ')

                    yield self.encodeStrict(k)
                    if not self.minimize_boolean_attributes or \
                        (k not in booleanAttributes.get(name, tuple())
                         and k not in booleanAttributes.get("", tuple())):
                        yield self.encodeStrict("=")
                        if self.quote_attr_values or not v:
                            quote_attr = True
                        else:
                            quote_attr = reduce(lambda x, y: x or (y in v),
                                                spaceCharacters + ">\"'=", False)
                        v = v.replace("&", "&amp;")
                        if self.escape_lt_in_attrs:
                            v = v.replace("<", "&lt;")
                        if quote_attr:
                            quote_char = self.quote_char
                            if self.use_best_quote_char:
                                if "'" in v and '"' not in v:
                                    quote_char = '"'
                                elif '"' in v and "'" not in v:
                                    quote_char = "'"
                            if quote_char == "'":
                                v = v.replace("'", "&#39;")
                            else:
                                v = v.replace('"', "&quot;")
                            yield self.encodeStrict(quote_char)
                            yield self.encode(v)
                            yield self.encodeStrict(quote_char)
                        else:
                            yield self.encode(v)
                if name in voidElements and self.use_trailing_solidus:
                    if self.space_before_trailing_solidus:
                        yield self.encodeStrict(" /")
                    else:
                        yield self.encodeStrict("/")
                yield self.encode(">")

            elif type == "EndTag":
                name = token["name"]
                if name in rcdataElements:
                    in_cdata = False
                elif in_cdata:
                    self.serializeError(_("Unexpected child element of a CDATA element"))
                yield self.encodeStrict("</%s>" % name)

            elif type == "Comment":
                data = token["data"]
                if data.find("--") >= 0:
                    self.serializeError(_("Comment contains --"))
                yield self.encodeStrict("<!--%s-->" % token["data"])

            elif type == "Entity":
                name = token["name"]
                key = name + ";"
                if not key in entities:
                    self.serializeError(_("Entity %s not recognized" % name))
                if self.resolve_entities and key not in xmlEntities:
                    data = entities[key]
                else:
                    data = "&%s;" % name
                yield self.encodeStrict(data)

            else:
                self.serializeError(token["data"])

    def render(self, treewalker, encoding=None):
        if encoding:
            return b"".join(list(self.serialize(treewalker, encoding)))
        else:
            return "".join(list(self.serialize(treewalker)))

    def serializeError(self, data="XXX ERROR MESSAGE NEEDED"):
        # XXX The idea is to make data mandatory.
        self.errors.append(data)
        if self.strict:
            raise SerializeError


def SerializeError(Exception):
    """Error in serialized tree"""
    pass
