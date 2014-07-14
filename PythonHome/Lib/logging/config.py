# Copyright 2001-2014 by Vinay Sajip. All Rights Reserved.
#
# Permission to use, copy, modify, and distribute this software and its
# documentation for any purpose and without fee is hereby granted,
# provided that the above copyright notice appear in all copies and that
# both that copyright notice and this permission notice appear in
# supporting documentation, and that the name of Vinay Sajip
# not be used in advertising or publicity pertaining to distribution
# of the software without specific, written prior permission.
# VINAY SAJIP DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE, INCLUDING
# ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL
# VINAY SAJIP BE LIABLE FOR ANY SPECIAL, INDIRECT OR CONSEQUENTIAL DAMAGES OR
# ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER
# IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT
# OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

"""
Configuration functions for the logging package for Python. The core package
is based on PEP 282 and comments thereto in comp.lang.python, and influenced
by Apache's log4j system.

Copyright (C) 2001-2014 Vinay Sajip. All Rights Reserved.

To use, simply 'import logging' and log away!
"""

import cStringIO
import errno
import io
import logging
import logging.handlers
import os
import re
import socket
import struct
import sys
import traceback
import types

try:
    import thread
    import threading
except ImportError:
    thread = None

from SocketServer import ThreadingTCPServer, StreamRequestHandler


DEFAULT_LOGGING_CONFIG_PORT = 9030

RESET_ERROR = errno.ECONNRESET

#
#   The following code implements a socket listener for on-the-fly
#   reconfiguration of logging.
#
#   _listener holds the server object doing the listening
_listener = None

def fileConfig(fname, defaults=None, disable_existing_loggers=True):
    """
    Read the logging configuration from a ConfigParser-format file.

    This can be called several times from an application, allowing an end user
    the ability to select from various pre-canned configurations (if the
    developer provides a mechanism to present the choices and load the chosen
    configuration).
    """
    import ConfigParser

    cp = ConfigParser.ConfigParser(defaults)
    if hasattr(fname, 'readline'):
        cp.readfp(fname)
    else:
        cp.read(fname)

    formatters = _create_formatters(cp)

    # critical section
    logging._acquireLock()
    try:
        logging._handlers.clear()
        del logging._handlerList[:]
        # Handlers add themselves to logging._handlers
        handlers = _install_handlers(cp, formatters)
        _install_loggers(cp, handlers, disable_existing_loggers)
    finally:
        logging._releaseLock()


def _resolve(name):
    """Resolve a dotted name to a global object."""
    name = name.split('.')
    used = name.pop(0)
    found = __import__(used)
    for n in name:
        used = used + '.' + n
        try:
            found = getattr(found, n)
        except AttributeError:
            __import__(used)
            found = getattr(found, n)
    return found

def _strip_spaces(alist):
    return map(lambda x: x.strip(), alist)

def _encoded(s):
    return s if isinstance(s, str) else s.encode('utf-8')

def _create_formatters(cp):
    """Create and return formatters"""
    flist = cp.get("formatters", "keys")
    if not len(flist):
        return {}
    flist = flist.split(",")
    flist = _strip_spaces(flist)
    formatters = {}
    for form in flist:
        sectname = "formatter_%s" % form
        opts = cp.options(sectname)
        if "format" in opts:
            fs = cp.get(sectname, "format", 1)
        else:
            fs = None
        if "datefmt" in opts:
            dfs = cp.get(sectname, "datefmt", 1)
        else:
            dfs = None
        c = logging.Formatter
        if "class" in opts:
            class_name = cp.get(sectname, "class")
            if class_name:
                c = _resolve(class_name)
        f = c(fs, dfs)
        formatters[form] = f
    return formatters


