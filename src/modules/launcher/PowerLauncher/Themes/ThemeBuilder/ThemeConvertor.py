import os,plistlib

def convert(path,templatePath):
    pl = plistlib.readPlist(path)
    with open(templatePath, 'r') as content_file:
        template = content_file.read()
        for key in pl:
            if "rgba" in pl[key]:
                template = template.replace("{%"+key+"%}",tohex(pl[key].replace("rgba","rgb")))
        f = open(path.replace(".alfredtheme",".xaml"),'w')
        f.write(template)
        f.close()


def tohex(string):
    string = string[4:]
    split = string.split(",")
    split[2] = ''.join(split[2].split(")")[0])
    r = int(split[0])
    g = int(split[1])
    b = int(split[2])
    tu = (r, g, b)
    return '#%02x%02x%02x' % tu

#print tohex("rgb(255,255,255,0.50)")
print convert("Night.alfredtheme","Light.xaml")
