#
# ElementTree
# $Id: ElementTree.py 3440 2008-07-18 14:45:01Z fredrik $
#
# light-weight XML support for Python 2.3 and later.
#
# history (since 1.2.6):
# 2005-11-12 fl   added tostringlist/fromstringlist helpers
# 2006-07-05 fl   merged in selected changes from the 1.3 sandbox
# 2006-07-05 fl   removed support for 2.1 and earlier
# 2007-06-21 fl   added deprecation/future warnings
# 2007-08-25 fl   added doctype hook, added parser version attribute etc
# 2007-08-26 fl   added new serializer code (better namespace handling, etc)
# 2007-08-27 fl   warn for broken /tag searches on tree level
# 2007-09-02 fl   added html/text methods to serializer (experimental)
# 2007-09-05 fl   added method argument to tostring/tostringlist
# 2007-09-06 fl   improved error handling
# 2007-09-13 fl   added itertext, iterfind; assorted cleanups
# 2007-12-15 fl   added C14N hooks, copy method (experimental)
#
# Copyright (c) 1999-2008 by Fredrik Lundh.  All rights reserved.
#
# fredrik@pythonware.com
# http://www.pythonware.com
#
# --------------------------------------------------------------------
# The ElementTree toolkit is
#
# Copyright (c) 1999-2008 by Fredrik Lundh
#
# By obtaining, using, and/or copying this software and/or its
# associated documentation, you agree that you have read, understood,
# and will comply with the following terms and conditions:
#
# Permission to use, copy, modify, and distribute this software and
# its associated documentation for any purpose and without fee is
# hereby granted, provided that the above copyright notice appears in
# all copies, and that both that copyright notice and this permission
# notice appear in supporting documentation, and that the name of
# Secret Labs AB or the author not be used in advertising or publicity
# pertaining to distribution of the software without specific, written
# prior permission.
#
# SECRET LABS AB AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD
# TO THIS SOFTWARE, INCLUDING ALL IMPLIED WARRANTIES OF MERCHANT-
# ABILITY AND FITNESS.  IN NO EVENT SHALL SECRET LABS AB OR THE AUTHOR
# BE LIABLE FOR ANY SPECIAL, INDIRECT OR CONSEQUENTIAL DAMAGES OR ANY
# DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS,
# WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS
# ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE
# OF THIS SOFTWARE.
# --------------------------------------------------------------------

# Licensed to PSF under a Contributor Agreement.
# See http://www.python.org/psf/license for licensing details.

__all__ = [
    # public symbols
    "Comment",
    "dump",
    "Element", "ElementTree",
    "fromstring", "fromstringlist",
    "iselement", "iterparse",
    "parse", "ParseError",
    "PI", "ProcessingInstruction",
    "QName",
    "SubElement",
    "tostring", "tostringlist",
    "TreeBuilder",
    "VERSION",
    "XML",
    "XMLParser", "XMLTreeBuilder",
    ]

VERSION = "1.3.0"

##
# The <b>Element</b> type is a flexible container object, designed to
# store hierarchical data structures in memory. The type can be
# described as a cross between a list and a dictionary.
# <p>
# Each element has a number of properties associated with it:
# <ul>
# <li>a <i>tag</i>. This is a string identifying what kind of data
# this element represents (the element type, in other words).</li>
# <li>a number of <i>attributes</i>, stored in a Python dictionary.</li>
# <li>a <i>text</i> string.</li>
# <li>an optional <i>tail</i> string.</li>
# <li>a number of <i>child elements</i>, stored in a Python sequence</li>
# </ul>
#
# To create an element instance, use the {@link #Element} constructor
# or the {@link #SubElement} factory function.
# <p>
# The {@link #ElementTree} class can be used to wrap an element
# structure, and convert it from and to XML.
##

import sys
import re
import warnings


class _SimpleElementPath(object):
    # emulate pre-1.2 find/findtext/findall behaviour
    def find(self, element, tag, namespaces=None):
        for elem in element:
            if elem.tag == tag:
                return elem
        return None
    def findtext(self, element, tag, default=None, namespaces=None):
        elem = self.find(element, tag)
        if elem is None:
            return default
        return elem.text or ""
    def iterfind(self, element, tag, namespaces=None):
        if tag[:3] == ".//":
            for elem in element.iter(tag[3:]):
                yield elem
        for elem in element:
            if elem.tag == tag:
                yield elem
    def findall(self, element, tag, namespaces=None):
        return list(self.iterfind(element, tag, namespaces))

try:
    from . import ElementPath
except ImportError:
    ElementPath = _SimpleElementPath()

##
# Parser error.  This is a subclass of <b>SyntaxError</b>.
# <p>
# In addition to the exception value, an exception instance contains a
# specific exception code in the <b>code</b> attribute, and the line and
# column of the error in the <b>position</b> attribute.

class ParseError(SyntaxError):
    pass

# --------------------------------------------------------------------

##
# Checks if an object appears to be a valid element object.
#
# @param An element instance.
# @return A true value if this is an element object.
# @defreturn flag

def iselement(element):
    # FIXME: not sure about this; might be a better idea to look
    # for tag/attrib/text attributes
    return isinstance(element, Element) or hasattr(element, "tag")

##
# Element class.  This class defines the Element interface, and
# provides a reference implementation of this interface.
# <p>
# The element name, attribute names, and attribute values can be
# either ASCII strings (ordinary Python strings containing only 7-bit
# ASCII characters) or Unicode strings.
#
# @param tag The element name.
# @param attrib An optional dictionary, containing element attributes.
# @param **extra Additional attributes, given as keyword arguments.
# @see Element
# @see SubElement
# @see Comment
# @see ProcessingInstruction

