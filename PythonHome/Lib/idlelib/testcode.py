import string

def f():
    a = 0
    b = 1
    c = 2
    d = 3
    e = 4
    g()

def g():
    h()

def h():
    i()

def i():
    j()

def j():
    k()

def k():
    l()

l = lambda: test()

def test():
    string.capwords(1)

f()
