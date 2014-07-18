try:
    import ast
    from _markerlib.markers import default_environment, compile, interpret
except ImportError:
    if 'ast' in globals():
        raise
    def default_environment():
        return {}
    def compile(marker):
        def marker_fn(environment=None, override=None):
            # 'empty markers are True' heuristic won't install extra deps.
            return not marker.strip()
        marker_fn.__doc__ = marker
        return marker_fn
    def interpret(marker, environment=None, override=None):
        return compile(marker)()
