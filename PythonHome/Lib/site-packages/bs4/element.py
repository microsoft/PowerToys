import collections
import re
import sys
import warnings
from bs4.dammit import EntitySubstitution

DEFAULT_OUTPUT_ENCODING = "utf-8"
PY3K = (sys.version_info[0] > 2)

whitespace_re = re.compile("\s+")

def _alias(attr):
    """Alias one attribute name to another for backward compatibility"""
    @property
    def alias(self):
        return getattr(self, attr)

    @alias.setter
    def alias(self):
        return setattr(self, attr)
    return alias


class NamespacedAttribute(unicode):

    def __new__(cls, prefix, name, namespace=None):
        if name is None:
            obj = unicode.__new__(cls, prefix)
        elif prefix is None:
            # Not really namespaced.
            obj = unicode.__new__(cls, name)
        else:
            obj = unicode.__new__(cls, prefix + ":" + name)
        obj.prefix = prefix
        obj.name = name
        obj.namespace = namespace
        return obj

class AttributeValueWithCharsetSubstitution(unicode):
    """A stand-in object for a character encoding specified in HTML."""

class CharsetMetaAttributeValue(AttributeValueWithCharsetSubstitution):
    """A generic stand-in for the value of a meta tag's 'charset' attribute.

    When Beautiful Soup parses the markup '<meta charset="utf8">', the
    value of the 'charset' attribute will be one of these objects.
    """

    def __new__(cls, original_value):
        obj = unicode.__new__(cls, original_value)
        obj.original_value = original_value
        return obj

    def encode(self, encoding):
        return encoding


class ContentMetaAttributeValue(AttributeValueWithCharsetSubstitution):
    """A generic stand-in for the value of a meta tag's 'content' attribute.

    When Beautiful Soup parses the markup:
     <meta http-equiv="content-type" content="text/html; charset=utf8">

    The value of the 'content' attribute will be one of these objects.
    """

    CHARSET_RE = re.compile("((^|;)\s*charset=)([^;]*)", re.M)

    def __new__(cls, original_value):
        match = cls.CHARSET_RE.search(original_value)
        if match is None:
            # No substitution necessary.
            return unicode.__new__(unicode, original_value)

        obj = unicode.__new__(cls, original_value)
        obj.original_value = original_value
        return obj

    def encode(self, encoding):
        def rewrite(match):
            return match.group(1) + encoding
        return self.CHARSET_RE.sub(rewrite, self.original_value)

class HTMLAwareEntitySubstitution(EntitySubstitution):

    """Entity substitution rules that are aware of some HTML quirks.

    Specifically, the contents of <script> and <style> tags should not
    undergo entity substitution.

    Incoming NavigableString objects are checked to see if they're the
    direct children of a <script> or <style> tag.
    """

    cdata_containing_tags = set(["script", "style"])

    preformatted_tags = set(["pre"])

    @classmethod
    def _substitute_if_appropriate(cls, ns, f):
        if (isinstance(ns, NavigableString)
            and ns.parent is not None
            and ns.parent.name in cls.cdata_containing_tags):
            # Do nothing.
            return ns
        # Substitute.
        return f(ns)

    @classmethod
    def substitute_html(cls, ns):
        return cls._substitute_if_appropriate(
            ns, EntitySubstitution.substitute_html)

    @classmethod
    def substitute_xml(cls, ns):
        return cls._substitute_if_appropriate(
            ns, EntitySubstitution.substitute_xml)

