"""Class browser.

XXX TO DO:

- reparse when source changed (maybe just a button would be OK?)
    (or recheck on window popup)
- add popup menu with more options (e.g. doc strings, base classes, imports)
- show function argument list? (have to do pattern matching on source)
- should the classes and methods lists also be in the module's menu bar?
- add base classes to class browser tree
"""

import os
import sys
import pyclbr

from idlelib import PyShell
from idlelib.WindowList import ListedToplevel
from idlelib.TreeWidget import TreeNode, TreeItem, ScrolledCanvas
from idlelib.configHandler import idleConf

class ClassBrowser:

    def __init__(self, flist, name, path, _htest=False):
        # XXX This API should change, if the file doesn't end in ".py"
        # XXX the code here is bogus!
        """
        _htest - bool, change box when location running htest.
        """
        self.name = name
        self.file = os.path.join(path[0], self.name + ".py")
        self._htest = _htest
        self.init(flist)

    def close(self, event=None):
        self.top.destroy()
        self.node.destroy()

    def init(self, flist):
        self.flist = flist
        # reset pyclbr
        pyclbr._modules.clear()
        # create top
        self.top = top = ListedToplevel(flist.root)
        top.protocol("WM_DELETE_WINDOW", self.close)
        top.bind("<Escape>", self.close)
        if self._htest: # place dialog below parent if running htest
            top.geometry("+%d+%d" %
                (flist.root.winfo_rootx(), flist.root.winfo_rooty() + 200))
        self.settitle()
        top.focus_set()
        # create scrolled canvas
        theme = idleConf.GetOption('main','Theme','name')
        background = idleConf.GetHighlight(theme, 'normal')['background']
        sc = ScrolledCanvas(top, bg=background, highlightthickness=0, takefocus=1)
        sc.frame.pack(expand=1, fill="both")
        item = self.rootnode()
        self.node = node = TreeNode(sc.canvas, None, item)
        node.update()
        node.expand()

    def settitle(self):
        self.top.wm_title("Class Browser - " + self.name)
        self.top.wm_iconname("Class Browser")

    def rootnode(self):
        return ModuleBrowserTreeItem(self.file)

class ModuleBrowserTreeItem(TreeItem):

    def __init__(self, file):
        self.file = file

    def GetText(self):
        return os.path.basename(self.file)

    def GetIconName(self):
        return "python"

    def GetSubList(self):
        sublist = []
        for name in self.listclasses():
            item = ClassBrowserTreeItem(name, self.classes, self.file)
            sublist.append(item)
        return sublist

    def OnDoubleClick(self):
        if os.path.normcase(self.file[-3:]) != ".py":
            return
        if not os.path.exists(self.file):
            return
        PyShell.flist.open(self.file)

    def IsExpandable(self):
        return os.path.normcase(self.file[-3:]) == ".py"

    def listclasses(self):
        dir, file = os.path.split(self.file)
        name, ext = os.path.splitext(file)
        if os.path.normcase(ext) != ".py":
            return []
        try:
            dict = pyclbr.readmodule_ex(name, [dir] + sys.path)
        except ImportError, msg:
            return []
        items = []
        self.classes = {}
        for key, cl in dict.items():
            if cl.module == name:
                s = key
                if hasattr(cl, 'super') and cl.super:
                    supers = []
                    for sup in cl.super:
                        if type(sup) is type(''):
                            sname = sup
                        else:
                            sname = sup.name
                            if sup.module != cl.module:
                                sname = "%s.%s" % (sup.module, sname)
                        supers.append(sname)
                    s = s + "(%s)" % ", ".join(supers)
                items.append((cl.lineno, s))
                self.classes[s] = cl
        items.sort()
        list = []
        for item, s in items:
            list.append(s)
        return list

class ClassBrowserTreeItem(TreeItem):

    def __init__(self, name, classes, file):
        self.name = name
        self.classes = classes
        self.file = file
        try:
            self.cl = self.classes[self.name]
        except (IndexError, KeyError):
            self.cl = None
        self.isfunction = isinstance(self.cl, pyclbr.Function)

    def GetText(self):
        if self.isfunction:
            return "def " + self.name + "(...)"
        else:
            return "class " + self.name

    def GetIconName(self):
        if self.isfunction:
            return "python"
        else:
            return "folder"

    def IsExpandable(self):
        if self.cl:
            try:
                return not not self.cl.methods
            except AttributeError:
                return False

    def GetSubList(self):
        if not self.cl:
            return []
        sublist = []
        for name in self.listmethods():
            item = MethodBrowserTreeItem(name, self.cl, self.file)
            sublist.append(item)
        return sublist

    def OnDoubleClick(self):
        if not os.path.exists(self.file):
            return
        edit = PyShell.flist.open(self.file)
        if hasattr(self.cl, 'lineno'):
            lineno = self.cl.lineno
            edit.gotoline(lineno)

    def listmethods(self):
        if not self.cl:
            return []
        items = []
        for name, lineno in self.cl.methods.items():
            items.append((lineno, name))
        items.sort()
        list = []
        for item, name in items:
            list.append(name)
        return list

class MethodBrowserTreeItem(TreeItem):

    def __init__(self, name, cl, file):
        self.name = name
        self.cl = cl
        self.file = file

    def GetText(self):
        return "def " + self.name + "(...)"

    def GetIconName(self):
        return "python" # XXX

    def IsExpandable(self):
        return 0

    def OnDoubleClick(self):
        if not os.path.exists(self.file):
            return
        edit = PyShell.flist.open(self.file)
        edit.gotoline(self.cl.methods[self.name])

def _class_browser(parent): #Wrapper for htest
    try:
        file = __file__
    except NameError:
        file = sys.argv[0]
        if sys.argv[1:]:
            file = sys.argv[1]
        else:
            file = sys.argv[0]
    dir, file = os.path.split(file)
    name = os.path.splitext(file)[0]
    flist = PyShell.PyShellFileList(parent)
    ClassBrowser(flist, name, [dir], _htest=True)
    parent.mainloop()

if __name__ == "__main__":
    from idlelib.idle_test.htest import run
    run(_class_browser)
