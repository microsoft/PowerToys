'''Define SearchDialogBase used by Search, Replace, and Grep dialogs.'''

from Tkinter import (Toplevel, Frame, Entry, Label, Button,
                     Checkbutton, Radiobutton)

class SearchDialogBase:
    '''Create most of a 3 or 4 row, 3 column search dialog.

    The left and wide middle column contain:
    1 or 2 labeled text entry lines (make_entry, create_entries);
    a row of standard Checkbuttons (make_frame, create_option_buttons),
    each of which corresponds to a search engine Variable;
    a row of dialog-specific Check/Radiobuttons (create_other_buttons).

    The narrow right column contains command buttons
    (make_button, create_command_buttons).
    These are bound to functions that execute the command.

    Except for command buttons, this base class is not limited to
    items common to all three subclasses.  Rather, it is the Find dialog
    minus the "Find Next" command and its execution function.
    The other dialogs override methods to replace and add widgets.
    '''

    title = "Search Dialog"  # replace in subclasses
    icon = "Search"
    needwrapbutton = 1  # not in Find in Files

    def __init__(self, root, engine):
        '''Initialize root, engine, and top attributes.

        top (level widget): set in create_widgets() called from open().
        text (Text being searched): set in open(), only used in subclasses().
        ent (ry): created in make_entry() called from create_entry().
        row (of grid): 0 in create_widgets(), +1 in make_entry/frame().

        title (of dialog): class attribute, override in subclasses.
        icon (of dialog): ditto, use unclear if cannot minimize dialog.
        '''
        self.root = root
        self.engine = engine
        self.top = None

    def open(self, text, searchphrase=None):
        "Make dialog visible on top of others and ready to use."
        self.text = text
        if not self.top:
            self.create_widgets()
        else:
            self.top.deiconify()
            self.top.tkraise()
        if searchphrase:
            self.ent.delete(0,"end")
            self.ent.insert("end",searchphrase)
        self.ent.focus_set()
        self.ent.selection_range(0, "end")
        self.ent.icursor(0)
        self.top.grab_set()

    def close(self, event=None):
        "Put dialog away for later use."
        if self.top:
            self.top.grab_release()
            self.top.withdraw()

    def create_widgets(self):
        '''Create basic 3 row x 3 col search (find) dialog.

        Other dialogs override subsidiary create_x methods as needed.
        Replace and Find-in-Files add another entry row.
        '''
        top = Toplevel(self.root)
        top.bind("<Return>", self.default_command)
        top.bind("<Escape>", self.close)
        top.protocol("WM_DELETE_WINDOW", self.close)
        top.wm_title(self.title)
        top.wm_iconname(self.icon)
        self.top = top

        self.row = 0
        self.top.grid_columnconfigure(0, pad=2, weight=0)
        self.top.grid_columnconfigure(1, pad=2, minsize=100, weight=100)

        self.create_entries()  # row 0 (and maybe 1), cols 0, 1
        self.create_option_buttons()  # next row, cols 0, 1
        self.create_other_buttons()  # next row, cols 0, 1
        self.create_command_buttons()  # col 2, all rows

    def make_entry(self, label, var):
        "Return gridded labeled Entry."
        l = Label(self.top, text=label)
        l.grid(row=self.row, column=0, sticky="nw")
        e = Entry(self.top, textvariable=var, exportselection=0)
        e.grid(row=self.row, column=1, sticky="nwe")
        self.row = self.row + 1
        return e

    def create_entries(self):
        "Create one or more entry lines with make_entry."
        self.ent = self.make_entry("Find:", self.engine.patvar)

    def make_frame(self,labeltext=None):
        "Return gridded labeled Frame for option or other buttons."
        if labeltext:
            l = Label(self.top, text=labeltext)
            l.grid(row=self.row, column=0, sticky="nw")
        f = Frame(self.top)
        f.grid(row=self.row, column=1, columnspan=1, sticky="nwe")
        self.row = self.row + 1
        return f

    def create_option_buttons(self):
        "Fill frame with Checkbuttons bound to SearchEngine booleanvars."
        f = self.make_frame("Options")

        btn = Checkbutton(f, anchor="w",
                variable=self.engine.revar,
                text="Regular expression")
        btn.pack(side="left", fill="both")
        if self.engine.isre():
            btn.select()

        btn = Checkbutton(f, anchor="w",
                variable=self.engine.casevar,
                text="Match case")
        btn.pack(side="left", fill="both")
        if self.engine.iscase():
            btn.select()

        btn = Checkbutton(f, anchor="w",
                variable=self.engine.wordvar,
                text="Whole word")
        btn.pack(side="left", fill="both")
        if self.engine.isword():
            btn.select()

        if self.needwrapbutton:
            btn = Checkbutton(f, anchor="w",
                    variable=self.engine.wrapvar,
                    text="Wrap around")
            btn.pack(side="left", fill="both")
            if self.engine.iswrap():
                btn.select()

    def create_other_buttons(self):
        "Fill frame with buttons tied to other options."
        f = self.make_frame("Direction")

        btn = Radiobutton(f, anchor="w",
                variable=self.engine.backvar, value=1,
                text="Up")
        btn.pack(side="left", fill="both")
        if self.engine.isback():
            btn.select()

        btn = Radiobutton(f, anchor="w",
                variable=self.engine.backvar, value=0,
                text="Down")
        btn.pack(side="left", fill="both")
        if not self.engine.isback():
            btn.select()

    def make_button(self, label, command, isdef=0):
        "Return command button gridded in command frame."
        b = Button(self.buttonframe,
                   text=label, command=command,
                   default=isdef and "active" or "normal")
        cols,rows=self.buttonframe.grid_size()
        b.grid(pady=1,row=rows,column=0,sticky="ew")
        self.buttonframe.grid(rowspan=rows+1)
        return b

    def create_command_buttons(self):
        "Place buttons in vertical command frame gridded on right."
        f = self.buttonframe = Frame(self.top)
        f.grid(row=0,column=2,padx=2,pady=2,ipadx=2,ipady=2)

        b = self.make_button("close", self.close)
        b.lower()

if __name__ == '__main__':
    import unittest
    unittest.main(
        'idlelib.idle_test.test_searchdialogbase', verbosity=2)