class PageElement(object):
    """Contains the navigational information for some part of the page
    (either a tag or a piece of text)"""

    # There are five possible values for the "formatter" argument passed in
    # to methods like encode() and prettify():
    #
    # "html" - All Unicode characters with corresponding HTML entities
    #   are converted to those entities on output.
    # "minimal" - Bare ampersands and angle brackets are converted to
    #   XML entities: &amp; &lt; &gt;
    # None - The null formatter. Unicode characters are never
    #   converted to entities.  This is not recommended, but it's
    #   faster than "minimal".
    # A function - This function will be called on every string that
    #  needs to undergo entity substitution.
    #

    # In an HTML document, the default "html" and "minimal" functions
    # will leave the contents of <script> and <style> tags alone. For
    # an XML document, all tags will be given the same treatment.

    HTML_FORMATTERS = {
        "html" : HTMLAwareEntitySubstitution.substitute_html,
        "minimal" : HTMLAwareEntitySubstitution.substitute_xml,
        None : None
        }

    XML_FORMATTERS = {
        "html" : EntitySubstitution.substitute_html,
        "minimal" : EntitySubstitution.substitute_xml,
        None : None
        }

    def format_string(self, s, formatter='minimal'):
        """Format the given string using the given formatter."""
        if not callable(formatter):
            formatter = self._formatter_for_name(formatter)
        if formatter is None:
            output = s
        else:
            output = formatter(s)
        return output

    @property
    def _is_xml(self):
        """Is this element part of an XML tree or an HTML tree?

        This is used when mapping a formatter name ("minimal") to an
        appropriate function (one that performs entity-substitution on
        the contents of <script> and <style> tags, or not). It's
        inefficient, but it should be called very rarely.
        """
        if self.parent is None:
            # This is the top-level object. It should have .is_xml set
            # from tree creation. If not, take a guess--BS is usually
            # used on HTML markup.
            return getattr(self, 'is_xml', False)
        return self.parent._is_xml

    def _formatter_for_name(self, name):
        "Look up a formatter function based on its name and the tree."
        if self._is_xml:
            return self.XML_FORMATTERS.get(
                name, EntitySubstitution.substitute_xml)
        else:
            return self.HTML_FORMATTERS.get(
                name, HTMLAwareEntitySubstitution.substitute_xml)

    def setup(self, parent=None, previous_element=None):
        """Sets up the initial relations between this element and
        other elements."""
        self.parent = parent
        self.previous_element = previous_element
        if previous_element is not None:
            self.previous_element.next_element = self
        self.next_element = None
        self.previous_sibling = None
        self.next_sibling = None
        if self.parent is not None and self.parent.contents:
            self.previous_sibling = self.parent.contents[-1]
            self.previous_sibling.next_sibling = self

    nextSibling = _alias("next_sibling")  # BS3
    previousSibling = _alias("previous_sibling")  # BS3

    def replace_with(self, replace_with):
        if replace_with is self:
            return
        if replace_with is self.parent:
            raise ValueError("Cannot replace a Tag with its parent.")
        old_parent = self.parent
        my_index = self.parent.index(self)
        self.extract()
        old_parent.insert(my_index, replace_with)
        return self
    replaceWith = replace_with  # BS3

    def unwrap(self):
        my_parent = self.parent
        my_index = self.parent.index(self)
        self.extract()
        for child in reversed(self.contents[:]):
            my_parent.insert(my_index, child)
        return self
    replace_with_children = unwrap
    replaceWithChildren = unwrap  # BS3

    def wrap(self, wrap_inside):
        me = self.replace_with(wrap_inside)
        wrap_inside.append(me)
        return wrap_inside

    def extract(self):
        """Destructively rips this element out of the tree."""
        if self.parent is not None:
            del self.parent.contents[self.parent.index(self)]

        #Find the two elements that would be next to each other if
        #this element (and any children) hadn't been parsed. Connect
        #the two.
        last_child = self._last_descendant()
        next_element = last_child.next_element

        if self.previous_element is not None:
            self.previous_element.next_element = next_element
        if next_element is not None:
            next_element.previous_element = self.previous_element
        self.previous_element = None
        last_child.next_element = None

        self.parent = None
        if self.previous_sibling is not None:
            self.previous_sibling.next_sibling = self.next_sibling
        if self.next_sibling is not None:
            self.next_sibling.previous_sibling = self.previous_sibling
        self.previous_sibling = self.next_sibling = None
        return self

    def _last_descendant(self, is_initialized=True, accept_self=True):
        "Finds the last element beneath this object to be parsed."
        if is_initialized and self.next_sibling:
            last_child = self.next_sibling.previous_element
        else:
            last_child = self
            while isinstance(last_child, Tag) and last_child.contents:
                last_child = last_child.contents[-1]
        if not accept_self and last_child == self:
            last_child = None
        return last_child
    # BS3: Not part of the API!
    _lastRecursiveChild = _last_descendant

    def insert(self, position, new_child):
        if new_child is self:
            raise ValueError("Cannot insert a tag into itself.")
        if (isinstance(new_child, basestring)
            and not isinstance(new_child, NavigableString)):
            new_child = NavigableString(new_child)

        position = min(position, len(self.contents))
        if hasattr(new_child, 'parent') and new_child.parent is not None:
            # We're 'inserting' an element that's already one
            # of this object's children.
            if new_child.parent is self:
                current_index = self.index(new_child)
                if current_index < position:
                    # We're moving this element further down the list
                    # of this object's children. That means that when
                    # we extract this element, our target index will
                    # jump down one.
                    position -= 1
            new_child.extract()

        new_child.parent = self
        previous_child = None
        if position == 0:
            new_child.previous_sibling = None
            new_child.previous_element = self
        else:
            previous_child = self.contents[position - 1]
            new_child.previous_sibling = previous_child
            new_child.previous_sibling.next_sibling = new_child
            new_child.previous_element = previous_child._last_descendant(False)
        if new_child.previous_element is not None:
            new_child.previous_element.next_element = new_child

        new_childs_last_element = new_child._last_descendant(False)

        if position >= len(self.contents):
            new_child.next_sibling = None

            parent = self
            parents_next_sibling = None
            while parents_next_sibling is None and parent is not None:
                parents_next_sibling = parent.next_sibling
                parent = parent.parent
                if parents_next_sibling is not None:
                    # We found the element that comes next in the document.
                    break
            if parents_next_sibling is not None:
                new_childs_last_element.next_element = parents_next_sibling
            else:
                # The last element of this tag is the last element in
                # the document.
                new_childs_last_element.next_element = None
        else:
            next_child = self.contents[position]
            new_child.next_sibling = next_child
            if new_child.next_sibling is not None:
                new_child.next_sibling.previous_sibling = new_child
            new_childs_last_element.next_element = next_child

        if new_childs_last_element.next_element is not None:
            new_childs_last_element.next_element.previous_element = new_childs_last_element
        self.contents.insert(position, new_child)

    def append(self, tag):
        """Appends the given tag to the contents of this tag."""
        self.insert(len(self.contents), tag)

    def insert_before(self, predecessor):
        """Makes the given element the immediate predecessor of this one.

        The two elements will have the same parent, and the given element
        will be immediately before this one.
        """
        if self is predecessor:
            raise ValueError("Can't insert an element before itself.")
        parent = self.parent
        if parent is None:
            raise ValueError(
                "Element has no parent, so 'before' has no meaning.")
        # Extract first so that the index won't be screwed up if they
        # are siblings.
        if isinstance(predecessor, PageElement):
            predecessor.extract()
        index = parent.index(self)
        parent.insert(index, predecessor)

    def insert_after(self, successor):
        """Makes the given element the immediate successor of this one.

        The two elements will have the same parent, and the given element
        will be immediately after this one.
        """
        if self is successor:
            raise ValueError("Can't insert an element after itself.")
        parent = self.parent
        if parent is None:
            raise ValueError(
                "Element has no parent, so 'after' has no meaning.")
        # Extract first so that the index won't be screwed up if they
        # are siblings.
        if isinstance(successor, PageElement):
            successor.extract()
        index = parent.index(self)
        parent.insert(index+1, successor)

    def find_next(self, name=None, attrs={}, text=None, **kwargs):
        """Returns the first item that matches the given criteria and
        appears after this Tag in the document."""
        return self._find_one(self.find_all_next, name, attrs, text, **kwargs)
    findNext = find_next  # BS3

    def find_all_next(self, name=None, attrs={}, text=None, limit=None,
                    **kwargs):
        """Returns all items that match the given criteria and appear
        after this Tag in the document."""
        return self._find_all(name, attrs, text, limit, self.next_elements,
                             **kwargs)
    findAllNext = find_all_next  # BS3

    def find_next_sibling(self, name=None, attrs={}, text=None, **kwargs):
        """Returns the closest sibling to this Tag that matches the
        given criteria and appears after this Tag in the document."""
        return self._find_one(self.find_next_siblings, name, attrs, text,
                             **kwargs)
    findNextSibling = find_next_sibling  # BS3

    def find_next_siblings(self, name=None, attrs={}, text=None, limit=None,
                           **kwargs):
        """Returns the siblings of this Tag that match the given
        criteria and appear after this Tag in the document."""
        return self._find_all(name, attrs, text, limit,
                              self.next_siblings, **kwargs)
    findNextSiblings = find_next_siblings   # BS3
    fetchNextSiblings = find_next_siblings  # BS2

    def find_previous(self, name=None, attrs={}, text=None, **kwargs):
        """Returns the first item that matches the given criteria and
        appears before this Tag in the document."""
        return self._find_one(
            self.find_all_previous, name, attrs, text, **kwargs)
    findPrevious = find_previous  # BS3

    def find_all_previous(self, name=None, attrs={}, text=None, limit=None,
                        **kwargs):
        """Returns all items that match the given criteria and appear
        before this Tag in the document."""
        return self._find_all(name, attrs, text, limit, self.previous_elements,
                           **kwargs)
    findAllPrevious = find_all_previous  # BS3
    fetchPrevious = find_all_previous    # BS2

    def find_previous_sibling(self, name=None, attrs={}, text=None, **kwargs):
        """Returns the closest sibling to this Tag that matches the
        given criteria and appears before this Tag in the document."""
        return self._find_one(self.find_previous_siblings, name, attrs, text,
                             **kwargs)
    findPreviousSibling = find_previous_sibling  # BS3

    def find_previous_siblings(self, name=None, attrs={}, text=None,
                               limit=None, **kwargs):
        """Returns the siblings of this Tag that match the given
        criteria and appear before this Tag in the document."""
        return self._find_all(name, attrs, text, limit,
                              self.previous_siblings, **kwargs)
    findPreviousSiblings = find_previous_siblings   # BS3
    fetchPreviousSiblings = find_previous_siblings  # BS2

    def find_parent(self, name=None, attrs={}, **kwargs):
        """Returns the closest parent of this Tag that matches the given
        criteria."""
        # NOTE: We can't use _find_one because findParents takes a different
        # set of arguments.
        r = None
        l = self.find_parents(name, attrs, 1, **kwargs)
        if l:
            r = l[0]
        return r
    findParent = find_parent  # BS3

    def find_parents(self, name=None, attrs={}, limit=None, **kwargs):
        """Returns the parents of this Tag that match the given
        criteria."""

        return self._find_all(name, attrs, None, limit, self.parents,
                             **kwargs)
    findParents = find_parents   # BS3
    fetchParents = find_parents  # BS2

    @property
    def next(self):
        return self.next_element

    @property
    def previous(self):
        return self.previous_element

    #These methods do the real heavy lifting.

    def _find_one(self, method, name, attrs, text, **kwargs):
        r = None
        l = method(name, attrs, text, 1, **kwargs)
        if l:
            r = l[0]
        return r

    def _find_all(self, name, attrs, text, limit, generator, **kwargs):
        "Iterates over a generator looking for things that match."

        if isinstance(name, SoupStrainer):
            strainer = name
        else:
            strainer = SoupStrainer(name, attrs, text, **kwargs)

        if text is None and not limit and not attrs and not kwargs:
            if name is True or name is None:
                # Optimization to find all tags.
                result = (element for element in generator
                          if isinstance(element, Tag))
                return ResultSet(strainer, result)
            elif isinstance(name, basestring):
                # Optimization to find all tags with a given name.
                result = (element for element in generator
                          if isinstance(element, Tag)
                            and element.name == name)
                return ResultSet(strainer, result)
        results = ResultSet(strainer)
        while True:
            try:
                i = next(generator)
            except StopIteration:
                break
            if i:
                found = strainer.search(i)
                if found:
                    results.append(found)
                    if limit and len(results) >= limit:
                        break
        return results

    #These generators can be used to navigate starting from both
    #NavigableStrings and Tags.
    @property
    def next_elements(self):
        i = self.next_element
        while i is not None:
            yield i
            i = i.next_element

    @property
    def next_siblings(self):
        i = self.next_sibling
        while i is not None:
            yield i
            i = i.next_sibling

    @property
    def previous_elements(self):
        i = self.previous_element
        while i is not None:
            yield i
            i = i.previous_element

    @property
    def previous_siblings(self):
        i = self.previous_sibling
        while i is not None:
            yield i
            i = i.previous_sibling

    @property
    def parents(self):
        i = self.parent
        while i is not None:
            yield i
            i = i.parent

    # Methods for supporting CSS selectors.

    tag_name_re = re.compile('^[a-z0-9]+$')

    # /^(\w+)\[(\w+)([=~\|\^\$\*]?)=?"?([^\]"]*)"?\]$/
    #   \---/  \---/\-------------/    \-------/
    #     |      |         |               |
    #     |      |         |           The value
    #     |      |    ~,|,^,$,* or =
    #     |   Attribute
    #    Tag
    attribselect_re = re.compile(
        r'^(?P<tag>\w+)?\[(?P<attribute>\w+)(?P<operator>[=~\|\^\$\*]?)' +
        r'=?"?(?P<value>[^\]"]*)"?\]$'
        )

    def _attr_value_as_string(self, value, default=None):
        """Force an attribute value into a string representation.

        A multi-valued attribute will be converted into a
        space-separated stirng.
        """
        value = self.get(value, default)
        if isinstance(value, list) or isinstance(value, tuple):
            value =" ".join(value)
        return value

    def _tag_name_matches_and(self, function, tag_name):
        if not tag_name:
            return function
        else:
            def _match(tag):
                return tag.name == tag_name and function(tag)
            return _match

    def _attribute_checker(self, operator, attribute, value=''):
        """Create a function that performs a CSS selector operation.

        Takes an operator, attribute and optional value. Returns a
        function that will return True for elements that match that
        combination.
        """
        if operator == '=':
            # string representation of `attribute` is equal to `value`
            return lambda el: el._attr_value_as_string(attribute) == value
        elif operator == '~':
            # space-separated list representation of `attribute`
            # contains `value`
            def _includes_value(element):
                attribute_value = element.get(attribute, [])
                if not isinstance(attribute_value, list):
                    attribute_value = attribute_value.split()
                return value in attribute_value
            return _includes_value
        elif operator == '^':
            # string representation of `attribute` starts with `value`
            return lambda el: el._attr_value_as_string(
                attribute, '').startswith(value)
        elif operator == '$':
            # string represenation of `attribute` ends with `value`
            return lambda el: el._attr_value_as_string(
                attribute, '').endswith(value)
        elif operator == '*':
            # string representation of `attribute` contains `value`
            return lambda el: value in el._attr_value_as_string(attribute, '')
        elif operator == '|':
            # string representation of `attribute` is either exactly
            # `value` or starts with `value` and then a dash.
            def _is_or_starts_with_dash(element):
                attribute_value = element._attr_value_as_string(attribute, '')
                return (attribute_value == value or attribute_value.startswith(
                        value + '-'))
            return _is_or_starts_with_dash
        else:
            return lambda el: el.has_attr(attribute)

    # Old non-property versions of the generators, for backwards
    # compatibility with BS3.
    def nextGenerator(self):
        return self.next_elements

    def nextSiblingGenerator(self):
        return self.next_siblings

    def previousGenerator(self):
        return self.previous_elements

    def previousSiblingGenerator(self):
        return self.previous_siblings

    def parentGenerator(self):
        return self.parents


