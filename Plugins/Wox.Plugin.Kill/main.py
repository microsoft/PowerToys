#encoding=utf8

from __future__ import unicode_literals
import psutil
import json

def signalResult(process):
    res = {}
    res["Title"] = process.name
    res["SubTitle"] = process.pid 
    res["ActionName"] = "killProcess"
    res["IcoPath"] = "Images\\app.png"
    res["ActionPara"] = process.pid
    return res

def query(key):
    name = key.split(" ")[1]
    results = []
    for i in psutil.get_process_list():
        try:
            if name:
                if name.lower() in i.name.lower():  
                    results.append(signalResult(i))
            else:
                results.append(signalResult(i))
        except:
            pass
    return json.dumps(results)

def killProcess(context,pid):
     p = psutil.Process(int(pid))
     if p:
         p.kill()

if __name__ == "__main__":
    print killProcess(10008)
