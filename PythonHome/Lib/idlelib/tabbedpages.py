"""An implementation of tabbed pages using only standard Tkinter.

Originally developed for use in IDLE. Based on tabpage.py.

Classes exported:
TabbedPageSet -- A Tkinter implementation of a tabbed-page widget.
TabSet -- A widget containing tabs (buttons) in one or more rows.

"""
from Tkinter import *

class InvalidNameError(Exception): pass
class AlreadyExistsError(Exception): pass


class TabSet(Frame):
    """A widget containing tabs (buttons) in one or more rows.

    Only one tab may be selected at a time.

    """
    def __init__(self, page_set, select_command,
                 tabs=None, n_rows=1, max_tabs_per_row=5,
                 expand_tabs=False, **kw):
        """Constructor arguments:

        select_command -- A callable which will be called when a tab is
        selected. It is called with the name of the selected tab as an
        argument.

        tabs -- A list of strings, the names of the tabs. Should be specified in
        the desired tab order. The first tab will be the default and first
        active tab. If tabs is None or empty, the TabSet will be initialized
        empty.

        n_rows -- Number of rows of tabs to be shown. If n_rows <= 0 or is
        None, then the number of rows will be decided by TabSet. See
        _arrange_tabs() for details.

        max_tabs_per_row -- Used for deciding how many rows of tabs are needed,
        when the number of rows is not constant. See _arrange_tabs() for
        details.

        """
        Frame.__init__(self, page_set, **kw)
        self.select_command = select_command
        self.n_rows = n_rows
        self.max_tabs_per_row = max_tabs_per_row
        self.expand_tabs = expand_tabs
        self.page_set = page_set

        self._tabs = {}
        self._tab2row = {}
        if tabs:
            self._tab_names = list(tabs)
        else:
            self._tab_names = []
        self._selected_tab = None
        self._tab_rows = []

        self.padding_frame = Frame(self, height=2,
                                   borderwidth=0, relief=FLAT,
                                   background=self.cget('background'))
        self.padding_frame.pack(side=TOP, fill=X, expand=False)

        self._arrange_tabs()

    def add_tab(self, tab_name):
        """Add a new tab with the name given in tab_name."""
        if not tab_name:
            raise InvalidNameError("Invalid Tab name: '%s'" % tab_name)
        if tab_name in self._tab_names:
            raise AlreadyExistsError("Tab named '%s' already exists" %tab_name)

        self._tab_names.append(tab_name)
        self._arrange_tabs()

    def remove_tab(self, tab_name):
        """Remove the tab named <tab_name>"""
        if not tab_name in self._tab_names:
            raise KeyError("No such Tab: '%s" % page_name)

        self._tab_names.remove(tab_name)
        self._arrange_tabs()

    def set_selected_tab(self, tab_name):
        """Show the tab named <tab_name> as the selected one"""
        if tab_name == self._selected_tab:
            return
        if tab_name is not None and tab_name not in self._tabs:
            raise KeyError("No such Tab: '%s" % page_name)

        # deselect the current selected tab
        if self._selected_tab is not None:
            self._tabs[self._selected_tab].set_normal()
        self._selected_tab = None

        if tab_name is not None:
            # activate the tab named tab_name
            self._selected_tab = tab_name
            tab = self._tabs[tab_name]
            tab.set_selected()
            # move the tab row with the selected tab to the bottom
            tab_row = self._tab2row[tab]
            tab_row.pack_forget()
            tab_row.pack(side=TOP, fill=X, expand=0)

    def _add_tab_row(self, tab_names, expand_tabs):
        if not tab_names:
            return

        tab_row = Frame(self)
        tab_row.pack(side=TOP, fill=X, expand=0)
        self._tab_rows.append(tab_row)

        for tab_name in tab_names:
            tab = TabSet.TabButton(tab_name, self.select_command,
                                   tab_row, self)
            if expand_tabs:
                tab.pack(side=LEFT, fill=X, expand=True)
            else:
                tab.pack(side=LEFT)
            self._tabs[tab_name] = tab
            self._tab2row[tab] = tab_row

        # tab is the last one created in the above loop
        tab.is_last_in_row = True

    def _reset_tab_rows(self):
        while self._tab_rows:
            tab_row = self._tab_rows.pop()
            tab_row.destroy()
        self._tab2row = {}

    def _arrange_tabs(self):
        """
        Arrange the tabs in rows, in the order in which they were added.

        If n_rows >= 1, this will be the number of rows used. Otherwise the
        number of rows will be calculated according to the number of tabs and
        max_tabs_per_row. In this case, the number of rows may change when
        adding/removing tabs.

        """
        # remove all tabs and rows
        for tab_name in self._tabs.keys():
            self._tabs.pop(tab_name).destroy()
        self._reset_tab_rows()

        if not self._tab_names:
            return

        if self.n_rows is not None and self.n_rows > 0:
            n_rows = self.n_rows
        else:
            # calculate the required number of rows
            n_rows = (len(self._tab_names) - 1) // self.max_tabs_per_row + 1

        # not expanding the tabs with more than one row is very ugly
        expand_tabs = self.expand_tabs or n_rows > 1
        i = 0 # index in self._tab_names
        for row_index in xrange(n_rows):
            # calculate required number of tabs in this row
            n_tabs = (len(self._tab_names) - i - 1) // (n_rows - row_index) + 1
            tab_names = self._tab_names[i:i + n_tabs]
            i += n_tabs
            self._add_tab_row(tab_names, expand_tabs)

        # re-select selected tab so it is properly displayed
        selected = self._selected_tab
        self.set_selected_tab(None)
        if selected in self._tab_names:
            self.set_selected_tab(selected)

    class TabButton(Frame):
        """A simple tab-like widget."""

        bw = 2 # borderwidth

        def __init__(self, name, select_command, tab_row, tab_set):
            """Constructor arguments:

            name -- The tab's name, which will appear in its button.

            select_command -- The command to be called upon selection of the
            tab. It is called with the tab's name as an argument.

            """
            Frame.__init__(self, tab_row, borderwidth=self.bw, relief=RAISED)

            self.name = name
            self.select_command = select_command
            self.tab_set = tab_set
            self.is_last_in_row = False

            self.button = Radiobutton(
                self, text=name, command=self._select_event,
                padx=5, pady=1, takefocus=FALSE, indicatoron=FALSE,
                highlightthickness=0, selectcolor='', borderwidth=0)
            self.button.pack(side=LEFT, fill=X, expand=True)

            self._init_masks()
            self.set_normal()

        def _select_event(self, *args):
            """Event handler for tab selection.

            With TabbedPageSet, this calls TabbedPageSet.change_page, so that
            selecting a tab changes the page.

            Note that this does -not- call set_selected -- it will be called by
            TabSet.set_selected_tab, which should be called when whatever the
            tabs are related to changes.

            """
            self.select_command(self.name)
            return

        def set_selected(self):
            """Assume selected look"""
            self._place_masks(selected=True)

        def set_normal(self):
            """Assume normal look"""
            self._place_masks(selected=False)

        def _init_masks(self):
            page_set = self.tab_set.page_set
            background = page_set.pages_frame.cget('background')
            # mask replaces the middle of the border with the background color
            self.mask = Frame(page_set, borderwidth=0, relief=FLAT,
                              background=background)
            # mskl replaces the bottom-left corner of the border with a normal
            # left border
            self.mskl = Frame(page_set, borderwidth=0, relief=FLAT,
                              background=background)
            self.mskl.ml = Frame(self.mskl, borderwidth=self.bw,
                                 relief=RAISED)
            self.mskl.ml.place(x=0, y=-self.bw,
                               width=2*self.bw, height=self.bw*4)
            # mskr replaces the bottom-right corner of the border with a normal
            # right border
            self.mskr = Frame(page_set, borderwidth=0, relief=FLAT,
                              background=background)
            self.mskr.mr = Frame(self.mskr, borderwidth=self.bw,
                                 relief=RAISED)

        def _place_masks(self, selected=False):
            height = self.bw
            if selected:
                height += self.bw

            self.mask.place(in_=self,
                            relx=0.0, x=0,
                            rely=1.0, y=0,
                            relwidth=1.0, width=0,
                            relheight=0.0, height=height)

            self.mskl.place(in_=self,
                            relx=0.0, x=-self.bw,
                            rely=1.0, y=0,
                            relwidth=0.0, width=self.bw,
                            relheight=0.0, height=height)

            page_set = self.tab_set.page_set
            if selected and ((not self.is_last_in_row) or
                             (self.winfo_rootx() + self.winfo_width() <
                              page_set.winfo_rootx() + page_set.winfo_width())
                             ):
                # for a selected tab, if its rightmost edge isn't on the
                # rightmost edge of the page set, the right mask should be one
                # borderwidth shorter (vertically)
                height -= self.bw

            self.mskr.place(in_=self,
                            relx=1.0, x=0,
                            rely=1.0, y=0,
                            relwidth=0.0, width=self.bw,
                            relheight=0.0, height=height)

            self.mskr.mr.place(x=-self.bw, y=-self.bw,
                               width=2*self.bw, height=height + self.bw*2)

            # finally, lower the tab set so that all of the frames we just
            # placed hide it
            self.tab_set.lower()

