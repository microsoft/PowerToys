"""Simple text browser for IDLE

"""

from Tkinter import *
import tkMessageBox

class TextViewer(Toplevel):
    """A simple text viewer dialog for IDLE

    """
    def __init__(self, parent, title, text, modal=True, _htest=False):
        """Show the given text in a scrollable window with a 'close' button

        If modal option set to False, user can interact with other windows,
        otherwise they will be unable to interact with other windows until
        the textview window is closed.

        _htest - bool; change box location when running htest.
        """
        Toplevel.__init__(self, parent)
        self.configure(borderwidth=5)
        # place dialog below parent if running htest
        self.geometry("=%dx%d+%d+%d" % (625, 500,
                           parent.winfo_rootx() + 10,
                           parent.winfo_rooty() + (10 if not _htest else 100)))
        #elguavas - config placeholders til config stuff completed
        self.bg = '#ffffff'
        self.fg = '#000000'

        self.CreateWidgets()
        self.title(title)
        self.protocol("WM_DELETE_WINDOW", self.Ok)
        self.parent = parent
        self.textView.focus_set()
        #key bindings for this dialog
        self.bind('<Return>',self.Ok) #dismiss dialog
        self.bind('<Escape>',self.Ok) #dismiss dialog
        self.textView.insert(0.0, text)
        self.textView.config(state=DISABLED)

        if modal:
            self.transient(parent)
            self.grab_set()
            self.wait_window()

    def CreateWidgets(self):
        frameText = Frame(self, relief=SUNKEN, height=700)
        frameButtons = Frame(self)
        self.buttonOk = Button(frameButtons, text='Close',
                               command=self.Ok, takefocus=FALSE)
        self.scrollbarView = Scrollbar(frameText, orient=VERTICAL,
                                       takefocus=FALSE, highlightthickness=0)
        self.textView = Text(frameText, wrap=WORD, highlightthickness=0,
                             fg=self.fg, bg=self.bg)
        self.scrollbarView.config(command=self.textView.yview)
        self.textView.config(yscrollcommand=self.scrollbarView.set)
        self.buttonOk.pack()
        self.scrollbarView.pack(side=RIGHT,fill=Y)
        self.textView.pack(side=LEFT,expand=TRUE,fill=BOTH)
        frameButtons.pack(side=BOTTOM,fill=X)
        frameText.pack(side=TOP,expand=TRUE,fill=BOTH)

    def Ok(self, event=None):
        self.destroy()


def view_text(parent, title, text, modal=True):
    return TextViewer(parent, title, text, modal)

def view_file(parent, title, filename, encoding=None, modal=True):
    try:
        if encoding:
            import codecs
            textFile = codecs.open(filename, 'r')
        else:
            textFile = open(filename, 'r')
    except IOError:
        tkMessageBox.showerror(title='File Load Error',
                               message='Unable to load file %r .' % filename,
                               parent=parent)
    else:
        return view_text(parent, title, textFile.read(), modal)


if __name__ == '__main__':
    import unittest
    unittest.main('idlelib.idle_test.test_textview', verbosity=2, exit=False)
    from idlelib.idle_test.htest import run
    run(TextViewer)
