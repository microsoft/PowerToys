"""CodeContext - Extension to display the block context above the edit window

Once code has scrolled off the top of a window, it can be difficult to
determine which block you are in.  This extension implements a pane at the top
of each IDLE edit window which provides block structure hints.  These hints are
the lines which contain the block opening keywords, e.g. 'if', for the
enclosing block.  The number of hint lines is determined by the numlines
variable in the CodeContext section of config-extensions.def. Lines which do
not open blocks are not shown in the context hints pane.

"""
import Tkinter
from Tkconstants import TOP, LEFT, X, W, SUNKEN
import re
from sys import maxint as INFINITY
from idlelib.configHandler import idleConf

BLOCKOPENERS = set(["class", "def", "elif", "else", "except", "finally", "for",
                    "if", "try", "while", "with"])
UPDATEINTERVAL = 100 # millisec
FONTUPDATEINTERVAL = 1000 # millisec

getspacesfirstword =\
                   lambda s, c=re.compile(r"^(\s*)(\w*)"): c.match(s).groups()

class CodeContext:
    menudefs = [('options', [('!Code Conte_xt', '<<toggle-code-context>>')])]
    context_depth = idleConf.GetOption("extensions", "CodeContext",
                                       "numlines", type="int", default=3)
    bgcolor = idleConf.GetOption("extensions", "CodeContext",
                                 "bgcolor", type="str", default="LightGray")
    fgcolor = idleConf.GetOption("extensions", "CodeContext",
                                 "fgcolor", type="str", default="Black")
    def __init__(self, editwin):
        self.editwin = editwin
        self.text = editwin.text
        self.textfont = self.text["font"]
        self.label = None
        # self.info is a list of (line number, indent level, line text, block
        # keyword) tuples providing the block structure associated with
        # self.topvisible (the linenumber of the line displayed at the top of
        # the edit window). self.info[0] is initialized as a 'dummy' line which
        # starts the toplevel 'block' of the module.
        self.info = [(0, -1, "", False)]
        self.topvisible = 1
        visible = idleConf.GetOption("extensions", "CodeContext",
                                     "visible", type="bool", default=False)
        if visible:
            self.toggle_code_context_event()
            self.editwin.setvar('<<toggle-code-context>>', True)
        # Start two update cycles, one for context lines, one for font changes.
        self.text.after(UPDATEINTERVAL, self.timer_event)
        self.text.after(FONTUPDATEINTERVAL, self.font_timer_event)

    def toggle_code_context_event(self, event=None):
        if not self.label:
            # Calculate the border width and horizontal padding required to
            # align the context with the text in the main Text widget.
            #
            # All values are passed through int(str(<value>)), since some
            # values may be pixel objects, which can't simply be added to ints.
            widgets = self.editwin.text, self.editwin.text_frame
            # Calculate the required vertical padding
            padx = 0
            for widget in widgets:
                padx += int(str( widget.pack_info()['padx'] ))
                padx += int(str( widget.cget('padx') ))
            # Calculate the required border width
            border = 0
            for widget in widgets:
                border += int(str( widget.cget('border') ))
            self.label = Tkinter.Label(self.editwin.top,
                                       text="\n" * (self.context_depth - 1),
                                       anchor=W, justify=LEFT,
                                       font=self.textfont,
                                       bg=self.bgcolor, fg=self.fgcolor,
                                       width=1, #don't request more than we get
                                       padx=padx, border=border,
                                       relief=SUNKEN)
            # Pack the label widget before and above the text_frame widget,
            # thus ensuring that it will appear directly above text_frame
            self.label.pack(side=TOP, fill=X, expand=False,
                            before=self.editwin.text_frame)
        else:
            self.label.destroy()
            self.label = None
        idleConf.SetOption("extensions", "CodeContext", "visible",
                           str(self.label is not None))
        idleConf.SaveUserCfgFiles()

    def get_line_info(self, linenum):
        """Get the line indent value, text, and any block start keyword

        If the line does not start a block, the keyword value is False.
        The indentation of empty lines (or comment lines) is INFINITY.

        """
        text = self.text.get("%d.0" % linenum, "%d.end" % linenum)
        spaces, firstword = getspacesfirstword(text)
        opener = firstword in BLOCKOPENERS and firstword
        if len(text) == len(spaces) or text[len(spaces)] == '#':
            indent = INFINITY
        else:
            indent = len(spaces)
        return indent, text, opener

    def get_context(self, new_topvisible, stopline=1, stopindent=0):
        """Get context lines, starting at new_topvisible and working backwards.

        Stop when stopline or stopindent is reached. Return a tuple of context
        data and the indent level at the top of the region inspected.

        """
        assert stopline > 0
        lines = []
        # The indentation level we are currently in:
        lastindent = INFINITY
        # For a line to be interesting, it must begin with a block opening
        # keyword, and have less indentation than lastindent.
        for linenum in xrange(new_topvisible, stopline-1, -1):
            indent, text, opener = self.get_line_info(linenum)
            if indent < lastindent:
                lastindent = indent
                if opener in ("else", "elif"):
                    # We also show the if statement
                    lastindent += 1
                if opener and linenum < new_topvisible and indent >= stopindent:
                    lines.append((linenum, indent, text, opener))
                if lastindent <= stopindent:
                    break
        lines.reverse()
        return lines, lastindent

    def update_code_context(self):
        """Update context information and lines visible in the context pane.

        """
        new_topvisible = int(self.text.index("@0,0").split('.')[0])
        if self.topvisible == new_topvisible:      # haven't scrolled
            return
        if self.topvisible < new_topvisible:       # scroll down
            lines, lastindent = self.get_context(new_topvisible,
                                                 self.topvisible)
            # retain only context info applicable to the region
            # between topvisible and new_topvisible:
            while self.info[-1][1] >= lastindent:
                del self.info[-1]
        elif self.topvisible > new_topvisible:     # scroll up
            stopindent = self.info[-1][1] + 1
            # retain only context info associated
            # with lines above new_topvisible:
            while self.info[-1][0] >= new_topvisible:
                stopindent = self.info[-1][1]
                del self.info[-1]
            lines, lastindent = self.get_context(new_topvisible,
                                                 self.info[-1][0]+1,
                                                 stopindent)
        self.info.extend(lines)
        self.topvisible = new_topvisible
        # empty lines in context pane:
        context_strings = [""] * max(0, self.context_depth - len(self.info))
        # followed by the context hint lines:
        context_strings += [x[2] for x in self.info[-self.context_depth:]]
        self.label["text"] = '\n'.join(context_strings)

    def timer_event(self):
        if self.label:
            self.update_code_context()
        self.text.after(UPDATEINTERVAL, self.timer_event)

    def font_timer_event(self):
        newtextfont = self.text["font"]
        if self.label and newtextfont != self.textfont:
            self.textfont = newtextfont
            self.label["font"] = self.textfont
        self.text.after(FONTUPDATEINTERVAL, self.font_timer_event)
