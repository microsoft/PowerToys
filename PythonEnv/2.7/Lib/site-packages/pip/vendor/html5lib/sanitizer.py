from __future__ import absolute_import, division, unicode_literals

import re
from xml.sax.saxutils import escape, unescape

from .tokenizer import HTMLTokenizer
from .constants import tokenTypes


class HTMLSanitizerMixin(object):
    """ sanitization of XHTML+MathML+SVG and of inline style attributes."""

    acceptable_elements = ['a', 'abbr', 'acronym', 'address', 'area',
                           'article', 'aside', 'audio', 'b', 'big', 'blockquote', 'br', 'button',
                           'canvas', 'caption', 'center', 'cite', 'code', 'col', 'colgroup',
                           'command', 'datagrid', 'datalist', 'dd', 'del', 'details', 'dfn',
                           'dialog', 'dir', 'div', 'dl', 'dt', 'em', 'event-source', 'fieldset',
                           'figcaption', 'figure', 'footer', 'font', 'form', 'header', 'h1',
                           'h2', 'h3', 'h4', 'h5', 'h6', 'hr', 'i', 'img', 'input', 'ins',
                           'keygen', 'kbd', 'label', 'legend', 'li', 'm', 'map', 'menu', 'meter',
                           'multicol', 'nav', 'nextid', 'ol', 'output', 'optgroup', 'option',
                           'p', 'pre', 'progress', 'q', 's', 'samp', 'section', 'select',
                           'small', 'sound', 'source', 'spacer', 'span', 'strike', 'strong',
                           'sub', 'sup', 'table', 'tbody', 'td', 'textarea', 'time', 'tfoot',
                           'th', 'thead', 'tr', 'tt', 'u', 'ul', 'var', 'video']

    mathml_elements = ['maction', 'math', 'merror', 'mfrac', 'mi',
                       'mmultiscripts', 'mn', 'mo', 'mover', 'mpadded', 'mphantom',
                       'mprescripts', 'mroot', 'mrow', 'mspace', 'msqrt', 'mstyle', 'msub',
                       'msubsup', 'msup', 'mtable', 'mtd', 'mtext', 'mtr', 'munder',
                       'munderover', 'none']

    svg_elements = ['a', 'animate', 'animateColor', 'animateMotion',
                    'animateTransform', 'clipPath', 'circle', 'defs', 'desc', 'ellipse',
                    'font-face', 'font-face-name', 'font-face-src', 'g', 'glyph', 'hkern',
                    'linearGradient', 'line', 'marker', 'metadata', 'missing-glyph',
                    'mpath', 'path', 'polygon', 'polyline', 'radialGradient', 'rect',
                    'set', 'stop', 'svg', 'switch', 'text', 'title', 'tspan', 'use']

    acceptable_attributes = ['abbr', 'accept', 'accept-charset', 'accesskey',
                             'action', 'align', 'alt', 'autocomplete', 'autofocus', 'axis',
                             'background', 'balance', 'bgcolor', 'bgproperties', 'border',
                             'bordercolor', 'bordercolordark', 'bordercolorlight', 'bottompadding',
                             'cellpadding', 'cellspacing', 'ch', 'challenge', 'char', 'charoff',
                             'choff', 'charset', 'checked', 'cite', 'class', 'clear', 'color',
                             'cols', 'colspan', 'compact', 'contenteditable', 'controls', 'coords',
                             'data', 'datafld', 'datapagesize', 'datasrc', 'datetime', 'default',
                             'delay', 'dir', 'disabled', 'draggable', 'dynsrc', 'enctype', 'end',
                             'face', 'for', 'form', 'frame', 'galleryimg', 'gutter', 'headers',
                             'height', 'hidefocus', 'hidden', 'high', 'href', 'hreflang', 'hspace',
                             'icon', 'id', 'inputmode', 'ismap', 'keytype', 'label', 'leftspacing',
                             'lang', 'list', 'longdesc', 'loop', 'loopcount', 'loopend',
                             'loopstart', 'low', 'lowsrc', 'max', 'maxlength', 'media', 'method',
                             'min', 'multiple', 'name', 'nohref', 'noshade', 'nowrap', 'open',
                             'optimum', 'pattern', 'ping', 'point-size', 'poster', 'pqg', 'preload',
                             'prompt', 'radiogroup', 'readonly', 'rel', 'repeat-max', 'repeat-min',
                             'replace', 'required', 'rev', 'rightspacing', 'rows', 'rowspan',
                             'rules', 'scope', 'selected', 'shape', 'size', 'span', 'src', 'start',
                             'step', 'style', 'summary', 'suppress', 'tabindex', 'target',
                             'template', 'title', 'toppadding', 'type', 'unselectable', 'usemap',
                             'urn', 'valign', 'value', 'variable', 'volume', 'vspace', 'vrml',
                             'width', 'wrap', 'xml:lang']

    mathml_attributes = ['actiontype', 'align', 'columnalign', 'columnalign',
                         'columnalign', 'columnlines', 'columnspacing', 'columnspan', 'depth',
                         'display', 'displaystyle', 'equalcolumns', 'equalrows', 'fence',
                         'fontstyle', 'fontweight', 'frame', 'height', 'linethickness', 'lspace',
                         'mathbackground', 'mathcolor', 'mathvariant', 'mathvariant', 'maxsize',
                         'minsize', 'other', 'rowalign', 'rowalign', 'rowalign', 'rowlines',
                         'rowspacing', 'rowspan', 'rspace', 'scriptlevel', 'selection',
                         'separator', 'stretchy', 'width', 'width', 'xlink:href', 'xlink:show',
                         'xlink:type', 'xmlns', 'xmlns:xlink']

    svg_attributes = ['accent-height', 'accumulate', 'additive', 'alphabetic',
                      'arabic-form', 'ascent', 'attributeName', 'attributeType',
                      'baseProfile', 'bbox', 'begin', 'by', 'calcMode', 'cap-height',
                      'class', 'clip-path', 'color', 'color-rendering', 'content', 'cx',
                      'cy', 'd', 'dx', 'dy', 'descent', 'display', 'dur', 'end', 'fill',
                      'fill-opacity', 'fill-rule', 'font-family', 'font-size',
                      'font-stretch', 'font-style', 'font-variant', 'font-weight', 'from',
                      'fx', 'fy', 'g1', 'g2', 'glyph-name', 'gradientUnits', 'hanging',
                      'height', 'horiz-adv-x', 'horiz-origin-x', 'id', 'ideographic', 'k',
                      'keyPoints', 'keySplines', 'keyTimes', 'lang', 'marker-end',
                      'marker-mid', 'marker-start', 'markerHeight', 'markerUnits',
                      'markerWidth', 'mathematical', 'max', 'min', 'name', 'offset',
                      'opacity', 'orient', 'origin', 'overline-position',
                      'overline-thickness', 'panose-1', 'path', 'pathLength', 'points',
                      'preserveAspectRatio', 'r', 'refX', 'refY', 'repeatCount',
                      'repeatDur', 'requiredExtensions', 'requiredFeatures', 'restart',
                      'rotate', 'rx', 'ry', 'slope', 'stemh', 'stemv', 'stop-color',
                      'stop-opacity', 'strikethrough-position', 'strikethrough-thickness',
                      'stroke', 'stroke-dasharray', 'stroke-dashoffset', 'stroke-linecap',
                      'stroke-linejoin', 'stroke-miterlimit', 'stroke-opacity',
                      'stroke-width', 'systemLanguage', 'target', 'text-anchor', 'to',
                      'transform', 'type', 'u1', 'u2', 'underline-position',
                      'underline-thickness', 'unicode', 'unicode-range', 'units-per-em',
                      'values', 'version', 'viewBox', 'visibility', 'width', 'widths', 'x',
                      'x-height', 'x1', 'x2', 'xlink:actuate', 'xlink:arcrole',
                      'xlink:href', 'xlink:role', 'xlink:show', 'xlink:title', 'xlink:type',
                      'xml:base', 'xml:lang', 'xml:space', 'xmlns', 'xmlns:xlink', 'y',
                      'y1', 'y2', 'zoomAndPan']

    attr_val_is_uri = ['href', 'src', 'cite', 'action', 'longdesc', 'poster',
                       'xlink:href', 'xml:base']

    svg_attr_val_allows_ref = ['clip-path', 'color-profile', 'cursor', 'fill',
                               'filter', 'marker', 'marker-start', 'marker-mid', 'marker-end',
                               'mask', 'stroke']

    svg_allow_local_href = ['altGlyph', 'animate', 'animateColor',
                            'animateMotion', 'animateTransform', 'cursor', 'feImage', 'filter',
                            'linearGradient', 'pattern', 'radialGradient', 'textpath', 'tref',
                            'set', 'use']

    acceptable_css_properties = ['azimuth', 'background-color',
                                 'border-bottom-color', 'border-collapse', 'border-color',
                                 'border-left-color', 'border-right-color', 'border-top-color', 'clear',
                                 'color', 'cursor', 'direction', 'display', 'elevation', 'float', 'font',
                                 'font-family', 'font-size', 'font-style', 'font-variant', 'font-weight',
                                 'height', 'letter-spacing', 'line-height', 'overflow', 'pause',
                                 'pause-after', 'pause-before', 'pitch', 'pitch-range', 'richness',
                                 'speak', 'speak-header', 'speak-numeral', 'speak-punctuation',
                                 'speech-rate', 'stress', 'text-align', 'text-decoration', 'text-indent',
                                 'unicode-bidi', 'vertical-align', 'voice-family', 'volume',
                                 'white-space', 'width']

    acceptable_css_keywords = ['auto', 'aqua', 'black', 'block', 'blue',
                               'bold', 'both', 'bottom', 'brown', 'center', 'collapse', 'dashed',
                               'dotted', 'fuchsia', 'gray', 'green', '!important', 'italic', 'left',
                               'lime', 'maroon', 'medium', 'none', 'navy', 'normal', 'nowrap', 'olive',
                               'pointer', 'purple', 'red', 'right', 'solid', 'silver', 'teal', 'top',
                               'transparent', 'underline', 'white', 'yellow']

    acceptable_svg_properties = ['fill', 'fill-opacity', 'fill-rule',
                                 'stroke', 'stroke-width', 'stroke-linecap', 'stroke-linejoin',
                                 'stroke-opacity']

    acceptable_protocols = ['ed2k', 'ftp', 'http', 'https', 'irc',
                            'mailto', 'news', 'gopher', 'nntp', 'telnet', 'webcal',
                            'xmpp', 'callto', 'feed', 'urn', 'aim', 'rsync', 'tag',
                            'ssh', 'sftp', 'rtsp', 'afs']

    # subclasses may define their own versions of these constants
    allowed_elements = acceptable_elements + mathml_elements + svg_elements
    allowed_attributes = acceptable_attributes + mathml_attributes + svg_attributes
    allowed_css_properties = acceptable_css_properties
    allowed_css_keywords = acceptable_css_keywords
    allowed_svg_properties = acceptable_svg_properties
    allowed_protocols = acceptable_protocols

    # Sanitize the +html+, escaping all elements not in ALLOWED_ELEMENTS, and
    # stripping out all # attributes not in ALLOWED_ATTRIBUTES. Style
    # attributes are parsed, and a restricted set, # specified by
    # ALLOWED_CSS_PROPERTIES and ALLOWED_CSS_KEYWORDS, are allowed through.
    # attributes in ATTR_VAL_IS_URI are scanned, and only URI schemes specified
    # in ALLOWED_PROTOCOLS are allowed.
    #
    #   sanitize_html('<script> do_nasty_stuff() </script>')
    #    => &lt;script> do_nasty_stuff() &lt;/script>
    #   sanitize_html('<a href="javascript: sucker();">Click here for $100</a>')
    #    => <a>Click here for $100</a>
    def sanitize_token(self, token):

        # accommodate filters which use token_type differently
        token_type = token["type"]
        if token_type in list(tokenTypes.keys()):
            token_type = tokenTypes[token_type]

        if token_type in (tokenTypes["StartTag"], tokenTypes["EndTag"],
                          tokenTypes["EmptyTag"]):
            if token["name"] in self.allowed_elements:
                return self.allowed_token(token, token_type)
            else:
                return self.disallowed_token(token, token_type)
        elif token_type == tokenTypes["Comment"]:
            pass
        else:
            return token

    def allowed_token(self, token, token_type):
        if "data" in token:
            attrs = dict([(name, val) for name, val in
                          token["data"][::-1]
                          if name in self.allowed_attributes])
            for attr in self.attr_val_is_uri:
                if attr not in attrs:
                    continue
                val_unescaped = re.sub("[`\000-\040\177-\240\s]+", '',
                                       unescape(attrs[attr])).lower()
                # remove replacement characters from unescaped characters
                val_unescaped = val_unescaped.replace("\ufffd", "")
                if (re.match("^[a-z0-9][-+.a-z0-9]*:", val_unescaped) and
                    (val_unescaped.split(':')[0] not in
                     self.allowed_protocols)):
                    del attrs[attr]
            for attr in self.svg_attr_val_allows_ref:
                if attr in attrs:
                    attrs[attr] = re.sub(r'url\s*\(\s*[^#\s][^)]+?\)',
                                         ' ',
                                         unescape(attrs[attr]))
            if (token["name"] in self.svg_allow_local_href and
                'xlink:href' in attrs and re.search('^\s*[^#\s].*',
                                                    attrs['xlink:href'])):
                del attrs['xlink:href']
            if 'style' in attrs:
                attrs['style'] = self.sanitize_css(attrs['style'])
            token["data"] = [[name, val] for name, val in list(attrs.items())]
        return token

    def disallowed_token(self, token, token_type):
        if token_type == tokenTypes["EndTag"]:
            token["data"] = "</%s>" % token["name"]
        elif token["data"]:
            attrs = ''.join([' %s="%s"' % (k, escape(v)) for k, v in token["data"]])
            token["data"] = "<%s%s>" % (token["name"], attrs)
        else:
            token["data"] = "<%s>" % token["name"]
        if token.get("selfClosing"):
            token["data"] = token["data"][:-1] + "/>"

        if token["type"] in list(tokenTypes.keys()):
            token["type"] = "Characters"
        else:
            token["type"] = tokenTypes["Characters"]

        del token["name"]
        return token

    def sanitize_css(self, style):
        # disallow urls
        style = re.compile('url\s*\(\s*[^\s)]+?\s*\)\s*').sub(' ', style)

        # gauntlet
        if not re.match("""^([:,;#%.\sa-zA-Z0-9!]|\w-\w|'[\s\w]+'|"[\s\w]+"|\([\d,\s]+\))*$""", style):
            return ''
        if not re.match("^\s*([-\w]+\s*:[^:;]*(;\s*|$))*$", style):
            return ''

        clean = []
        for prop, value in re.findall("([-\w]+)\s*:\s*([^:;]*)", style):
            if not value:
                continue
            if prop.lower() in self.allowed_css_properties:
                clean.append(prop + ': ' + value + ';')
            elif prop.split('-')[0].lower() in ['background', 'border', 'margin',
                                                'padding']:
                for keyword in value.split():
                    if not keyword in self.acceptable_css_keywords and \
                            not re.match("^(#[0-9a-f]+|rgb\(\d+%?,\d*%?,?\d*%?\)?|\d{0,2}\.?\d{0,2}(cm|em|ex|in|mm|pc|pt|px|%|,|\))?)$", keyword):
                        break
                else:
                    clean.append(prop + ': ' + value + ';')
            elif prop.lower() in self.allowed_svg_properties:
                clean.append(prop + ': ' + value + ';')

        return ' '.join(clean)


class HTMLSanitizer(HTMLTokenizer, HTMLSanitizerMixin):
    def __init__(self, stream, encoding=None, parseMeta=True, useChardet=True,
                 lowercaseElementName=False, lowercaseAttrName=False, parser=None):
        # Change case matching defaults as we only output lowercase html anyway
        # This solution doesn't seem ideal...
        HTMLTokenizer.__init__(self, stream, encoding, parseMeta, useChardet,
                               lowercaseElementName, lowercaseAttrName, parser=parser)

    def __iter__(self):
        for token in HTMLTokenizer.__iter__(self):
            token = self.sanitize_token(token)
            if token:
                yield token
