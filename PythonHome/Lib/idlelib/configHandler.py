"""Provides access to stored IDLE configuration information.

Refer to the comments at the beginning of config-main.def for a description of
the available configuration files and the design implemented to update user
configuration information.  In particular, user configuration choices which
duplicate the defaults will be removed from the user's configuration files,
and if a file becomes empty, it will be deleted.

The contents of the user files may be altered using the Options/Configure IDLE
menu to access the configuration GUI (configDialog.py), or manually.

Throughout this module there is an emphasis on returning useable defaults
when a problem occurs in returning a requested configuration value back to
idle. This is to allow IDLE to continue to function in spite of errors in
the retrieval of config information. When a default is returned instead of
a requested config value, a message is printed to stderr to aid in
configuration problem notification and resolution.

"""
import os
import sys
import string
from ConfigParser import ConfigParser, NoOptionError, NoSectionError

class InvalidConfigType(Exception): pass
class InvalidConfigSet(Exception): pass
class InvalidFgBg(Exception): pass
class InvalidTheme(Exception): pass

class IdleConfParser(ConfigParser):
    """
    A ConfigParser specialised for idle configuration file handling
    """
    def __init__(self, cfgFile, cfgDefaults=None):
        """
        cfgFile - string, fully specified configuration file name
        """
        self.file=cfgFile
        ConfigParser.__init__(self,defaults=cfgDefaults)

    def Get(self, section, option, type=None, default=None, raw=False):
        """
        Get an option value for given section/option or return default.
        If type is specified, return as type.
        """
        if not self.has_option(section, option):
            return default
        if type=='bool':
            return self.getboolean(section, option)
        elif type=='int':
            return self.getint(section, option)
        else:
            return self.get(section, option, raw=raw)

    def GetOptionList(self,section):
        """
        Get an option list for given section
        """
        if self.has_section(section):
            return self.options(section)
        else:  #return a default value
            return []

    def Load(self):
        """
        Load the configuration file from disk
        """
        self.read(self.file)

class IdleUserConfParser(IdleConfParser):
    """
    IdleConfigParser specialised for user configuration handling.
    """

    def AddSection(self,section):
        """
        if section doesn't exist, add it
        """
        if not self.has_section(section):
            self.add_section(section)

    def RemoveEmptySections(self):
        """
        remove any sections that have no options
        """
        for section in self.sections():
            if not self.GetOptionList(section):
                self.remove_section(section)

    def IsEmpty(self):
        """
        Remove empty sections and then return 1 if parser has no sections
        left, else return 0.
        """
        self.RemoveEmptySections()
        if self.sections():
            return 0
        else:
            return 1

    def RemoveOption(self,section,option):
        """
        If section/option exists, remove it.
        Returns 1 if option was removed, 0 otherwise.
        """
        if self.has_section(section):
            return self.remove_option(section,option)

    def SetOption(self,section,option,value):
        """
        Sets option to value, adding section if required.
        Returns 1 if option was added or changed, otherwise 0.
        """
        if self.has_option(section,option):
            if self.get(section,option)==value:
                return 0
            else:
                self.set(section,option,value)
                return 1
        else:
            if not self.has_section(section):
                self.add_section(section)
            self.set(section,option,value)
            return 1

    def RemoveFile(self):
        """
        Removes the user config file from disk if it exists.
        """
        if os.path.exists(self.file):
            os.remove(self.file)

    def Save(self):
        """Update user configuration file.

        Remove empty sections. If resulting config isn't empty, write the file
        to disk. If config is empty, remove the file from disk if it exists.

        """
        if not self.IsEmpty():
            fname = self.file
            try:
                cfgFile = open(fname, 'w')
            except IOError:
                os.unlink(fname)
                cfgFile = open(fname, 'w')
            self.write(cfgFile)
        else:
            self.RemoveFile()