class Element(object):
    # <tag attrib>text<child/>...</tag>tail

    ##
    # (Attribute) Element tag.

    tag = None

    ##
    # (Attribute) Element attribute dictionary.  Where possible, use
    # {@link #Element.get},
    # {@link #Element.set},
    # {@link #Element.keys}, and
    # {@link #Element.items} to access
    # element attributes.

    attrib = None

    ##
    # (Attribute) Text before first subelement.  This is either a
    # string or the value None.  Note that if there was no text, this
    # attribute may be either None or an empty string, depending on
    # the parser.

    text = None

    ##
    # (Attribute) Text after this element's end tag, but before the
    # next sibling element's start tag.  This is either a string or
    # the value None.  Note that if there was no text, this attribute
    # may be either None or an empty string, depending on the parser.

    tail = None # text after end tag, if any

    # constructor

    def __init__(self, tag, attrib={}, **extra):
        attrib = attrib.copy()
        attrib.update(extra)
        self.tag = tag
        self.attrib = attrib
        self._children = []

    def __repr__(self):
        return "<Element %s at 0x%x>" % (repr(self.tag), id(self))

    ##
    # Creates a new element object of the same type as this element.
    #
    # @param tag Element tag.
    # @param attrib Element attributes, given as a dictionary.
    # @return A new element instance.

    def makeelement(self, tag, attrib):
        return self.__class__(tag, attrib)

    ##
    # (Experimental) Copies the current element.  This creates a
    # shallow copy; subelements will be shared with the original tree.
    #
    # @return A new element instance.

    def copy(self):
        elem = self.makeelement(self.tag, self.attrib)
        elem.text = self.text
        elem.tail = self.tail
        elem[:] = self
        return elem

    ##
    # Returns the number of subelements.  Note that this only counts
    # full elements; to check if there's any content in an element, you
    # have to check both the length and the <b>text</b> attribute.
    #
    # @return The number of subelements.

    def __len__(self):
        return len(self._children)

    def __nonzero__(self):
        warnings.warn(
            "The behavior of this method will change in future versions.  "
            "Use specific 'len(elem)' or 'elem is not None' test instead.",
            FutureWarning, stacklevel=2
            )
        return len(self._children) != 0 # emulate old behaviour, for now

    ##
    # Returns the given subelement, by index.
    #
    # @param index What subelement to return.
    # @return The given subelement.
    # @exception IndexError If the given element does not exist.

    def __getitem__(self, index):
        return self._children[index]

    ##
    # Replaces the given subelement, by index.
    #
    # @param index What subelement to replace.
    # @param element The new element value.
    # @exception IndexError If the given element does not exist.

    def __setitem__(self, index, element):
        # if isinstance(index, slice):
        #     for elt in element:
        #         assert iselement(elt)
        # else:
        #     assert iselement(element)
        self._children[index] = element

    ##
    # Deletes the given subelement, by index.
    #
    # @param index What subelement to delete.
    # @exception IndexError If the given element does not exist.

    def __delitem__(self, index):
        del self._children[index]

    ##
    # Adds a subelement to the end of this element.  In document order,
    # the new element will appear after the last existing subelement (or
    # directly after the text, if it's the first subelement), but before
    # the end tag for this element.
    #
    # @param element The element to add.

    def append(self, element):
        # assert iselement(element)
        self._children.append(element)

    ##
    # Appends subelements from a sequence.
    #
    # @param elements A sequence object with zero or more elements.
    # @since 1.3

    def extend(self, elements):
        # for element in elements:
        #     assert iselement(element)
        self._children.extend(elements)

    ##
    # Inserts a subelement at the given position in this element.
    #
    # @param index Where to insert the new subelement.

    def insert(self, index, element):
        # assert iselement(element)
        self._children.insert(index, element)

    ##
    # Removes a matching subelement.  Unlike the <b>find</b> methods,
    # this method compares elements based on identity, not on tag
    # value or contents.  To remove subelements by other means, the
    # easiest way is often to use a list comprehension to select what
    # elements to keep, and use slice assignment to update the parent
    # element.
    #
    # @param element What element to remove.
    # @exception ValueError If a matching element could not be found.

    def remove(self, element):
        # assert iselement(element)
        self._children.remove(element)

    ##
    # (Deprecated) Returns all subelements.  The elements are returned
    # in document order.
    #
    # @return A list of subelements.
    # @defreturn list of Element instances

    def getchildren(self):
        warnings.warn(
            "This method will be removed in future versions.  "
            "Use 'list(elem)' or iteration over elem instead.",
            DeprecationWarning, stacklevel=2
            )
        return self._children

    ##
    # Finds the first matching subelement, by tag name or path.
    #
    # @param path What element to look for.
    # @keyparam namespaces Optional namespace prefix map.
    # @return The first matching element, or None if no element was found.
    # @defreturn Element or None

    def find(self, path, namespaces=None):
        return ElementPath.find(self, path, namespaces)

    ##
    # Finds text for the first matching subelement, by tag name or path.
    #
    # @param path What element to look for.
    # @param default What to return if the element was not found.
    # @keyparam namespaces Optional namespace prefix map.
    # @return The text content of the first matching element, or the
    #     default value no element was found.  Note that if the element
    #     is found, but has no text content, this method returns an
    #     empty string.
    # @defreturn string

    def findtext(self, path, default=None, namespaces=None):
        return ElementPath.findtext(self, path, default, namespaces)

    ##
    # Finds all matching subelements, by tag name or path.
    #
    # @param path What element to look for.
    # @keyparam namespaces Optional namespace prefix map.
    # @return A list or other sequence containing all matching elements,
    #    in document order.
    # @defreturn list of Element instances

    def findall(self, path, namespaces=None):
        return ElementPath.findall(self, path, namespaces)

    ##
    # Finds all matching subelements, by tag name or path.
    #
    # @param path What element to look for.
    # @keyparam namespaces Optional namespace prefix map.
    # @return An iterator or sequence containing all matching elements,
    #    in document order.
    # @defreturn a generated sequence of Element instances

    def iterfind(self, path, namespaces=None):
        return ElementPath.iterfind(self, path, namespaces)

    ##
    # Resets an element.  This function removes all subelements, clears
    # all attributes, and sets the <b>text</b> and <b>tail</b> attributes
    # to None.

    def clear(self):
        self.attrib.clear()
        self._children = []
        self.text = self.tail = None

    ##
    # Gets an element attribute.  Equivalent to <b>attrib.get</b>, but
    # some implementations may handle this a bit more efficiently.
    #
    # @param key What attribute to look for.
    # @param default What to return if the attribute was not found.
    # @return The attribute value, or the default value, if the
    #     attribute was not found.
    # @defreturn string or None

    def get(self, key, default=None):
        return self.attrib.get(key, default)

    ##
    # Sets an element attribute.  Equivalent to <b>attrib[key] = value</b>,
    # but some implementations may handle this a bit more efficiently.
    #
    # @param key What attribute to set.
    # @param value The attribute value.

    def set(self, key, value):
        self.attrib[key] = value

    ##
    # Gets a list of attribute names.  The names are returned in an
    # arbitrary order (just like for an ordinary Python dictionary).
    # Equivalent to <b>attrib.keys()</b>.
    #
    # @return A list of element attribute names.
    # @defreturn list of strings

    def keys(self):
        return self.attrib.keys()

    ##
    # Gets element attributes, as a sequence.  The attributes are
    # returned in an arbitrary order.  Equivalent to <b>attrib.items()</b>.
    #
    # @return A list of (name, value) tuples for all attributes.
    # @defreturn list of (string, string) tuples

    def items(self):
        return self.attrib.items()

    ##
    # Creates a tree iterator.  The iterator loops over this element
    # and all subelements, in document order, and returns all elements
    # with a matching tag.
    # <p>
    # If the tree structure is modified during iteration, new or removed
    # elements may or may not be included.  To get a stable set, use the
    # list() function on the iterator, and loop over the resulting list.
    #
    # @param tag What tags to look for (default is to return all elements).
    # @return An iterator containing all the matching elements.
    # @defreturn iterator

    def iter(self, tag=None):
        if tag == "*":
            tag = None
        if tag is None or self.tag == tag:
            yield self
        for e in self._children:
            for e in e.iter(tag):
                yield e

    # compatibility
    def getiterator(self, tag=None):
        # Change for a DeprecationWarning in 1.4
        warnings.warn(
            "This method will be removed in future versions.  "
            "Use 'elem.iter()' or 'list(elem.iter())' instead.",
            PendingDeprecationWarning, stacklevel=2
        )
        return list(self.iter(tag))

    ##
    # Creates a text iterator.  The iterator loops over this element
    # and all subelements, in document order, and returns all inner
    # text.
    #
    # @return An iterator containing all inner text.
    # @defreturn iterator

    def itertext(self):
        tag = self.tag
        if not isinstance(tag, basestring) and tag is not None:
            return
        if self.text:
            yield self.text
        for e in self:
            for s in e.itertext():
                yield s
            if e.tail:
                yield e.tail

