# -*- coding: utf-8 -*-

from wox import Wox

class HelloWorld(Wox):

    def query(self, query):
        results = []
        results.append({
            "Title": "Hello World",
            "SubTitle": "Query: {}".format(query),            
            "IcoPath":"Images/app.ico"
        })
        return results

if __name__ == "__main__":
    HelloWorld()