def _install_handlers(cp, formatters):
    """Install and return handlers"""
    hlist = cp.get("handlers", "keys")
    if not len(hlist):
        return {}
    hlist = hlist.split(",")
    hlist = _strip_spaces(hlist)
    handlers = {}
    fixups = [] #for inter-handler references
    for hand in hlist:
        sectname = "handler_%s" % hand
        klass = cp.get(sectname, "class")
        opts = cp.options(sectname)
        if "formatter" in opts:
            fmt = cp.get(sectname, "formatter")
        else:
            fmt = ""
        try:
            klass = eval(klass, vars(logging))
        except (AttributeError, NameError):
            klass = _resolve(klass)
        args = cp.get(sectname, "args")
        args = eval(args, vars(logging))
        h = klass(*args)
        if "level" in opts:
            level = cp.get(sectname, "level")
            h.setLevel(logging._levelNames[level])
        if len(fmt):
            h.setFormatter(formatters[fmt])
        if issubclass(klass, logging.handlers.MemoryHandler):
            if "target" in opts:
                target = cp.get(sectname,"target")
            else:
                target = ""
            if len(target): #the target handler may not be loaded yet, so keep for later...
                fixups.append((h, target))
        handlers[hand] = h
    #now all handlers are loaded, fixup inter-handler references...
    for h, t in fixups:
        h.setTarget(handlers[t])
    return handlers


def _install_loggers(cp, handlers, disable_existing_loggers):
    """Create and install loggers"""

    # configure the root first
    llist = cp.get("loggers", "keys")
    llist = llist.split(",")
    llist = list(map(lambda x: x.strip(), llist))
    llist.remove("root")
    sectname = "logger_root"
    root = logging.root
    log = root
    opts = cp.options(sectname)
    if "level" in opts:
        level = cp.get(sectname, "level")
        log.setLevel(logging._levelNames[level])
    for h in root.handlers[:]:
        root.removeHandler(h)
    hlist = cp.get(sectname, "handlers")
    if len(hlist):
        hlist = hlist.split(",")
        hlist = _strip_spaces(hlist)
        for hand in hlist:
            log.addHandler(handlers[hand])

    #and now the others...
    #we don't want to lose the existing loggers,
    #since other threads may have pointers to them.
    #existing is set to contain all existing loggers,
    #and as we go through the new configuration we
    #remove any which are configured. At the end,
    #what's left in existing is the set of loggers
    #which were in the previous configuration but
    #which are not in the new configuration.
    existing = list(root.manager.loggerDict.keys())
    #The list needs to be sorted so that we can
    #avoid disabling child loggers of explicitly
    #named loggers. With a sorted list it is easier
    #to find the child loggers.
    existing.sort()
    #We'll keep the list of existing loggers
    #which are children of named loggers here...
    child_loggers = []
    #now set up the new ones...
    for log in llist:
        sectname = "logger_%s" % log
        qn = cp.get(sectname, "qualname")
        opts = cp.options(sectname)
        if "propagate" in opts:
            propagate = cp.getint(sectname, "propagate")
        else:
            propagate = 1
        logger = logging.getLogger(qn)
        if qn in existing:
            i = existing.index(qn) + 1 # start with the entry after qn
            prefixed = qn + "."
            pflen = len(prefixed)
            num_existing = len(existing)
            while i < num_existing:
                if existing[i][:pflen] == prefixed:
                    child_loggers.append(existing[i])
                i += 1
            existing.remove(qn)
        if "level" in opts:
            level = cp.get(sectname, "level")
            logger.setLevel(logging._levelNames[level])
        for h in logger.handlers[:]:
            logger.removeHandler(h)
        logger.propagate = propagate
        logger.disabled = 0
        hlist = cp.get(sectname, "handlers")
        if len(hlist):
            hlist = hlist.split(",")
            hlist = _strip_spaces(hlist)
            for hand in hlist:
                logger.addHandler(handlers[hand])

    #Disable any old loggers. There's no point deleting
    #them as other threads may continue to hold references
    #and by disabling them, you stop them doing any logging.
    #However, don't disable children of named loggers, as that's
    #probably not what was intended by the user.
    for log in existing:
        logger = root.manager.loggerDict[log]
        if log in child_loggers:
            logger.level = logging.NOTSET
            logger.handlers = []
            logger.propagate = 1
        else:
            logger.disabled = disable_existing_loggers



