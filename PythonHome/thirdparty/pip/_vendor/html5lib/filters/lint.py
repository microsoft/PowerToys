from __future__ import absolute_import, division, unicode_literals

from gettext import gettext
_ = gettext

from . import _base
from ..constants import cdataElements, rcdataElements, voidElements

from ..constants import spaceCharacters
spaceCharacters = "".join(spaceCharacters)


class LintError(Exception):
    pass


class Filter(_base.Filter):
    def __iter__(self):
        open_elements = []
        contentModelFlag = "PCDATA"
        for token in _base.Filter.__iter__(self):
            type = token["type"]
            if type in ("StartTag", "EmptyTag"):
                name = token["name"]
                if contentModelFlag != "PCDATA":
                    raise LintError(_("StartTag not in PCDATA content model flag: %(tag)s") % {"tag": name})
                if not isinstance(name, str):
                    raise LintError(_("Tag name is not a string: %(tag)r") % {"tag": name})
                if not name:
                    raise LintError(_("Empty tag name"))
                if type == "StartTag" and name in voidElements:
                    raise LintError(_("Void element reported as StartTag token: %(tag)s") % {"tag": name})
                elif type == "EmptyTag" and name not in voidElements:
                    raise LintError(_("Non-void element reported as EmptyTag token: %(tag)s") % {"tag": token["name"]})
                if type == "StartTag":
                    open_elements.append(name)
                for name, value in token["data"]:
                    if not isinstance(name, str):
                        raise LintError(_("Attribute name is not a string: %(name)r") % {"name": name})
                    if not name:
                        raise LintError(_("Empty attribute name"))
                    if not isinstance(value, str):
                        raise LintError(_("Attribute value is not a string: %(value)r") % {"value": value})
                if name in cdataElements:
                    contentModelFlag = "CDATA"
                elif name in rcdataElements:
                    contentModelFlag = "RCDATA"
                elif name == "plaintext":
                    contentModelFlag = "PLAINTEXT"

            elif type == "EndTag":
                name = token["name"]
                if not isinstance(name, str):
                    raise LintError(_("Tag name is not a string: %(tag)r") % {"tag": name})
                if not name:
                    raise LintError(_("Empty tag name"))
                if name in voidElements:
                    raise LintError(_("Void element reported as EndTag token: %(tag)s") % {"tag": name})
                start_name = open_elements.pop()
                if start_name != name:
                    raise LintError(_("EndTag (%(end)s) does not match StartTag (%(start)s)") % {"end": name, "start": start_name})
                contentModelFlag = "PCDATA"

            elif type == "Comment":
                if contentModelFlag != "PCDATA":
                    raise LintError(_("Comment not in PCDATA content model flag"))

            elif type in ("Characters", "SpaceCharacters"):
                data = token["data"]
                if not isinstance(data, str):
                    raise LintError(_("Attribute name is not a string: %(name)r") % {"name": data})
                if not data:
                    raise LintError(_("%(type)s token with empty data") % {"type": type})
                if type == "SpaceCharacters":
                    data = data.strip(spaceCharacters)
                    if data:
                        raise LintError(_("Non-space character(s) found in SpaceCharacters token: %(token)r") % {"token": data})

            elif type == "Doctype":
                name = token["name"]
                if contentModelFlag != "PCDATA":
                    raise LintError(_("Doctype not in PCDATA content model flag: %(name)s") % {"name": name})
                if not isinstance(name, str):
                    raise LintError(_("Tag name is not a string: %(tag)r") % {"tag": name})
                # XXX: what to do with token["data"] ?

            elif type in ("ParseError", "SerializeError"):
                pass

            else:
                raise LintError(_("Unknown token type: %(type)s") % {"type": type})

            yield token