class NavigableString(unicode, PageElement):

    PREFIX = ''
    SUFFIX = ''

    def __new__(cls, value):
        """Create a new NavigableString.

        When unpickling a NavigableString, this method is called with
        the string in DEFAULT_OUTPUT_ENCODING. That encoding needs to be
        passed in to the superclass's __new__ or the superclass won't know
        how to handle non-ASCII characters.
        """
        if isinstance(value, unicode):
            return unicode.__new__(cls, value)
        return unicode.__new__(cls, value, DEFAULT_OUTPUT_ENCODING)

    def __copy__(self):
        return self

    def __getnewargs__(self):
        return (unicode(self),)

    def __getattr__(self, attr):
        """text.string gives you text. This is for backwards
        compatibility for Navigable*String, but for CData* it lets you
        get the string without the CData wrapper."""
        if attr == 'string':
            return self
        else:
            raise AttributeError(
                "'%s' object has no attribute '%s'" % (
                    self.__class__.__name__, attr))

    def output_ready(self, formatter="minimal"):
        output = self.format_string(self, formatter)
        return self.PREFIX + output + self.SUFFIX

    @property
    def name(self):
        return None

    @name.setter
    def name(self, name):
        raise AttributeError("A NavigableString cannot be given a name.")

class PreformattedString(NavigableString):
    """A NavigableString not subject to the normal formatting rules.

    The string will be passed into the formatter (to trigger side effects),
    but the return value will be ignored.
    """

    def output_ready(self, formatter="minimal"):
        """CData strings are passed into the formatter.
        But the return value is ignored."""
        self.format_string(self, formatter)
        return self.PREFIX + self + self.SUFFIX

