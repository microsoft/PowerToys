import os

ReleaseDirectory = "Release"

for root, dirs, files in os.walk(ReleaseDirectory):
    for file in files:
        if file.endswith(".xml"):
            path = os.path.join(root, file)
            print "delete file:" + path
            os.remove(path)
