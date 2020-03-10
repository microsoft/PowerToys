# -*- coding: utf-8 -*-

from wox import Wox

class HelloWorld(Wox):

    def query(self, query):
        results = []
        results.append({
            "Title": "Hello World",
            "SubTitle": "Query: {}".format(query),
            "IcoPath":"Images/app.ico",
            "ContextData": "ctxData"
        })
        return results

    def context_menu(self, data):
        results = []
        results.append({
            "Title": "Context menu entry",
            "SubTitle": "Data: {}".format(data),
            "IcoPath":"Images/app.ico"
        })
        return results

if __name__ == "__main__":
    HelloWorld()