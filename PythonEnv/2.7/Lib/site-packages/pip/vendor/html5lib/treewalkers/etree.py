from __future__ import absolute_import, division, unicode_literals

import gettext
_ = gettext.gettext

import re

from pip.vendor.six import text_type

from . import _base
from ..utils import moduleFactoryFactory

tag_regexp = re.compile("{([^}]*)}(.*)")


def getETreeBuilder(ElementTreeImplementation):
    ElementTree = ElementTreeImplementation
    ElementTreeCommentType = ElementTree.Comment("asd").tag

    class TreeWalker(_base.NonRecursiveTreeWalker):
        """Given the particular ElementTree representation, this implementation,
        to avoid using recursion, returns "nodes" as tuples with the following
        content:

        1. The current element

        2. The index of the element relative to its parent

        3. A stack of ancestor elements

        4. A flag "text", "tail" or None to indicate if the current node is a
           text node; either the text or tail of the current element (1)
        """
        def getNodeDetails(self, node):
            if isinstance(node, tuple):  # It might be the root Element
                elt, key, parents, flag = node
                if flag in ("text", "tail"):
                    return _base.TEXT, getattr(elt, flag)
                else:
                    node = elt

            if not(hasattr(node, "tag")):
                node = node.getroot()

            if node.tag in ("DOCUMENT_ROOT", "DOCUMENT_FRAGMENT"):
                return (_base.DOCUMENT,)

            elif node.tag == "<!DOCTYPE>":
                return (_base.DOCTYPE, node.text,
                        node.get("publicId"), node.get("systemId"))

            elif node.tag == ElementTreeCommentType:
                return _base.COMMENT, node.text

            else:
                assert type(node.tag) == text_type, type(node.tag)
                # This is assumed to be an ordinary element
                match = tag_regexp.match(node.tag)
                if match:
                    namespace, tag = match.groups()
                else:
                    namespace = None
                    tag = node.tag
                attrs = {}
                for name, value in list(node.attrib.items()):
                    match = tag_regexp.match(name)
                    if match:
                        attrs[(match.group(1), match.group(2))] = value
                    else:
                        attrs[(None, name)] = value
                return (_base.ELEMENT, namespace, tag,
                        attrs, len(node) or node.text)

        def getFirstChild(self, node):
            if isinstance(node, tuple):
                element, key, parents, flag = node
            else:
                element, key, parents, flag = node, None, [], None

            if flag in ("text", "tail"):
                return None
            else:
                if element.text:
                    return element, key, parents, "text"
                elif len(element):
                    parents.append(element)
                    return element[0], 0, parents, None
                else:
                    return None

        def getNextSibling(self, node):
            if isinstance(node, tuple):
                element, key, parents, flag = node
            else:
                return None

            if flag == "text":
                if len(element):
                    parents.append(element)
                    return element[0], 0, parents, None
                else:
                    return None
            else:
                if element.tail and flag != "tail":
                    return element, key, parents, "tail"
                elif key < len(parents[-1]) - 1:
                    return parents[-1][key + 1], key + 1, parents, None
                else:
                    return None

        def getParentNode(self, node):
            if isinstance(node, tuple):
                element, key, parents, flag = node
            else:
                return None

            if flag == "text":
                if not parents:
                    return element
                else:
                    return element, key, parents, None
            else:
                parent = parents.pop()
                if not parents:
                    return parent
                else:
                    return parent, list(parents[-1]).index(parent), parents, None

    return locals()

getETreeModule = moduleFactoryFactory(getETreeBuilder)
