#encoding=utf8
import requests
from bs4 import BeautifulSoup
import json

class PyWinAlfred():

    def query(self,key):
        k = key.split(" ")[1]
        r = requests.get('http://movie.douban.com/subject_search?search_text=' + k)
        bs = BeautifulSoup(r.text)
        results = []
        for i in bs.select(".article table .pl2 a"):
            res = {}
            t = i.text.strip().replace(" ","")
            res["Title"] = t.replace("\\n","")
            results.append(res)
        return json.dumps(results) 

if __name__ == "__main__":
    p = PyWinAlfred()
    print p.query("movie geo")