# compatibility
_Element = _ElementInterface = Element

##
# Subelement factory.  This function creates an element instance, and
# appends it to an existing element.
# <p>
# The element name, attribute names, and attribute values can be
# either 8-bit ASCII strings or Unicode strings.
#
# @param parent The parent element.
# @param tag The subelement name.
# @param attrib An optional dictionary, containing element attributes.
# @param **extra Additional attributes, given as keyword arguments.
# @return An element instance.
# @defreturn Element

def SubElement(parent, tag, attrib={}, **extra):
    attrib = attrib.copy()
    attrib.update(extra)
    element = parent.makeelement(tag, attrib)
    parent.append(element)
    return element

##
# Comment element factory.  This factory function creates a special
# element that will be serialized as an XML comment by the standard
# serializer.
# <p>
# The comment string can be either an 8-bit ASCII string or a Unicode
# string.
#
# @param text A string containing the comment string.
# @return An element instance, representing a comment.
# @defreturn Element

def Comment(text=None):
    element = Element(Comment)
    element.text = text
    return element

##
# PI element factory.  This factory function creates a special element
# that will be serialized as an XML processing instruction by the standard
# serializer.
#
# @param target A string containing the PI target.
# @param text A string containing the PI contents, if any.
# @return An element instance, representing a PI.
# @defreturn Element

def ProcessingInstruction(target, text=None):
    element = Element(ProcessingInstruction)
    element.text = target
    if text:
        element.text = element.text + " " + text
    return element

PI = ProcessingInstruction

##
# QName wrapper.  This can be used to wrap a QName attribute value, in
# order to get proper namespace handling on output.
#
# @param text A string containing the QName value, in the form {uri}local,
#     or, if the tag argument is given, the URI part of a QName.
# @param tag Optional tag.  If given, the first argument is interpreted as
#     an URI, and this argument is interpreted as a local name.
# @return An opaque object, representing the QName.

class QName(object):
    def __init__(self, text_or_uri, tag=None):
        if tag:
            text_or_uri = "{%s}%s" % (text_or_uri, tag)
        self.text = text_or_uri
    def __str__(self):
        return self.text
    def __hash__(self):
        return hash(self.text)
    def __cmp__(self, other):
        if isinstance(other, QName):
            return cmp(self.text, other.text)
        return cmp(self.text, other)

# --------------------------------------------------------------------

##
# ElementTree wrapper class.  This class represents an entire element
# hierarchy, and adds some extra support for serialization to and from
# standard XML.
#
# @param element Optional root element.
# @keyparam file Optional file handle or file name.  If given, the
#     tree is initialized with the contents of this XML file.

