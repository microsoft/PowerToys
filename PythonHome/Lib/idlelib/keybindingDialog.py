"""
Dialog for building Tkinter accelerator key bindings
"""
from Tkinter import *
import tkMessageBox
import string
import sys

class GetKeysDialog(Toplevel):
    def __init__(self,parent,title,action,currentKeySequences,_htest=False):
        """
        action - string, the name of the virtual event these keys will be
                 mapped to
        currentKeys - list, a list of all key sequence lists currently mapped
                 to virtual events, for overlap checking
        _htest - bool, change box location when running htest
        """
        Toplevel.__init__(self, parent)
        self.configure(borderwidth=5)
        self.resizable(height=FALSE,width=FALSE)
        self.title(title)
        self.transient(parent)
        self.grab_set()
        self.protocol("WM_DELETE_WINDOW", self.Cancel)
        self.parent = parent
        self.action=action
        self.currentKeySequences=currentKeySequences
        self.result=''
        self.keyString=StringVar(self)
        self.keyString.set('')
        self.SetModifiersForPlatform() # set self.modifiers, self.modifier_label
        self.modifier_vars = []
        for modifier in self.modifiers:
            variable = StringVar(self)
            variable.set('')
            self.modifier_vars.append(variable)
        self.advanced = False
        self.CreateWidgets()
        self.LoadFinalKeyList()
        self.withdraw() #hide while setting geometry
        self.update_idletasks()
        self.geometry(
                "+%d+%d" % (
                    parent.winfo_rootx() +
                    (parent.winfo_width()/2 - self.winfo_reqwidth()/2),
                    parent.winfo_rooty() +
                    ((parent.winfo_height()/2 - self.winfo_reqheight()/2)
                    if not _htest else 150)
                ) )  #centre dialog over parent (or below htest box)
        self.deiconify() #geometry set, unhide
        self.wait_window()

    def CreateWidgets(self):
        frameMain = Frame(self,borderwidth=2,relief=SUNKEN)
        frameMain.pack(side=TOP,expand=TRUE,fill=BOTH)
        frameButtons=Frame(self)
        frameButtons.pack(side=BOTTOM,fill=X)
        self.buttonOK = Button(frameButtons,text='OK',
                width=8,command=self.OK)
        self.buttonOK.grid(row=0,column=0,padx=5,pady=5)
        self.buttonCancel = Button(frameButtons,text='Cancel',
                width=8,command=self.Cancel)
        self.buttonCancel.grid(row=0,column=1,padx=5,pady=5)
        self.frameKeySeqBasic = Frame(frameMain)
        self.frameKeySeqAdvanced = Frame(frameMain)
        self.frameControlsBasic = Frame(frameMain)
        self.frameHelpAdvanced = Frame(frameMain)
        self.frameKeySeqAdvanced.grid(row=0,column=0,sticky=NSEW,padx=5,pady=5)
        self.frameKeySeqBasic.grid(row=0,column=0,sticky=NSEW,padx=5,pady=5)
        self.frameKeySeqBasic.lift()
        self.frameHelpAdvanced.grid(row=1,column=0,sticky=NSEW,padx=5)
        self.frameControlsBasic.grid(row=1,column=0,sticky=NSEW,padx=5)
        self.frameControlsBasic.lift()
        self.buttonLevel = Button(frameMain,command=self.ToggleLevel,
                text='Advanced Key Binding Entry >>')
        self.buttonLevel.grid(row=2,column=0,stick=EW,padx=5,pady=5)
        labelTitleBasic = Label(self.frameKeySeqBasic,
                text="New keys for  '"+self.action+"' :")
        labelTitleBasic.pack(anchor=W)
        labelKeysBasic = Label(self.frameKeySeqBasic,justify=LEFT,
                textvariable=self.keyString,relief=GROOVE,borderwidth=2)
        labelKeysBasic.pack(ipadx=5,ipady=5,fill=X)
        self.modifier_checkbuttons = {}
        column = 0
        for modifier, variable in zip(self.modifiers, self.modifier_vars):
            label = self.modifier_label.get(modifier, modifier)
            check=Checkbutton(self.frameControlsBasic,
                command=self.BuildKeyString,
                text=label,variable=variable,onvalue=modifier,offvalue='')
            check.grid(row=0,column=column,padx=2,sticky=W)
            self.modifier_checkbuttons[modifier] = check
            column += 1
        labelFnAdvice=Label(self.frameControlsBasic,justify=LEFT,
                            text=\
                            "Select the desired modifier keys\n"+
                            "above, and the final key from the\n"+
                            "list on the right.\n\n" +
                            "Use upper case Symbols when using\n" +
                            "the Shift modifier.  (Letters will be\n" +
                            "converted automatically.)")
        labelFnAdvice.grid(row=1,column=0,columnspan=4,padx=2,sticky=W)
        self.listKeysFinal=Listbox(self.frameControlsBasic,width=15,height=10,
                selectmode=SINGLE)
        self.listKeysFinal.bind('<ButtonRelease-1>',self.FinalKeySelected)
        self.listKeysFinal.grid(row=0,column=4,rowspan=4,sticky=NS)
        scrollKeysFinal=Scrollbar(self.frameControlsBasic,orient=VERTICAL,
                command=self.listKeysFinal.yview)
        self.listKeysFinal.config(yscrollcommand=scrollKeysFinal.set)
        scrollKeysFinal.grid(row=0,column=5,rowspan=4,sticky=NS)
        self.buttonClear=Button(self.frameControlsBasic,
                text='Clear Keys',command=self.ClearKeySeq)
        self.buttonClear.grid(row=2,column=0,columnspan=4)
        labelTitleAdvanced = Label(self.frameKeySeqAdvanced,justify=LEFT,
                text="Enter new binding(s) for  '"+self.action+"' :\n"+
                "(These bindings will not be checked for validity!)")
        labelTitleAdvanced.pack(anchor=W)
        self.entryKeysAdvanced=Entry(self.frameKeySeqAdvanced,
                textvariable=self.keyString)
        self.entryKeysAdvanced.pack(fill=X)
        labelHelpAdvanced=Label(self.frameHelpAdvanced,justify=LEFT,
            text="Key bindings are specified using Tkinter keysyms as\n"+
                 "in these samples: <Control-f>, <Shift-F2>, <F12>,\n"
                 "<Control-space>, <Meta-less>, <Control-Alt-Shift-X>.\n"
                 "Upper case is used when the Shift modifier is present!\n\n" +
                 "'Emacs style' multi-keystroke bindings are specified as\n" +
                 "follows: <Control-x><Control-y>, where the first key\n" +
                 "is the 'do-nothing' keybinding.\n\n" +
                 "Multiple separate bindings for one action should be\n"+
                 "separated by a space, eg., <Alt-v> <Meta-v>." )
        labelHelpAdvanced.grid(row=0,column=0,sticky=NSEW)

    def SetModifiersForPlatform(self):
        """Determine list of names of key modifiers for this platform.

        The names are used to build Tk bindings -- it doesn't matter if the
        keyboard has these keys, it matters if Tk understands them. The
        order is also important: key binding equality depends on it, so
        config-keys.def must use the same ordering.
        """
        if sys.platform == "darwin":
            self.modifiers = ['Shift', 'Control', 'Option', 'Command']
        else:
            self.modifiers = ['Control', 'Alt', 'Shift']
        self.modifier_label = {'Control': 'Ctrl'} # short name

    def ToggleLevel(self):
        if  self.buttonLevel.cget('text')[:8]=='Advanced':
            self.ClearKeySeq()
            self.buttonLevel.config(text='<< Basic Key Binding Entry')
            self.frameKeySeqAdvanced.lift()
            self.frameHelpAdvanced.lift()
            self.entryKeysAdvanced.focus_set()
            self.advanced = True
        else:
            self.ClearKeySeq()
            self.buttonLevel.config(text='Advanced Key Binding Entry >>')
            self.frameKeySeqBasic.lift()
            self.frameControlsBasic.lift()
            self.advanced = False

    def FinalKeySelected(self,event):
        self.BuildKeyString()

    def BuildKeyString(self):
        keyList = modifiers = self.GetModifiers()
        finalKey = self.listKeysFinal.get(ANCHOR)
        if finalKey:
            finalKey = self.TranslateKey(finalKey, modifiers)
            keyList.append(finalKey)
        self.keyString.set('<' + string.join(keyList,'-') + '>')

    def GetModifiers(self):
        modList = [variable.get() for variable in self.modifier_vars]
        return [mod for mod in modList if mod]

    def ClearKeySeq(self):
        self.listKeysFinal.select_clear(0,END)
        self.listKeysFinal.yview(MOVETO, '0.0')
        for variable in self.modifier_vars:
            variable.set('')
        self.keyString.set('')

    def LoadFinalKeyList(self):
        #these tuples are also available for use in validity checks
        self.functionKeys=('F1','F2','F2','F4','F5','F6','F7','F8','F9',
                'F10','F11','F12')
        self.alphanumKeys=tuple(string.ascii_lowercase+string.digits)
        self.punctuationKeys=tuple('~!@#%^&*()_-+={}[]|;:,.<>/?')
        self.whitespaceKeys=('Tab','Space','Return')
        self.editKeys=('BackSpace','Delete','Insert')
        self.moveKeys=('Home','End','Page Up','Page Down','Left Arrow',
                'Right Arrow','Up Arrow','Down Arrow')
        #make a tuple of most of the useful common 'final' keys
        keys=(self.alphanumKeys+self.punctuationKeys+self.functionKeys+
                self.whitespaceKeys+self.editKeys+self.moveKeys)
        self.listKeysFinal.insert(END, *keys)

    def TranslateKey(self, key, modifiers):
        "Translate from keycap symbol to the Tkinter keysym"
        translateDict = {'Space':'space',
                '~':'asciitilde','!':'exclam','@':'at','#':'numbersign',
                '%':'percent','^':'asciicircum','&':'ampersand','*':'asterisk',
                '(':'parenleft',')':'parenright','_':'underscore','-':'minus',
                '+':'plus','=':'equal','{':'braceleft','}':'braceright',
                '[':'bracketleft',']':'bracketright','|':'bar',';':'semicolon',
                ':':'colon',',':'comma','.':'period','<':'less','>':'greater',
                '/':'slash','?':'question','Page Up':'Prior','Page Down':'Next',
                'Left Arrow':'Left','Right Arrow':'Right','Up Arrow':'Up',
                'Down Arrow': 'Down', 'Tab':'Tab'}
        if key in translateDict.keys():
            key = translateDict[key]
        if 'Shift' in modifiers and key in string.ascii_lowercase:
            key = key.upper()
        key = 'Key-' + key
        return key

    def OK(self, event=None):
        if self.advanced or self.KeysOK():  # doesn't check advanced string yet
            self.result=self.keyString.get()
            self.destroy()

    def Cancel(self, event=None):
        self.result=''
        self.destroy()

    def KeysOK(self):
        '''Validity check on user's 'basic' keybinding selection.

        Doesn't check the string produced by the advanced dialog because
        'modifiers' isn't set.

        '''
        keys = self.keyString.get()
        keys.strip()
        finalKey = self.listKeysFinal.get(ANCHOR)
        modifiers = self.GetModifiers()
        # create a key sequence list for overlap check:
        keySequence = keys.split()
        keysOK = False
        title = 'Key Sequence Error'
        if not keys:
            tkMessageBox.showerror(title=title, parent=self,
                                   message='No keys specified.')
        elif not keys.endswith('>'):
            tkMessageBox.showerror(title=title, parent=self,
                                   message='Missing the final Key')
        elif (not modifiers
              and finalKey not in self.functionKeys + self.moveKeys):
            tkMessageBox.showerror(title=title, parent=self,
                                   message='No modifier key(s) specified.')
        elif (modifiers == ['Shift']) \
                 and (finalKey not in
                      self.functionKeys + self.moveKeys + ('Tab', 'Space')):
            msg = 'The shift modifier by itself may not be used with'\
                  ' this key symbol.'
            tkMessageBox.showerror(title=title, parent=self, message=msg)
        elif keySequence in self.currentKeySequences:
            msg = 'This key combination is already in use.'
            tkMessageBox.showerror(title=title, parent=self, message=msg)
        else:
            keysOK = True
        return keysOK

if __name__ == '__main__':
    from idlelib.idle_test.htest import run
    run(GetKeysDialog)
