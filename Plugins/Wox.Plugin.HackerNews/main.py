#encoding=utf8

import requests
from bs4 import BeautifulSoup
import webbrowser
from wox import Wox,WoxAPI

class HackerNews(Wox):

    def query(self,key):
        r = requests.get('https://news.ycombinator.com/')
        bs = BeautifulSoup(r.text)
        results = []
        for i in bs.select(".comhead"):
            title = i.previous_sibling.text
            url = i.previous_sibling["href"]
            #results.append({"Title": title ,"IcoPath":"Images/app.ico","JsonRPCAction":{"method": "Wox.ChangeQuery","parameters":[url,True]}})
            results.append({"Title": title ,"IcoPath":"Images/app.ico","JsonRPCAction":{"method": "openUrl","parameters":[url],"dontHideAfterAction":True}})
            #results.append({"Title": title ,"IcoPath":"Images/app.ico","JsonRPCAction":{"method": "Wox.ShowApp"}})

        return results

    def openUrl(self,url):
        webbrowser.open(url)
        #todo:doesn't work when move this line up 
        WoxAPI.change_query(url)

if __name__ == "__main__":
    HackerNews()