class ElementTree(object):

    def __init__(self, element=None, file=None):
        # assert element is None or iselement(element)
        self._root = element # first node
        if file:
            self.parse(file)

    ##
    # Gets the root element for this tree.
    #
    # @return An element instance.
    # @defreturn Element

    def getroot(self):
        return self._root

    ##
    # Replaces the root element for this tree.  This discards the
    # current contents of the tree, and replaces it with the given
    # element.  Use with care.
    #
    # @param element An element instance.

    def _setroot(self, element):
        # assert iselement(element)
        self._root = element

    ##
    # Loads an external XML document into this element tree.
    #
    # @param source A file name or file object.  If a file object is
    #     given, it only has to implement a <b>read(n)</b> method.
    # @keyparam parser An optional parser instance.  If not given, the
    #     standard {@link XMLParser} parser is used.
    # @return The document root element.
    # @defreturn Element
    # @exception ParseError If the parser fails to parse the document.

    def parse(self, source, parser=None):
        close_source = False
        if not hasattr(source, "read"):
            source = open(source, "rb")
            close_source = True
        try:
            if not parser:
                parser = XMLParser(target=TreeBuilder())
            while 1:
                data = source.read(65536)
                if not data:
                    break
                parser.feed(data)
            self._root = parser.close()
            return self._root
        finally:
            if close_source:
                source.close()

    ##
    # Creates a tree iterator for the root element.  The iterator loops
    # over all elements in this tree, in document order.
    #
    # @param tag What tags to look for (default is to return all elements)
    # @return An iterator.
    # @defreturn iterator

    def iter(self, tag=None):
        # assert self._root is not None
        return self._root.iter(tag)

    # compatibility
    def getiterator(self, tag=None):
        # Change for a DeprecationWarning in 1.4
        warnings.warn(
            "This method will be removed in future versions.  "
            "Use 'tree.iter()' or 'list(tree.iter())' instead.",
            PendingDeprecationWarning, stacklevel=2
        )
        return list(self.iter(tag))

    ##
    # Same as getroot().find(path), starting at the root of the
    # tree.
    #
    # @param path What element to look for.
    # @keyparam namespaces Optional namespace prefix map.
    # @return The first matching element, or None if no element was found.
    # @defreturn Element or None

    def find(self, path, namespaces=None):
        # assert self._root is not None
        if path[:1] == "/":
            path = "." + path
            warnings.warn(
                "This search is broken in 1.3 and earlier, and will be "
                "fixed in a future version.  If you rely on the current "
                "behaviour, change it to %r" % path,
                FutureWarning, stacklevel=2
                )
        return self._root.find(path, namespaces)

    ##
    # Same as getroot().findtext(path), starting at the root of the tree.
    #
    # @param path What element to look for.
    # @param default What to return if the element was not found.
    # @keyparam namespaces Optional namespace prefix map.
    # @return The text content of the first matching element, or the
    #     default value no element was found.  Note that if the element
    #     is found, but has no text content, this method returns an
    #     empty string.
    # @defreturn string

    def findtext(self, path, default=None, namespaces=None):
        # assert self._root is not None
        if path[:1] == "/":
            path = "." + path
            warnings.warn(
                "This search is broken in 1.3 and earlier, and will be "
                "fixed in a future version.  If you rely on the current "
                "behaviour, change it to %r" % path,
                FutureWarning, stacklevel=2
                )
        return self._root.findtext(path, default, namespaces)

    ##
    # Same as getroot().findall(path), starting at the root of the tree.
    #
    # @param path What element to look for.
    # @keyparam namespaces Optional namespace prefix map.
    # @return A list or iterator containing all matching elements,
    #    in document order.
    # @defreturn list of Element instances

    def findall(self, path, namespaces=None):
        # assert self._root is not None
        if path[:1] == "/":
            path = "." + path
            warnings.warn(
                "This search is broken in 1.3 and earlier, and will be "
                "fixed in a future version.  If you rely on the current "
                "behaviour, change it to %r" % path,
                FutureWarning, stacklevel=2
                )
        return self._root.findall(path, namespaces)

    ##
    # Finds all matching subelements, by tag name or path.
    # Same as getroot().iterfind(path).
    #
    # @param path What element to look for.
    # @keyparam namespaces Optional namespace prefix map.
    # @return An iterator or sequence containing all matching elements,
    #    in document order.
    # @defreturn a generated sequence of Element instances

    def iterfind(self, path, namespaces=None):
        # assert self._root is not None
        if path[:1] == "/":
            path = "." + path
            warnings.warn(
                "This search is broken in 1.3 and earlier, and will be "
                "fixed in a future version.  If you rely on the current "
                "behaviour, change it to %r" % path,
                FutureWarning, stacklevel=2
                )
        return self._root.iterfind(path, namespaces)

    ##
    # Writes the element tree to a file, as XML.
    #
    # @def write(file, **options)
    # @param file A file name, or a file object opened for writing.
    # @param **options Options, given as keyword arguments.
    # @keyparam encoding Optional output encoding (default is US-ASCII).
    # @keyparam xml_declaration Controls if an XML declaration should
    #     be added to the file.  Use False for never, True for always,
    #     None for only if not US-ASCII or UTF-8.  None is default.
    # @keyparam default_namespace Sets the default XML namespace (for "xmlns").
    # @keyparam method Optional output method ("xml", "html", "text" or
    #     "c14n"; default is "xml").

    def write(self, file_or_filename,
              # keyword arguments
              encoding=None,
              xml_declaration=None,
              default_namespace=None,
              method=None):
        # assert self._root is not None
        if not method:
            method = "xml"
        elif method not in _serialize:
            # FIXME: raise an ImportError for c14n if ElementC14N is missing?
            raise ValueError("unknown method %r" % method)
        if hasattr(file_or_filename, "write"):
            file = file_or_filename
        else:
            file = open(file_or_filename, "wb")
        write = file.write
        if not encoding:
            if method == "c14n":
                encoding = "utf-8"
            else:
                encoding = "us-ascii"
        elif xml_declaration or (xml_declaration is None and
                                 encoding not in ("utf-8", "us-ascii")):
            if method == "xml":
                write("<?xml version='1.0' encoding='%s'?>\n" % encoding)
        if method == "text":
            _serialize_text(write, self._root, encoding)
        else:
            qnames, namespaces = _namespaces(
                self._root, encoding, default_namespace
                )
            serialize = _serialize[method]
            serialize(write, self._root, encoding, qnames, namespaces)
        if file_or_filename is not file:
            file.close()

    def write_c14n(self, file):
        # lxml.etree compatibility.  use output method instead
        return self.write(file, method="c14n")

