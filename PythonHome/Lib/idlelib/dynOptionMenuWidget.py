"""
OptionMenu widget modified to allow dynamic menu reconfiguration
and setting of highlightthickness
"""
from Tkinter import OptionMenu, _setit, Tk, StringVar, Button

import copy
import re

class DynOptionMenu(OptionMenu):
    """
    unlike OptionMenu, our kwargs can include highlightthickness
    """
    def __init__(self, master, variable, value, *values, **kwargs):
        #get a copy of kwargs before OptionMenu.__init__ munges them
        kwargsCopy=copy.copy(kwargs)
        if 'highlightthickness' in kwargs.keys():
            del(kwargs['highlightthickness'])
        OptionMenu.__init__(self, master, variable, value, *values, **kwargs)
        self.config(highlightthickness=kwargsCopy.get('highlightthickness'))
        #self.menu=self['menu']
        self.variable=variable
        self.command=kwargs.get('command')

    def SetMenu(self,valueList,value=None):
        """
        clear and reload the menu with a new set of options.
        valueList - list of new options
        value - initial value to set the optionmenu's menubutton to
        """
        self['menu'].delete(0,'end')
        for item in valueList:
            self['menu'].add_command(label=item,
                    command=_setit(self.variable,item,self.command))
        if value:
            self.variable.set(value)

def _dyn_option_menu(parent):
    root = Tk()
    root.title("Tets dynamic option menu")
    var = StringVar(root)
    width, height, x, y = list(map(int, re.split('[x+]', parent.geometry())))
    root.geometry("+%d+%d"%(x, y + 150))
    var.set("Old option set") #Set the default value
    dyn = DynOptionMenu(root,var, "old1","old2","old3","old4")
    dyn.pack()

    def update():
        dyn.SetMenu(["new1","new2","new3","new4"],value="new option set")

    button = Button(root, text="Change option set", command=update)
    button.pack()
    root.mainloop()

if __name__ == '__main__':
    from idlelib.idle_test.htest import run
    run(_dyn_option_menu)
