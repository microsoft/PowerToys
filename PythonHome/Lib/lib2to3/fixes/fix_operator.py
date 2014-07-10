"""Fixer for operator functions.

operator.isCallable(obj)       -> hasattr(obj, '__call__')
operator.sequenceIncludes(obj) -> operator.contains(obj)
operator.isSequenceType(obj)   -> isinstance(obj, collections.Sequence)
operator.isMappingType(obj)    -> isinstance(obj, collections.Mapping)
operator.isNumberType(obj)     -> isinstance(obj, numbers.Number)
operator.repeat(obj, n)        -> operator.mul(obj, n)
operator.irepeat(obj, n)       -> operator.imul(obj, n)
"""

# Local imports
from lib2to3 import fixer_base
from lib2to3.fixer_util import Call, Name, String, touch_import


def invocation(s):
    def dec(f):
        f.invocation = s
        return f
    return dec


class FixOperator(fixer_base.BaseFix):
    BM_compatible = True
    order = "pre"

    methods = """
              method=('isCallable'|'sequenceIncludes'
                     |'isSequenceType'|'isMappingType'|'isNumberType'
                     |'repeat'|'irepeat')
              """
    obj = "'(' obj=any ')'"
    PATTERN = """
              power< module='operator'
                trailer< '.' %(methods)s > trailer< %(obj)s > >
              |
              power< %(methods)s trailer< %(obj)s > >
              """ % dict(methods=methods, obj=obj)

    def transform(self, node, results):
        method = self._check_method(node, results)
        if method is not None:
            return method(node, results)

    @invocation("operator.contains(%s)")
    def _sequenceIncludes(self, node, results):
        return self._handle_rename(node, results, u"contains")

    @invocation("hasattr(%s, '__call__')")
    def _isCallable(self, node, results):
        obj = results["obj"]
        args = [obj.clone(), String(u", "), String(u"'__call__'")]
        return Call(Name(u"hasattr"), args, prefix=node.prefix)

    @invocation("operator.mul(%s)")
    def _repeat(self, node, results):
        return self._handle_rename(node, results, u"mul")

    @invocation("operator.imul(%s)")
    def _irepeat(self, node, results):
        return self._handle_rename(node, results, u"imul")

    @invocation("isinstance(%s, collections.Sequence)")
    def _isSequenceType(self, node, results):
        return self._handle_type2abc(node, results, u"collections", u"Sequence")

    @invocation("isinstance(%s, collections.Mapping)")
    def _isMappingType(self, node, results):
        return self._handle_type2abc(node, results, u"collections", u"Mapping")

    @invocation("isinstance(%s, numbers.Number)")
    def _isNumberType(self, node, results):
        return self._handle_type2abc(node, results, u"numbers", u"Number")

    def _handle_rename(self, node, results, name):
        method = results["method"][0]
        method.value = name
        method.changed()

    def _handle_type2abc(self, node, results, module, abc):
        touch_import(None, module, node)
        obj = results["obj"]
        args = [obj.clone(), String(u", " + u".".join([module, abc]))]
        return Call(Name(u"isinstance"), args, prefix=node.prefix)

    def _check_method(self, node, results):
        method = getattr(self, "_" + results["method"][0].value.encode("ascii"))
        if callable(method):
            if "module" in results:
                return method
            else:
                sub = (unicode(results["obj"]),)
                invocation_str = unicode(method.invocation) % sub
                self.warning(node, u"You should use '%s' here." % invocation_str)
        return None