class TabbedPageSet(Frame):
    """A Tkinter tabbed-pane widget.

    Constains set of 'pages' (or 'panes') with tabs above for selecting which
    page is displayed. Only one page will be displayed at a time.

    Pages may be accessed through the 'pages' attribute, which is a dictionary
    of pages, using the name given as the key. A page is an instance of a
    subclass of Tk's Frame widget.

    The page widgets will be created (and destroyed when required) by the
    TabbedPageSet. Do not call the page's pack/place/grid/destroy methods.

    Pages may be added or removed at any time using the add_page() and
    remove_page() methods.

    """
    class Page(object):
        """Abstract base class for TabbedPageSet's pages.

        Subclasses must override the _show() and _hide() methods.

        """
        uses_grid = False

        def __init__(self, page_set):
            self.frame = Frame(page_set, borderwidth=2, relief=RAISED)

        def _show(self):
            raise NotImplementedError

        def _hide(self):
            raise NotImplementedError

    class PageRemove(Page):
        """Page class using the grid placement manager's "remove" mechanism."""
        uses_grid = True

        def _show(self):
            self.frame.grid(row=0, column=0, sticky=NSEW)

        def _hide(self):
            self.frame.grid_remove()

    class PageLift(Page):
        """Page class using the grid placement manager's "lift" mechanism."""
        uses_grid = True

        def __init__(self, page_set):
            super(TabbedPageSet.PageLift, self).__init__(page_set)
            self.frame.grid(row=0, column=0, sticky=NSEW)
            self.frame.lower()

        def _show(self):
            self.frame.lift()

        def _hide(self):
            self.frame.lower()

    class PagePackForget(Page):
        """Page class using the pack placement manager's "forget" mechanism."""
        def _show(self):
            self.frame.pack(fill=BOTH, expand=True)

        def _hide(self):
            self.frame.pack_forget()

    def __init__(self, parent, page_names=None, page_class=PageLift,
                 n_rows=1, max_tabs_per_row=5, expand_tabs=False,
                 **kw):
        """Constructor arguments:

        page_names -- A list of strings, each will be the dictionary key to a
        page's widget, and the name displayed on the page's tab. Should be
        specified in the desired page order. The first page will be the default
        and first active page. If page_names is None or empty, the
        TabbedPageSet will be initialized empty.

        n_rows, max_tabs_per_row -- Parameters for the TabSet which will
        manage the tabs. See TabSet's docs for details.

        page_class -- Pages can be shown/hidden using three mechanisms:

        * PageLift - All pages will be rendered one on top of the other. When
          a page is selected, it will be brought to the top, thus hiding all
          other pages. Using this method, the TabbedPageSet will not be resized
          when pages are switched. (It may still be resized when pages are
          added/removed.)

        * PageRemove - When a page is selected, the currently showing page is
          hidden, and the new page shown in its place. Using this method, the
          TabbedPageSet may resize when pages are changed.

        * PagePackForget - This mechanism uses the pack placement manager.
          When a page is shown it is packed, and when it is hidden it is
          unpacked (i.e. pack_forget). This mechanism may also cause the
          TabbedPageSet to resize when the page is changed.

        """
        Frame.__init__(self, parent, **kw)

        self.page_class = page_class
        self.pages = {}
        self._pages_order = []
        self._current_page = None
        self._default_page = None

        self.columnconfigure(0, weight=1)
        self.rowconfigure(1, weight=1)

        self.pages_frame = Frame(self)
        self.pages_frame.grid(row=1, column=0, sticky=NSEW)
        if self.page_class.uses_grid:
            self.pages_frame.columnconfigure(0, weight=1)
            self.pages_frame.rowconfigure(0, weight=1)

        # the order of the following commands is important
        self._tab_set = TabSet(self, self.change_page, n_rows=n_rows,
                               max_tabs_per_row=max_tabs_per_row,
                               expand_tabs=expand_tabs)
        if page_names:
            for name in page_names:
                self.add_page(name)
        self._tab_set.grid(row=0, column=0, sticky=NSEW)

        self.change_page(self._default_page)

    def add_page(self, page_name):
        """Add a new page with the name given in page_name."""
        if not page_name:
            raise InvalidNameError("Invalid TabPage name: '%s'" % page_name)
        if page_name in self.pages:
            raise AlreadyExistsError(
                "TabPage named '%s' already exists" % page_name)

        self.pages[page_name] = self.page_class(self.pages_frame)
        self._pages_order.append(page_name)
        self._tab_set.add_tab(page_name)

        if len(self.pages) == 1: # adding first page
            self._default_page = page_name
            self.change_page(page_name)

    def remove_page(self, page_name):
        """Destroy the page whose name is given in page_name."""
        if not page_name in self.pages:
            raise KeyError("No such TabPage: '%s" % page_name)

        self._pages_order.remove(page_name)

        # handle removing last remaining, default, or currently shown page
        if len(self._pages_order) > 0:
            if page_name == self._default_page:
                # set a new default page
                self._default_page = self._pages_order[0]
        else:
            self._default_page = None

        if page_name == self._current_page:
            self.change_page(self._default_page)

        self._tab_set.remove_tab(page_name)
        page = self.pages.pop(page_name)
        page.frame.destroy()

    def change_page(self, page_name):
        """Show the page whose name is given in page_name."""
        if self._current_page == page_name:
            return
        if page_name is not None and page_name not in self.pages:
            raise KeyError("No such TabPage: '%s'" % page_name)

        if self._current_page is not None:
            self.pages[self._current_page]._hide()
        self._current_page = None

        if page_name is not None:
            self._current_page = page_name
            self.pages[page_name]._show()

        self._tab_set.set_selected_tab(page_name)

