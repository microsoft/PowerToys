#encoding=utf8
import requests
from bs4 import BeautifulSoup
import json
import webbrowser

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
        res["Title"] = title.split("/")[0]
        year = i.select("p.pl")[0].text.split("-")[0] if i.select("p.pl")[0] else "Null"
        alias = title.split("/")[1] if len(title.split("/")) >= 2 else "Null"
        res["SubTitle"] = "Year: " + year  + "   Score: " + score + "   Alias: " + alias
        res["ActionName"] = "openUrl"
        res["IcoPath"] = "Images\\movies.png"
        res["ActionPara"] = i.select("a[href]")[0]["href"]
        results.append(res)
    return json.dumps(results)

def openUrl(context,url):
    #shift + enter
    #if context["SpecialKeyState"]["ShiftPressed"] == "True":
    webbrowser.open(url)

if __name__ == "__main__":
    print query("movie geo")
