"""A CallTip window class for Tkinter/IDLE.

After ToolTip.py, which uses ideas gleaned from PySol
Used by the CallTips IDLE extension.

"""
from Tkinter import *

HIDE_VIRTUAL_EVENT_NAME = "<<calltipwindow-hide>>"
HIDE_SEQUENCES = ("<Key-Escape>", "<FocusOut>")
CHECKHIDE_VIRTUAL_EVENT_NAME = "<<calltipwindow-checkhide>>"
CHECKHIDE_SEQUENCES = ("<KeyRelease>", "<ButtonRelease>")
CHECKHIDE_TIME = 100 # miliseconds

MARK_RIGHT = "calltipwindowregion_right"

class CallTip:

    def __init__(self, widget):
        self.widget = widget
        self.tipwindow = self.label = None
        self.parenline = self.parencol = None
        self.lastline = None
        self.hideid = self.checkhideid = None
        self.checkhide_after_id = None

    def position_window(self):
        """Check if needs to reposition the window, and if so - do it."""
        curline = int(self.widget.index("insert").split('.')[0])
        if curline == self.lastline:
            return
        self.lastline = curline
        self.widget.see("insert")
        if curline == self.parenline:
            box = self.widget.bbox("%d.%d" % (self.parenline,
                                              self.parencol))
        else:
            box = self.widget.bbox("%d.0" % curline)
        if not box:
            box = list(self.widget.bbox("insert"))
            # align to left of window
            box[0] = 0
            box[2] = 0
        x = box[0] + self.widget.winfo_rootx() + 2
        y = box[1] + box[3] + self.widget.winfo_rooty()
        self.tipwindow.wm_geometry("+%d+%d" % (x, y))

    def showtip(self, text, parenleft, parenright):
        """Show the calltip, bind events which will close it and reposition it.
        """
        # Only called in CallTips, where lines are truncated
        self.text = text
        if self.tipwindow or not self.text:
            return

        self.widget.mark_set(MARK_RIGHT, parenright)
        self.parenline, self.parencol = map(
            int, self.widget.index(parenleft).split("."))

        self.tipwindow = tw = Toplevel(self.widget)
        self.position_window()
        # remove border on calltip window
        tw.wm_overrideredirect(1)
        try:
            # This command is only needed and available on Tk >= 8.4.0 for OSX
            # Without it, call tips intrude on the typing process by grabbing
            # the focus.
            tw.tk.call("::tk::unsupported::MacWindowStyle", "style", tw._w,
                       "help", "noActivates")
        except TclError:
            pass
        self.label = Label(tw, text=self.text, justify=LEFT,
                           background="#ffffe0", relief=SOLID, borderwidth=1,
                           font = self.widget['font'])
        self.label.pack()

        self.checkhideid = self.widget.bind(CHECKHIDE_VIRTUAL_EVENT_NAME,
                                            self.checkhide_event)
        for seq in CHECKHIDE_SEQUENCES:
            self.widget.event_add(CHECKHIDE_VIRTUAL_EVENT_NAME, seq)
        self.widget.after(CHECKHIDE_TIME, self.checkhide_event)
        self.hideid = self.widget.bind(HIDE_VIRTUAL_EVENT_NAME,
                                       self.hide_event)
        for seq in HIDE_SEQUENCES:
            self.widget.event_add(HIDE_VIRTUAL_EVENT_NAME, seq)

    def checkhide_event(self, event=None):
        if not self.tipwindow:
            # If the event was triggered by the same event that unbinded
            # this function, the function will be called nevertheless,
            # so do nothing in this case.
            return
        curline, curcol = map(int, self.widget.index("insert").split('.'))
        if curline < self.parenline or \
           (curline == self.parenline and curcol <= self.parencol) or \
           self.widget.compare("insert", ">", MARK_RIGHT):
            self.hidetip()
        else:
            self.position_window()
            if self.checkhide_after_id is not None:
                self.widget.after_cancel(self.checkhide_after_id)
            self.checkhide_after_id = \
                self.widget.after(CHECKHIDE_TIME, self.checkhide_event)

    def hide_event(self, event):
        if not self.tipwindow:
            # See the explanation in checkhide_event.
            return
        self.hidetip()

    def hidetip(self):
        if not self.tipwindow:
            return

        for seq in CHECKHIDE_SEQUENCES:
            self.widget.event_delete(CHECKHIDE_VIRTUAL_EVENT_NAME, seq)
        self.widget.unbind(CHECKHIDE_VIRTUAL_EVENT_NAME, self.checkhideid)
        self.checkhideid = None
        for seq in HIDE_SEQUENCES:
            self.widget.event_delete(HIDE_VIRTUAL_EVENT_NAME, seq)
        self.widget.unbind(HIDE_VIRTUAL_EVENT_NAME, self.hideid)
        self.hideid = None

        self.label.destroy()
        self.label = None
        self.tipwindow.destroy()
        self.tipwindow = None

        self.widget.mark_unset(MARK_RIGHT)
        self.parenline = self.parencol = self.lastline = None

    def is_active(self):
        return bool(self.tipwindow)


def _calltip_window(parent):
    root = Tk()
    root.title("Test calltips")
    width, height, x, y = list(map(int, re.split('[x+]', parent.geometry())))
    root.geometry("+%d+%d"%(x, y + 150))

    class MyEditWin: # comparenceptually an editor_window
        def __init__(self):
            text = self.text = Text(root)
            text.pack(side=LEFT, fill=BOTH, expand=1)
            text.insert("insert", "string.split")
            root.update()
            self.calltip = CallTip(text)

            text.event_add("<<calltip-show>>", "(")
            text.event_add("<<calltip-hide>>", ")")
            text.bind("<<calltip-show>>", self.calltip_show)
            text.bind("<<calltip-hide>>", self.calltip_hide)

            text.focus_set()
            root.mainloop()

        def calltip_show(self, event):
            self.calltip.showtip("Hello world", "insert", "end")

        def calltip_hide(self, event):
            self.calltip.hidetip()

    editwin = MyEditWin()

if __name__=='__main__':
    from idlelib.idle_test.htest import run
    run(_calltip_window)
