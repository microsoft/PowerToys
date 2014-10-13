#encoding=utf8
import json
import sys
import inspect

class Wox(object):
    """
    Wox python plugin base
    """

    def __init__(self):
        rpc_request = json.loads(sys.argv[1],encoding="gb2312")
        self.proxy = rpc_request.get("proxy",{})
        request_method_name = rpc_request.get("method")
        request_parameters = rpc_request.get("parameters")
        methods = inspect.getmembers(self, predicate=inspect.ismethod)

        request_method = dict(methods)[request_method_name]
        results = request_method(*request_parameters)
        if request_method_name == "query":
            print json.dumps({"result": results})

    def query(self,query):
        """
        sub class need to override this method
        """
        return []
    
    def debug(self,msg):
        """
        alert msg
        """
        print "DEBUG:{}".format(msg)
        sys.exit()

class WoxAPI(object):

    @classmethod
    def change_query(cls,query,requery = False):
        """
        change wox query
        """
        print json.dumps({"method": "Wox.ChangeQuery","parameters":[query,requery]})
