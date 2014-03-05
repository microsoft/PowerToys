#encoding=utf8
import requests
from bs4 import BeautifulSoup
import json
import webbrowser

def safeSelectText(s,path):
    return s.select(path)[0].text if len(s.select(path)) > 0 else ""

def query(key):
    r = requests.get('http://www.gewara.com/movie/searchMovie.xhtml')
    bs = BeautifulSoup(r.text)
    results = []
    for i in bs.select(".ui_left .ui_media"):
        res = {}
        score = safeSelectText(i,".grade sub") + safeSelectText(i,".grade sup")
        res["Title"] = safeSelectText(i,".title a") + " / " + score
        res["SubTitle"] = i.select(".ui_text p")[1].text
        res["ActionName"] = "openUrl"
        res["IcoPath"] = "Images\\movies.png"
        res["ActionPara"] = "http://www.gewara.com" + i.select(".title a")[0]["href"]
        results.append(res)
    return json.dumps(results)

def openUrl(context,url):
    webbrowser.open(url)

if __name__ == "__main__":
    print query("movie geo")
