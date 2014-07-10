"""Beautiful Soup
Elixir and Tonic
"The Screen-Scraper's Friend"
http://www.crummy.com/software/BeautifulSoup/

Beautiful Soup uses a pluggable XML or HTML parser to parse a
(possibly invalid) document into a tree representation. Beautiful Soup
provides provides methods and Pythonic idioms that make it easy to
navigate, search, and modify the parse tree.

Beautiful Soup works with Python 2.6 and up. It works better if lxml
and/or html5lib is installed.

For more than you ever wanted to know about Beautiful Soup, see the
documentation:
http://www.crummy.com/software/BeautifulSoup/bs4/doc/
"""

__author__ = "Leonard Richardson (leonardr@segfault.org)"
__version__ = "4.3.2"
__copyright__ = "Copyright (c) 2004-2013 Leonard Richardson"
__license__ = "MIT"

__all__ = ['BeautifulSoup']

import os
import re
import warnings

from .builder import builder_registry, ParserRejectedMarkup
from .dammit import UnicodeDammit
from .element import (
    CData,
    Comment,
    DEFAULT_OUTPUT_ENCODING,
    Declaration,
    Doctype,
    NavigableString,
    PageElement,
    ProcessingInstruction,
    ResultSet,
    SoupStrainer,
    Tag,
    )

# The very first thing we do is give a useful error if someone is
# running this code under Python 3 without converting it.
syntax_error = u'You are trying to run the Python 2 version of Beautiful Soup under Python 3. This will not work. You need to convert the code, either by installing it (`python setup.py install`) or by running 2to3 (`2to3 -w bs4`).'