IDENTIFIER = re.compile('^[a-z_][a-z0-9_]*$', re.I)


def valid_ident(s):
    m = IDENTIFIER.match(s)
    if not m:
        raise ValueError('Not a valid Python identifier: %r' % s)
    return True


class ConvertingMixin(object):
    """For ConvertingXXX's, this mixin class provides common functions"""

    def convert_with_key(self, key, value, replace=True):
        result = self.configurator.convert(value)
        #If the converted value is different, save for next time
        if value is not result:
            if replace:
                self[key] = result
            if type(result) in (ConvertingDict, ConvertingList,
                               ConvertingTuple):
                result.parent = self
                result.key = key
        return result

    def convert(self, value):
        result = self.configurator.convert(value)
        if value is not result:
            if type(result) in (ConvertingDict, ConvertingList,
                               ConvertingTuple):
                result.parent = self
        return result


# The ConvertingXXX classes are wrappers around standard Python containers,
# and they serve to convert any suitable values in the container. The
# conversion converts base dicts, lists and tuples to their wrapped
# equivalents, whereas strings which match a conversion format are converted
# appropriately.
#
# Each wrapper should have a configurator attribute holding the actual
# configurator to use for conversion.

class ConvertingDict(dict, ConvertingMixin):
    """A converting dictionary wrapper."""

    def __getitem__(self, key):
        value = dict.__getitem__(self, key)
        return self.convert_with_key(key, value)

    def get(self, key, default=None):
        value = dict.get(self, key, default)
        return self.convert_with_key(key, value)

    def pop(self, key, default=None):
        value = dict.pop(self, key, default)
        return self.convert_with_key(key, value, replace=False)

class ConvertingList(list, ConvertingMixin):
    """A converting list wrapper."""
    def __getitem__(self, key):
        value = list.__getitem__(self, key)
        return self.convert_with_key(key, value)

    def pop(self, idx=-1):
        value = list.pop(self, idx)
        return self.convert(value)

class ConvertingTuple(tuple, ConvertingMixin):
    """A converting tuple wrapper."""
    def __getitem__(self, key):
        value = tuple.__getitem__(self, key)
        # Can't replace a tuple entry.
        return self.convert_with_key(key, value, replace=False)

