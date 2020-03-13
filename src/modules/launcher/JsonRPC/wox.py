# -*- coding: utf-8 -*-
from __future__ import print_function
import json
import sys
import inspect

class Wox(object):
    """
    Wox python plugin base
    """

    def __init__(self):
        rpc_request = json.loads(sys.argv[1])
        # proxy is not working now
        self.proxy = rpc_request.get("proxy",{})
        request_method_name = rpc_request.get("method")
        request_parameters = rpc_request.get("parameters")
        methods = inspect.getmembers(self, predicate=inspect.ismethod)

        request_method = dict(methods)[request_method_name]
        results = request_method(*request_parameters)

        if request_method_name == "query" or request_method_name == "context_menu":
            print(json.dumps({"result": results}))

    def query(self,query):
        """
        sub class need to override this method
        """
        return []

    def context_menu(self, data):
        """
        optional context menu entries for a result
        """
        return []

    def debug(self,msg):
        """
        alert msg
        """
        print("DEBUG:{}".format(msg))
        sys.exit()

class WoxAPI(object):

    @classmethod
    def change_query(cls,query,requery = False):
        """
        change wox query
        """
        print(json.dumps({"method": "Wox.ChangeQuery","parameters":[query,requery]}))

    @classmethod
    def shell_run(cls,cmd):
        """
        run shell commands
        """
        print(json.dumps({"method": "Wox.ShellRun","parameters":[cmd]}))

    @classmethod
    def close_app(cls):
        """
        close wox
        """
        print(json.dumps({"method": "Wox.CloseApp","parameters":[]}))

    @classmethod
    def hide_app(cls):
        """
        hide wox
        """
        print(json.dumps({"method": "Wox.HideApp","parameters":[]}))

    @classmethod
    def show_app(cls):
        """
        show wox
        """
        print(json.dumps({"method": "Wox.ShowApp","parameters":[]}))

    @classmethod
    def show_msg(cls,title,sub_title,ico_path=""):
        """
        show messagebox
        """
        print(json.dumps({"method": "Wox.ShowMsg","parameters":[title,sub_title,ico_path]}))

    @classmethod
    def open_setting_dialog(cls):
        """
        open setting dialog
        """
        print(json.dumps({"method": "Wox.OpenSettingDialog","parameters":[]}))

    @classmethod
    def start_loadingbar(cls):
        """
        start loading animation in wox
        """
        print(json.dumps({"method": "Wox.StartLoadingBar","parameters":[]}))

    @classmethod
    def stop_loadingbar(cls):
        """
        stop loading animation in wox
        """
        print(json.dumps({"method": "Wox.StopLoadingBar","parameters":[]}))

    @classmethod
    def reload_plugins(cls):
        """
        reload all wox plugins
        """
        print(json.dumps({"method": "Wox.ReloadPlugins","parameters":[]}))