class CData(PreformattedString):

    PREFIX = u'<![CDATA['
    SUFFIX = u']]>'

class ProcessingInstruction(PreformattedString):

    PREFIX = u'<?'
    SUFFIX = u'?>'

class Comment(PreformattedString):

    PREFIX = u'<!--'
    SUFFIX = u'-->'


class Declaration(PreformattedString):
    PREFIX = u'<!'
    SUFFIX = u'!>'


class Doctype(PreformattedString):

    @classmethod
    def for_name_and_ids(cls, name, pub_id, system_id):
        value = name or ''
        if pub_id is not None:
            value += ' PUBLIC "%s"' % pub_id
            if system_id is not None:
                value += ' "%s"' % system_id
        elif system_id is not None:
            value += ' SYSTEM "%s"' % system_id

        return Doctype(value)

    PREFIX = u'<!DOCTYPE '
    SUFFIX = u'>\n'


class Tag(PageElement):

    """Represents a found HTML tag with its attributes and contents."""

    def __init__(self, parser=None, builder=None, name=None, namespace=None,
                 prefix=None, attrs=None, parent=None, previous=None):
        "Basic constructor."

        if parser is None:
            self.parser_class = None
        else:
            # We don't actually store the parser object: that lets extracted
            # chunks be garbage-collected.
            self.parser_class = parser.__class__
        if name is None:
            raise ValueError("No value provided for new tag's name.")
        self.name = name
        self.namespace = namespace
        self.prefix = prefix
        if attrs is None:
            attrs = {}
        elif attrs and builder.cdata_list_attributes:
            attrs = builder._replace_cdata_list_attribute_values(
                self.name, attrs)
        else:
            attrs = dict(attrs)
        self.attrs = attrs
        self.contents = []
        self.setup(parent, previous)
        self.hidden = False

        # Set up any substitutions, such as the charset in a META tag.
        if builder is not None:
            builder.set_up_substitutions(self)
            self.can_be_empty_element = builder.can_be_empty_element(name)
        else:
            self.can_be_empty_element = False

    parserClass = _alias("parser_class")  # BS3

    @property
    def is_empty_element(self):
        """Is this tag an empty-element tag? (aka a self-closing tag)

        A tag that has contents is never an empty-element tag.

        A tag that has no contents may or may not be an empty-element
        tag. It depends on the builder used to create the tag. If the
        builder has a designated list of empty-element tags, then only
        a tag whose name shows up in that list is considered an
        empty-element tag.

        If the builder has no designated list of empty-element tags,
        then any tag with no contents is an empty-element tag.
        """
        return len(self.contents) == 0 and self.can_be_empty_element
    isSelfClosing = is_empty_element  # BS3

    @property
    def string(self):
        """Convenience property to get the single string within this tag.

        :Return: If this tag has a single string child, return value
         is that string. If this tag has no children, or more than one
         child, return value is None. If this tag has one child tag,
         return value is the 'string' attribute of the child tag,
         recursively.
        """
        if len(self.contents) != 1:
            return None
        child = self.contents[0]
        if isinstance(child, NavigableString):
            return child
        return child.string

    @string.setter
    def string(self, string):
        self.clear()
        self.append(string.__class__(string))

    def _all_strings(self, strip=False, types=(NavigableString, CData)):
        """Yield all strings of certain classes, possibly stripping them.

        By default, yields only NavigableString and CData objects. So
        no comments, processing instructions, etc.
        """
        for descendant in self.descendants:
            if (
                (types is None and not isinstance(descendant, NavigableString))
                or
                (types is not None and type(descendant) not in types)):
                continue
            if strip:
                descendant = descendant.strip()
                if len(descendant) == 0:
                    continue
            yield descendant

    strings = property(_all_strings)

    @property
    def stripped_strings(self):
        for string in self._all_strings(True):
            yield string

    def get_text(self, separator=u"", strip=False,
                 types=(NavigableString, CData)):
        """
        Get all child strings, concatenated using the given separator.
        """
        return separator.join([s for s in self._all_strings(
                    strip, types=types)])
    getText = get_text
    text = property(get_text)

    def decompose(self):
        """Recursively destroys the contents of this tree."""
        self.extract()
        i = self
        while i is not None:
            next = i.next_element
            i.__dict__.clear()
            i.contents = []
            i = next

    def clear(self, decompose=False):
        """
        Extract all children. If decompose is True, decompose instead.
        """
        if decompose:
            for element in self.contents[:]:
                if isinstance(element, Tag):
                    element.decompose()
                else:
                    element.extract()
        else:
            for element in self.contents[:]:
                element.extract()

    def index(self, element):
        """
        Find the index of a child by identity, not value. Avoids issues with
        tag.contents.index(element) getting the index of equal elements.
        """
        for i, child in enumerate(self.contents):
            if child is element:
                return i
        raise ValueError("Tag.index: element not in tag")

    def get(self, key, default=None):
        """Returns the value of the 'key' attribute for the tag, or
        the value given for 'default' if it doesn't have that
        attribute."""
        return self.attrs.get(key, default)

    def has_attr(self, key):
        return key in self.attrs

    def __hash__(self):
        return str(self).__hash__()

    def __getitem__(self, key):
        """tag[key] returns the value of the 'key' attribute for the tag,
        and throws an exception if it's not there."""
        return self.attrs[key]

    def __iter__(self):
        "Iterating over a tag iterates over its contents."
        return iter(self.contents)

    def __len__(self):
        "The length of a tag is the length of its list of contents."
        return len(self.contents)

    def __contains__(self, x):
        return x in self.contents

    def __nonzero__(self):
        "A tag is non-None even if it has no contents."
        return True

    def __setitem__(self, key, value):
        """Setting tag[key] sets the value of the 'key' attribute for the
        tag."""
        self.attrs[key] = value

    def __delitem__(self, key):
        "Deleting tag[key] deletes all 'key' attributes for the tag."
        self.attrs.pop(key, None)

    def __call__(self, *args, **kwargs):
        """Calling a tag like a function is the same as calling its
        find_all() method. Eg. tag('a') returns a list of all the A tags
        found within this tag."""
        return self.find_all(*args, **kwargs)

    def __getattr__(self, tag):
        #print "Getattr %s.%s" % (self.__class__, tag)
        if len(tag) > 3 and tag.endswith('Tag'):
            # BS3: soup.aTag -> "soup.find("a")
            tag_name = tag[:-3]
            warnings.warn(
                '.%sTag is deprecated, use .find("%s") instead.' % (
                    tag_name, tag_name))
            return self.find(tag_name)
        # We special case contents to avoid recursion.
        elif not tag.startswith("__") and not tag=="contents":
            return self.find(tag)
        raise AttributeError(
            "'%s' object has no attribute '%s'" % (self.__class__, tag))

    def __eq__(self, other):
        """Returns true iff this tag has the same name, the same attributes,
        and the same contents (recursively) as the given tag."""
        if self is other:
            return True
        if (not hasattr(other, 'name') or
            not hasattr(other, 'attrs') or
            not hasattr(other, 'contents') or
            self.name != other.name or
            self.attrs != other.attrs or
            len(self) != len(other)):
            return False
        for i, my_child in enumerate(self.contents):
            if my_child != other.contents[i]:
                return False
        return True

    def __ne__(self, other):
        """Returns true iff this tag is not identical to the other tag,
        as defined in __eq__."""
        return not self == other

    def __repr__(self, encoding=DEFAULT_OUTPUT_ENCODING):
        """Renders this tag as a string."""
        return self.encode(encoding)

    def __unicode__(self):
        return self.decode()

    def __str__(self):
        return self.encode()

    if PY3K:
        __str__ = __repr__ = __unicode__

    def encode(self, encoding=DEFAULT_OUTPUT_ENCODING,
               indent_level=None, formatter="minimal",
               errors="xmlcharrefreplace"):
        # Turn the data structure into Unicode, then encode the
        # Unicode.
        u = self.decode(indent_level, encoding, formatter)
        return u.encode(encoding, errors)

    def _should_pretty_print(self, indent_level):
        """Should this tag be pretty-printed?"""
        return (
            indent_level is not None and
            (self.name not in HTMLAwareEntitySubstitution.preformatted_tags
             or self._is_xml))

    def decode(self, indent_level=None,
               eventual_encoding=DEFAULT_OUTPUT_ENCODING,
               formatter="minimal"):
        """Returns a Unicode representation of this tag and its contents.

        :param eventual_encoding: The tag is destined to be
           encoded into this encoding. This method is _not_
           responsible for performing that encoding. This information
           is passed in so that it can be substituted in if the
           document contains a <META> tag that mentions the document's
           encoding.
        """

        # First off, turn a string formatter into a function. This
        # will stop the lookup from happening over and over again.
        if not callable(formatter):
            formatter = self._formatter_for_name(formatter)

        attrs = []
        if self.attrs:
            for key, val in sorted(self.attrs.items()):
                if val is None:
                    decoded = key
                else:
                    if isinstance(val, list) or isinstance(val, tuple):
                        val = ' '.join(val)
                    elif not isinstance(val, basestring):
                        val = unicode(val)
                    elif (
                        isinstance(val, AttributeValueWithCharsetSubstitution)
                        and eventual_encoding is not None):
                        val = val.encode(eventual_encoding)

                    text = self.format_string(val, formatter)
                    decoded = (
                        unicode(key) + '='
                        + EntitySubstitution.quoted_attribute_value(text))
                attrs.append(decoded)
        close = ''
        closeTag = ''

        prefix = ''
        if self.prefix:
            prefix = self.prefix + ":"

        if self.is_empty_element:
            close = '/'
        else:
            closeTag = '</%s%s>' % (prefix, self.name)

        pretty_print = self._should_pretty_print(indent_level)
        space = ''
        indent_space = ''
        if indent_level is not None:
            indent_space = (' ' * (indent_level - 1))
        if pretty_print:
            space = indent_space
            indent_contents = indent_level + 1
        else:
            indent_contents = None
        contents = self.decode_contents(
            indent_contents, eventual_encoding, formatter)

        if self.hidden:
            # This is the 'document root' object.
            s = contents
        else:
            s = []
            attribute_string = ''
            if attrs:
                attribute_string = ' ' + ' '.join(attrs)
            if indent_level is not None:
                # Even if this particular tag is not pretty-printed,
                # we should indent up to the start of the tag.
                s.append(indent_space)
            s.append('<%s%s%s%s>' % (
                    prefix, self.name, attribute_string, close))
            if pretty_print:
                s.append("\n")
            s.append(contents)
            if pretty_print and contents and contents[-1] != "\n":
                s.append("\n")
            if pretty_print and closeTag:
                s.append(space)
            s.append(closeTag)
            if indent_level is not None and closeTag and self.next_sibling:
                # Even if this particular tag is not pretty-printed,
                # we're now done with the tag, and we should add a
                # newline if appropriate.
                s.append("\n")
            s = ''.join(s)
        return s

    def prettify(self, encoding=None, formatter="minimal"):
        if encoding is None:
            return self.decode(True, formatter=formatter)
        else:
            return self.encode(encoding, True, formatter=formatter)

    def decode_contents(self, indent_level=None,
                       eventual_encoding=DEFAULT_OUTPUT_ENCODING,
                       formatter="minimal"):
        """Renders the contents of this tag as a Unicode string.

        :param eventual_encoding: The tag is destined to be
           encoded into this encoding. This method is _not_
           responsible for performing that encoding. This information
           is passed in so that it can be substituted in if the
           document contains a <META> tag that mentions the document's
           encoding.
        """
        # First off, turn a string formatter into a function. This
        # will stop the lookup from happening over and over again.
        if not callable(formatter):
            formatter = self._formatter_for_name(formatter)

        pretty_print = (indent_level is not None)
        s = []
        for c in self:
            text = None
            if isinstance(c, NavigableString):
                text = c.output_ready(formatter)
            elif isinstance(c, Tag):
                s.append(c.decode(indent_level, eventual_encoding,
                                  formatter))
            if text and indent_level and not self.name == 'pre':
                text = text.strip()
            if text:
                if pretty_print and not self.name == 'pre':
                    s.append(" " * (indent_level - 1))
                s.append(text)
                if pretty_print and not self.name == 'pre':
                    s.append("\n")
        return ''.join(s)

    def encode_contents(
        self, indent_level=None, encoding=DEFAULT_OUTPUT_ENCODING,
        formatter="minimal"):
        """Renders the contents of this tag as a bytestring."""
        contents = self.decode_contents(indent_level, encoding, formatter)
        return contents.encode(encoding)

    # Old method for BS3 compatibility
    def renderContents(self, encoding=DEFAULT_OUTPUT_ENCODING,
                       prettyPrint=False, indentLevel=0):
        if not prettyPrint:
            indentLevel = None
        return self.encode_contents(
            indent_level=indentLevel, encoding=encoding)

    #Soup methods

    def find(self, name=None, attrs={}, recursive=True, text=None,
             **kwargs):
        """Return only the first child of this Tag matching the given
        criteria."""
        r = None
        l = self.find_all(name, attrs, recursive, text, 1, **kwargs)
        if l:
            r = l[0]
        return r
    findChild = find

    def find_all(self, name=None, attrs={}, recursive=True, text=None,
                 limit=None, **kwargs):
        """Extracts a list of Tag objects that match the given
        criteria.  You can specify the name of the Tag and any
        attributes you want the Tag to have.

        The value of a key-value pair in the 'attrs' map can be a
        string, a list of strings, a regular expression object, or a
        callable that takes a string and returns whether or not the
        string matches for some custom definition of 'matches'. The
        same is true of the tag name."""

        generator = self.descendants
        if not recursive:
            generator = self.children
        return self._find_all(name, attrs, text, limit, generator, **kwargs)
    findAll = find_all       # BS3
    findChildren = find_all  # BS2

    #Generator methods
    @property
    def children(self):
        # return iter() to make the purpose of the method clear
        return iter(self.contents)  # XXX This seems to be untested.

    @property
    def descendants(self):
        if not len(self.contents):
            return
        stopNode = self._last_descendant().next_element
        current = self.contents[0]
        while current is not stopNode:
            yield current
            current = current.next_element

    # CSS selector code

    _selector_combinators = ['>', '+', '~']
    _select_debug = False
    def select(self, selector, _candidate_generator=None):
        """Perform a CSS selection operation on the current element."""
        tokens = selector.split()
        current_context = [self]

        if tokens[-1] in self._selector_combinators:
            raise ValueError(
                'Final combinator "%s" is missing an argument.' % tokens[-1])
        if self._select_debug:
            print 'Running CSS selector "%s"' % selector
        for index, token in enumerate(tokens):
            if self._select_debug:
                print ' Considering token "%s"' % token
            recursive_candidate_generator = None
            tag_name = None
            if tokens[index-1] in self._selector_combinators:
                # This token was consumed by the previous combinator. Skip it.
                if self._select_debug:
                    print '  Token was consumed by the previous combinator.'
                continue
            # Each operation corresponds to a checker function, a rule
            # for determining whether a candidate matches the
            # selector. Candidates are generated by the active
            # iterator.
            checker = None

            m = self.attribselect_re.match(token)
            if m is not None:
                # Attribute selector
                tag_name, attribute, operator, value = m.groups()
                checker = self._attribute_checker(operator, attribute, value)

            elif '#' in token:
                # ID selector
                tag_name, tag_id = token.split('#', 1)
                def id_matches(tag):
                    return tag.get('id', None) == tag_id
                checker = id_matches

            elif '.' in token:
                # Class selector
                tag_name, klass = token.split('.', 1)
                classes = set(klass.split('.'))
                def classes_match(candidate):
                    return classes.issubset(candidate.get('class', []))
                checker = classes_match

            elif ':' in token:
                # Pseudo-class
                tag_name, pseudo = token.split(':', 1)
                if tag_name == '':
                    raise ValueError(
                        "A pseudo-class must be prefixed with a tag name.")
                pseudo_attributes = re.match('([a-zA-Z\d-]+)\(([a-zA-Z\d]+)\)', pseudo)
                found = []
                if pseudo_attributes is not None:
                    pseudo_type, pseudo_value = pseudo_attributes.groups()
                    if pseudo_type == 'nth-of-type':
                        try:
                            pseudo_value = int(pseudo_value)
                        except:
                            raise NotImplementedError(
                                'Only numeric values are currently supported for the nth-of-type pseudo-class.')
                        if pseudo_value < 1:
                            raise ValueError(
                                'nth-of-type pseudo-class value must be at least 1.')
                        class Counter(object):
                            def __init__(self, destination):
                                self.count = 0
                                self.destination = destination

                            def nth_child_of_type(self, tag):
                                self.count += 1
                                if self.count == self.destination:
                                    return True
                                if self.count > self.destination:
                                    # Stop the generator that's sending us
                                    # these things.
                                    raise StopIteration()
                                return False
                        checker = Counter(pseudo_value).nth_child_of_type
                    else:
                        raise NotImplementedError(
                            'Only the following pseudo-classes are implemented: nth-of-type.')

            elif token == '*':
                # Star selector -- matches everything
                pass
            elif token == '>':
                # Run the next token as a CSS selector against the
                # direct children of each tag in the current context.
                recursive_candidate_generator = lambda tag: tag.children
            elif token == '~':
                # Run the next token as a CSS selector against the
                # siblings of each tag in the current context.
                recursive_candidate_generator = lambda tag: tag.next_siblings
            elif token == '+':
                # For each tag in the current context, run the next
                # token as a CSS selector against the tag's next
                # sibling that's a tag.
                def next_tag_sibling(tag):
                    yield tag.find_next_sibling(True)
                recursive_candidate_generator = next_tag_sibling

            elif self.tag_name_re.match(token):
                # Just a tag name.
                tag_name = token
            else:
                raise ValueError(
                    'Unsupported or invalid CSS selector: "%s"' % token)

            if recursive_candidate_generator:
                # This happens when the selector looks like  "> foo".
                #
                # The generator calls select() recursively on every
                # member of the current context, passing in a different
                # candidate generator and a different selector.
                #
                # In the case of "> foo", the candidate generator is
                # one that yields a tag's direct children (">"), and
                # the selector is "foo".
                next_token = tokens[index+1]
                def recursive_select(tag):
                    if self._select_debug:
                        print '    Calling select("%s") recursively on %s %s' % (next_token, tag.name, tag.attrs)
                        print '-' * 40
                    for i in tag.select(next_token, recursive_candidate_generator):
                        if self._select_debug:
                            print '(Recursive select picked up candidate %s %s)' % (i.name, i.attrs)
                        yield i
                    if self._select_debug:
                        print '-' * 40
                _use_candidate_generator = recursive_select
            elif _candidate_generator is None:
                # By default, a tag's candidates are all of its
                # children. If tag_name is defined, only yield tags
                # with that name.
                if self._select_debug:
                    if tag_name:
                        check = "[any]"
                    else:
                        check = tag_name
                    print '   Default candidate generator, tag name="%s"' % check
                if self._select_debug:
                    # This is redundant with later code, but it stops
                    # a bunch of bogus tags from cluttering up the
                    # debug log.
                    def default_candidate_generator(tag):
                        for child in tag.descendants:
                            if not isinstance(child, Tag):
                                continue
                            if tag_name and not child.name == tag_name:
                                continue
                            yield child
                    _use_candidate_generator = default_candidate_generator
                else:
                    _use_candidate_generator = lambda tag: tag.descendants
            else:
                _use_candidate_generator = _candidate_generator

            new_context = []
            new_context_ids = set([])
            for tag in current_context:
                if self._select_debug:
                    print "    Running candidate generator on %s %s" % (
                        tag.name, repr(tag.attrs))
                for candidate in _use_candidate_generator(tag):
                    if not isinstance(candidate, Tag):
                        continue
                    if tag_name and candidate.name != tag_name:
                        continue
                    if checker is not None:
                        try:
                            result = checker(candidate)
                        except StopIteration:
                            # The checker has decided we should no longer
                            # run the generator.
                            break
                    if checker is None or result:
                        if self._select_debug:
                            print "     SUCCESS %s %s" % (candidate.name, repr(candidate.attrs))
                        if id(candidate) not in new_context_ids:
                            # If a tag matches a selector more than once,
                            # don't include it in the context more than once.
                            new_context.append(candidate)
                            new_context_ids.add(id(candidate))
                    elif self._select_debug:
                        print "     FAILURE %s %s" % (candidate.name, repr(candidate.attrs))

            current_context = new_context

        if self._select_debug:
            print "Final verdict:"
            for i in current_context:
                print " %s %s" % (i.name, i.attrs)
        return current_context

    # Old names for backwards compatibility
    def childGenerator(self):
        return self.children

    def recursiveChildGenerator(self):
        return self.descendants

    def has_key(self, key):
        """This was kind of misleading because has_key() (attributes)
        was different from __in__ (contents). has_key() is gone in
        Python 3, anyway."""
        warnings.warn('has_key is deprecated. Use has_attr("%s") instead.' % (
                key))
        return self.has_attr(key)

