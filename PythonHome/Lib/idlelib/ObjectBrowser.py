# XXX TO DO:
# - popup menu
# - support partial or total redisplay
# - more doc strings
# - tooltips

# object browser

# XXX TO DO:
# - for classes/modules, add "open source" to object browser

import re

from idlelib.TreeWidget import TreeItem, TreeNode, ScrolledCanvas

from repr import Repr

myrepr = Repr()
myrepr.maxstring = 100
myrepr.maxother = 100

class ObjectTreeItem(TreeItem):
    def __init__(self, labeltext, object, setfunction=None):
        self.labeltext = labeltext
        self.object = object
        self.setfunction = setfunction
    def GetLabelText(self):
        return self.labeltext
    def GetText(self):
        return myrepr.repr(self.object)
    def GetIconName(self):
        if not self.IsExpandable():
            return "python"
    def IsEditable(self):
        return self.setfunction is not None
    def SetText(self, text):
        try:
            value = eval(text)
            self.setfunction(value)
        except:
            pass
        else:
            self.object = value
    def IsExpandable(self):
        return not not dir(self.object)
    def GetSubList(self):
        keys = dir(self.object)
        sublist = []
        for key in keys:
            try:
                value = getattr(self.object, key)
            except AttributeError:
                continue
            item = make_objecttreeitem(
                str(key) + " =",
                value,
                lambda value, key=key, object=self.object:
                    setattr(object, key, value))
            sublist.append(item)
        return sublist

class InstanceTreeItem(ObjectTreeItem):
    def IsExpandable(self):
        return True
    def GetSubList(self):
        sublist = ObjectTreeItem.GetSubList(self)
        sublist.insert(0,
            make_objecttreeitem("__class__ =", self.object.__class__))
        return sublist

class ClassTreeItem(ObjectTreeItem):
    def IsExpandable(self):
        return True
    def GetSubList(self):
        sublist = ObjectTreeItem.GetSubList(self)
        if len(self.object.__bases__) == 1:
            item = make_objecttreeitem("__bases__[0] =",
                self.object.__bases__[0])
        else:
            item = make_objecttreeitem("__bases__ =", self.object.__bases__)
        sublist.insert(0, item)
        return sublist

class AtomicObjectTreeItem(ObjectTreeItem):
    def IsExpandable(self):
        return 0

class SequenceTreeItem(ObjectTreeItem):
    def IsExpandable(self):
        return len(self.object) > 0
    def keys(self):
        return range(len(self.object))
    def GetSubList(self):
        sublist = []
        for key in self.keys():
            try:
                value = self.object[key]
            except KeyError:
                continue
            def setfunction(value, key=key, object=self.object):
                object[key] = value
            item = make_objecttreeitem("%r:" % (key,), value, setfunction)
            sublist.append(item)
        return sublist

class DictTreeItem(SequenceTreeItem):
    def keys(self):
        keys = self.object.keys()
        try:
            keys.sort()
        except:
            pass
        return keys

from types import *

dispatch = {
    IntType: AtomicObjectTreeItem,
    LongType: AtomicObjectTreeItem,
    FloatType: AtomicObjectTreeItem,
    StringType: AtomicObjectTreeItem,
    TupleType: SequenceTreeItem,
    ListType: SequenceTreeItem,
    DictType: DictTreeItem,
    InstanceType: InstanceTreeItem,
    ClassType: ClassTreeItem,
}

def make_objecttreeitem(labeltext, object, setfunction=None):
    t = type(object)
    if t in dispatch:
        c = dispatch[t]
    else:
        c = ObjectTreeItem
    return c(labeltext, object, setfunction)


def _object_browser(parent):
    import sys
    from Tkinter import Tk
    root = Tk()
    root.title("Test ObjectBrowser")
    width, height, x, y = list(map(int, re.split('[x+]', parent.geometry())))
    root.geometry("+%d+%d"%(x, y + 150))
    root.configure(bd=0, bg="yellow")
    root.focus_set()
    sc = ScrolledCanvas(root, bg="white", highlightthickness=0, takefocus=1)
    sc.frame.pack(expand=1, fill="both")
    item = make_objecttreeitem("sys", sys)
    node = TreeNode(sc.canvas, None, item)
    node.update()
    root.mainloop()

if __name__ == '__main__':
    from idlelib.idle_test.htest import run
    run(_object_browser)