# --------------------------------------------------------------------
# serialization support

def _namespaces(elem, encoding, default_namespace=None):
    # identify namespaces used in this tree

    # maps qnames to *encoded* prefix:local names
    qnames = {None: None}

    # maps uri:s to prefixes
    namespaces = {}
    if default_namespace:
        namespaces[default_namespace] = ""

    def encode(text):
        return text.encode(encoding)

    def add_qname(qname):
        # calculate serialized qname representation
        try:
            if qname[:1] == "{":
                uri, tag = qname[1:].rsplit("}", 1)
                prefix = namespaces.get(uri)
                if prefix is None:
                    prefix = _namespace_map.get(uri)
                    if prefix is None:
                        prefix = "ns%d" % len(namespaces)
                    if prefix != "xml":
                        namespaces[uri] = prefix
                if prefix:
                    qnames[qname] = encode("%s:%s" % (prefix, tag))
                else:
                    qnames[qname] = encode(tag) # default element
            else:
                if default_namespace:
                    # FIXME: can this be handled in XML 1.0?
                    raise ValueError(
                        "cannot use non-qualified names with "
                        "default_namespace option"
                        )
                qnames[qname] = encode(qname)
        except TypeError:
            _raise_serialization_error(qname)

    # populate qname and namespaces table
    try:
        iterate = elem.iter
    except AttributeError:
        iterate = elem.getiterator # cET compatibility
    for elem in iterate():
        tag = elem.tag
        if isinstance(tag, QName):
            if tag.text not in qnames:
                add_qname(tag.text)
        elif isinstance(tag, basestring):
            if tag not in qnames:
                add_qname(tag)
        elif tag is not None and tag is not Comment and tag is not PI:
            _raise_serialization_error(tag)
        for key, value in elem.items():
            if isinstance(key, QName):
                key = key.text
            if key not in qnames:
                add_qname(key)
            if isinstance(value, QName) and value.text not in qnames:
                add_qname(value.text)
        text = elem.text
        if isinstance(text, QName) and text.text not in qnames:
            add_qname(text.text)
    return qnames, namespaces

def _serialize_xml(write, elem, encoding, qnames, namespaces):
    tag = elem.tag
    text = elem.text
    if tag is Comment:
        write("<!--%s-->" % _encode(text, encoding))
    elif tag is ProcessingInstruction:
        write("<?%s?>" % _encode(text, encoding))
    else:
        tag = qnames[tag]
        if tag is None:
            if text:
                write(_escape_cdata(text, encoding))
            for e in elem:
                _serialize_xml(write, e, encoding, qnames, None)
        else:
            write("<" + tag)
            items = elem.items()
            if items or namespaces:
                if namespaces:
                    for v, k in sorted(namespaces.items(),
                                       key=lambda x: x[1]):  # sort on prefix
                        if k:
                            k = ":" + k
                        write(" xmlns%s=\"%s\"" % (
                            k.encode(encoding),
                            _escape_attrib(v, encoding)
                            ))
                for k, v in sorted(items):  # lexical order
                    if isinstance(k, QName):
                        k = k.text
                    if isinstance(v, QName):
                        v = qnames[v.text]
                    else:
                        v = _escape_attrib(v, encoding)
                    write(" %s=\"%s\"" % (qnames[k], v))
            if text or len(elem):
                write(">")
                if text:
                    write(_escape_cdata(text, encoding))
                for e in elem:
                    _serialize_xml(write, e, encoding, qnames, None)
                write("</" + tag + ">")
            else:
                write(" />")
    if elem.tail:
        write(_escape_cdata(elem.tail, encoding))

HTML_EMPTY = ("area", "base", "basefont", "br", "col", "frame", "hr",
              "img", "input", "isindex", "link", "meta", "param")

try:
    HTML_EMPTY = set(HTML_EMPTY)
except NameError:
    pass

def _serialize_html(write, elem, encoding, qnames, namespaces):
    tag = elem.tag
    text = elem.text
    if tag is Comment:
        write("<!--%s-->" % _escape_cdata(text, encoding))
    elif tag is ProcessingInstruction:
        write("<?%s?>" % _escape_cdata(text, encoding))
    else:
        tag = qnames[tag]
        if tag is None:
            if text:
                write(_escape_cdata(text, encoding))
            for e in elem:
                _serialize_html(write, e, encoding, qnames, None)
        else:
            write("<" + tag)
            items = elem.items()
            if items or namespaces:
                if namespaces:
                    for v, k in sorted(namespaces.items(),
                                       key=lambda x: x[1]):  # sort on prefix
                        if k:
                            k = ":" + k
                        write(" xmlns%s=\"%s\"" % (
                            k.encode(encoding),
                            _escape_attrib(v, encoding)
                            ))
                for k, v in sorted(items):  # lexical order
                    if isinstance(k, QName):
                        k = k.text
                    if isinstance(v, QName):
                        v = qnames[v.text]
                    else:
                        v = _escape_attrib_html(v, encoding)
                    # FIXME: handle boolean attributes
                    write(" %s=\"%s\"" % (qnames[k], v))
            write(">")
            ltag = tag.lower()
            if text:
                if ltag == "script" or ltag == "style":
                    write(_encode(text, encoding))
                else:
                    write(_escape_cdata(text, encoding))
            for e in elem:
                _serialize_html(write, e, encoding, qnames, None)
            if ltag not in HTML_EMPTY:
                write("</" + tag + ">")
    if elem.tail:
        write(_escape_cdata(elem.tail, encoding))

def _serialize_text(write, elem, encoding):
    for part in elem.itertext():
        write(part.encode(encoding))
    if elem.tail:
        write(elem.tail.encode(encoding))

_serialize = {
    "xml": _serialize_xml,
    "html": _serialize_html,
    "text": _serialize_text,
# this optional method is imported at the end of the module
#   "c14n": _serialize_c14n,
}

