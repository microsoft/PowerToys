#encoding=utf8
import json
import sys
import inspect

class Wox(object):
    """
    Wox python plugin base
    """

    def __init__(self):
        rpc_request = json.loads(sys.argv[1])
        request_method_name = rpc_request.get("method")
        request_parameters = rpc_request.get("parameters")
        methods = inspect.getmembers(self, predicate=inspect.ismethod)

        request_method = dict(methods)[request_method_name]
        results = request_method(request_parameters)
        print json.dumps({"result": results})

    def query(self,query):
        """
        sub class need to override this method
        """
        return []
