#encoding=utf8

from __future__ import unicode_literals
import requests
from bs4 import BeautifulSoup
import json
import webbrowser

def safeSelectText(s,path):
    return s.select(path)[0].text if len(s.select(path)) > 0 else ""

def query(key):
    r = requests.get('http://v2ex.com/?tab=all')
    bs = BeautifulSoup(r.text)
    results = []
    for i in bs.select(".box div.item"):
        res = {}
        title = safeSelectText(i,".item_title")
        subTitle = safeSelectText(i,".fade")
        url = "http://v2ex.com" + i.select(".item_title a")[0]["href"]

        res["Title"] = title
        res["SubTitle"] = subTitle
        res["ActionName"] = "openUrl"
        res["IcoPath"] = "Images\\app.ico"
        res["ActionPara"] = url
        results.append(res)
    return json.dumps(results)

def openUrl(context,url):
    webbrowser.open(url)

if __name__ == "__main__":
    print query("movie geo")