##
# Registers a namespace prefix.  The registry is global, and any
# existing mapping for either the given prefix or the namespace URI
# will be removed.
#
# @param prefix Namespace prefix.
# @param uri Namespace uri.  Tags and attributes in this namespace
#     will be serialized with the given prefix, if at all possible.
# @exception ValueError If the prefix is reserved, or is otherwise
#     invalid.

def register_namespace(prefix, uri):
    if re.match("ns\d+$", prefix):
        raise ValueError("Prefix format reserved for internal use")
    for k, v in _namespace_map.items():
        if k == uri or v == prefix:
            del _namespace_map[k]
    _namespace_map[uri] = prefix

_namespace_map = {
    # "well-known" namespace prefixes
    "http://www.w3.org/XML/1998/namespace": "xml",
    "http://www.w3.org/1999/xhtml": "html",
    "http://www.w3.org/1999/02/22-rdf-syntax-ns#": "rdf",
    "http://schemas.xmlsoap.org/wsdl/": "wsdl",
    # xml schema
    "http://www.w3.org/2001/XMLSchema": "xs",
    "http://www.w3.org/2001/XMLSchema-instance": "xsi",
    # dublin core
    "http://purl.org/dc/elements/1.1/": "dc",
}

def _raise_serialization_error(text):
    raise TypeError(
        "cannot serialize %r (type %s)" % (text, type(text).__name__)
        )

def _encode(text, encoding):
    try:
        return text.encode(encoding, "xmlcharrefreplace")
    except (TypeError, AttributeError):
        _raise_serialization_error(text)

def _escape_cdata(text, encoding):
    # escape character data
    try:
        # it's worth avoiding do-nothing calls for strings that are
        # shorter than 500 character, or so.  assume that's, by far,
        # the most common case in most applications.
        if "&" in text:
            text = text.replace("&", "&amp;")
        if "<" in text:
            text = text.replace("<", "&lt;")
        if ">" in text:
            text = text.replace(">", "&gt;")
        return text.encode(encoding, "xmlcharrefreplace")
    except (TypeError, AttributeError):
        _raise_serialization_error(text)

def _escape_attrib(text, encoding):
    # escape attribute value
    try:
        if "&" in text:
            text = text.replace("&", "&amp;")
        if "<" in text:
            text = text.replace("<", "&lt;")
        if ">" in text:
            text = text.replace(">", "&gt;")
        if "\"" in text:
            text = text.replace("\"", "&quot;")
        if "\n" in text:
            text = text.replace("\n", "&#10;")
        return text.encode(encoding, "xmlcharrefreplace")
    except (TypeError, AttributeError):
        _raise_serialization_error(text)

def _escape_attrib_html(text, encoding):
    # escape attribute value
    try:
        if "&" in text:
            text = text.replace("&", "&amp;")
        if ">" in text:
            text = text.replace(">", "&gt;")
        if "\"" in text:
            text = text.replace("\"", "&quot;")
        return text.encode(encoding, "xmlcharrefreplace")
    except (TypeError, AttributeError):
        _raise_serialization_error(text)

# --------------------------------------------------------------------

##
# Generates a string representation of an XML element, including all
# subelements.
#
# @param element An Element instance.
# @keyparam encoding Optional output encoding (default is US-ASCII).
# @keyparam method Optional output method ("xml", "html", "text" or
#     "c14n"; default is "xml").
# @return An encoded string containing the XML data.
# @defreturn string

def tostring(element, encoding=None, method=None):
    class dummy:
        pass
    data = []
    file = dummy()
    file.write = data.append
    ElementTree(element).write(file, encoding, method=method)
    return "".join(data)

##
# Generates a string representation of an XML element, including all
# subelements.  The string is returned as a sequence of string fragments.
#
# @param element An Element instance.
# @keyparam encoding Optional output encoding (default is US-ASCII).
# @keyparam method Optional output method ("xml", "html", "text" or
#     "c14n"; default is "xml").
# @return A sequence object containing the XML data.
# @defreturn sequence
# @since 1.3

def tostringlist(element, encoding=None, method=None):
    class dummy:
        pass
    data = []
    file = dummy()
    file.write = data.append
    ElementTree(element).write(file, encoding, method=method)
    # FIXME: merge small fragments into larger parts
    return data

##
# Writes an element tree or element structure to sys.stdout.  This
# function should be used for debugging only.
# <p>
# The exact output format is implementation dependent.  In this
# version, it's written as an ordinary XML file.
#
# @param elem An element tree or an individual element.

def dump(elem):
    # debugging
    if not isinstance(elem, ElementTree):
        elem = ElementTree(elem)
    elem.write(sys.stdout)
    tail = elem.getroot().tail
    if not tail or tail[-1] != "\n":
        sys.stdout.write("\n")

# --------------------------------------------------------------------
# parsing

##
# Parses an XML document into an element tree.
#
# @param source A filename or file object containing XML data.
# @param parser An optional parser instance.  If not given, the
#     standard {@link XMLParser} parser is used.
# @return An ElementTree instance

def parse(source, parser=None):
    tree = ElementTree()
    tree.parse(source, parser)
    return tree

##
# Parses an XML document into an element tree incrementally, and reports
# what's going on to the user.
#
# @param source A filename or file object containing XML data.
# @param events A list of events to report back.  If omitted, only "end"
#     events are reported.
# @param parser An optional parser instance.  If not given, the
#     standard {@link XMLParser} parser is used.
# @return A (event, elem) iterator.

def iterparse(source, events=None, parser=None):
    close_source = False
    if not hasattr(source, "read"):
        source = open(source, "rb")
        close_source = True
    if not parser:
        parser = XMLParser(target=TreeBuilder())
    return _IterParseIterator(source, events, parser, close_source)

