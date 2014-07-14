from Tkinter import *

class ScrolledList:

    default = "(None)"

    def __init__(self, master, **options):
        # Create top frame, with scrollbar and listbox
        self.master = master
        self.frame = frame = Frame(master)
        self.frame.pack(fill="both", expand=1)
        self.vbar = vbar = Scrollbar(frame, name="vbar")
        self.vbar.pack(side="right", fill="y")
        self.listbox = listbox = Listbox(frame, exportselection=0,
            background="white")
        if options:
            listbox.configure(options)
        listbox.pack(expand=1, fill="both")
        # Tie listbox and scrollbar together
        vbar["command"] = listbox.yview
        listbox["yscrollcommand"] = vbar.set
        # Bind events to the list box
        listbox.bind("<ButtonRelease-1>", self.click_event)
        listbox.bind("<Double-ButtonRelease-1>", self.double_click_event)
        listbox.bind("<ButtonPress-3>", self.popup_event)
        listbox.bind("<Key-Up>", self.up_event)
        listbox.bind("<Key-Down>", self.down_event)
        # Mark as empty
        self.clear()

    def close(self):
        self.frame.destroy()

    def clear(self):
        self.listbox.delete(0, "end")
        self.empty = 1
        self.listbox.insert("end", self.default)

    def append(self, item):
        if self.empty:
            self.listbox.delete(0, "end")
            self.empty = 0
        self.listbox.insert("end", str(item))

    def get(self, index):
        return self.listbox.get(index)

    def click_event(self, event):
        self.listbox.activate("@%d,%d" % (event.x, event.y))
        index = self.listbox.index("active")
        self.select(index)
        self.on_select(index)
        return "break"

    def double_click_event(self, event):
        index = self.listbox.index("active")
        self.select(index)
        self.on_double(index)
        return "break"

    menu = None

    def popup_event(self, event):
        if not self.menu:
            self.make_menu()
        menu = self.menu
        self.listbox.activate("@%d,%d" % (event.x, event.y))
        index = self.listbox.index("active")
        self.select(index)
        menu.tk_popup(event.x_root, event.y_root)

    def make_menu(self):
        menu = Menu(self.listbox, tearoff=0)
        self.menu = menu
        self.fill_menu()

    def up_event(self, event):
        index = self.listbox.index("active")
        if self.listbox.selection_includes(index):
            index = index - 1
        else:
            index = self.listbox.size() - 1
        if index < 0:
            self.listbox.bell()
        else:
            self.select(index)
            self.on_select(index)
        return "break"

    def down_event(self, event):
        index = self.listbox.index("active")
        if self.listbox.selection_includes(index):
            index = index + 1
        else:
            index = 0
        if index >= self.listbox.size():
            self.listbox.bell()
        else:
            self.select(index)
            self.on_select(index)
        return "break"

    def select(self, index):
        self.listbox.focus_set()
        self.listbox.activate(index)
        self.listbox.selection_clear(0, "end")
        self.listbox.selection_set(index)
        self.listbox.see(index)

    # Methods to override for specific actions

    def fill_menu(self):
        pass

    def on_select(self, index):
        pass

    def on_double(self, index):
        pass


def _scrolled_list(parent):
    root = Tk()
    root.title("Test ScrolledList")
    width, height, x, y = list(map(int, re.split('[x+]', parent.geometry())))
    root.geometry("+%d+%d"%(x, y + 150))
    class MyScrolledList(ScrolledList):
        def fill_menu(self): self.menu.add_command(label="right click")
        def on_select(self, index): print "select", self.get(index)
        def on_double(self, index): print "double", self.get(index)

    scrolled_list = MyScrolledList(root)
    for i in range(30):
        scrolled_list.append("Item %02d" % i)

    root.mainloop()

if __name__ == '__main__':
    from idlelib.idle_test.htest import run
    run(_scrolled_list)
