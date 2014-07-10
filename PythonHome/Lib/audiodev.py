"""Classes for manipulating audio devices (currently only for Sun and SGI)"""
from warnings import warnpy3k
warnpy3k("the audiodev module has been removed in Python 3.0", stacklevel=2)
del warnpy3k

__all__ = ["error","AudioDev"]

class error(Exception):
    pass

class Play_Audio_sgi:
    # Private instance variables
##      if 0: access frameratelist, nchannelslist, sampwidthlist, oldparams, \
##                params, config, inited_outrate, inited_width, \
##                inited_nchannels, port, converter, classinited: private

    classinited = 0
    frameratelist = nchannelslist = sampwidthlist = None

    def initclass(self):
        import AL
        self.frameratelist = [
                  (48000, AL.RATE_48000),
                  (44100, AL.RATE_44100),
                  (32000, AL.RATE_32000),
                  (22050, AL.RATE_22050),
                  (16000, AL.RATE_16000),
                  (11025, AL.RATE_11025),
                  ( 8000,  AL.RATE_8000),
                  ]
        self.nchannelslist = [
                  (1, AL.MONO),
                  (2, AL.STEREO),
                  (4, AL.QUADRO),
                  ]
        self.sampwidthlist = [
                  (1, AL.SAMPLE_8),
                  (2, AL.SAMPLE_16),
                  (3, AL.SAMPLE_24),
                  ]
        self.classinited = 1

    def __init__(self):
        import al, AL
        if not self.classinited:
            self.initclass()
        self.oldparams = []
        self.params = [AL.OUTPUT_RATE, 0]
        self.config = al.newconfig()
        self.inited_outrate = 0
        self.inited_width = 0
        self.inited_nchannels = 0
        self.converter = None
        self.port = None
        return

    def __del__(self):
        if self.port:
            self.stop()
        if self.oldparams:
            import al, AL
            al.setparams(AL.DEFAULT_DEVICE, self.oldparams)
            self.oldparams = []

    def wait(self):
        if not self.port:
            return
        import time
        while self.port.getfilled() > 0:
            time.sleep(0.1)
        self.stop()

    def stop(self):
        if self.port:
            self.port.closeport()
            self.port = None
        if self.oldparams:
            import al, AL
            al.setparams(AL.DEFAULT_DEVICE, self.oldparams)
            self.oldparams = []

    def setoutrate(self, rate):
        for (raw, cooked) in self.frameratelist:
            if rate == raw:
                self.params[1] = cooked
                self.inited_outrate = 1
                break
        else:
            raise error, 'bad output rate'

    def setsampwidth(self, width):
        for (raw, cooked) in self.sampwidthlist:
            if width == raw:
                self.config.setwidth(cooked)
                self.inited_width = 1
                break
        else:
            if width == 0:
                import AL
                self.inited_width = 0
                self.config.setwidth(AL.SAMPLE_16)
                self.converter = self.ulaw2lin
            else:
                raise error, 'bad sample width'

    def setnchannels(self, nchannels):
        for (raw, cooked) in self.nchannelslist:
            if nchannels == raw:
                self.config.setchannels(cooked)
                self.inited_nchannels = 1
                break
        else:
            raise error, 'bad # of channels'

    def writeframes(self, data):
        if not (self.inited_outrate and self.inited_nchannels):
            raise error, 'params not specified'
        if not self.port:
            import al, AL
            self.port = al.openport('Python', 'w', self.config)
            self.oldparams = self.params[:]
            al.getparams(AL.DEFAULT_DEVICE, self.oldparams)
            al.setparams(AL.DEFAULT_DEVICE, self.params)
        if self.converter:
            data = self.converter(data)
        self.port.writesamps(data)

    def getfilled(self):
        if self.port:
            return self.port.getfilled()
        else:
            return 0

    def getfillable(self):
        if self.port:
            return self.port.getfillable()
        else:
            return self.config.getqueuesize()

    # private methods
##      if 0: access *: private

    def ulaw2lin(self, data):
        import audioop
        return audioop.ulaw2lin(data, 2)

class Play_Audio_sun:
##      if 0: access outrate, sampwidth, nchannels, inited_outrate, inited_width, \
##                inited_nchannels, converter: private

    def __init__(self):
        self.outrate = 0
        self.sampwidth = 0
        self.nchannels = 0
        self.inited_outrate = 0
        self.inited_width = 0
        self.inited_nchannels = 0
        self.converter = None
        self.port = None
        return

    def __del__(self):
        self.stop()

    def setoutrate(self, rate):
        self.outrate = rate
        self.inited_outrate = 1

    def setsampwidth(self, width):
        self.sampwidth = width
        self.inited_width = 1

    def setnchannels(self, nchannels):
        self.nchannels = nchannels
        self.inited_nchannels = 1

    def writeframes(self, data):
        if not (self.inited_outrate and self.inited_width and self.inited_nchannels):
            raise error, 'params not specified'
        if not self.port:
            import sunaudiodev, SUNAUDIODEV
            self.port = sunaudiodev.open('w')
            info = self.port.getinfo()
            info.o_sample_rate = self.outrate
            info.o_channels = self.nchannels
            if self.sampwidth == 0:
                info.o_precision = 8
                self.o_encoding = SUNAUDIODEV.ENCODING_ULAW
                # XXX Hack, hack -- leave defaults
            else:
                info.o_precision = 8 * self.sampwidth
                info.o_encoding = SUNAUDIODEV.ENCODING_LINEAR
                self.port.setinfo(info)
        if self.converter:
            data = self.converter(data)
        self.port.write(data)

    def wait(self):
        if not self.port:
            return
        self.port.drain()
        self.stop()

    def stop(self):
        if self.port:
            self.port.flush()
            self.port.close()
            self.port = None

    def getfilled(self):
        if self.port:
            return self.port.obufcount()
        else:
            return 0

##    # Nobody remembers what this method does, and it's broken. :-(
##    def getfillable(self):
##        return BUFFERSIZE - self.getfilled()

def AudioDev():
    # Dynamically try to import and use a platform specific module.
    try:
        import al
    except ImportError:
        try:
            import sunaudiodev
            return Play_Audio_sun()
        except ImportError:
            try:
                import Audio_mac
            except ImportError:
                raise error, 'no audio device'
            else:
                return Audio_mac.Play_Audio_mac()
    else:
        return Play_Audio_sgi()

def test(fn = None):
    import sys
    if sys.argv[1:]:
        fn = sys.argv[1]
    else:
        fn = 'f:just samples:just.aif'
    import aifc
    af = aifc.open(fn, 'r')
    print fn, af.getparams()
    p = AudioDev()
    p.setoutrate(af.getframerate())
    p.setsampwidth(af.getsampwidth())
    p.setnchannels(af.getnchannels())
    BUFSIZ = af.getframerate()/af.getsampwidth()/af.getnchannels()
    while 1:
        data = af.readframes(BUFSIZ)
        if not data: break
        print len(data)
        p.writeframes(data)
    p.wait()

if __name__ == '__main__':
    test()
