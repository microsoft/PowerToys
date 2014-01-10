#encoding=utf8
import requests
from bs4 import BeautifulSoup
import json


def query(key):
    k = key.split(" ")[1]
    if not k:
        return ""
    r = requests.get('http://movie.douban.com/subject_search?search_text=' + k)
    bs = BeautifulSoup(r.text)
    results = []
    for i in bs.select(".article table .pl2"):
        res = {}
        title = i.select("a")[0].text.replace("\n","").replace(" ","")
        score = i.select("span.rating_nums")[0].text if i.select("span.rating_nums") else "0"
        res["Title"] = title
        res["SubTitle"] = score
        res["ActionName"] = "openUrl"
        res["ActionPara"] = i.select("a[href]")[0]["href"]
        results.append(res)
    return json.dumps(results)

def openUrl(url):
    pass

if __name__ == "__main__":
    print query("movie geo")