class _IterParseIterator(object):

    def __init__(self, source, events, parser, close_source=False):
        self._file = source
        self._close_file = close_source
        self._events = []
        self._index = 0
        self._error = None
        self.root = self._root = None
        self._parser = parser
        # wire up the parser for event reporting
        parser = self._parser._parser
        append = self._events.append
        if events is None:
            events = ["end"]
        for event in events:
            if event == "start":
                try:
                    parser.ordered_attributes = 1
                    parser.specified_attributes = 1
                    def handler(tag, attrib_in, event=event, append=append,
                                start=self._parser._start_list):
                        append((event, start(tag, attrib_in)))
                    parser.StartElementHandler = handler
                except AttributeError:
                    def handler(tag, attrib_in, event=event, append=append,
                                start=self._parser._start):
                        append((event, start(tag, attrib_in)))
                    parser.StartElementHandler = handler
            elif event == "end":
                def handler(tag, event=event, append=append,
                            end=self._parser._end):
                    append((event, end(tag)))
                parser.EndElementHandler = handler
            elif event == "start-ns":
                def handler(prefix, uri, event=event, append=append):
                    try:
                        uri = (uri or "").encode("ascii")
                    except UnicodeError:
                        pass
                    append((event, (prefix or "", uri or "")))
                parser.StartNamespaceDeclHandler = handler
            elif event == "end-ns":
                def handler(prefix, event=event, append=append):
                    append((event, None))
                parser.EndNamespaceDeclHandler = handler
            else:
                raise ValueError("unknown event %r" % event)

    def next(self):
        while 1:
            try:
                item = self._events[self._index]
                self._index += 1
                return item
            except IndexError:
                pass
            if self._error:
                e = self._error
                self._error = None
                raise e
            if self._parser is None:
                self.root = self._root
                if self._close_file:
                    self._file.close()
                raise StopIteration
            # load event buffer
            del self._events[:]
            self._index = 0
            data = self._file.read(16384)
            if data:
                try:
                    self._parser.feed(data)
                except SyntaxError as exc:
                    self._error = exc
            else:
                self._root = self._parser.close()
                self._parser = None

    def __iter__(self):
        return self

##
# Parses an XML document from a string constant.  This function can
# be used to embed "XML literals" in Python code.
#
# @param source A string containing XML data.
# @param parser An optional parser instance.  If not given, the
#     standard {@link XMLParser} parser is used.
# @return An Element instance.
# @defreturn Element

def XML(text, parser=None):
    if not parser:
        parser = XMLParser(target=TreeBuilder())
    parser.feed(text)
    return parser.close()

##
# Parses an XML document from a string constant, and also returns
# a dictionary which maps from element id:s to elements.
#
# @param source A string containing XML data.
# @param parser An optional parser instance.  If not given, the
#     standard {@link XMLParser} parser is used.
# @return A tuple containing an Element instance and a dictionary.
# @defreturn (Element, dictionary)

def XMLID(text, parser=None):
    if not parser:
        parser = XMLParser(target=TreeBuilder())
    parser.feed(text)
    tree = parser.close()
    ids = {}
    for elem in tree.iter():
        id = elem.get("id")
        if id:
            ids[id] = elem
    return tree, ids

##
# Parses an XML document from a string constant.  Same as {@link #XML}.
#
# @def fromstring(text)
# @param source A string containing XML data.
# @return An Element instance.
# @defreturn Element

fromstring = XML

##
# Parses an XML document from a sequence of string fragments.
#
# @param sequence A list or other sequence containing XML data fragments.
# @param parser An optional parser instance.  If not given, the
#     standard {@link XMLParser} parser is used.
# @return An Element instance.
# @defreturn Element
# @since 1.3

def fromstringlist(sequence, parser=None):
    if not parser:
        parser = XMLParser(target=TreeBuilder())
    for text in sequence:
        parser.feed(text)
    return parser.close()

# --------------------------------------------------------------------

##
# Generic element structure builder.  This builder converts a sequence
# of {@link #TreeBuilder.start}, {@link #TreeBuilder.data}, and {@link
# #TreeBuilder.end} method calls to a well-formed element structure.
# <p>
# You can use this class to build an element structure using a custom XML
# parser, or a parser for some other XML-like format.
#
# @param element_factory Optional element factory.  This factory
#    is called to create new Element instances, as necessary.

class TreeBuilder(object):

    def __init__(self, element_factory=None):
        self._data = [] # data collector
        self._elem = [] # element stack
        self._last = None # last element
        self._tail = None # true if we're after an end tag
        if element_factory is None:
            element_factory = Element
        self._factory = element_factory

    ##
    # Flushes the builder buffers, and returns the toplevel document
    # element.
    #
    # @return An Element instance.
    # @defreturn Element

    def close(self):
        assert len(self._elem) == 0, "missing end tags"
        assert self._last is not None, "missing toplevel element"
        return self._last

    def _flush(self):
        if self._data:
            if self._last is not None:
                text = "".join(self._data)
                if self._tail:
                    assert self._last.tail is None, "internal error (tail)"
                    self._last.tail = text
                else:
                    assert self._last.text is None, "internal error (text)"
                    self._last.text = text
            self._data = []

    ##
    # Adds text to the current element.
    #
    # @param data A string.  This should be either an 8-bit string
    #    containing ASCII text, or a Unicode string.

    def data(self, data):
        self._data.append(data)

    ##
    # Opens a new element.
    #
    # @param tag The element name.
    # @param attrib A dictionary containing element attributes.
    # @return The opened element.
    # @defreturn Element

    def start(self, tag, attrs):
        self._flush()
        self._last = elem = self._factory(tag, attrs)
        if self._elem:
            self._elem[-1].append(elem)
        self._elem.append(elem)
        self._tail = 0
        return elem

    ##
    # Closes the current element.
    #
    # @param tag The element name.
    # @return The closed element.
    # @defreturn Element

    def end(self, tag):
        self._flush()
        self._last = self._elem.pop()
        assert self._last.tag == tag,\
               "end tag mismatch (expected %s, got %s)" % (
                   self._last.tag, tag)
        self._tail = 1
        return self._last

##
# Element structure builder for XML source data, based on the
# <b>expat</b> parser.
#
# @keyparam target Target object.  If omitted, the builder uses an
#     instance of the standard {@link #TreeBuilder} class.
# @keyparam html Predefine HTML entities.  This flag is not supported
#     by the current implementation.
# @keyparam encoding Optional encoding.  If given, the value overrides
#     the encoding specified in the XML file.
# @see #ElementTree
# @see #TreeBuilder