def _tabbed_pages(parent):
    # test dialog
    root=Tk()
    width, height, x, y = list(map(int, re.split('[x+]', parent.geometry())))
    root.geometry("+%d+%d"%(x, y + 175))
    root.title("Test tabbed pages")
    tabPage=TabbedPageSet(root, page_names=['Foobar','Baz'], n_rows=0,
                          expand_tabs=False,
                          )
    tabPage.pack(side=TOP, expand=TRUE, fill=BOTH)
    Label(tabPage.pages['Foobar'].frame, text='Foo', pady=20).pack()
    Label(tabPage.pages['Foobar'].frame, text='Bar', pady=20).pack()
    Label(tabPage.pages['Baz'].frame, text='Baz').pack()
    entryPgName=Entry(root)
    buttonAdd=Button(root, text='Add Page',
            command=lambda:tabPage.add_page(entryPgName.get()))
    buttonRemove=Button(root, text='Remove Page',
            command=lambda:tabPage.remove_page(entryPgName.get()))
    labelPgName=Label(root, text='name of page to add/remove:')
    buttonAdd.pack(padx=5, pady=5)
    buttonRemove.pack(padx=5, pady=5)
    labelPgName.pack(padx=5)
    entryPgName.pack(padx=5)
    root.mainloop()


if __name__ == '__main__':
    from idlelib.idle_test.htest import run
    run(_tabbed_pages)