class BaseConfigurator(object):
    """
    The configurator base class which defines some useful defaults.
    """

    CONVERT_PATTERN = re.compile(r'^(?P<prefix>[a-z]+)://(?P<suffix>.*)$')

    WORD_PATTERN = re.compile(r'^\s*(\w+)\s*')
    DOT_PATTERN = re.compile(r'^\.\s*(\w+)\s*')
    INDEX_PATTERN = re.compile(r'^\[\s*(\w+)\s*\]\s*')
    DIGIT_PATTERN = re.compile(r'^\d+$')

    value_converters = {
        'ext' : 'ext_convert',
        'cfg' : 'cfg_convert',
    }

    # We might want to use a different one, e.g. importlib
    importer = __import__

    def __init__(self, config):
        self.config = ConvertingDict(config)
        self.config.configurator = self
        # Issue 12718: winpdb replaces __import__ with a Python function, which
        # ends up being treated as a bound method. To avoid problems, we
        # set the importer on the instance, but leave it defined in the class
        # so existing code doesn't break
        if type(__import__) == types.FunctionType:
            self.importer = __import__

    def resolve(self, s):
        """
        Resolve strings to objects using standard import and attribute
        syntax.
        """
        name = s.split('.')
        used = name.pop(0)
        try:
            found = self.importer(used)
            for frag in name:
                used += '.' + frag
                try:
                    found = getattr(found, frag)
                except AttributeError:
                    self.importer(used)
                    found = getattr(found, frag)
            return found
        except ImportError:
            e, tb = sys.exc_info()[1:]
            v = ValueError('Cannot resolve %r: %s' % (s, e))
            v.__cause__, v.__traceback__ = e, tb
            raise v

    def ext_convert(self, value):
        """Default converter for the ext:// protocol."""
        return self.resolve(value)

    def cfg_convert(self, value):
        """Default converter for the cfg:// protocol."""
        rest = value
        m = self.WORD_PATTERN.match(rest)
        if m is None:
            raise ValueError("Unable to convert %r" % value)
        else:
            rest = rest[m.end():]
            d = self.config[m.groups()[0]]
            #print d, rest
            while rest:
                m = self.DOT_PATTERN.match(rest)
                if m:
                    d = d[m.groups()[0]]
                else:
                    m = self.INDEX_PATTERN.match(rest)
                    if m:
                        idx = m.groups()[0]
                        if not self.DIGIT_PATTERN.match(idx):
                            d = d[idx]
                        else:
                            try:
                                n = int(idx) # try as number first (most likely)
                                d = d[n]
                            except TypeError:
                                d = d[idx]
                if m:
                    rest = rest[m.end():]
                else:
                    raise ValueError('Unable to convert '
                                     '%r at %r' % (value, rest))
        #rest should be empty
        return d

    def convert(self, value):
        """
        Convert values to an appropriate type. dicts, lists and tuples are
        replaced by their converting alternatives. Strings are checked to
        see if they have a conversion format and are converted if they do.
        """
        if not isinstance(value, ConvertingDict) and isinstance(value, dict):
            value = ConvertingDict(value)
            value.configurator = self
        elif not isinstance(value, ConvertingList) and isinstance(value, list):
            value = ConvertingList(value)
            value.configurator = self
        elif not isinstance(value, ConvertingTuple) and\
                 isinstance(value, tuple):
            value = ConvertingTuple(value)
            value.configurator = self
        elif isinstance(value, basestring): # str for py3k
            m = self.CONVERT_PATTERN.match(value)
            if m:
                d = m.groupdict()
                prefix = d['prefix']
                converter = self.value_converters.get(prefix, None)
                if converter:
                    suffix = d['suffix']
                    converter = getattr(self, converter)
                    value = converter(suffix)
        return value

    def configure_custom(self, config):
        """Configure an object with a user-supplied factory."""
        c = config.pop('()')
        if not hasattr(c, '__call__') and hasattr(types, 'ClassType') and type(c) != types.ClassType:
            c = self.resolve(c)
        props = config.pop('.', None)
        # Check for valid identifiers
        kwargs = dict([(k, config[k]) for k in config if valid_ident(k)])
        result = c(**kwargs)
        if props:
            for name, value in props.items():
                setattr(result, name, value)
        return result

    def as_tuple(self, value):
        """Utility function which converts lists to tuples."""
        if isinstance(value, list):
            value = tuple(value)
        return value

