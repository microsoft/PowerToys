def is_fp_closed(obj):
    """
    Checks whether a given file-like object is closed.

    :param obj:
        The file-like object to check.
    """
    if hasattr(obj, 'fp'):
        # Object is a container for another file-like object that gets released
        # on exhaustion (e.g. HTTPResponse)
        return obj.fp is None

    return obj.closed
