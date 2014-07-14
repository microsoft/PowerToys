import sys
import os
from platform import python_version
import re
import imp
from Tkinter import *
import tkSimpleDialog
import tkMessageBox
import webbrowser

from idlelib.MultiCall import MultiCallCreator
from idlelib import idlever
from idlelib import WindowList
from idlelib import SearchDialog
from idlelib import GrepDialog
from idlelib import ReplaceDialog
from idlelib import PyParse
from idlelib.configHandler import idleConf
from idlelib import aboutDialog, textView, configDialog
from idlelib import macosxSupport

# The default tab setting for a Text widget, in average-width characters.
TK_TABWIDTH_DEFAULT = 8

def _sphinx_version():
    "Format sys.version_info to produce the Sphinx version string used to install the chm docs"
    major, minor, micro, level, serial = sys.version_info
    release = '%s%s' % (major, minor)
    if micro:
        release += '%s' % (micro,)
    if level == 'candidate':
        release += 'rc%s' % (serial,)
    elif level != 'final':
        release += '%s%s' % (level[0], serial)
    return release

def _find_module(fullname, path=None):
    """Version of imp.find_module() that handles hierarchical module names"""

    file = None
    for tgt in fullname.split('.'):
        if file is not None:
            file.close()            # close intermediate files
        (file, filename, descr) = imp.find_module(tgt, path)
        if descr[2] == imp.PY_SOURCE:
            break                   # find but not load the source file
        module = imp.load_module(tgt, file, filename, descr)
        try:
            path = module.__path__
        except AttributeError:
            raise ImportError, 'No source for module ' + module.__name__
    if descr[2] != imp.PY_SOURCE:
        # If all of the above fails and didn't raise an exception,fallback
        # to a straight import which can find __init__.py in a package.
        m = __import__(fullname)
        try:
            filename = m.__file__
        except AttributeError:
            pass
        else:
            file = None
            base, ext = os.path.splitext(filename)
            if ext == '.pyc':
                ext = '.py'
            filename = base + ext
            descr = filename, None, imp.PY_SOURCE
    return file, filename, descr


class HelpDialog(object):

    def __init__(self):
        self.parent = None      # parent of help window
        self.dlg = None         # the help window iteself

    def display(self, parent, near=None):
        """ Display the help dialog.

            parent - parent widget for the help window

            near - a Toplevel widget (e.g. EditorWindow or PyShell)
                   to use as a reference for placing the help window
        """
        if self.dlg is None:
            self.show_dialog(parent)
        if near:
            self.nearwindow(near)

    def show_dialog(self, parent):
        self.parent = parent
        fn=os.path.join(os.path.abspath(os.path.dirname(__file__)),'help.txt')
        self.dlg = dlg = textView.view_file(parent,'Help',fn, modal=False)
        dlg.bind('<Destroy>', self.destroy, '+')

    def nearwindow(self, near):
        # Place the help dialog near the window specified by parent.
        # Note - this may not reposition the window in Metacity
        #  if "/apps/metacity/general/disable_workarounds" is enabled
        dlg = self.dlg
        geom = (near.winfo_rootx() + 10, near.winfo_rooty() + 10)
        dlg.withdraw()
        dlg.geometry("=+%d+%d" % geom)
        dlg.deiconify()
        dlg.lift()

    def destroy(self, ev=None):
        self.dlg = None
        self.parent = None

helpDialog = HelpDialog()  # singleton instance
def _help_dialog(parent):  # wrapper for htest
    helpDialog.show_dialog(parent)