class XMLParser(object):

    def __init__(self, html=0, target=None, encoding=None):
        try:
            from xml.parsers import expat
        except ImportError:
            try:
                import pyexpat as expat
            except ImportError:
                raise ImportError(
                    "No module named expat; use SimpleXMLTreeBuilder instead"
                    )
        parser = expat.ParserCreate(encoding, "}")
        if target is None:
            target = TreeBuilder()
        # underscored names are provided for compatibility only
        self.parser = self._parser = parser
        self.target = self._target = target
        self._error = expat.error
        self._names = {} # name memo cache
        # callbacks
        parser.DefaultHandlerExpand = self._default
        parser.StartElementHandler = self._start
        parser.EndElementHandler = self._end
        parser.CharacterDataHandler = self._data
        # optional callbacks
        parser.CommentHandler = self._comment
        parser.ProcessingInstructionHandler = self._pi
        # let expat do the buffering, if supported
        try:
            self._parser.buffer_text = 1
        except AttributeError:
            pass
        # use new-style attribute handling, if supported
        try:
            self._parser.ordered_attributes = 1
            self._parser.specified_attributes = 1
            parser.StartElementHandler = self._start_list
        except AttributeError:
            pass
        self._doctype = None
        self.entity = {}
        try:
            self.version = "Expat %d.%d.%d" % expat.version_info
        except AttributeError:
            pass # unknown

    def _raiseerror(self, value):
        err = ParseError(value)
        err.code = value.code
        err.position = value.lineno, value.offset
        raise err

    def _fixtext(self, text):
        # convert text string to ascii, if possible
        try:
            return text.encode("ascii")
        except UnicodeError:
            return text

    def _fixname(self, key):
        # expand qname, and convert name string to ascii, if possible
        try:
            name = self._names[key]
        except KeyError:
            name = key
            if "}" in name:
                name = "{" + name
            self._names[key] = name = self._fixtext(name)
        return name

    def _start(self, tag, attrib_in):
        fixname = self._fixname
        fixtext = self._fixtext
        tag = fixname(tag)
        attrib = {}
        for key, value in attrib_in.items():
            attrib[fixname(key)] = fixtext(value)
        return self.target.start(tag, attrib)

    def _start_list(self, tag, attrib_in):
        fixname = self._fixname
        fixtext = self._fixtext
        tag = fixname(tag)
        attrib = {}
        if attrib_in:
            for i in range(0, len(attrib_in), 2):
                attrib[fixname(attrib_in[i])] = fixtext(attrib_in[i+1])
        return self.target.start(tag, attrib)

    def _data(self, text):
        return self.target.data(self._fixtext(text))

    def _end(self, tag):
        return self.target.end(self._fixname(tag))

    def _comment(self, data):
        try:
            comment = self.target.comment
        except AttributeError:
            pass
        else:
            return comment(self._fixtext(data))

    def _pi(self, target, data):
        try:
            pi = self.target.pi
        except AttributeError:
            pass
        else:
            return pi(self._fixtext(target), self._fixtext(data))

    def _default(self, text):
        prefix = text[:1]
        if prefix == "&":
            # deal with undefined entities
            try:
                self.target.data(self.entity[text[1:-1]])
            except KeyError:
                from xml.parsers import expat
                err = expat.error(
                    "undefined entity %s: line %d, column %d" %
                    (text, self._parser.ErrorLineNumber,
                    self._parser.ErrorColumnNumber)
                    )
                err.code = 11 # XML_ERROR_UNDEFINED_ENTITY
                err.lineno = self._parser.ErrorLineNumber
                err.offset = self._parser.ErrorColumnNumber
                raise err
        elif prefix == "<" and text[:9] == "<!DOCTYPE":
            self._doctype = [] # inside a doctype declaration
        elif self._doctype is not None:
            # parse doctype contents
            if prefix == ">":
                self._doctype = None
                return
            text = text.strip()
            if not text:
                return
            self._doctype.append(text)
            n = len(self._doctype)
            if n > 2:
                type = self._doctype[1]
                if type == "PUBLIC" and n == 4:
                    name, type, pubid, system = self._doctype
                elif type == "SYSTEM" and n == 3:
                    name, type, system = self._doctype
                    pubid = None
                else:
                    return
                if pubid:
                    pubid = pubid[1:-1]
                if hasattr(self.target, "doctype"):
                    self.target.doctype(name, pubid, system[1:-1])
                elif self.doctype is not self._XMLParser__doctype:
                    # warn about deprecated call
                    self._XMLParser__doctype(name, pubid, system[1:-1])
                    self.doctype(name, pubid, system[1:-1])
                self._doctype = None

    ##
    # (Deprecated) Handles a doctype declaration.
    #
    # @param name Doctype name.
    # @param pubid Public identifier.
    # @param system System identifier.

    def doctype(self, name, pubid, system):
        """This method of XMLParser is deprecated."""
        warnings.warn(
            "This method of XMLParser is deprecated.  Define doctype() "
            "method on the TreeBuilder target.",
            DeprecationWarning,
            )

    # sentinel, if doctype is redefined in a subclass
    __doctype = doctype

    ##
    # Feeds data to the parser.
    #
    # @param data Encoded data.

    def feed(self, data):
        try:
            self._parser.Parse(data, 0)
        except self._error, v:
            self._raiseerror(v)

    ##
    # Finishes feeding data to the parser.
    #
    # @return An element structure.
    # @defreturn Element

    def close(self):
        try:
            self._parser.Parse("", 1) # end of data
        except self._error, v:
            self._raiseerror(v)
        tree = self.target.close()
        del self.target, self._parser # get rid of circular references
        return tree

# compatibility
XMLTreeBuilder = XMLParser

# workaround circular import.
try:
    from ElementC14N import _serialize_c14n
    _serialize["c14n"] = _serialize_c14n
except ImportError:
    pass
