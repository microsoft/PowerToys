#encoding=utf8

from __future__ import unicode_literals
import requests
from bs4 import BeautifulSoup
import json
import webbrowser
from wox import Wox

class HackerNews(Wox):

    def query(self,key):
        r = requests.get('https://news.ycombinator.com/')
        bs = BeautifulSoup(r.text)
        results = []
        for i in bs.select(".comhead"):
            title = i.previous_sibling.text
            url = i.previous_sibling["href"]
            results.append({"Title": title ,"IcoPath":"Images/app.ico","JsonRPCAction":{"method": "openUrl", "parameters": url}})

        return results

    def openUrl(self,url):
        webbrowser.open(url)

if __name__ == "__main__":
    HackerNews()