class DictConfigurator(BaseConfigurator):
    """
    Configure logging using a dictionary-like object to describe the
    configuration.
    """

    def configure(self):
        """Do the configuration."""

        config = self.config
        if 'version' not in config:
            raise ValueError("dictionary doesn't specify a version")
        if config['version'] != 1:
            raise ValueError("Unsupported version: %s" % config['version'])
        incremental = config.pop('incremental', False)
        EMPTY_DICT = {}
        logging._acquireLock()
        try:
            if incremental:
                handlers = config.get('handlers', EMPTY_DICT)
                for name in handlers:
                    if name not in logging._handlers:
                        raise ValueError('No handler found with '
                                         'name %r'  % name)
                    else:
                        try:
                            handler = logging._handlers[name]
                            handler_config = handlers[name]
                            level = handler_config.get('level', None)
                            if level:
                                handler.setLevel(logging._checkLevel(level))
                        except StandardError as e:
                            raise ValueError('Unable to configure handler '
                                             '%r: %s' % (name, e))
                loggers = config.get('loggers', EMPTY_DICT)
                for name in loggers:
                    try:
                        self.configure_logger(name, loggers[name], True)
                    except StandardError as e:
                        raise ValueError('Unable to configure logger '
                                         '%r: %s' % (name, e))
                root = config.get('root', None)
                if root:
                    try:
                        self.configure_root(root, True)
                    except StandardError as e:
                        raise ValueError('Unable to configure root '
                                         'logger: %s' % e)
            else:
                disable_existing = config.pop('disable_existing_loggers', True)

                logging._handlers.clear()
                del logging._handlerList[:]

                # Do formatters first - they don't refer to anything else
                formatters = config.get('formatters', EMPTY_DICT)
                for name in formatters:
                    try:
                        formatters[name] = self.configure_formatter(
                                                            formatters[name])
                    except StandardError as e:
                        raise ValueError('Unable to configure '
                                         'formatter %r: %s' % (name, e))
                # Next, do filters - they don't refer to anything else, either
                filters = config.get('filters', EMPTY_DICT)
                for name in filters:
                    try:
                        filters[name] = self.configure_filter(filters[name])
                    except StandardError as e:
                        raise ValueError('Unable to configure '
                                         'filter %r: %s' % (name, e))

                # Next, do handlers - they refer to formatters and filters
                # As handlers can refer to other handlers, sort the keys
                # to allow a deterministic order of configuration
                handlers = config.get('handlers', EMPTY_DICT)
                deferred = []
                for name in sorted(handlers):
                    try:
                        handler = self.configure_handler(handlers[name])
                        handler.name = name
                        handlers[name] = handler
                    except StandardError as e:
                        if 'target not configured yet' in str(e):
                            deferred.append(name)
                        else:
                            raise ValueError('Unable to configure handler '
                                             '%r: %s' % (name, e))

                # Now do any that were deferred
                for name in deferred:
                    try:
                        handler = self.configure_handler(handlers[name])
                        handler.name = name
                        handlers[name] = handler
                    except StandardError as e:
                        raise ValueError('Unable to configure handler '
                                         '%r: %s' % (name, e))

                # Next, do loggers - they refer to handlers and filters

                #we don't want to lose the existing loggers,
                #since other threads may have pointers to them.
                #existing is set to contain all existing loggers,
                #and as we go through the new configuration we
                #remove any which are configured. At the end,
                #what's left in existing is the set of loggers
                #which were in the previous configuration but
                #which are not in the new configuration.
                root = logging.root
                existing = root.manager.loggerDict.keys()
                #The list needs to be sorted so that we can
                #avoid disabling child loggers of explicitly
                #named loggers. With a sorted list it is easier
                #to find the child loggers.
                existing.sort()
                #We'll keep the list of existing loggers
                #which are children of named loggers here...
                child_loggers = []
                #now set up the new ones...
                loggers = config.get('loggers', EMPTY_DICT)
                for name in loggers:
                    name = _encoded(name)
                    if name in existing:
                        i = existing.index(name)
                        prefixed = name + "."
                        pflen = len(prefixed)
                        num_existing = len(existing)
                        i = i + 1 # look at the entry after name
                        while (i < num_existing) and\
                              (existing[i][:pflen] == prefixed):
                            child_loggers.append(existing[i])
                            i = i + 1
                        existing.remove(name)
                    try:
                        self.configure_logger(name, loggers[name])
                    except StandardError as e:
                        raise ValueError('Unable to configure logger '
                                         '%r: %s' % (name, e))

                #Disable any old loggers. There's no point deleting
                #them as other threads may continue to hold references
                #and by disabling them, you stop them doing any logging.
                #However, don't disable children of named loggers, as that's
                #probably not what was intended by the user.
                for log in existing:
                    logger = root.manager.loggerDict[log]
                    if log in child_loggers:
                        logger.level = logging.NOTSET
                        logger.handlers = []
                        logger.propagate = True
                    elif disable_existing:
                        logger.disabled = True

                # And finally, do the root logger
                root = config.get('root', None)
                if root:
                    try:
                        self.configure_root(root)
                    except StandardError as e:
                        raise ValueError('Unable to configure root '
                                         'logger: %s' % e)
        finally:
            logging._releaseLock()

    def configure_formatter(self, config):
        """Configure a formatter from a dictionary."""
        if '()' in config:
            factory = config['()'] # for use in exception handler
            try:
                result = self.configure_custom(config)
            except TypeError as te:
                if "'format'" not in str(te):
                    raise
                #Name of parameter changed from fmt to format.
                #Retry with old name.
                #This is so that code can be used with older Python versions
                #(e.g. by Django)
                config['fmt'] = config.pop('format')
                config['()'] = factory
                result = self.configure_custom(config)
        else:
            fmt = config.get('format', None)
            dfmt = config.get('datefmt', None)
            result = logging.Formatter(fmt, dfmt)
        return result

    def configure_filter(self, config):
        """Configure a filter from a dictionary."""
        if '()' in config:
            result = self.configure_custom(config)
        else:
            name = config.get('name', '')
            result = logging.Filter(name)
        return result

    def add_filters(self, filterer, filters):
        """Add filters to a filterer from a list of names."""
        for f in filters:
            try:
                filterer.addFilter(self.config['filters'][f])
            except StandardError as e:
                raise ValueError('Unable to add filter %r: %s' % (f, e))

    def configure_handler(self, config):
        """Configure a handler from a dictionary."""
        formatter = config.pop('formatter', None)
        if formatter:
            try:
                formatter = self.config['formatters'][formatter]
            except StandardError as e:
                raise ValueError('Unable to set formatter '
                                 '%r: %s' % (formatter, e))
        level = config.pop('level', None)
        filters = config.pop('filters', None)
        if '()' in config:
            c = config.pop('()')
            if not hasattr(c, '__call__') and hasattr(types, 'ClassType') and type(c) != types.ClassType:
                c = self.resolve(c)
            factory = c
        else:
            cname = config.pop('class')
            klass = self.resolve(cname)
            #Special case for handler which refers to another handler
            if issubclass(klass, logging.handlers.MemoryHandler) and\
                'target' in config:
                try:
                    th = self.config['handlers'][config['target']]
                    if not isinstance(th, logging.Handler):
                        config['class'] = cname # restore for deferred configuration
                        raise StandardError('target not configured yet')
                    config['target'] = th
                except StandardError as e:
                    raise ValueError('Unable to set target handler '
                                     '%r: %s' % (config['target'], e))
            elif issubclass(klass, logging.handlers.SMTPHandler) and\
                'mailhost' in config:
                config['mailhost'] = self.as_tuple(config['mailhost'])
            elif issubclass(klass, logging.handlers.SysLogHandler) and\
                'address' in config:
                config['address'] = self.as_tuple(config['address'])
            factory = klass
        kwargs = dict([(k, config[k]) for k in config if valid_ident(k)])
        try:
            result = factory(**kwargs)
        except TypeError as te:
            if "'stream'" not in str(te):
                raise
            #The argument name changed from strm to stream
            #Retry with old name.
            #This is so that code can be used with older Python versions
            #(e.g. by Django)
            kwargs['strm'] = kwargs.pop('stream')
            result = factory(**kwargs)
        if formatter:
            result.setFormatter(formatter)
        if level is not None:
            result.setLevel(logging._checkLevel(level))
        if filters:
            self.add_filters(result, filters)
        return result

    def add_handlers(self, logger, handlers):
        """Add handlers to a logger from a list of names."""
        for h in handlers:
            try:
                logger.addHandler(self.config['handlers'][h])
            except StandardError as e:
                raise ValueError('Unable to add handler %r: %s' % (h, e))

    def common_logger_config(self, logger, config, incremental=False):
        """
        Perform configuration which is common to root and non-root loggers.
        """
        level = config.get('level', None)
        if level is not None:
            logger.setLevel(logging._checkLevel(level))
        if not incremental:
            #Remove any existing handlers
            for h in logger.handlers[:]:
                logger.removeHandler(h)
            handlers = config.get('handlers', None)
            if handlers:
                self.add_handlers(logger, handlers)
            filters = config.get('filters', None)
            if filters:
                self.add_filters(logger, filters)

    def configure_logger(self, name, config, incremental=False):
        """Configure a non-root logger from a dictionary."""
        logger = logging.getLogger(name)
        self.common_logger_config(logger, config, incremental)
        propagate = config.get('propagate', None)
        if propagate is not None:
            logger.propagate = propagate

    def configure_root(self, config, incremental=False):
        """Configure a root logger from a dictionary."""
        root = logging.getLogger()
        self.common_logger_config(root, config, incremental)

