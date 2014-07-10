"""
Dialog that allows user to specify a new config file section name.
Used to get new highlight theme and keybinding set names.
The 'return value' for the dialog, used two placed in configDialog.py,
is the .result attribute set in the Ok and Cancel methods.
"""
from Tkinter import *
import tkMessageBox
class GetCfgSectionNameDialog(Toplevel):
    def __init__(self, parent, title, message, used_names, _htest=False):
        """
        message - string, informational message to display
        used_names - string collection, names already in use for validity check
        _htest - bool, change box location when running htest
        """
        Toplevel.__init__(self, parent)
        self.configure(borderwidth=5)
        self.resizable(height=FALSE, width=FALSE)
        self.title(title)
        self.transient(parent)
        self.grab_set()
        self.protocol("WM_DELETE_WINDOW", self.Cancel)
        self.parent = parent
        self.message = message
        self.used_names = used_names
        self.create_widgets()
        self.withdraw()  #hide while setting geometry
        self.update_idletasks()
        #needs to be done here so that the winfo_reqwidth is valid
        self.messageInfo.config(width=self.frameMain.winfo_reqwidth())
        self.geometry(
                "+%d+%d" % (
                    parent.winfo_rootx() +
                    (parent.winfo_width()/2 - self.winfo_reqwidth()/2),
                    parent.winfo_rooty() +
                    ((parent.winfo_height()/2 - self.winfo_reqheight()/2)
                    if not _htest else 100)
                ) )  #centre dialog over parent (or below htest box)
        self.deiconify()  #geometry set, unhide
        self.wait_window()
    def create_widgets(self):
        self.name = StringVar(self.parent)
        self.fontSize = StringVar(self.parent)
        self.frameMain = Frame(self, borderwidth=2, relief=SUNKEN)
        self.frameMain.pack(side=TOP, expand=TRUE, fill=BOTH)
        self.messageInfo = Message(self.frameMain, anchor=W, justify=LEFT,
                    padx=5, pady=5, text=self.message) #,aspect=200)
        entryName = Entry(self.frameMain, textvariable=self.name, width=30)
        entryName.focus_set()
        self.messageInfo.pack(padx=5, pady=5) #, expand=TRUE, fill=BOTH)
        entryName.pack(padx=5, pady=5)
        frameButtons = Frame(self, pady=2)
        frameButtons.pack(side=BOTTOM)
        self.buttonOk = Button(frameButtons, text='Ok',
                width=8, command=self.Ok)
        self.buttonOk.pack(side=LEFT, padx=5)
        self.buttonCancel = Button(frameButtons, text='Cancel',
                width=8, command=self.Cancel)
        self.buttonCancel.pack(side=RIGHT, padx=5)

    def name_ok(self):
        ''' After stripping entered name, check that it is a  sensible
        ConfigParser file section name. Return it if it is, '' if not.
        '''
        name = self.name.get().strip()
        if not name: #no name specified
            tkMessageBox.showerror(title='Name Error',
                    message='No name specified.', parent=self)
        elif len(name)>30: #name too long
            tkMessageBox.showerror(title='Name Error',
                    message='Name too long. It should be no more than '+
                    '30 characters.', parent=self)
            name = ''
        elif name in self.used_names:
            tkMessageBox.showerror(title='Name Error',
                    message='This name is already in use.', parent=self)
            name = ''
        return name
    def Ok(self, event=None):
        name = self.name_ok()
        if name:
            self.result = name
            self.destroy()
    def Cancel(self, event=None):
        self.result = ''
        self.destroy()
if __name__ == '__main__':
    import unittest
    unittest.main('idlelib.idle_test.test_config_name', verbosity=2, exit=False)

    from idlelib.idle_test.htest import run
    run(GetCfgSectionNameDialog)