class IdleConf:
    """
    holds config parsers for all idle config files:
    default config files
        (idle install dir)/config-main.def
        (idle install dir)/config-extensions.def
        (idle install dir)/config-highlight.def
        (idle install dir)/config-keys.def
    user config  files
        (user home dir)/.idlerc/config-main.cfg
        (user home dir)/.idlerc/config-extensions.cfg
        (user home dir)/.idlerc/config-highlight.cfg
        (user home dir)/.idlerc/config-keys.cfg
    """
    def __init__(self):
        self.defaultCfg={}
        self.userCfg={}
        self.cfg={}
        self.CreateConfigHandlers()
        self.LoadCfgFiles()
        #self.LoadCfg()

    def CreateConfigHandlers(self):
        """
        set up a dictionary of config parsers for default and user
        configurations respectively
        """
        #build idle install path
        if __name__ != '__main__': # we were imported
            idleDir=os.path.dirname(__file__)
        else: # we were exec'ed (for testing only)
            idleDir=os.path.abspath(sys.path[0])
        userDir=self.GetUserCfgDir()
        configTypes=('main','extensions','highlight','keys')
        defCfgFiles={}
        usrCfgFiles={}
        for cfgType in configTypes: #build config file names
            defCfgFiles[cfgType]=os.path.join(idleDir,'config-'+cfgType+'.def')
            usrCfgFiles[cfgType]=os.path.join(userDir,'config-'+cfgType+'.cfg')
        for cfgType in configTypes: #create config parsers
            self.defaultCfg[cfgType]=IdleConfParser(defCfgFiles[cfgType])
            self.userCfg[cfgType]=IdleUserConfParser(usrCfgFiles[cfgType])

    def GetUserCfgDir(self):
        """
        Creates (if required) and returns a filesystem directory for storing
        user config files.

        """
        cfgDir = '.idlerc'
        userDir = os.path.expanduser('~')
        if userDir != '~': # expanduser() found user home dir
            if not os.path.exists(userDir):
                warn = ('\n Warning: os.path.expanduser("~") points to\n '+
                        userDir+',\n but the path does not exist.\n')
                try:
                    sys.stderr.write(warn)
                except IOError:
                    pass
                userDir = '~'
        if userDir == "~": # still no path to home!
            # traditionally IDLE has defaulted to os.getcwd(), is this adequate?
            userDir = os.getcwd()
        userDir = os.path.join(userDir, cfgDir)
        if not os.path.exists(userDir):
            try:
                os.mkdir(userDir)
            except (OSError, IOError):
                warn = ('\n Warning: unable to create user config directory\n'+
                        userDir+'\n Check path and permissions.\n Exiting!\n\n')
                sys.stderr.write(warn)
                raise SystemExit
        return userDir

    def GetOption(self, configType, section, option, default=None, type=None,
                  warn_on_default=True, raw=False):
        """
        Get an option value for given config type and given general
        configuration section/option or return a default. If type is specified,
        return as type. Firstly the user configuration is checked, with a
        fallback to the default configuration, and a final 'catch all'
        fallback to a useable passed-in default if the option isn't present in
        either the user or the default configuration.
        configType must be one of ('main','extensions','highlight','keys')
        If a default is returned, and warn_on_default is True, a warning is
        printed to stderr.

        """
        try:
            if self.userCfg[configType].has_option(section,option):
                return self.userCfg[configType].Get(section, option,
                                                    type=type, raw=raw)
        except ValueError:
            warning = ('\n Warning: configHandler.py - IdleConf.GetOption -\n'
                       ' invalid %r value for configuration option %r\n'
                       ' from section %r: %r\n' %
                       (type, option, section,
                        self.userCfg[configType].Get(section, option,
                                                     raw=raw)))
            try:
                sys.stderr.write(warning)
            except IOError:
                pass
        try:
            if self.defaultCfg[configType].has_option(section,option):
                return self.defaultCfg[configType].Get(section, option,
                                                       type=type, raw=raw)
        except ValueError:
            pass
        #returning default, print warning
        if warn_on_default:
            warning = ('\n Warning: configHandler.py - IdleConf.GetOption -\n'
                       ' problem retrieving configuration option %r\n'
                       ' from section %r.\n'
                       ' returning default value: %r\n' %
                       (option, section, default))
            try:
                sys.stderr.write(warning)
            except IOError:
                pass
        return default

    def SetOption(self, configType, section, option, value):
        """In user's config file, set section's option to value.

        """
        self.userCfg[configType].SetOption(section, option, value)

    def GetSectionList(self, configSet, configType):
        """
        Get a list of sections from either the user or default config for
        the given config type.
        configSet must be either 'user' or 'default'
        configType must be one of ('main','extensions','highlight','keys')
        """
        if not (configType in ('main','extensions','highlight','keys')):
            raise InvalidConfigType, 'Invalid configType specified'
        if configSet == 'user':
            cfgParser=self.userCfg[configType]
        elif configSet == 'default':
            cfgParser=self.defaultCfg[configType]
        else:
            raise InvalidConfigSet, 'Invalid configSet specified'
        return cfgParser.sections()

    def GetHighlight(self, theme, element, fgBg=None):
        """
        return individual highlighting theme elements.
        fgBg - string ('fg'or'bg') or None, if None return a dictionary
        containing fg and bg colours (appropriate for passing to Tkinter in,
        e.g., a tag_config call), otherwise fg or bg colour only as specified.
        """
        if self.defaultCfg['highlight'].has_section(theme):
            themeDict=self.GetThemeDict('default',theme)
        else:
            themeDict=self.GetThemeDict('user',theme)
        fore=themeDict[element+'-foreground']
        if element=='cursor': #there is no config value for cursor bg
            back=themeDict['normal-background']
        else:
            back=themeDict[element+'-background']
        highlight={"foreground": fore,"background": back}
        if not fgBg: #return dict of both colours
            return highlight
        else: #return specified colour only
            if fgBg == 'fg':
                return highlight["foreground"]
            if fgBg == 'bg':
                return highlight["background"]
            else:
                raise InvalidFgBg, 'Invalid fgBg specified'

    def GetThemeDict(self,type,themeName):
        """
        type - string, 'default' or 'user' theme type
        themeName - string, theme name
        Returns a dictionary which holds {option:value} for each element
        in the specified theme. Values are loaded over a set of ultimate last
        fallback defaults to guarantee that all theme elements are present in
        a newly created theme.
        """
        if type == 'user':
            cfgParser=self.userCfg['highlight']
        elif type == 'default':
            cfgParser=self.defaultCfg['highlight']
        else:
            raise InvalidTheme, 'Invalid theme type specified'
        #foreground and background values are provded for each theme element
        #(apart from cursor) even though all these values are not yet used
        #by idle, to allow for their use in the future. Default values are
        #generally black and white.
        theme={ 'normal-foreground':'#000000',
                'normal-background':'#ffffff',
                'keyword-foreground':'#000000',
                'keyword-background':'#ffffff',
                'builtin-foreground':'#000000',
                'builtin-background':'#ffffff',
                'comment-foreground':'#000000',
                'comment-background':'#ffffff',
                'string-foreground':'#000000',
                'string-background':'#ffffff',
                'definition-foreground':'#000000',
                'definition-background':'#ffffff',
                'hilite-foreground':'#000000',
                'hilite-background':'gray',
                'break-foreground':'#ffffff',
                'break-background':'#000000',
                'hit-foreground':'#ffffff',
                'hit-background':'#000000',
                'error-foreground':'#ffffff',
                'error-background':'#000000',
                #cursor (only foreground can be set)
                'cursor-foreground':'#000000',
                #shell window
                'stdout-foreground':'#000000',
                'stdout-background':'#ffffff',
                'stderr-foreground':'#000000',
                'stderr-background':'#ffffff',
                'console-foreground':'#000000',
                'console-background':'#ffffff' }
        for element in theme.keys():
            if not cfgParser.has_option(themeName,element):
                #we are going to return a default, print warning
                warning=('\n Warning: configHandler.py - IdleConf.GetThemeDict'
                           ' -\n problem retrieving theme element %r'
                           '\n from theme %r.\n'
                           ' returning default value: %r\n' %
                           (element, themeName, theme[element]))
                try:
                    sys.stderr.write(warning)
                except IOError:
                    pass
            colour=cfgParser.Get(themeName,element,default=theme[element])
            theme[element]=colour
        return theme

    def CurrentTheme(self):
        """
        Returns the name of the currently active theme
        """
        return self.GetOption('main','Theme','name',default='')

    def CurrentKeys(self):
        """
        Returns the name of the currently active key set
        """
        return self.GetOption('main','Keys','name',default='')

    def GetExtensions(self, active_only=True, editor_only=False, shell_only=False):
        """
        Gets a list of all idle extensions declared in the config files.
        active_only - boolean, if true only return active (enabled) extensions
        """
        extns=self.RemoveKeyBindNames(
                self.GetSectionList('default','extensions'))
        userExtns=self.RemoveKeyBindNames(
                self.GetSectionList('user','extensions'))
        for extn in userExtns:
            if extn not in extns: #user has added own extension
                extns.append(extn)
        if active_only:
            activeExtns=[]
            for extn in extns:
                if self.GetOption('extensions', extn, 'enable', default=True,
                                  type='bool'):
                    #the extension is enabled
                    if editor_only or shell_only:
                        if editor_only:
                            option = "enable_editor"
                        else:
                            option = "enable_shell"
                        if self.GetOption('extensions', extn,option,
                                          default=True, type='bool',
                                          warn_on_default=False):
                            activeExtns.append(extn)
                    else:
                        activeExtns.append(extn)
            return activeExtns
        else:
            return extns

    def RemoveKeyBindNames(self,extnNameList):
        #get rid of keybinding section names
        names=extnNameList
        kbNameIndicies=[]
        for name in names:
            if name.endswith(('_bindings', '_cfgBindings')):
                kbNameIndicies.append(names.index(name))
        kbNameIndicies.sort()
        kbNameIndicies.reverse()
        for index in kbNameIndicies: #delete each keybinding section name
            del(names[index])
        return names

    def GetExtnNameForEvent(self,virtualEvent):
        """
        Returns the name of the extension that virtualEvent is bound in, or
        None if not bound in any extension.
        virtualEvent - string, name of the virtual event to test for, without
                       the enclosing '<< >>'
        """
        extName=None
        vEvent='<<'+virtualEvent+'>>'
        for extn in self.GetExtensions(active_only=0):
            for event in self.GetExtensionKeys(extn).keys():
                if event == vEvent:
                    extName=extn
        return extName

    def GetExtensionKeys(self,extensionName):
        """
        returns a dictionary of the configurable keybindings for a particular
        extension,as they exist in the dictionary returned by GetCurrentKeySet;
        that is, where previously used bindings are disabled.
        """
        keysName=extensionName+'_cfgBindings'
        activeKeys=self.GetCurrentKeySet()
        extKeys={}
        if self.defaultCfg['extensions'].has_section(keysName):
            eventNames=self.defaultCfg['extensions'].GetOptionList(keysName)
            for eventName in eventNames:
                event='<<'+eventName+'>>'
                binding=activeKeys[event]
                extKeys[event]=binding
        return extKeys

    def __GetRawExtensionKeys(self,extensionName):
        """
        returns a dictionary of the configurable keybindings for a particular
        extension, as defined in the configuration files, or an empty dictionary
        if no bindings are found
        """
        keysName=extensionName+'_cfgBindings'
        extKeys={}
        if self.defaultCfg['extensions'].has_section(keysName):
            eventNames=self.defaultCfg['extensions'].GetOptionList(keysName)
            for eventName in eventNames:
                binding=self.GetOption('extensions',keysName,
                        eventName,default='').split()
                event='<<'+eventName+'>>'
                extKeys[event]=binding
        return extKeys

    def GetExtensionBindings(self,extensionName):
        """
        Returns a dictionary of all the event bindings for a particular
        extension. The configurable keybindings are returned as they exist in
        the dictionary returned by GetCurrentKeySet; that is, where re-used
        keybindings are disabled.
        """
        bindsName=extensionName+'_bindings'
        extBinds=self.GetExtensionKeys(extensionName)
        #add the non-configurable bindings
        if self.defaultCfg['extensions'].has_section(bindsName):
            eventNames=self.defaultCfg['extensions'].GetOptionList(bindsName)
            for eventName in eventNames:
                binding=self.GetOption('extensions',bindsName,
                        eventName,default='').split()
                event='<<'+eventName+'>>'
                extBinds[event]=binding

        return extBinds

    def GetKeyBinding(self, keySetName, eventStr):
        """
        returns the keybinding for a specific event.
        keySetName - string, name of key binding set
        eventStr - string, the virtual event we want the binding for,
                   represented as a string, eg. '<<event>>'
        """
        eventName=eventStr[2:-2] #trim off the angle brackets
        binding=self.GetOption('keys',keySetName,eventName,default='').split()
        return binding

    def GetCurrentKeySet(self):
        result = self.GetKeySet(self.CurrentKeys())

        if sys.platform == "darwin":
            # OS X Tk variants do not support the "Alt" keyboard modifier.
            # So replace all keybingings that use "Alt" with ones that
            # use the "Option" keyboard modifier.
            # TO DO: the "Option" modifier does not work properly for
            #        Cocoa Tk and XQuartz Tk so we should not use it
            #        in default OS X KeySets.
            for k, v in result.items():
                v2 = [ x.replace('<Alt-', '<Option-') for x in v ]
                if v != v2:
                    result[k] = v2

        return result

    def GetKeySet(self,keySetName):
        """
        Returns a dictionary of: all requested core keybindings, plus the
        keybindings for all currently active extensions. If a binding defined
        in an extension is already in use, that binding is disabled.
        """
        keySet=self.GetCoreKeys(keySetName)
        activeExtns=self.GetExtensions(active_only=1)
        for extn in activeExtns:
            extKeys=self.__GetRawExtensionKeys(extn)
            if extKeys: #the extension defines keybindings
                for event in extKeys.keys():
                    if extKeys[event] in keySet.values():
                        #the binding is already in use
                        extKeys[event]='' #disable this binding
                    keySet[event]=extKeys[event] #add binding
        return keySet

    def IsCoreBinding(self,virtualEvent):
        """
        returns true if the virtual event is bound in the core idle keybindings.
        virtualEvent - string, name of the virtual event to test for, without
                       the enclosing '<< >>'
        """
        return ('<<'+virtualEvent+'>>') in self.GetCoreKeys().keys()

    def GetCoreKeys(self, keySetName=None):
        """
        returns the requested set of core keybindings, with fallbacks if
        required.
        Keybindings loaded from the config file(s) are loaded _over_ these
        defaults, so if there is a problem getting any core binding there will
        be an 'ultimate last resort fallback' to the CUA-ish bindings
        defined here.
        """
        keyBindings={
            '<<copy>>': ['<Control-c>', '<Control-C>'],
            '<<cut>>': ['<Control-x>', '<Control-X>'],
            '<<paste>>': ['<Control-v>', '<Control-V>'],
            '<<beginning-of-line>>': ['<Control-a>', '<Home>'],
            '<<center-insert>>': ['<Control-l>'],
            '<<close-all-windows>>': ['<Control-q>'],
            '<<close-window>>': ['<Alt-F4>'],
            '<<do-nothing>>': ['<Control-x>'],
            '<<end-of-file>>': ['<Control-d>'],
            '<<python-docs>>': ['<F1>'],
            '<<python-context-help>>': ['<Shift-F1>'],
            '<<history-next>>': ['<Alt-n>'],
            '<<history-previous>>': ['<Alt-p>'],
            '<<interrupt-execution>>': ['<Control-c>'],
            '<<view-restart>>': ['<F6>'],
            '<<restart-shell>>': ['<Control-F6>'],
            '<<open-class-browser>>': ['<Alt-c>'],
            '<<open-module>>': ['<Alt-m>'],
            '<<open-new-window>>': ['<Control-n>'],
            '<<open-window-from-file>>': ['<Control-o>'],
            '<<plain-newline-and-indent>>': ['<Control-j>'],
            '<<print-window>>': ['<Control-p>'],
            '<<redo>>': ['<Control-y>'],
            '<<remove-selection>>': ['<Escape>'],
            '<<save-copy-of-window-as-file>>': ['<Alt-Shift-S>'],
            '<<save-window-as-file>>': ['<Alt-s>'],
            '<<save-window>>': ['<Control-s>'],
            '<<select-all>>': ['<Alt-a>'],
            '<<toggle-auto-coloring>>': ['<Control-slash>'],
            '<<undo>>': ['<Control-z>'],
            '<<find-again>>': ['<Control-g>', '<F3>'],
            '<<find-in-files>>': ['<Alt-F3>'],
            '<<find-selection>>': ['<Control-F3>'],
            '<<find>>': ['<Control-f>'],
            '<<replace>>': ['<Control-h>'],
            '<<goto-line>>': ['<Alt-g>'],
            '<<smart-backspace>>': ['<Key-BackSpace>'],
            '<<newline-and-indent>>': ['<Key-Return>', '<Key-KP_Enter>'],
            '<<smart-indent>>': ['<Key-Tab>'],
            '<<indent-region>>': ['<Control-Key-bracketright>'],
            '<<dedent-region>>': ['<Control-Key-bracketleft>'],
            '<<comment-region>>': ['<Alt-Key-3>'],
            '<<uncomment-region>>': ['<Alt-Key-4>'],
            '<<tabify-region>>': ['<Alt-Key-5>'],
            '<<untabify-region>>': ['<Alt-Key-6>'],
            '<<toggle-tabs>>': ['<Alt-Key-t>'],
            '<<change-indentwidth>>': ['<Alt-Key-u>'],
            '<<del-word-left>>': ['<Control-Key-BackSpace>'],
            '<<del-word-right>>': ['<Control-Key-Delete>']
            }
        if keySetName:
            for event in keyBindings.keys():
                binding=self.GetKeyBinding(keySetName,event)
                if binding:
                    keyBindings[event]=binding
                else: #we are going to return a default, print warning
                    warning=('\n Warning: configHandler.py - IdleConf.GetCoreKeys'
                               ' -\n problem retrieving key binding for event %r'
                               '\n from key set %r.\n'
                               ' returning default value: %r\n' %
                               (event, keySetName, keyBindings[event]))
                    try:
                        sys.stderr.write(warning)
                    except IOError:
                        pass
        return keyBindings

    def GetExtraHelpSourceList(self,configSet):
        """Fetch list of extra help sources from a given configSet.

        Valid configSets are 'user' or 'default'.  Return a list of tuples of
        the form (menu_item , path_to_help_file , option), or return the empty
        list.  'option' is the sequence number of the help resource.  'option'
        values determine the position of the menu items on the Help menu,
        therefore the returned list must be sorted by 'option'.

        """
        helpSources=[]
        if configSet=='user':
            cfgParser=self.userCfg['main']
        elif configSet=='default':
            cfgParser=self.defaultCfg['main']
        else:
            raise InvalidConfigSet, 'Invalid configSet specified'
        options=cfgParser.GetOptionList('HelpFiles')
        for option in options:
            value=cfgParser.Get('HelpFiles',option,default=';')
            if value.find(';')==-1: #malformed config entry with no ';'
                menuItem='' #make these empty
                helpPath='' #so value won't be added to list
            else: #config entry contains ';' as expected
                value=string.split(value,';')
                menuItem=value[0].strip()
                helpPath=value[1].strip()
            if menuItem and helpPath: #neither are empty strings
                helpSources.append( (menuItem,helpPath,option) )
        helpSources.sort(key=lambda x: int(x[2]))
        return helpSources

    def GetAllExtraHelpSourcesList(self):
        """
        Returns a list of tuples containing the details of all additional help
        sources configured, or an empty list if there are none. Tuples are of
        the format returned by GetExtraHelpSourceList.
        """
        allHelpSources=( self.GetExtraHelpSourceList('default')+
                self.GetExtraHelpSourceList('user') )
        return allHelpSources

    def LoadCfgFiles(self):
        """
        load all configuration files.
        """
        for key in self.defaultCfg.keys():
            self.defaultCfg[key].Load()
            self.userCfg[key].Load() #same keys

    def SaveUserCfgFiles(self):
        """
        write all loaded user configuration files back to disk
        """
        for key in self.userCfg.keys():
            self.userCfg[key].Save()

idleConf=IdleConf()

### module test
if __name__ == '__main__':
    def dumpCfg(cfg):
        print '\n',cfg,'\n'
        for key in cfg.keys():
            sections=cfg[key].sections()
            print key
            print sections
            for section in sections:
                options=cfg[key].options(section)
                print section
                print options
                for option in options:
                    print option, '=', cfg[key].Get(section,option)
    dumpCfg(idleConf.defaultCfg)
    dumpCfg(idleConf.userCfg)
    print idleConf.userCfg['main'].Get('Theme','name')
    #print idleConf.userCfg['highlight'].GetDefHighlight('Foo','normal')
