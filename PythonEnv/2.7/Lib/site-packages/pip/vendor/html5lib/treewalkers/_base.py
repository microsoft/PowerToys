from __future__ import absolute_import, division, unicode_literals
from pip.vendor.six import text_type

import gettext
_ = gettext.gettext

from ..constants import voidElements, spaceCharacters
spaceCharacters = "".join(spaceCharacters)


class TreeWalker(object):
    def __init__(self, tree):
        self.tree = tree

    def __iter__(self):
        raise NotImplementedError

    def error(self, msg):
        return {"type": "SerializeError", "data": msg}

    def emptyTag(self, namespace, name, attrs, hasChildren=False):
        assert namespace is None or isinstance(namespace, text_type), type(namespace)
        assert isinstance(name, text_type), type(name)
        assert all((namespace is None or isinstance(namespace, text_type)) and
                   isinstance(name, text_type) and
                   isinstance(value, text_type)
                   for (namespace, name), value in attrs.items())

        yield {"type": "EmptyTag", "name": name,
               "namespace": namespace,
               "data": attrs}
        if hasChildren:
            yield self.error(_("Void element has children"))

    def startTag(self, namespace, name, attrs):
        assert namespace is None or isinstance(namespace, text_type), type(namespace)
        assert isinstance(name, text_type), type(name)
        assert all((namespace is None or isinstance(namespace, text_type)) and
                   isinstance(name, text_type) and
                   isinstance(value, text_type)
                   for (namespace, name), value in attrs.items())

        return {"type": "StartTag",
                "name": name,
                "namespace": namespace,
                "data": attrs}

    def endTag(self, namespace, name):
        assert namespace is None or isinstance(namespace, text_type), type(namespace)
        assert isinstance(name, text_type), type(namespace)

        return {"type": "EndTag",
                "name": name,
                "namespace": namespace,
                "data": {}}

    def text(self, data):
        assert isinstance(data, text_type), type(data)

        data = data
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
        assert isinstance(data, text_type), type(data)

        return {"type": "Comment", "data": data}

    def doctype(self, name, publicId=None, systemId=None, correct=True):
        assert name is None or isinstance(name, text_type), type(name)
        assert publicId is None or isinstance(publicId, text_type), type(publicId)
        assert systemId is None or isinstance(systemId, text_type), type(systemId)

        return {"type": "Doctype",
                "name": name if name is not None else "",
                "publicId": publicId,
                "systemId": systemId,
                "correct": correct}

    def entity(self, name):
        assert isinstance(name, text_type), type(name)

        return {"type": "Entity", "name": name}

    def unknown(self, nodeType):
        return self.error(_("Unknown node type: ") + nodeType)


class RecursiveTreeWalker(TreeWalker):
    def walkChildren(self, node):
        raise NotImplementedError

    def element(self, node, namespace, name, attrs, hasChildren):
        if name in voidElements:
            for token in self.emptyTag(namespace, name, attrs, hasChildren):
                yield token
        else:
            yield self.startTag(name, attrs)
            if hasChildren:
                for token in self.walkChildren(node):
                    yield token
            yield self.endTag(name)

from xml.dom import Node

DOCUMENT = Node.DOCUMENT_NODE
DOCTYPE = Node.DOCUMENT_TYPE_NODE
TEXT = Node.TEXT_NODE
ELEMENT = Node.ELEMENT_NODE
COMMENT = Node.COMMENT_NODE
ENTITY = Node.ENTITY_NODE
UNKNOWN = "<#UNKNOWN#>"


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