class EditorWindow(object):
    from idlelib.Percolator import Percolator
    from idlelib.ColorDelegator import ColorDelegator
    from idlelib.UndoDelegator import UndoDelegator
    from idlelib.IOBinding import IOBinding, filesystemencoding, encoding
    from idlelib import Bindings
    from Tkinter import Toplevel
    from idlelib.MultiStatusBar import MultiStatusBar

    help_url = None

    def __init__(self, flist=None, filename=None, key=None, root=None):
        if EditorWindow.help_url is None:
            dochome =  os.path.join(sys.prefix, 'Doc', 'index.html')
            if sys.platform.count('linux'):
                # look for html docs in a couple of standard places
                pyver = 'python-docs-' + '%s.%s.%s' % sys.version_info[:3]
                if os.path.isdir('/var/www/html/python/'):  # "python2" rpm
                    dochome = '/var/www/html/python/index.html'
                else:
                    basepath = '/usr/share/doc/'  # standard location
                    dochome = os.path.join(basepath, pyver,
                                           'Doc', 'index.html')
            elif sys.platform[:3] == 'win':
                chmfile = os.path.join(sys.prefix, 'Doc',
                                       'Python%s.chm' % _sphinx_version())
                if os.path.isfile(chmfile):
                    dochome = chmfile
            elif sys.platform == 'darwin':
                # documentation may be stored inside a python framework
                dochome = os.path.join(sys.prefix,
                        'Resources/English.lproj/Documentation/index.html')
            dochome = os.path.normpath(dochome)
            if os.path.isfile(dochome):
                EditorWindow.help_url = dochome
                if sys.platform == 'darwin':
                    # Safari requires real file:-URLs
                    EditorWindow.help_url = 'file://' + EditorWindow.help_url
            else:
                EditorWindow.help_url = "http://docs.python.org/%d.%d" % sys.version_info[:2]
        currentTheme=idleConf.CurrentTheme()
        self.flist = flist
        root = root or flist.root
        self.root = root
        try:
            sys.ps1
        except AttributeError:
            sys.ps1 = '>>> '
        self.menubar = Menu(root)
        self.top = top = WindowList.ListedToplevel(root, menu=self.menubar)
        if flist:
            self.tkinter_vars = flist.vars
            #self.top.instance_dict makes flist.inversedict available to
            #configDialog.py so it can access all EditorWindow instances
            self.top.instance_dict = flist.inversedict
        else:
            self.tkinter_vars = {}  # keys: Tkinter event names
                                    # values: Tkinter variable instances
            self.top.instance_dict = {}
        self.recent_files_path = os.path.join(idleConf.GetUserCfgDir(),
                'recent-files.lst')
        self.text_frame = text_frame = Frame(top)
        self.vbar = vbar = Scrollbar(text_frame, name='vbar')
        self.width = idleConf.GetOption('main','EditorWindow','width', type='int')
        text_options = {
                'name': 'text',
                'padx': 5,
                'wrap': 'none',
                'width': self.width,
                'height': idleConf.GetOption('main', 'EditorWindow', 'height', type='int')}
        if TkVersion >= 8.5:
            # Starting with tk 8.5 we have to set the new tabstyle option
            # to 'wordprocessor' to achieve the same display of tabs as in
            # older tk versions.
            text_options['tabstyle'] = 'wordprocessor'
        self.text = text = MultiCallCreator(Text)(text_frame, **text_options)
        self.top.focused_widget = self.text

        self.createmenubar()
        self.apply_bindings()

        self.top.protocol("WM_DELETE_WINDOW", self.close)
        self.top.bind("<<close-window>>", self.close_event)
        if macosxSupport.isAquaTk():
            # Command-W on editorwindows doesn't work without this.
            text.bind('<<close-window>>', self.close_event)
            # Some OS X systems have only one mouse button,
            # so use control-click for pulldown menus there.
            #  (Note, AquaTk defines <2> as the right button if
            #   present and the Tk Text widget already binds <2>.)
            text.bind("<Control-Button-1>",self.right_menu_event)
        else:
            # Elsewhere, use right-click for pulldown menus.
            text.bind("<3>",self.right_menu_event)
        text.bind("<<cut>>", self.cut)
        text.bind("<<copy>>", self.copy)
        text.bind("<<paste>>", self.paste)
        text.bind("<<center-insert>>", self.center_insert_event)
        text.bind("<<help>>", self.help_dialog)
        text.bind("<<python-docs>>", self.python_docs)
        text.bind("<<about-idle>>", self.about_dialog)
        text.bind("<<open-config-dialog>>", self.config_dialog)
        text.bind("<<open-module>>", self.open_module)
        text.bind("<<do-nothing>>", lambda event: "break")
        text.bind("<<select-all>>", self.select_all)
        text.bind("<<remove-selection>>", self.remove_selection)
        text.bind("<<find>>", self.find_event)
        text.bind("<<find-again>>", self.find_again_event)
        text.bind("<<find-in-files>>", self.find_in_files_event)
        text.bind("<<find-selection>>", self.find_selection_event)
        text.bind("<<replace>>", self.replace_event)
        text.bind("<<goto-line>>", self.goto_line_event)
        text.bind("<<smart-backspace>>",self.smart_backspace_event)
        text.bind("<<newline-and-indent>>",self.newline_and_indent_event)
        text.bind("<<smart-indent>>",self.smart_indent_event)
        text.bind("<<indent-region>>",self.indent_region_event)
        text.bind("<<dedent-region>>",self.dedent_region_event)
        text.bind("<<comment-region>>",self.comment_region_event)
        text.bind("<<uncomment-region>>",self.uncomment_region_event)
        text.bind("<<tabify-region>>",self.tabify_region_event)
        text.bind("<<untabify-region>>",self.untabify_region_event)
        text.bind("<<toggle-tabs>>",self.toggle_tabs_event)
        text.bind("<<change-indentwidth>>",self.change_indentwidth_event)
        text.bind("<Left>", self.move_at_edge_if_selection(0))
        text.bind("<Right>", self.move_at_edge_if_selection(1))
        text.bind("<<del-word-left>>", self.del_word_left)
        text.bind("<<del-word-right>>", self.del_word_right)
        text.bind("<<beginning-of-line>>", self.home_callback)

        if flist:
            flist.inversedict[self] = key
            if key:
                flist.dict[key] = self
            text.bind("<<open-new-window>>", self.new_callback)
            text.bind("<<close-all-windows>>", self.flist.close_all_callback)
            text.bind("<<open-class-browser>>", self.open_class_browser)
            text.bind("<<open-path-browser>>", self.open_path_browser)

        self.set_status_bar()
        vbar['command'] = text.yview
        vbar.pack(side=RIGHT, fill=Y)
        text['yscrollcommand'] = vbar.set
        fontWeight = 'normal'
        if idleConf.GetOption('main', 'EditorWindow', 'font-bold', type='bool'):
            fontWeight='bold'
        text.config(font=(idleConf.GetOption('main', 'EditorWindow', 'font'),
                          idleConf.GetOption('main', 'EditorWindow',
                                             'font-size', type='int'),
                          fontWeight))
        text_frame.pack(side=LEFT, fill=BOTH, expand=1)
        text.pack(side=TOP, fill=BOTH, expand=1)
        text.focus_set()

        # usetabs true  -> literal tab characters are used by indent and
        #                  dedent cmds, possibly mixed with spaces if
        #                  indentwidth is not a multiple of tabwidth,
        #                  which will cause Tabnanny to nag!
        #         false -> tab characters are converted to spaces by indent
        #                  and dedent cmds, and ditto TAB keystrokes
        # Although use-spaces=0 can be configured manually in config-main.def,
        # configuration of tabs v. spaces is not supported in the configuration
        # dialog.  IDLE promotes the preferred Python indentation: use spaces!
        usespaces = idleConf.GetOption('main', 'Indent', 'use-spaces', type='bool')
        self.usetabs = not usespaces

        # tabwidth is the display width of a literal tab character.
        # CAUTION:  telling Tk to use anything other than its default
        # tab setting causes it to use an entirely different tabbing algorithm,
        # treating tab stops as fixed distances from the left margin.
        # Nobody expects this, so for now tabwidth should never be changed.
        self.tabwidth = 8    # must remain 8 until Tk is fixed.

        # indentwidth is the number of screen characters per indent level.
        # The recommended Python indentation is four spaces.
        self.indentwidth = self.tabwidth
        self.set_notabs_indentwidth()

        # If context_use_ps1 is true, parsing searches back for a ps1 line;
        # else searches for a popular (if, def, ...) Python stmt.
        self.context_use_ps1 = False

        # When searching backwards for a reliable place to begin parsing,
        # first start num_context_lines[0] lines back, then
        # num_context_lines[1] lines back if that didn't work, and so on.
        # The last value should be huge (larger than the # of lines in a
        # conceivable file).
        # Making the initial values larger slows things down more often.
        self.num_context_lines = 50, 500, 5000000

        self.per = per = self.Percolator(text)

        self.undo = undo = self.UndoDelegator()
        per.insertfilter(undo)
        text.undo_block_start = undo.undo_block_start
        text.undo_block_stop = undo.undo_block_stop
        undo.set_saved_change_hook(self.saved_change_hook)

        # IOBinding implements file I/O and printing functionality
        self.io = io = self.IOBinding(self)
        io.set_filename_change_hook(self.filename_change_hook)

        # Create the recent files submenu
        self.recent_files_menu = Menu(self.menubar)
        self.menudict['file'].insert_cascade(3, label='Recent Files',
                                             underline=0,
                                             menu=self.recent_files_menu)
        self.update_recent_files_list()

        self.color = None # initialized below in self.ResetColorizer
        if filename:
            if os.path.exists(filename) and not os.path.isdir(filename):
                io.loadfile(filename)
            else:
                io.set_filename(filename)
        self.ResetColorizer()
        self.saved_change_hook()

        self.set_indentation_params(self.ispythonsource(filename))

        self.load_extensions()

        menu = self.menudict.get('windows')
        if menu:
            end = menu.index("end")
            if end is None:
                end = -1
            if end >= 0:
                menu.add_separator()
                end = end + 1
            self.wmenu_end = end
            WindowList.register_callback(self.postwindowsmenu)

        # Some abstractions so IDLE extensions are cross-IDE
        self.askyesno = tkMessageBox.askyesno
        self.askinteger = tkSimpleDialog.askinteger
        self.showerror = tkMessageBox.showerror

        self._highlight_workaround()  # Fix selection tags on Windows

    def _highlight_workaround(self):
        # On Windows, Tk removes painting of the selection
        # tags which is different behavior than on Linux and Mac.
        # See issue14146 for more information.
        if not sys.platform.startswith('win'):
            return

        text = self.text
        text.event_add("<<Highlight-FocusOut>>", "<FocusOut>")
        text.event_add("<<Highlight-FocusIn>>", "<FocusIn>")
        def highlight_fix(focus):
            sel_range = text.tag_ranges("sel")
            if sel_range:
                if focus == 'out':
                    HILITE_CONFIG = idleConf.GetHighlight(
                            idleConf.CurrentTheme(), 'hilite')
                    text.tag_config("sel_fix", HILITE_CONFIG)
                    text.tag_raise("sel_fix")
                    text.tag_add("sel_fix", *sel_range)
                elif focus == 'in':
                    text.tag_remove("sel_fix", "1.0", "end")

        text.bind("<<Highlight-FocusOut>>",
                lambda ev: highlight_fix("out"))
        text.bind("<<Highlight-FocusIn>>",
                lambda ev: highlight_fix("in"))


    def _filename_to_unicode(self, filename):
        """convert filename to unicode in order to display it in Tk"""
        if isinstance(filename, unicode) or not filename:
            return filename
        else:
            try:
                return filename.decode(self.filesystemencoding)
            except UnicodeDecodeError:
                # XXX
                try:
                    return filename.decode(self.encoding)
                except UnicodeDecodeError:
                    # byte-to-byte conversion
                    return filename.decode('iso8859-1')

    def new_callback(self, event):
        dirname, basename = self.io.defaultfilename()
        self.flist.new(dirname)
        return "break"

    def home_callback(self, event):
        if (event.state & 4) != 0 and event.keysym == "Home":
            # state&4==Control. If <Control-Home>, use the Tk binding.
            return
        if self.text.index("iomark") and \
           self.text.compare("iomark", "<=", "insert lineend") and \
           self.text.compare("insert linestart", "<=", "iomark"):
            # In Shell on input line, go to just after prompt
            insertpt = int(self.text.index("iomark").split(".")[1])
        else:
            line = self.text.get("insert linestart", "insert lineend")
            for insertpt in xrange(len(line)):
                if line[insertpt] not in (' ','\t'):
                    break
            else:
                insertpt=len(line)
        lineat = int(self.text.index("insert").split('.')[1])
        if insertpt == lineat:
            insertpt = 0
        dest = "insert linestart+"+str(insertpt)+"c"
        if (event.state&1) == 0:
            # shift was not pressed
            self.text.tag_remove("sel", "1.0", "end")
        else:
            if not self.text.index("sel.first"):
                self.text.mark_set("my_anchor", "insert")  # there was no previous selection
            else:
                if self.text.compare(self.text.index("sel.first"), "<", self.text.index("insert")):
                    self.text.mark_set("my_anchor", "sel.first") # extend back
                else:
                    self.text.mark_set("my_anchor", "sel.last") # extend forward
            first = self.text.index(dest)
            last = self.text.index("my_anchor")
            if self.text.compare(first,">",last):
                first,last = last,first
            self.text.tag_remove("sel", "1.0", "end")
            self.text.tag_add("sel", first, last)
        self.text.mark_set("insert", dest)
        self.text.see("insert")
        return "break"

    def set_status_bar(self):
        self.status_bar = self.MultiStatusBar(self.top)
        if sys.platform == "darwin":
            # Insert some padding to avoid obscuring some of the statusbar
            # by the resize widget.
            self.status_bar.set_label('_padding1', '    ', side=RIGHT)
        self.status_bar.set_label('column', 'Col: ?', side=RIGHT)
        self.status_bar.set_label('line', 'Ln: ?', side=RIGHT)
        self.status_bar.pack(side=BOTTOM, fill=X)
        self.text.bind("<<set-line-and-column>>", self.set_line_and_column)
        self.text.event_add("<<set-line-and-column>>",
                            "<KeyRelease>", "<ButtonRelease>")
        self.text.after_idle(self.set_line_and_column)

    def set_line_and_column(self, event=None):
        line, column = self.text.index(INSERT).split('.')
        self.status_bar.set_label('column', 'Col: %s' % column)
        self.status_bar.set_label('line', 'Ln: %s' % line)

    menu_specs = [
        ("file", "_File"),
        ("edit", "_Edit"),
        ("format", "F_ormat"),
        ("run", "_Run"),
        ("options", "_Options"),
        ("windows", "_Windows"),
        ("help", "_Help"),
    ]

    if sys.platform == "darwin":
        menu_specs[-2] = ("windows", "_Window")


    def createmenubar(self):
        mbar = self.menubar
        self.menudict = menudict = {}
        for name, label in self.menu_specs:
            underline, label = prepstr(label)
            menudict[name] = menu = Menu(mbar, name=name)
            mbar.add_cascade(label=label, menu=menu, underline=underline)

        if macosxSupport.isCarbonTk():
            # Insert the application menu
            menudict['application'] = menu = Menu(mbar, name='apple')
            mbar.add_cascade(label='IDLE', menu=menu)

        self.fill_menus()
        self.base_helpmenu_length = self.menudict['help'].index(END)
        self.reset_help_menu_entries()

    def postwindowsmenu(self):
        # Only called when Windows menu exists
        menu = self.menudict['windows']
        end = menu.index("end")
        if end is None:
            end = -1
        if end > self.wmenu_end:
            menu.delete(self.wmenu_end+1, end)
        WindowList.add_windows_to_menu(menu)

    rmenu = None

    def right_menu_event(self, event):
        self.text.mark_set("insert", "@%d,%d" % (event.x, event.y))
        if not self.rmenu:
            self.make_rmenu()
        rmenu = self.rmenu
        self.event = event
        iswin = sys.platform[:3] == 'win'
        if iswin:
            self.text.config(cursor="arrow")

        for item in self.rmenu_specs:
            try:
                label, eventname, verify_state = item
            except ValueError: # see issue1207589
                continue

            if verify_state is None:
                continue
            state = getattr(self, verify_state)()
            rmenu.entryconfigure(label, state=state)

        rmenu.tk_popup(event.x_root, event.y_root)
        if iswin:
            self.text.config(cursor="ibeam")

    rmenu_specs = [
        # ("Label", "<<virtual-event>>", "statefuncname"), ...
        ("Close", "<<close-window>>", None), # Example
    ]

    def make_rmenu(self):
        rmenu = Menu(self.text, tearoff=0)
        for item in self.rmenu_specs:
            label, eventname = item[0], item[1]
            if label is not None:
                def command(text=self.text, eventname=eventname):
                    text.event_generate(eventname)
                rmenu.add_command(label=label, command=command)
            else:
                rmenu.add_separator()
        self.rmenu = rmenu

    def rmenu_check_cut(self):
        return self.rmenu_check_copy()

    def rmenu_check_copy(self):
        try:
            indx = self.text.index('sel.first')
        except TclError:
            return 'disabled'
        else:
            return 'normal' if indx else 'disabled'

    def rmenu_check_paste(self):
        try:
            self.text.tk.call('tk::GetSelection', self.text, 'CLIPBOARD')
        except TclError:
            return 'disabled'
        else:
            return 'normal'

    def about_dialog(self, event=None):
        aboutDialog.AboutDialog(self.top,'About IDLE')

    def config_dialog(self, event=None):
        configDialog.ConfigDialog(self.top,'Settings')

    def help_dialog(self, event=None):
        if self.root:
            parent = self.root
        else:
            parent = self.top
        helpDialog.display(parent, near=self.top)

    def python_docs(self, event=None):
        if sys.platform[:3] == 'win':
            try:
                os.startfile(self.help_url)
            except WindowsError as why:
                tkMessageBox.showerror(title='Document Start Failure',
                    message=str(why), parent=self.text)
        else:
            webbrowser.open(self.help_url)
        return "break"

    def cut(self,event):
        self.text.event_generate("<<Cut>>")
        return "break"

    def copy(self,event):
        if not self.text.tag_ranges("sel"):
            # There is no selection, so do nothing and maybe interrupt.
            return
        self.text.event_generate("<<Copy>>")
        return "break"

    def paste(self,event):
        self.text.event_generate("<<Paste>>")
        self.text.see("insert")
        return "break"

    def select_all(self, event=None):
        self.text.tag_add("sel", "1.0", "end-1c")
        self.text.mark_set("insert", "1.0")
        self.text.see("insert")
        return "break"

    def remove_selection(self, event=None):
        self.text.tag_remove("sel", "1.0", "end")
        self.text.see("insert")

    def move_at_edge_if_selection(self, edge_index):
        """Cursor move begins at start or end of selection

        When a left/right cursor key is pressed create and return to Tkinter a
        function which causes a cursor move from the associated edge of the
        selection.

        """
        self_text_index = self.text.index
        self_text_mark_set = self.text.mark_set
        edges_table = ("sel.first+1c", "sel.last-1c")
        def move_at_edge(event):
            if (event.state & 5) == 0: # no shift(==1) or control(==4) pressed
                try:
                    self_text_index("sel.first")
                    self_text_mark_set("insert", edges_table[edge_index])
                except TclError:
                    pass
        return move_at_edge

    def del_word_left(self, event):
        self.text.event_generate('<Meta-Delete>')
        return "break"

    def del_word_right(self, event):
        self.text.event_generate('<Meta-d>')
        return "break"

    def find_event(self, event):
        SearchDialog.find(self.text)
        return "break"

    def find_again_event(self, event):
        SearchDialog.find_again(self.text)
        return "break"

    def find_selection_event(self, event):
        SearchDialog.find_selection(self.text)
        return "break"

    def find_in_files_event(self, event):
        GrepDialog.grep(self.text, self.io, self.flist)
        return "break"

    def replace_event(self, event):
        ReplaceDialog.replace(self.text)
        return "break"

    def goto_line_event(self, event):
        text = self.text
        lineno = tkSimpleDialog.askinteger("Goto",
                "Go to line number:",parent=text)
        if lineno is None:
            return "break"
        if lineno <= 0:
            text.bell()
            return "break"
        text.mark_set("insert", "%d.0" % lineno)
        text.see("insert")

    def open_module(self, event=None):
        # XXX Shouldn't this be in IOBinding or in FileList?
        try:
            name = self.text.get("sel.first", "sel.last")
        except TclError:
            name = ""
        else:
            name = name.strip()
        name = tkSimpleDialog.askstring("Module",
                 "Enter the name of a Python module\n"
                 "to search on sys.path and open:",
                 parent=self.text, initialvalue=name)
        if name:
            name = name.strip()
        if not name:
            return
        # XXX Ought to insert current file's directory in front of path
        try:
            (f, file, (suffix, mode, type)) = _find_module(name)
        except (NameError, ImportError) as msg:
            tkMessageBox.showerror("Import error", str(msg), parent=self.text)
            return
        if type != imp.PY_SOURCE:
            tkMessageBox.showerror("Unsupported type",
                "%s is not a source module" % name, parent=self.text)
            return
        if f:
            f.close()
        if self.flist:
            self.flist.open(file)
        else:
            self.io.loadfile(file)

    def open_class_browser(self, event=None):
        filename = self.io.filename
        if not filename:
            tkMessageBox.showerror(
                "No filename",
                "This buffer has no associated filename",
                master=self.text)
            self.text.focus_set()
            return None
        head, tail = os.path.split(filename)
        base, ext = os.path.splitext(tail)
        from idlelib import ClassBrowser
        ClassBrowser.ClassBrowser(self.flist, base, [head])

    def open_path_browser(self, event=None):
        from idlelib import PathBrowser
        PathBrowser.PathBrowser(self.flist)

    def gotoline(self, lineno):
        if lineno is not None and lineno > 0:
            self.text.mark_set("insert", "%d.0" % lineno)
            self.text.tag_remove("sel", "1.0", "end")
            self.text.tag_add("sel", "insert", "insert +1l")
            self.center()

    def ispythonsource(self, filename):
        if not filename or os.path.isdir(filename):
            return True
        base, ext = os.path.splitext(os.path.basename(filename))
        if os.path.normcase(ext) in (".py", ".pyw"):
            return True
        try:
            f = open(filename)
            line = f.readline()
            f.close()
        except IOError:
            return False
        return line.startswith('#!') and line.find('python') >= 0

    def close_hook(self):
        if self.flist:
            self.flist.unregister_maybe_terminate(self)
            self.flist = None

    def set_close_hook(self, close_hook):
        self.close_hook = close_hook

    def filename_change_hook(self):
        if self.flist:
            self.flist.filename_changed_edit(self)
        self.saved_change_hook()
        self.top.update_windowlist_registry(self)
        self.ResetColorizer()

    def _addcolorizer(self):
        if self.color:
            return
        if self.ispythonsource(self.io.filename):
            self.color = self.ColorDelegator()
        # can add more colorizers here...
        if self.color:
            self.per.removefilter(self.undo)
            self.per.insertfilter(self.color)
            self.per.insertfilter(self.undo)

    def _rmcolorizer(self):
        if not self.color:
            return
        self.color.removecolors()
        self.per.removefilter(self.color)
        self.color = None

    def ResetColorizer(self):
        "Update the colour theme"
        # Called from self.filename_change_hook and from configDialog.py
        self._rmcolorizer()
        self._addcolorizer()
        theme = idleConf.GetOption('main','Theme','name')
        normal_colors = idleConf.GetHighlight(theme, 'normal')
        cursor_color = idleConf.GetHighlight(theme, 'cursor', fgBg='fg')
        select_colors = idleConf.GetHighlight(theme, 'hilite')
        self.text.config(
            foreground=normal_colors['foreground'],
            background=normal_colors['background'],
            insertbackground=cursor_color,
            selectforeground=select_colors['foreground'],
            selectbackground=select_colors['background'],
            )

    def ResetFont(self):
        "Update the text widgets' font if it is changed"
        # Called from configDialog.py
        fontWeight='normal'
        if idleConf.GetOption('main','EditorWindow','font-bold',type='bool'):
            fontWeight='bold'
        self.text.config(font=(idleConf.GetOption('main','EditorWindow','font'),
                idleConf.GetOption('main','EditorWindow','font-size',
                                   type='int'),
                fontWeight))

    def RemoveKeybindings(self):
        "Remove the keybindings before they are changed."
        # Called from configDialog.py
        self.Bindings.default_keydefs = keydefs = idleConf.GetCurrentKeySet()
        for event, keylist in keydefs.items():
            self.text.event_delete(event, *keylist)
        for extensionName in self.get_standard_extension_names():
            xkeydefs = idleConf.GetExtensionBindings(extensionName)
            if xkeydefs:
                for event, keylist in xkeydefs.items():
                    self.text.event_delete(event, *keylist)

    def ApplyKeybindings(self):
        "Update the keybindings after they are changed"
        # Called from configDialog.py
        self.Bindings.default_keydefs = keydefs = idleConf.GetCurrentKeySet()
        self.apply_bindings()
        for extensionName in self.get_standard_extension_names():
            xkeydefs = idleConf.GetExtensionBindings(extensionName)
            if xkeydefs:
                self.apply_bindings(xkeydefs)
        #update menu accelerators
        menuEventDict = {}
        for menu in self.Bindings.menudefs:
            menuEventDict[menu[0]] = {}
            for item in menu[1]:
                if item:
                    menuEventDict[menu[0]][prepstr(item[0])[1]] = item[1]
        for menubarItem in self.menudict.keys():
            menu = self.menudict[menubarItem]
            end = menu.index(END)
            if end is None:
                # Skip empty menus
                continue
            end += 1
            for index in range(0, end):
                if menu.type(index) == 'command':
                    accel = menu.entrycget(index, 'accelerator')
                    if accel:
                        itemName = menu.entrycget(index, 'label')
                        event = ''
                        if menubarItem in menuEventDict:
                            if itemName in menuEventDict[menubarItem]:
                                event = menuEventDict[menubarItem][itemName]
                        if event:
                            accel = get_accelerator(keydefs, event)
                            menu.entryconfig(index, accelerator=accel)

    def set_notabs_indentwidth(self):
        "Update the indentwidth if changed and not using tabs in this window"
        # Called from configDialog.py
        if not self.usetabs:
            self.indentwidth = idleConf.GetOption('main', 'Indent','num-spaces',
                                                  type='int')

    def reset_help_menu_entries(self):
        "Update the additional help entries on the Help menu"
        help_list = idleConf.GetAllExtraHelpSourcesList()
        helpmenu = self.menudict['help']
        # first delete the extra help entries, if any
        helpmenu_length = helpmenu.index(END)
        if helpmenu_length > self.base_helpmenu_length:
            helpmenu.delete((self.base_helpmenu_length + 1), helpmenu_length)
        # then rebuild them
        if help_list:
            helpmenu.add_separator()
            for entry in help_list:
                cmd = self.__extra_help_callback(entry[1])
                helpmenu.add_command(label=entry[0], command=cmd)
        # and update the menu dictionary
        self.menudict['help'] = helpmenu

    def __extra_help_callback(self, helpfile):
        "Create a callback with the helpfile value frozen at definition time"
        def display_extra_help(helpfile=helpfile):
            if not helpfile.startswith(('www', 'http')):
                helpfile = os.path.normpath(helpfile)
            if sys.platform[:3] == 'win':
                try:
                    os.startfile(helpfile)
                except WindowsError as why:
                    tkMessageBox.showerror(title='Document Start Failure',
                        message=str(why), parent=self.text)
            else:
                webbrowser.open(helpfile)
        return display_extra_help

    def update_recent_files_list(self, new_file=None):
        "Load and update the recent files list and menus"
        rf_list = []
        if os.path.exists(self.recent_files_path):
            with  open(self.recent_files_path, 'r') as rf_list_file:
                rf_list = rf_list_file.readlines()
        if new_file:
            new_file = os.path.abspath(new_file) + '\n'
            if new_file in rf_list:
                rf_list.remove(new_file)  # move to top
            rf_list.insert(0, new_file)
        # clean and save the recent files list
        bad_paths = []
        for path in rf_list:
            if '\0' in path or not os.path.exists(path[0:-1]):
                bad_paths.append(path)
        rf_list = [path for path in rf_list if path not in bad_paths]
        ulchars = "1234567890ABCDEFGHIJK"
        rf_list = rf_list[0:len(ulchars)]
        try:
            with open(self.recent_files_path, 'w') as rf_file:
                rf_file.writelines(rf_list)
        except IOError as err:
            if not getattr(self.root, "recentfilelist_error_displayed", False):
                self.root.recentfilelist_error_displayed = True
                tkMessageBox.showerror(title='IDLE Error',
                    message='Unable to update Recent Files list:\n%s'
                        % str(err),
                    parent=self.text)
        # for each edit window instance, construct the recent files menu
        for instance in self.top.instance_dict.keys():
            menu = instance.recent_files_menu
            menu.delete(0, END)  # clear, and rebuild:
            for i, file_name in enumerate(rf_list):
                file_name = file_name.rstrip()  # zap \n
                # make unicode string to display non-ASCII chars correctly
                ufile_name = self._filename_to_unicode(file_name)
                callback = instance.__recent_file_callback(file_name)
                menu.add_command(label=ulchars[i] + " " + ufile_name,
                                 command=callback,
                                 underline=0)

    def __recent_file_callback(self, file_name):
        def open_recent_file(fn_closure=file_name):
            self.io.open(editFile=fn_closure)
        return open_recent_file

    def saved_change_hook(self):
        short = self.short_title()
        long = self.long_title()
        if short and long:
            title = short + " - " + long
        elif short:
            title = short
        elif long:
            title = long
        else:
            title = "Untitled"
        icon = short or long or title
        if not self.get_saved():
            title = "*%s*" % title
            icon = "*%s" % icon
        self.top.wm_title(title)
        self.top.wm_iconname(icon)

    def get_saved(self):
        return self.undo.get_saved()

    def set_saved(self, flag):
        self.undo.set_saved(flag)

    def reset_undo(self):
        self.undo.reset_undo()

    def short_title(self):
        pyversion = "Python " + python_version() + ": "
        filename = self.io.filename
        if filename:
            filename = os.path.basename(filename)
        else:
            filename = "Untitled"
        # return unicode string to display non-ASCII chars correctly
        return pyversion + self._filename_to_unicode(filename)

    def long_title(self):
        # return unicode string to display non-ASCII chars correctly
        return self._filename_to_unicode(self.io.filename or "")

    def center_insert_event(self, event):
        self.center()

    def center(self, mark="insert"):
        text = self.text
        top, bot = self.getwindowlines()
        lineno = self.getlineno(mark)
        height = bot - top
        newtop = max(1, lineno - height//2)
        text.yview(float(newtop))

    def getwindowlines(self):
        text = self.text
        top = self.getlineno("@0,0")
        bot = self.getlineno("@0,65535")
        if top == bot and text.winfo_height() == 1:
            # Geometry manager hasn't run yet
            height = int(text['height'])
            bot = top + height - 1
        return top, bot

    def getlineno(self, mark="insert"):
        text = self.text
        return int(float(text.index(mark)))

    def get_geometry(self):
        "Return (width, height, x, y)"
        geom = self.top.wm_geometry()
        m = re.match(r"(\d+)x(\d+)\+(-?\d+)\+(-?\d+)", geom)
        tuple = (map(int, m.groups()))
        return tuple

    def close_event(self, event):
        self.close()

    def maybesave(self):
        if self.io:
            if not self.get_saved():
                if self.top.state()!='normal':
                    self.top.deiconify()
                self.top.lower()
                self.top.lift()
            return self.io.maybesave()

    def close(self):
        reply = self.maybesave()
        if str(reply) != "cancel":
            self._close()
        return reply

    def _close(self):
        if self.io.filename:
            self.update_recent_files_list(new_file=self.io.filename)
        WindowList.unregister_callback(self.postwindowsmenu)
        self.unload_extensions()
        self.io.close()
        self.io = None
        self.undo = None
        if self.color:
            self.color.close(False)
            self.color = None
        self.text = None
        self.tkinter_vars = None
        self.per.close()
        self.per = None
        self.top.destroy()
        if self.close_hook:
            # unless override: unregister from flist, terminate if last window
            self.close_hook()

    def load_extensions(self):
        self.extensions = {}
        self.load_standard_extensions()

    def unload_extensions(self):
        for ins in self.extensions.values():
            if hasattr(ins, "close"):
                ins.close()
        self.extensions = {}

    def load_standard_extensions(self):
        for name in self.get_standard_extension_names():
            try:
                self.load_extension(name)
            except:
                print "Failed to load extension", repr(name)
                import traceback
                traceback.print_exc()

    def get_standard_extension_names(self):
        return idleConf.GetExtensions(editor_only=True)

    def load_extension(self, name):
        try:
            mod = __import__(name, globals(), locals(), [])
        except ImportError:
            print "\nFailed to import extension: ", name
            return
        cls = getattr(mod, name)
        keydefs = idleConf.GetExtensionBindings(name)
        if hasattr(cls, "menudefs"):
            self.fill_menus(cls.menudefs, keydefs)
        ins = cls(self)
        self.extensions[name] = ins
        if keydefs:
            self.apply_bindings(keydefs)
            for vevent in keydefs.keys():
                methodname = vevent.replace("-", "_")
                while methodname[:1] == '<':
                    methodname = methodname[1:]
                while methodname[-1:] == '>':
                    methodname = methodname[:-1]
                methodname = methodname + "_event"
                if hasattr(ins, methodname):
                    self.text.bind(vevent, getattr(ins, methodname))

    def apply_bindings(self, keydefs=None):
        if keydefs is None:
            keydefs = self.Bindings.default_keydefs
        text = self.text
        text.keydefs = keydefs
        for event, keylist in keydefs.items():
            if keylist:
                text.event_add(event, *keylist)

    def fill_menus(self, menudefs=None, keydefs=None):
        """Add appropriate entries to the menus and submenus

        Menus that are absent or None in self.menudict are ignored.
        """
        if menudefs is None:
            menudefs = self.Bindings.menudefs
        if keydefs is None:
            keydefs = self.Bindings.default_keydefs
        menudict = self.menudict
        text = self.text
        for mname, entrylist in menudefs:
            menu = menudict.get(mname)
            if not menu:
                continue
            for entry in entrylist:
                if not entry:
                    menu.add_separator()
                else:
                    label, eventname = entry
                    checkbutton = (label[:1] == '!')
                    if checkbutton:
                        label = label[1:]
                    underline, label = prepstr(label)
                    accelerator = get_accelerator(keydefs, eventname)
                    def command(text=text, eventname=eventname):
                        text.event_generate(eventname)
                    if checkbutton:
                        var = self.get_var_obj(eventname, BooleanVar)
                        menu.add_checkbutton(label=label, underline=underline,
                            command=command, accelerator=accelerator,
                            variable=var)
                    else:
                        menu.add_command(label=label, underline=underline,
                                         command=command,
                                         accelerator=accelerator)

    def getvar(self, name):
        var = self.get_var_obj(name)
        if var:
            value = var.get()
            return value
        else:
            raise NameError, name

    def setvar(self, name, value, vartype=None):
        var = self.get_var_obj(name, vartype)
        if var:
            var.set(value)
        else:
            raise NameError, name

    def get_var_obj(self, name, vartype=None):
        var = self.tkinter_vars.get(name)
        if not var and vartype:
            # create a Tkinter variable object with self.text as master:
            self.tkinter_vars[name] = var = vartype(self.text)
        return var

    # Tk implementations of "virtual text methods" -- each platform
    # reusing IDLE's support code needs to define these for its GUI's
    # flavor of widget.

    # Is character at text_index in a Python string?  Return 0 for
    # "guaranteed no", true for anything else.  This info is expensive
    # to compute ab initio, but is probably already known by the
    # platform's colorizer.

    def is_char_in_string(self, text_index):
        if self.color:
            # Return true iff colorizer hasn't (re)gotten this far
            # yet, or the character is tagged as being in a string
            return self.text.tag_prevrange("TODO", text_index) or \
                   "STRING" in self.text.tag_names(text_index)
        else:
            # The colorizer is missing: assume the worst
            return 1

    # If a selection is defined in the text widget, return (start,
    # end) as Tkinter text indices, otherwise return (None, None)
    def get_selection_indices(self):
        try:
            first = self.text.index("sel.first")
            last = self.text.index("sel.last")
            return first, last
        except TclError:
            return None, None

    # Return the text widget's current view of what a tab stop means
    # (equivalent width in spaces).

    def get_tabwidth(self):
        current = self.text['tabs'] or TK_TABWIDTH_DEFAULT
        return int(current)

    # Set the text widget's current view of what a tab stop means.

    def set_tabwidth(self, newtabwidth):
        text = self.text
        if self.get_tabwidth() != newtabwidth:
            pixels = text.tk.call("font", "measure", text["font"],
                                  "-displayof", text.master,
                                  "n" * newtabwidth)
            text.configure(tabs=pixels)

    # If ispythonsource and guess are true, guess a good value for
    # indentwidth based on file content (if possible), and if
    # indentwidth != tabwidth set usetabs false.
    # In any case, adjust the Text widget's view of what a tab
    # character means.

    def set_indentation_params(self, ispythonsource, guess=True):
        if guess and ispythonsource:
            i = self.guess_indent()
            if 2 <= i <= 8:
                self.indentwidth = i
            if self.indentwidth != self.tabwidth:
                self.usetabs = False
        self.set_tabwidth(self.tabwidth)

    def smart_backspace_event(self, event):
        text = self.text
        first, last = self.get_selection_indices()
        if first and last:
            text.delete(first, last)
            text.mark_set("insert", first)
            return "break"
        # Delete whitespace left, until hitting a real char or closest
        # preceding virtual tab stop.
        chars = text.get("insert linestart", "insert")
        if chars == '':
            if text.compare("insert", ">", "1.0"):
                # easy: delete preceding newline
                text.delete("insert-1c")
            else:
                text.bell()     # at start of buffer
            return "break"
        if  chars[-1] not in " \t":
            # easy: delete preceding real char
            text.delete("insert-1c")
            return "break"
        # Ick.  It may require *inserting* spaces if we back up over a
        # tab character!  This is written to be clear, not fast.
        tabwidth = self.tabwidth
        have = len(chars.expandtabs(tabwidth))
        assert have > 0
        want = ((have - 1) // self.indentwidth) * self.indentwidth
        # Debug prompt is multilined....
        if self.context_use_ps1:
            last_line_of_prompt = sys.ps1.split('\n')[-1]
        else:
            last_line_of_prompt = ''
        ncharsdeleted = 0
        while 1:
            if chars == last_line_of_prompt:
                break
            chars = chars[:-1]
            ncharsdeleted = ncharsdeleted + 1
            have = len(chars.expandtabs(tabwidth))
            if have <= want or chars[-1] not in " \t":
                break
        text.undo_block_start()
        text.delete("insert-%dc" % ncharsdeleted, "insert")
        if have < want:
            text.insert("insert", ' ' * (want - have))
        text.undo_block_stop()
        return "break"

    def smart_indent_event(self, event):
        # if intraline selection:
        #     delete it
        # elif multiline selection:
        #     do indent-region
        # else:
        #     indent one level
        text = self.text
        first, last = self.get_selection_indices()
        text.undo_block_start()
        try:
            if first and last:
                if index2line(first) != index2line(last):
                    return self.indent_region_event(event)
                text.delete(first, last)
                text.mark_set("insert", first)
            prefix = text.get("insert linestart", "insert")
            raw, effective = classifyws(prefix, self.tabwidth)
            if raw == len(prefix):
                # only whitespace to the left
                self.reindent_to(effective + self.indentwidth)
            else:
                # tab to the next 'stop' within or to right of line's text:
                if self.usetabs:
                    pad = '\t'
                else:
                    effective = len(prefix.expandtabs(self.tabwidth))
                    n = self.indentwidth
                    pad = ' ' * (n - effective % n)
                text.insert("insert", pad)
            text.see("insert")
            return "break"
        finally:
            text.undo_block_stop()

    def newline_and_indent_event(self, event):
        text = self.text
        first, last = self.get_selection_indices()
        text.undo_block_start()
        try:
            if first and last:
                text.delete(first, last)
                text.mark_set("insert", first)
            line = text.get("insert linestart", "insert")
            i, n = 0, len(line)
            while i < n and line[i] in " \t":
                i = i+1
            if i == n:
                # the cursor is in or at leading indentation in a continuation
                # line; just inject an empty line at the start
                text.insert("insert linestart", '\n')
                return "break"
            indent = line[:i]
            # strip whitespace before insert point unless it's in the prompt
            i = 0
            last_line_of_prompt = sys.ps1.split('\n')[-1]
            while line and line[-1] in " \t" and line != last_line_of_prompt:
                line = line[:-1]
                i = i+1
            if i:
                text.delete("insert - %d chars" % i, "insert")
            # strip whitespace after insert point
            while text.get("insert") in " \t":
                text.delete("insert")
            # start new line
            text.insert("insert", '\n')

            # adjust indentation for continuations and block
            # open/close first need to find the last stmt
            lno = index2line(text.index('insert'))
            y = PyParse.Parser(self.indentwidth, self.tabwidth)
            if not self.context_use_ps1:
                for context in self.num_context_lines:
                    startat = max(lno - context, 1)
                    startatindex = repr(startat) + ".0"
                    rawtext = text.get(startatindex, "insert")
                    y.set_str(rawtext)
                    bod = y.find_good_parse_start(
                              self.context_use_ps1,
                              self._build_char_in_string_func(startatindex))
                    if bod is not None or startat == 1:
                        break
                y.set_lo(bod or 0)
            else:
                r = text.tag_prevrange("console", "insert")
                if r:
                    startatindex = r[1]
                else:
                    startatindex = "1.0"
                rawtext = text.get(startatindex, "insert")
                y.set_str(rawtext)
                y.set_lo(0)

            c = y.get_continuation_type()
            if c != PyParse.C_NONE:
                # The current stmt hasn't ended yet.
                if c == PyParse.C_STRING_FIRST_LINE:
                    # after the first line of a string; do not indent at all
                    pass
                elif c == PyParse.C_STRING_NEXT_LINES:
                    # inside a string which started before this line;
                    # just mimic the current indent
                    text.insert("insert", indent)
                elif c == PyParse.C_BRACKET:
                    # line up with the first (if any) element of the
                    # last open bracket structure; else indent one
                    # level beyond the indent of the line with the
                    # last open bracket
                    self.reindent_to(y.compute_bracket_indent())
                elif c == PyParse.C_BACKSLASH:
                    # if more than one line in this stmt already, just
                    # mimic the current indent; else if initial line
                    # has a start on an assignment stmt, indent to
                    # beyond leftmost =; else to beyond first chunk of
                    # non-whitespace on initial line
                    if y.get_num_lines_in_stmt() > 1:
                        text.insert("insert", indent)
                    else:
                        self.reindent_to(y.compute_backslash_indent())
                else:
                    assert 0, "bogus continuation type %r" % (c,)
                return "break"

            # This line starts a brand new stmt; indent relative to
            # indentation of initial line of closest preceding
            # interesting stmt.
            indent = y.get_base_indent_string()
            text.insert("insert", indent)
            if y.is_block_opener():
                self.smart_indent_event(event)
            elif indent and y.is_block_closer():
                self.smart_backspace_event(event)
            return "break"
        finally:
            text.see("insert")
            text.undo_block_stop()

    # Our editwin provides a is_char_in_string function that works
    # with a Tk text index, but PyParse only knows about offsets into
    # a string. This builds a function for PyParse that accepts an
    # offset.

    def _build_char_in_string_func(self, startindex):
        def inner(offset, _startindex=startindex,
                  _icis=self.is_char_in_string):
            return _icis(_startindex + "+%dc" % offset)
        return inner

    def indent_region_event(self, event):
        head, tail, chars, lines = self.get_region()
        for pos in range(len(lines)):
            line = lines[pos]
            if line:
                raw, effective = classifyws(line, self.tabwidth)
                effective = effective + self.indentwidth
                lines[pos] = self._make_blanks(effective) + line[raw:]
        self.set_region(head, tail, chars, lines)
        return "break"

    def dedent_region_event(self, event):
        head, tail, chars, lines = self.get_region()
        for pos in range(len(lines)):
            line = lines[pos]
            if line:
                raw, effective = classifyws(line, self.tabwidth)
                effective = max(effective - self.indentwidth, 0)
                lines[pos] = self._make_blanks(effective) + line[raw:]
        self.set_region(head, tail, chars, lines)
        return "break"

    def comment_region_event(self, event):
        head, tail, chars, lines = self.get_region()
        for pos in range(len(lines) - 1):
            line = lines[pos]
            lines[pos] = '##' + line
        self.set_region(head, tail, chars, lines)

    def uncomment_region_event(self, event):
        head, tail, chars, lines = self.get_region()
        for pos in range(len(lines)):
            line = lines[pos]
            if not line:
                continue
            if line[:2] == '##':
                line = line[2:]
            elif line[:1] == '#':
                line = line[1:]
            lines[pos] = line
        self.set_region(head, tail, chars, lines)

    def tabify_region_event(self, event):
        head, tail, chars, lines = self.get_region()
        tabwidth = self._asktabwidth()
        if tabwidth is None: return
        for pos in range(len(lines)):
            line = lines[pos]
            if line:
                raw, effective = classifyws(line, tabwidth)
                ntabs, nspaces = divmod(effective, tabwidth)
                lines[pos] = '\t' * ntabs + ' ' * nspaces + line[raw:]
        self.set_region(head, tail, chars, lines)

    def untabify_region_event(self, event):
        head, tail, chars, lines = self.get_region()
        tabwidth = self._asktabwidth()
        if tabwidth is None: return
        for pos in range(len(lines)):
            lines[pos] = lines[pos].expandtabs(tabwidth)
        self.set_region(head, tail, chars, lines)

    def toggle_tabs_event(self, event):
        if self.askyesno(
              "Toggle tabs",
              "Turn tabs " + ("on", "off")[self.usetabs] +
              "?\nIndent width " +
              ("will be", "remains at")[self.usetabs] + " 8." +
              "\n Note: a tab is always 8 columns",
              parent=self.text):
            self.usetabs = not self.usetabs
            # Try to prevent inconsistent indentation.
            # User must change indent width manually after using tabs.
            self.indentwidth = 8
        return "break"

    # XXX this isn't bound to anything -- see tabwidth comments
##     def change_tabwidth_event(self, event):
##         new = self._asktabwidth()
##         if new != self.tabwidth:
##             self.tabwidth = new
##             self.set_indentation_params(0, guess=0)
##         return "break"

    def change_indentwidth_event(self, event):
        new = self.askinteger(
                  "Indent width",
                  "New indent width (2-16)\n(Always use 8 when using tabs)",
                  parent=self.text,
                  initialvalue=self.indentwidth,
                  minvalue=2,
                  maxvalue=16)
        if new and new != self.indentwidth and not self.usetabs:
            self.indentwidth = new
        return "break"

    def get_region(self):
        text = self.text
        first, last = self.get_selection_indices()
        if first and last:
            head = text.index(first + " linestart")
            tail = text.index(last + "-1c lineend +1c")
        else:
            head = text.index("insert linestart")
            tail = text.index("insert lineend +1c")
        chars = text.get(head, tail)
        lines = chars.split("\n")
        return head, tail, chars, lines

    def set_region(self, head, tail, chars, lines):
        text = self.text
        newchars = "\n".join(lines)
        if newchars == chars:
            text.bell()
            return
        text.tag_remove("sel", "1.0", "end")
        text.mark_set("insert", head)
        text.undo_block_start()
        text.delete(head, tail)
        text.insert(head, newchars)
        text.undo_block_stop()
        text.tag_add("sel", head, "insert")

    # Make string that displays as n leading blanks.

    def _make_blanks(self, n):
        if self.usetabs:
            ntabs, nspaces = divmod(n, self.tabwidth)
            return '\t' * ntabs + ' ' * nspaces
        else:
            return ' ' * n

    # Delete from beginning of line to insert point, then reinsert
    # column logical (meaning use tabs if appropriate) spaces.

    def reindent_to(self, column):
        text = self.text
        text.undo_block_start()
        if text.compare("insert linestart", "!=", "insert"):
            text.delete("insert linestart", "insert")
        if column:
            text.insert("insert", self._make_blanks(column))
        text.undo_block_stop()

    def _asktabwidth(self):
        return self.askinteger(
            "Tab width",
            "Columns per tab? (2-16)",
            parent=self.text,
            initialvalue=self.indentwidth,
            minvalue=2,
            maxvalue=16)

    # Guess indentwidth from text content.
    # Return guessed indentwidth.  This should not be believed unless
    # it's in a reasonable range (e.g., it will be 0 if no indented
    # blocks are found).

    def guess_indent(self):
        opener, indented = IndentSearcher(self.text, self.tabwidth).run()
        if opener and indented:
            raw, indentsmall = classifyws(opener, self.tabwidth)
            raw, indentlarge = classifyws(indented, self.tabwidth)
        else:
            indentsmall = indentlarge = 0
        return indentlarge - indentsmall

# "line.col" -> line, as an int
def index2line(index):
    return int(float(index))

# Look at the leading whitespace in s.
# Return pair (# of leading ws characters,
#              effective # of leading blanks after expanding
#              tabs to width tabwidth)

def classifyws(s, tabwidth):
    raw = effective = 0
    for ch in s:
        if ch == ' ':
            raw = raw + 1
            effective = effective + 1
        elif ch == '\t':
            raw = raw + 1
            effective = (effective // tabwidth + 1) * tabwidth
        else:
            break
    return raw, effective

import tokenize
_tokenize = tokenize
del tokenize

class IndentSearcher(object):

    # .run() chews over the Text widget, looking for a block opener
    # and the stmt following it.  Returns a pair,
    #     (line containing block opener, line containing stmt)
    # Either or both may be None.

    def __init__(self, text, tabwidth):
        self.text = text
        self.tabwidth = tabwidth
        self.i = self.finished = 0
        self.blkopenline = self.indentedline = None

    def readline(self):
        if self.finished:
            return ""
        i = self.i = self.i + 1
        mark = repr(i) + ".0"
        if self.text.compare(mark, ">=", "end"):
            return ""
        return self.text.get(mark, mark + " lineend+1c")

    def tokeneater(self, type, token, start, end, line,
                   INDENT=_tokenize.INDENT,
                   NAME=_tokenize.NAME,
                   OPENERS=('class', 'def', 'for', 'if', 'try', 'while')):
        if self.finished:
            pass
        elif type == NAME and token in OPENERS:
            self.blkopenline = line
        elif type == INDENT and self.blkopenline:
            self.indentedline = line
            self.finished = 1

    def run(self):
        save_tabsize = _tokenize.tabsize
        _tokenize.tabsize = self.tabwidth
        try:
            try:
                _tokenize.tokenize(self.readline, self.tokeneater)
            except (_tokenize.TokenError, SyntaxError):
                # since we cut off the tokenizer early, we can trigger
                # spurious errors
                pass
        finally:
            _tokenize.tabsize = save_tabsize
        return self.blkopenline, self.indentedline

### end autoindent code ###

def prepstr(s):
    # Helper to extract the underscore from a string, e.g.
    # prepstr("Co_py") returns (2, "Copy").
    i = s.find('_')
    if i >= 0:
        s = s[:i] + s[i+1:]
    return i, s


keynames = {
 'bracketleft': '[',
 'bracketright': ']',
 'slash': '/',
}

def get_accelerator(keydefs, eventname):
    keylist = keydefs.get(eventname)
    # issue10940: temporary workaround to prevent hang with OS X Cocoa Tk 8.5
    # if not keylist:
    if (not keylist) or (macosxSupport.isCocoaTk() and eventname in {
                            "<<open-module>>",
                            "<<goto-line>>",
                            "<<change-indentwidth>>"}):
        return ""
    s = keylist[0]
    s = re.sub(r"-[a-z]\b", lambda m: m.group().upper(), s)
    s = re.sub(r"\b\w+\b", lambda m: keynames.get(m.group(), m.group()), s)
    s = re.sub("Key-", "", s)
    s = re.sub("Cancel","Ctrl-Break",s)   # dscherer@cmu.edu
    s = re.sub("Control-", "Ctrl-", s)
    s = re.sub("-", "+", s)
    s = re.sub("><", " ", s)
    s = re.sub("<", "", s)
    s = re.sub(">", "", s)
    return s


def fixwordbreaks(root):
    # Make sure that Tk's double-click and next/previous word
    # operations use our definition of a word (i.e. an identifier)
    tk = root.tk
    tk.call('tcl_wordBreakAfter', 'a b', 0) # make sure word.tcl is loaded
    tk.call('set', 'tcl_wordchars', '[a-zA-Z0-9_]')
    tk.call('set', 'tcl_nonwordchars', '[^a-zA-Z0-9_]')


def _editor_window(parent):
    root = parent
    fixwordbreaks(root)
    if sys.argv[1:]:
        filename = sys.argv[1]
    else:
        filename = None
    macosxSupport.setupApp(root, None)
    edit = EditorWindow(root=root, filename=filename)
    edit.text.bind("<<close-all-windows>>", edit.close_event)
    parent.mainloop()


if __name__ == '__main__':
    from idlelib.idle_test.htest import run
    run(_help_dialog, _editor_window)