class BeautifulSoup(Tag):
    """
    This class defines the basic interface called by the tree builders.

    These methods will be called by the parser:
      reset()
      feed(markup)

    The tree builder may call these methods from its feed() implementation:
      handle_starttag(name, attrs) # See note about return value
      handle_endtag(name)
      handle_data(data) # Appends to the current data node
      endData(containerClass=NavigableString) # Ends the current data node

    No matter how complicated the underlying parser is, you should be
    able to build a tree using 'start tag' events, 'end tag' events,
    'data' events, and "done with data" events.

    If you encounter an empty-element tag (aka a self-closing tag,
    like HTML's <br> tag), call handle_starttag and then
    handle_endtag.
    """
    ROOT_TAG_NAME = u'[document]'

    # If the end-user gives no indication which tree builder they
    # want, look for one with these features.
    DEFAULT_BUILDER_FEATURES = ['html', 'fast']

    ASCII_SPACES = '\x20\x0a\x09\x0c\x0d'

    def __init__(self, markup="", features=None, builder=None,
                 parse_only=None, from_encoding=None, **kwargs):
        """The Soup object is initialized as the 'root tag', and the
        provided markup (which can be a string or a file-like object)
        is fed into the underlying parser."""

        if 'convertEntities' in kwargs:
            warnings.warn(
                "BS4 does not respect the convertEntities argument to the "
                "BeautifulSoup constructor. Entities are always converted "
                "to Unicode characters.")

        if 'markupMassage' in kwargs:
            del kwargs['markupMassage']
            warnings.warn(
                "BS4 does not respect the markupMassage argument to the "
                "BeautifulSoup constructor. The tree builder is responsible "
                "for any necessary markup massage.")

        if 'smartQuotesTo' in kwargs:
            del kwargs['smartQuotesTo']
            warnings.warn(
                "BS4 does not respect the smartQuotesTo argument to the "
                "BeautifulSoup constructor. Smart quotes are always converted "
                "to Unicode characters.")

        if 'selfClosingTags' in kwargs:
            del kwargs['selfClosingTags']
            warnings.warn(
                "BS4 does not respect the selfClosingTags argument to the "
                "BeautifulSoup constructor. The tree builder is responsible "
                "for understanding self-closing tags.")

        if 'isHTML' in kwargs:
            del kwargs['isHTML']
            warnings.warn(
                "BS4 does not respect the isHTML argument to the "
                "BeautifulSoup constructor. You can pass in features='html' "
                "or features='xml' to get a builder capable of handling "
                "one or the other.")

        def deprecated_argument(old_name, new_name):
            if old_name in kwargs:
                warnings.warn(
                    'The "%s" argument to the BeautifulSoup constructor '
                    'has been renamed to "%s."' % (old_name, new_name))
                value = kwargs[old_name]
                del kwargs[old_name]
                return value
            return None

        parse_only = parse_only or deprecated_argument(
            "parseOnlyThese", "parse_only")

        from_encoding = from_encoding or deprecated_argument(
            "fromEncoding", "from_encoding")

        if len(kwargs) > 0:
            arg = kwargs.keys().pop()
            raise TypeError(
                "__init__() got an unexpected keyword argument '%s'" % arg)

        if builder is None:
            if isinstance(features, basestring):
                features = [features]
            if features is None or len(features) == 0:
                features = self.DEFAULT_BUILDER_FEATURES
            builder_class = builder_registry.lookup(*features)
            if builder_class is None:
                raise FeatureNotFound(
                    "Couldn't find a tree builder with the features you "
                    "requested: %s. Do you need to install a parser library?"
                    % ",".join(features))
            builder = builder_class()
        self.builder = builder
        self.is_xml = builder.is_xml
        self.builder.soup = self

        self.parse_only = parse_only

        if hasattr(markup, 'read'):        # It's a file-type object.
            markup = markup.read()
        elif len(markup) <= 256:
            # Print out warnings for a couple beginner problems
            # involving passing non-markup to Beautiful Soup.
            # Beautiful Soup will still parse the input as markup,
            # just in case that's what the user really wants.
            if (isinstance(markup, unicode)
                and not os.path.supports_unicode_filenames):
                possible_filename = markup.encode("utf8")
            else:
                possible_filename = markup
            is_file = False
            try:
                is_file = os.path.exists(possible_filename)
            except Exception, e:
                # This is almost certainly a problem involving
                # characters not valid in filenames on this
                # system. Just let it go.
                pass
            if is_file:
                warnings.warn(
                    '"%s" looks like a filename, not markup. You should probably open this file and pass the filehandle into Beautiful Soup.' % markup)
            if markup[:5] == "http:" or markup[:6] == "https:":
                # TODO: This is ugly but I couldn't get it to work in
                # Python 3 otherwise.
                if ((isinstance(markup, bytes) and not b' ' in markup)
                    or (isinstance(markup, unicode) and not u' ' in markup)):
                    warnings.warn(
                        '"%s" looks like a URL. Beautiful Soup is not an HTTP client. You should probably use an HTTP client to get the document behind the URL, and feed that document to Beautiful Soup.' % markup)

        for (self.markup, self.original_encoding, self.declared_html_encoding,
         self.contains_replacement_characters) in (
            self.builder.prepare_markup(markup, from_encoding)):
            self.reset()
            try:
                self._feed()
                break
            except ParserRejectedMarkup:
                pass

        # Clear out the markup and remove the builder's circular
        # reference to this object.
        self.markup = None
        self.builder.soup = None

    def _feed(self):
        # Convert the document to Unicode.
        self.builder.reset()

        self.builder.feed(self.markup)
        # Close out any unfinished strings and close all the open tags.
        self.endData()
        while self.currentTag.name != self.ROOT_TAG_NAME:
            self.popTag()

    def reset(self):
        Tag.__init__(self, self, self.builder, self.ROOT_TAG_NAME)
        self.hidden = 1
        self.builder.reset()
        self.current_data = []
        self.currentTag = None
        self.tagStack = []
        self.preserve_whitespace_tag_stack = []
        self.pushTag(self)

    def new_tag(self, name, namespace=None, nsprefix=None, **attrs):
        """Create a new tag associated with this soup."""
        return Tag(None, self.builder, name, namespace, nsprefix, attrs)

    def new_string(self, s, subclass=NavigableString):
        """Create a new NavigableString associated with this soup."""
        navigable = subclass(s)
        navigable.setup()
        return navigable

    def insert_before(self, successor):
        raise NotImplementedError("BeautifulSoup objects don't support insert_before().")

    def insert_after(self, successor):
        raise NotImplementedError("BeautifulSoup objects don't support insert_after().")

    def popTag(self):
        tag = self.tagStack.pop()
        if self.preserve_whitespace_tag_stack and tag == self.preserve_whitespace_tag_stack[-1]:
            self.preserve_whitespace_tag_stack.pop()
        #print "Pop", tag.name
        if self.tagStack:
            self.currentTag = self.tagStack[-1]
        return self.currentTag

    def pushTag(self, tag):
        #print "Push", tag.name
        if self.currentTag:
            self.currentTag.contents.append(tag)
        self.tagStack.append(tag)
        self.currentTag = self.tagStack[-1]
        if tag.name in self.builder.preserve_whitespace_tags:
            self.preserve_whitespace_tag_stack.append(tag)

    def endData(self, containerClass=NavigableString):
        if self.current_data:
            current_data = u''.join(self.current_data)
            # If whitespace is not preserved, and this string contains
            # nothing but ASCII spaces, replace it with a single space
            # or newline.
            if not self.preserve_whitespace_tag_stack:
                strippable = True
                for i in current_data:
                    if i not in self.ASCII_SPACES:
                        strippable = False
                        break
                if strippable:
                    if '\n' in current_data:
                        current_data = '\n'
                    else:
                        current_data = ' '

            # Reset the data collector.
            self.current_data = []

            # Should we add this string to the tree at all?
            if self.parse_only and len(self.tagStack) <= 1 and \
                   (not self.parse_only.text or \
                    not self.parse_only.search(current_data)):
                return

            o = containerClass(current_data)
            self.object_was_parsed(o)

    def object_was_parsed(self, o, parent=None, most_recent_element=None):
        """Add an object to the parse tree."""
        parent = parent or self.currentTag
        most_recent_element = most_recent_element or self._most_recent_element
        o.setup(parent, most_recent_element)

        if most_recent_element is not None:
            most_recent_element.next_element = o
        self._most_recent_element = o
        parent.contents.append(o)

    def _popToTag(self, name, nsprefix=None, inclusivePop=True):
        """Pops the tag stack up to and including the most recent
        instance of the given tag. If inclusivePop is false, pops the tag
        stack up to but *not* including the most recent instqance of
        the given tag."""
        #print "Popping to %s" % name
        if name == self.ROOT_TAG_NAME:
            # The BeautifulSoup object itself can never be popped.
            return

        most_recently_popped = None

        stack_size = len(self.tagStack)
        for i in range(stack_size - 1, 0, -1):
            t = self.tagStack[i]
            if (name == t.name and nsprefix == t.prefix):
                if inclusivePop:
                    most_recently_popped = self.popTag()
                break
            most_recently_popped = self.popTag()

        return most_recently_popped

    def handle_starttag(self, name, namespace, nsprefix, attrs):
        """Push a start tag on to the stack.

        If this method returns None, the tag was rejected by the
        SoupStrainer. You should proceed as if the tag had not occured
        in the document. For instance, if this was a self-closing tag,
        don't call handle_endtag.
        """

        # print "Start tag %s: %s" % (name, attrs)
        self.endData()

        if (self.parse_only and len(self.tagStack) <= 1
            and (self.parse_only.text
                 or not self.parse_only.search_tag(name, attrs))):
            return None

        tag = Tag(self, self.builder, name, namespace, nsprefix, attrs,
                  self.currentTag, self._most_recent_element)
        if tag is None:
            return tag
        if self._most_recent_element:
            self._most_recent_element.next_element = tag
        self._most_recent_element = tag
        self.pushTag(tag)
        return tag

    def handle_endtag(self, name, nsprefix=None):
        #print "End tag: " + name
        self.endData()
        self._popToTag(name, nsprefix)

    def handle_data(self, data):
        self.current_data.append(data)

    def decode(self, pretty_print=False,
               eventual_encoding=DEFAULT_OUTPUT_ENCODING,
               formatter="minimal"):
        """Returns a string or Unicode representation of this document.
        To get Unicode, pass None for encoding."""

        if self.is_xml:
            # Print the XML declaration
            encoding_part = ''
            if eventual_encoding != None:
                encoding_part = ' encoding="%s"' % eventual_encoding
            prefix = u'<?xml version="1.0"%s?>\n' % encoding_part
        else:
            prefix = u''
        if not pretty_print:
            indent_level = None
        else:
            indent_level = 0
        return prefix + super(BeautifulSoup, self).decode(
            indent_level, eventual_encoding, formatter)

# Alias to make it easier to type import: 'from bs4 import _soup'
_s = BeautifulSoup
_soup = BeautifulSoup

class BeautifulStoneSoup(BeautifulSoup):
    """Deprecated interface to an XML parser."""

    def __init__(self, *args, **kwargs):
        kwargs['features'] = 'xml'
        warnings.warn(
            'The BeautifulStoneSoup class is deprecated. Instead of using '
            'it, pass features="xml" into the BeautifulSoup constructor.')
        super(BeautifulStoneSoup, self).__init__(*args, **kwargs)


class StopParsing(Exception):
    pass

class FeatureNotFound(ValueError):
    pass


#By default, act as an HTML pretty-printer.
if __name__ == '__main__':
    import sys
    soup = BeautifulSoup(sys.stdin)
    print soup.prettify()
