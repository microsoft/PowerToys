from __future__ import absolute_import, division, unicode_literals
from pip._vendor.six import text_type, string_types

import gettext
_ = gettext.gettext

from xml.dom import Node

DOCUMENT = Node.DOCUMENT_NODE
DOCTYPE = Node.DOCUMENT_TYPE_NODE
TEXT = Node.TEXT_NODE
ELEMENT = Node.ELEMENT_NODE
COMMENT = Node.COMMENT_NODE
ENTITY = Node.ENTITY_NODE
UNKNOWN = "<#UNKNOWN#>"

from ..constants import voidElements, spaceCharacters
spaceCharacters = "".join(spaceCharacters)


def to_text(s, blank_if_none=True):
    """Wrapper around six.text_type to convert None to empty string"""
    if s is None:
        if blank_if_none:
            return ""
        else:
            return None
    elif isinstance(s, text_type):
        return s
    else:
        return text_type(s)


def is_text_or_none(string):
    """Wrapper around isinstance(string_types) or is None"""
    return string is None or isinstance(string, string_types)


class TreeWalker(object):
    def __init__(self, tree):
        self.tree = tree

    def __iter__(self):
        raise NotImplementedError

    def error(self, msg):
        return {"type": "SerializeError", "data": msg}

    def emptyTag(self, namespace, name, attrs, hasChildren=False):
        assert namespace is None or isinstance(namespace, string_types), type(namespace)
        assert isinstance(name, string_types), type(name)
        assert all((namespace is None or isinstance(namespace, string_types)) and
                   isinstance(name, string_types) and
                   isinstance(value, string_types)
                   for (namespace, name), value in attrs.items())

        yield {"type": "EmptyTag", "name": to_text(name, False),
               "namespace": to_text(namespace),
               "data": attrs}
        if hasChildren:
            yield self.error(_("Void element has children"))

    def startTag(self, namespace, name, attrs):
        assert namespace is None or isinstance(namespace, string_types), type(namespace)
        assert isinstance(name, string_types), type(name)
        assert all((namespace is None or isinstance(namespace, string_types)) and
                   isinstance(name, string_types) and
                   isinstance(value, string_types)
                   for (namespace, name), value in attrs.items())

        return {"type": "StartTag",
                "name": text_type(name),
                "namespace": to_text(namespace),
                "data": dict(((to_text(namespace, False), to_text(name)),
                              to_text(value, False))
                             for (namespace, name), value in attrs.items())}

    def endTag(self, namespace, name):
        assert namespace is None or isinstance(namespace, string_types), type(namespace)
        assert isinstance(name, string_types), type(namespace)

        return {"type": "EndTag",
                "name": to_text(name, False),
                "namespace": to_text(namespace),
                "data": {}}

    def text(self, data):
        assert isinstance(data, string_types), type(data)

        data = to_text(data)
        middle = data.lstrip(spaceCharacters)
        left = data[:len(data) - len(middle)]
        if left:
            yield {"type": "SpaceCharacters", "data": left}
        data = middle
        middle = data.rstrip(spaceCharacters)
        right = data[len(middle):]
        if middle:
            yield {"type": "Characters", "data": middle}
        if right:
            yield {"type": "SpaceCharacters", "data": right}

    def comment(self, data):
        assert isinstance(data, string_types), type(data)

        return {"type": "Comment", "data": text_type(data)}

    def doctype(self, name, publicId=None, systemId=None, correct=True):
        assert is_text_or_none(name), type(name)
        assert is_text_or_none(publicId), type(publicId)
        assert is_text_or_none(systemId), type(systemId)

        return {"type": "Doctype",
                "name": to_text(name),
                "publicId": to_text(publicId),
                "systemId": to_text(systemId),
                "correct": to_text(correct)}

    def entity(self, name):
        assert isinstance(name, string_types), type(name)

        return {"type": "Entity", "name": text_type(name)}

    def unknown(self, nodeType):
        return self.error(_("Unknown node type: ") + nodeType)


class NonRecursiveTreeWalker(TreeWalker):
    def getNodeDetails(self, node):
        raise NotImplementedError

    def getFirstChild(self, node):
        raise NotImplementedError

    def getNextSibling(self, node):
        raise NotImplementedError

    def getParentNode(self, node):
        raise NotImplementedError

    def __iter__(self):
        currentNode = self.tree
        while currentNode is not None:
            details = self.getNodeDetails(currentNode)
            type, details = details[0], details[1:]
            hasChildren = False

            if type == DOCTYPE:
                yield self.doctype(*details)

            elif type == TEXT:
                for token in self.text(*details):
                    yield token

            elif type == ELEMENT:
                namespace, name, attributes, hasChildren = details
                if name in voidElements:
                    for token in self.emptyTag(namespace, name, attributes,
                                               hasChildren):
                        yield token
                    hasChildren = False
                else:
                    yield self.startTag(namespace, name, attributes)

            elif type == COMMENT:
                yield self.comment(details[0])

            elif type == ENTITY:
                yield self.entity(details[0])

            elif type == DOCUMENT:
                hasChildren = True

            else:
                yield self.unknown(details[0])

            if hasChildren:
                firstChild = self.getFirstChild(currentNode)
            else:
                firstChild = None

            if firstChild is not None:
                currentNode = firstChild
            else:
                while currentNode is not None:
                    details = self.getNodeDetails(currentNode)
                    type, details = details[0], details[1:]
                    if type == ELEMENT:
                        namespace, name, attributes, hasChildren = details
                        if name not in voidElements:
                            yield self.endTag(namespace, name)
                    if self.tree is currentNode:
                        currentNode = None
                        break
                    nextSibling = self.getNextSibling(currentNode)
                    if nextSibling is not None:
                        currentNode = nextSibling
                        break
                    else:
                        currentNode = self.getParentNode(currentNode)
