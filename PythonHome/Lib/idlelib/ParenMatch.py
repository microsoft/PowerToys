"""ParenMatch -- An IDLE extension for parenthesis matching.

When you hit a right paren, the cursor should move briefly to the left
paren.  Paren here is used generically; the matching applies to
parentheses, square brackets, and curly braces.
"""

from idlelib.HyperParser import HyperParser
from idlelib.configHandler import idleConf

_openers = {')':'(',']':'[','}':'{'}
CHECK_DELAY = 100 # miliseconds

class ParenMatch:
    """Highlight matching parentheses

    There are three supported style of paren matching, based loosely
    on the Emacs options.  The style is select based on the
    HILITE_STYLE attribute; it can be changed used the set_style
    method.

    The supported styles are:

    default -- When a right paren is typed, highlight the matching
        left paren for 1/2 sec.

    expression -- When a right paren is typed, highlight the entire
        expression from the left paren to the right paren.

    TODO:
        - extend IDLE with configuration dialog to change options
        - implement rest of Emacs highlight styles (see below)
        - print mismatch warning in IDLE status window

    Note: In Emacs, there are several styles of highlight where the
    matching paren is highlighted whenever the cursor is immediately
    to the right of a right paren.  I don't know how to do that in Tk,
    so I haven't bothered.
    """
    menudefs = [
        ('edit', [
            ("Show surrounding parens", "<<flash-paren>>"),
        ])
    ]
    STYLE = idleConf.GetOption('extensions','ParenMatch','style',
            default='expression')
    FLASH_DELAY = idleConf.GetOption('extensions','ParenMatch','flash-delay',
            type='int',default=500)
    HILITE_CONFIG = idleConf.GetHighlight(idleConf.CurrentTheme(),'hilite')
    BELL = idleConf.GetOption('extensions','ParenMatch','bell',
            type='bool',default=1)

    RESTORE_VIRTUAL_EVENT_NAME = "<<parenmatch-check-restore>>"
    # We want the restore event be called before the usual return and
    # backspace events.
    RESTORE_SEQUENCES = ("<KeyPress>", "<ButtonPress>",
                         "<Key-Return>", "<Key-BackSpace>")

    def __init__(self, editwin):
        self.editwin = editwin
        self.text = editwin.text
        # Bind the check-restore event to the function restore_event,
        # so that we can then use activate_restore (which calls event_add)
        # and deactivate_restore (which calls event_delete).
        editwin.text.bind(self.RESTORE_VIRTUAL_EVENT_NAME,
                          self.restore_event)
        self.counter = 0
        self.is_restore_active = 0
        self.set_style(self.STYLE)

    def activate_restore(self):
        if not self.is_restore_active:
            for seq in self.RESTORE_SEQUENCES:
                self.text.event_add(self.RESTORE_VIRTUAL_EVENT_NAME, seq)
            self.is_restore_active = True

    def deactivate_restore(self):
        if self.is_restore_active:
            for seq in self.RESTORE_SEQUENCES:
                self.text.event_delete(self.RESTORE_VIRTUAL_EVENT_NAME, seq)
            self.is_restore_active = False

    def set_style(self, style):
        self.STYLE = style
        if style == "default":
            self.create_tag = self.create_tag_default
            self.set_timeout = self.set_timeout_last
        elif style == "expression":
            self.create_tag = self.create_tag_expression
            self.set_timeout = self.set_timeout_none

    def flash_paren_event(self, event):
        indices = (HyperParser(self.editwin, "insert")
                   .get_surrounding_brackets())
        if indices is None:
            self.warn_mismatched()
            return
        self.activate_restore()
        self.create_tag(indices)
        self.set_timeout_last()

    def paren_closed_event(self, event):
        # If it was a shortcut and not really a closing paren, quit.
        closer = self.text.get("insert-1c")
        if closer not in _openers:
            return
        hp = HyperParser(self.editwin, "insert-1c")
        if not hp.is_in_code():
            return
        indices = hp.get_surrounding_brackets(_openers[closer], True)
        if indices is None:
            self.warn_mismatched()
            return
        self.activate_restore()
        self.create_tag(indices)
        self.set_timeout()

    def restore_event(self, event=None):
        self.text.tag_delete("paren")
        self.deactivate_restore()
        self.counter += 1   # disable the last timer, if there is one.

    def handle_restore_timer(self, timer_count):
        if timer_count == self.counter:
            self.restore_event()

    def warn_mismatched(self):
        if self.BELL:
            self.text.bell()

    # any one of the create_tag_XXX methods can be used depending on
    # the style

    def create_tag_default(self, indices):
        """Highlight the single paren that matches"""
        self.text.tag_add("paren", indices[0])
        self.text.tag_config("paren", self.HILITE_CONFIG)

    def create_tag_expression(self, indices):
        """Highlight the entire expression"""
        if self.text.get(indices[1]) in (')', ']', '}'):
            rightindex = indices[1]+"+1c"
        else:
            rightindex = indices[1]
        self.text.tag_add("paren", indices[0], rightindex)
        self.text.tag_config("paren", self.HILITE_CONFIG)

    # any one of the set_timeout_XXX methods can be used depending on
    # the style

    def set_timeout_none(self):
        """Highlight will remain until user input turns it off
        or the insert has moved"""
        # After CHECK_DELAY, call a function which disables the "paren" tag
        # if the event is for the most recent timer and the insert has changed,
        # or schedules another call for itself.
        self.counter += 1
        def callme(callme, self=self, c=self.counter,
                   index=self.text.index("insert")):
            if index != self.text.index("insert"):
                self.handle_restore_timer(c)
            else:
                self.editwin.text_frame.after(CHECK_DELAY, callme, callme)
        self.editwin.text_frame.after(CHECK_DELAY, callme, callme)

    def set_timeout_last(self):
        """The last highlight created will be removed after .5 sec"""
        # associate a counter with an event; only disable the "paren"
        # tag if the event is for the most recent timer.
        self.counter += 1
        self.editwin.text_frame.after(
            self.FLASH_DELAY,
            lambda self=self, c=self.counter: self.handle_restore_timer(c))


if __name__ == '__main__':
    import unittest
    unittest.main('idlelib.idle_test.test_parenmatch', verbosity=2)