dictConfigClass = DictConfigurator

def dictConfig(config):
    """Configure logging using a dictionary."""
    dictConfigClass(config).configure()


def listen(port=DEFAULT_LOGGING_CONFIG_PORT):
    """
    Start up a socket server on the specified port, and listen for new
    configurations.

    These will be sent as a file suitable for processing by fileConfig().
    Returns a Thread object on which you can call start() to start the server,
    and which you can join() when appropriate. To stop the server, call
    stopListening().
    """
    if not thread:
        raise NotImplementedError("listen() needs threading to work")

    class ConfigStreamHandler(StreamRequestHandler):
        """
        Handler for a logging configuration request.

        It expects a completely new logging configuration and uses fileConfig
        to install it.
        """
        def handle(self):
            """
            Handle a request.

            Each request is expected to be a 4-byte length, packed using
            struct.pack(">L", n), followed by the config file.
            Uses fileConfig() to do the grunt work.
            """
            import tempfile
            try:
                conn = self.connection
                chunk = conn.recv(4)
                if len(chunk) == 4:
                    slen = struct.unpack(">L", chunk)[0]
                    chunk = self.connection.recv(slen)
                    while len(chunk) < slen:
                        chunk = chunk + conn.recv(slen - len(chunk))
                    try:
                        import json
                        d =json.loads(chunk)
                        assert isinstance(d, dict)
                        dictConfig(d)
                    except:
                        #Apply new configuration.

                        file = cStringIO.StringIO(chunk)
                        try:
                            fileConfig(file)
                        except (KeyboardInterrupt, SystemExit):
                            raise
                        except:
                            traceback.print_exc()
                    if self.server.ready:
                        self.server.ready.set()
            except socket.error as e:
                if e.errno != RESET_ERROR:
                    raise

    class ConfigSocketReceiver(ThreadingTCPServer):
        """
        A simple TCP socket-based logging config receiver.
        """

        allow_reuse_address = 1

        def __init__(self, host='localhost', port=DEFAULT_LOGGING_CONFIG_PORT,
                     handler=None, ready=None):
            ThreadingTCPServer.__init__(self, (host, port), handler)
            logging._acquireLock()
            self.abort = 0
            logging._releaseLock()
            self.timeout = 1
            self.ready = ready

        def serve_until_stopped(self):
            import select
            abort = 0
            while not abort:
                rd, wr, ex = select.select([self.socket.fileno()],
                                           [], [],
                                           self.timeout)
                if rd:
                    self.handle_request()
                logging._acquireLock()
                abort = self.abort
                logging._releaseLock()
            self.socket.close()

    class Server(threading.Thread):

        def __init__(self, rcvr, hdlr, port):
            super(Server, self).__init__()
            self.rcvr = rcvr
            self.hdlr = hdlr
            self.port = port
            self.ready = threading.Event()

        def run(self):
            server = self.rcvr(port=self.port, handler=self.hdlr,
                               ready=self.ready)
            if self.port == 0:
                self.port = server.server_address[1]
            self.ready.set()
            global _listener
            logging._acquireLock()
            _listener = server
            logging._releaseLock()
            server.serve_until_stopped()

    return Server(ConfigSocketReceiver, ConfigStreamHandler, port)

def stopListening():
    """
    Stop the listening server which was created with a call to listen().
    """
    global _listener
    logging._acquireLock()
    try:
        if _listener:
            _listener.abort = 1
            _listener = None
    finally:
        logging._releaseLock()