# Next, a couple classes to represent queries and their results.
class SoupStrainer(object):
    """Encapsulates a number of ways of matching a markup element (tag or
    text)."""

    def __init__(self, name=None, attrs={}, text=None, **kwargs):
        self.name = self._normalize_search_value(name)
        if not isinstance(attrs, dict):
            # Treat a non-dict value for attrs as a search for the 'class'
            # attribute.
            kwargs['class'] = attrs
            attrs = None

        if 'class_' in kwargs:
            # Treat class_="foo" as a search for the 'class'
            # attribute, overriding any non-dict value for attrs.
            kwargs['class'] = kwargs['class_']
            del kwargs['class_']

        if kwargs:
            if attrs:
                attrs = attrs.copy()
                attrs.update(kwargs)
            else:
                attrs = kwargs
        normalized_attrs = {}
        for key, value in attrs.items():
            normalized_attrs[key] = self._normalize_search_value(value)

        self.attrs = normalized_attrs
        self.text = self._normalize_search_value(text)

    def _normalize_search_value(self, value):
        # Leave it alone if it's a Unicode string, a callable, a
        # regular expression, a boolean, or None.
        if (isinstance(value, unicode) or callable(value) or hasattr(value, 'match')
            or isinstance(value, bool) or value is None):
            return value

        # If it's a bytestring, convert it to Unicode, treating it as UTF-8.
        if isinstance(value, bytes):
            return value.decode("utf8")

        # If it's listlike, convert it into a list of strings.
        if hasattr(value, '__iter__'):
            new_value = []
            for v in value:
                if (hasattr(v, '__iter__') and not isinstance(v, bytes)
                    and not isinstance(v, unicode)):
                    # This is almost certainly the user's mistake. In the
                    # interests of avoiding infinite loops, we'll let
                    # it through as-is rather than doing a recursive call.
                    new_value.append(v)
                else:
                    new_value.append(self._normalize_search_value(v))
            return new_value

        # Otherwise, convert it into a Unicode string.
        # The unicode(str()) thing is so this will do the same thing on Python 2
        # and Python 3.
        return unicode(str(value))

    def __str__(self):
        if self.text:
            return self.text
        else:
            return "%s|%s" % (self.name, self.attrs)

    def search_tag(self, markup_name=None, markup_attrs={}):
        found = None
        markup = None
        if isinstance(markup_name, Tag):
            markup = markup_name
            markup_attrs = markup
        call_function_with_tag_data = (
            isinstance(self.name, collections.Callable)
            and not isinstance(markup_name, Tag))

        if ((not self.name)
            or call_function_with_tag_data
            or (markup and self._matches(markup, self.name))
            or (not markup and self._matches(markup_name, self.name))):
            if call_function_with_tag_data:
                match = self.name(markup_name, markup_attrs)
            else:
                match = True
                markup_attr_map = None
                for attr, match_against in list(self.attrs.items()):
                    if not markup_attr_map:
                        if hasattr(markup_attrs, 'get'):
                            markup_attr_map = markup_attrs
                        else:
                            markup_attr_map = {}
                            for k, v in markup_attrs:
                                markup_attr_map[k] = v
                    attr_value = markup_attr_map.get(attr)
                    if not self._matches(attr_value, match_against):
                        match = False
                        break
            if match:
                if markup:
                    found = markup
                else:
                    found = markup_name
        if found and self.text and not self._matches(found.string, self.text):
            found = None
        return found
    searchTag = search_tag

    def search(self, markup):
        # print 'looking for %s in %s' % (self, markup)
        found = None
        # If given a list of items, scan it for a text element that
        # matches.
        if hasattr(markup, '__iter__') and not isinstance(markup, (Tag, basestring)):
            for element in markup:
                if isinstance(element, NavigableString) \
                       and self.search(element):
                    found = element
                    break
        # If it's a Tag, make sure its name or attributes match.
        # Don't bother with Tags if we're searching for text.
        elif isinstance(markup, Tag):
            if not self.text or self.name or self.attrs:
                found = self.search_tag(markup)
        # If it's text, make sure the text matches.
        elif isinstance(markup, NavigableString) or \
                 isinstance(markup, basestring):
            if not self.name and not self.attrs and self._matches(markup, self.text):
                found = markup
        else:
            raise Exception(
                "I don't know how to match against a %s" % markup.__class__)
        return found

    def _matches(self, markup, match_against):
        # print u"Matching %s against %s" % (markup, match_against)
        result = False
        if isinstance(markup, list) or isinstance(markup, tuple):
            # This should only happen when searching a multi-valued attribute
            # like 'class'.
            if (isinstance(match_against, unicode)
                and ' ' in match_against):
                # A bit of a special case. If they try to match "foo
                # bar" on a multivalue attribute's value, only accept
                # the literal value "foo bar"
                #
                # XXX This is going to be pretty slow because we keep
                # splitting match_against. But it shouldn't come up
                # too often.
                return (whitespace_re.split(match_against) == markup)
            else:
                for item in markup:
                    if self._matches(item, match_against):
                        return True
                return False

        if match_against is True:
            # True matches any non-None value.
            return markup is not None

        if isinstance(match_against, collections.Callable):
            return match_against(markup)

        # Custom callables take the tag as an argument, but all
        # other ways of matching match the tag name as a string.
        if isinstance(markup, Tag):
            markup = markup.name

        # Ensure that `markup` is either a Unicode string, or None.
        markup = self._normalize_search_value(markup)

        if markup is None:
            # None matches None, False, an empty string, an empty list, and so on.
            return not match_against

        if isinstance(match_against, unicode):
            # Exact string match
            return markup == match_against

        if hasattr(match_against, 'match'):
            # Regexp match
            return match_against.search(markup)

        if hasattr(match_against, '__iter__'):
            # The markup must be an exact match against something
            # in the iterable.
            return markup in match_against


class ResultSet(list):
    """A ResultSet is just a list that keeps track of the SoupStrainer
    that created it."""
    def __init__(self, source, result=()):
        super(ResultSet, self).__init__(result)
        self.source = source
