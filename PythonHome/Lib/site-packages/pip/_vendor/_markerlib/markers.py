# -*- coding: utf-8 -*-
"""Interpret PEP 345 environment markers.

EXPR [in|==|!=|not in] EXPR [or|and] ...

where EXPR belongs to any of those:

    python_version = '%s.%s' % (sys.version_info[0], sys.version_info[1])
    python_full_version = sys.version.split()[0]
    os.name = os.name
    sys.platform = sys.platform
    platform.version = platform.version()
    platform.machine = platform.machine()
    platform.python_implementation = platform.python_implementation()
    a free string, like '2.6', or 'win32'
"""

__all__ = ['default_environment', 'compile', 'interpret']

import ast
import os
import platform
import sys
import weakref

_builtin_compile = compile

try:
    from platform import python_implementation
except ImportError:
    if os.name == "java":
        # Jython 2.5 has ast module, but not platform.python_implementation() function.
        def python_implementation():
            return "Jython"
    else:
        raise


# restricted set of variables
_VARS = {'sys.platform': sys.platform,
         'python_version': '%s.%s' % sys.version_info[:2],
         # FIXME parsing sys.platform is not reliable, but there is no other
         # way to get e.g. 2.7.2+, and the PEP is defined with sys.version
         'python_full_version': sys.version.split(' ', 1)[0],
         'os.name': os.name,
         'platform.version': platform.version(),
         'platform.machine': platform.machine(),
         'platform.python_implementation': python_implementation(),
         'extra': None # wheel extension
        }

for var in list(_VARS.keys()):
    if '.' in var:
        _VARS[var.replace('.', '_')] = _VARS[var]

def default_environment():
    """Return copy of default PEP 385 globals dictionary."""
    return dict(_VARS)

class ASTWhitelist(ast.NodeTransformer):
    def __init__(self, statement):
        self.statement = statement # for error messages

    ALLOWED = (ast.Compare, ast.BoolOp, ast.Attribute, ast.Name, ast.Load, ast.Str)
    # Bool operations
    ALLOWED += (ast.And, ast.Or)
    # Comparison operations
    ALLOWED += (ast.Eq, ast.Gt, ast.GtE, ast.In, ast.Is, ast.IsNot, ast.Lt, ast.LtE, ast.NotEq, ast.NotIn)

    def visit(self, node):
        """Ensure statement only contains allowed nodes."""
        if not isinstance(node, self.ALLOWED):
            raise SyntaxError('Not allowed in environment markers.\n%s\n%s' %
                               (self.statement,
                               (' ' * node.col_offset) + '^'))
        return ast.NodeTransformer.visit(self, node)

    def visit_Attribute(self, node):
        """Flatten one level of attribute access."""
        new_node = ast.Name("%s.%s" % (node.value.id, node.attr), node.ctx)
        return ast.copy_location(new_node, node)

def parse_marker(marker):
    tree = ast.parse(marker, mode='eval')
    new_tree = ASTWhitelist(marker).generic_visit(tree)
    return new_tree

def compile_marker(parsed_marker):
    return _builtin_compile(parsed_marker, '<environment marker>', 'eval',
                   dont_inherit=True)

_cache = weakref.WeakValueDictionary()

def compile(marker):
    """Return compiled marker as a function accepting an environment dict."""
    try:
        return _cache[marker]
    except KeyError:
        pass
    if not marker.strip():
        def marker_fn(environment=None, override=None):
            """"""
            return True
    else:
        compiled_marker = compile_marker(parse_marker(marker))
        def marker_fn(environment=None, override=None):
            """override updates environment"""
            if override is None:
                override = {}
            if environment is None:
                environment = default_environment()
            environment.update(override)
            return eval(compiled_marker, environment)
    marker_fn.__doc__ = marker
    _cache[marker] = marker_fn
    return _cache[marker]

def interpret(marker, environment=None):
    return compile(marker)(environment)
