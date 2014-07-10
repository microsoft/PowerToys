from idlelib.WidgetRedirector import WidgetRedirector
from idlelib.Delegator import Delegator

class Percolator:

    def __init__(self, text):
        # XXX would be nice to inherit from Delegator
        self.text = text
        self.redir = WidgetRedirector(text)
        self.top = self.bottom = Delegator(text)
        self.bottom.insert = self.redir.register("insert", self.insert)
        self.bottom.delete = self.redir.register("delete", self.delete)
        self.filters = []

    def close(self):
        while self.top is not self.bottom:
            self.removefilter(self.top)
        self.top = None
        self.bottom.setdelegate(None); self.bottom = None
        self.redir.close(); self.redir = None
        self.text = None

    def insert(self, index, chars, tags=None):
        # Could go away if inheriting from Delegator
        self.top.insert(index, chars, tags)

    def delete(self, index1, index2=None):
        # Could go away if inheriting from Delegator
        self.top.delete(index1, index2)

    def insertfilter(self, filter):
        # Perhaps rename to pushfilter()?
        assert isinstance(filter, Delegator)
        assert filter.delegate is None
        filter.setdelegate(self.top)
        self.top = filter

    def removefilter(self, filter):
        # XXX Perhaps should only support popfilter()?
        assert isinstance(filter, Delegator)
        assert filter.delegate is not None
        f = self.top
        if f is filter:
            self.top = filter.delegate
            filter.setdelegate(None)
        else:
            while f.delegate is not filter:
                assert f is not self.bottom
                f.resetcache()
                f = f.delegate
            f.setdelegate(filter.delegate)
            filter.setdelegate(None)


def _percolator(parent):
    import Tkinter as tk
    import re
    class Tracer(Delegator):
        def __init__(self, name):
            self.name = name
            Delegator.__init__(self, None)
        def insert(self, *args):
            print self.name, ": insert", args
            self.delegate.insert(*args)
        def delete(self, *args):
            print self.name, ": delete", args
            self.delegate.delete(*args)
    root = tk.Tk()
    root.title("Test Percolator")
    width, height, x, y = list(map(int, re.split('[x+]', parent.geometry())))
    root.geometry("+%d+%d"%(x, y + 150))
    text = tk.Text(root)
    p = Percolator(text)
    t1 = Tracer("t1")
    t2 = Tracer("t2")

    def toggle1():
        if var1.get() == 0:
            var1.set(1)
            p.insertfilter(t1)
        elif var1.get() == 1:
            var1.set(0)
            p.removefilter(t1)

    def toggle2():
        if var2.get() == 0:
            var2.set(1)
            p.insertfilter(t2)
        elif var2.get() == 1:
            var2.set(0)
            p.removefilter(t2)

    text.pack()
    var1 = tk.IntVar()
    cb1 = tk.Checkbutton(root, text="Tracer1", command=toggle1, variable=var1)
    cb1.pack()
    var2 = tk.IntVar()
    cb2 = tk.Checkbutton(root, text="Tracer2", command=toggle2, variable=var2)
    cb2.pack()

if __name__ == "__main__":
    from idlelib.idle_test.htest import run
    run(_percolator)
