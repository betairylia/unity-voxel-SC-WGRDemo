from io import BytesIO

from numba import njit
import numpy as np
import requests

SERVER = "127.0.0.1:4444"

def hextoints(h):
    if len(h) == 7:   #rrggbb
        return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) + (255,) # skip '#'
    elif len(h) == 9: #rrggbbaa
        return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5, 7)) # skip '#'

def ToBlockRGB(r, g, b, a = 255):
    r = int(r) % 256
    g = int(g) % 256
    b = int(b) % 256
    a = int(a) % 256
    return r * (256 ** 3) + g * (256 ** 2) + b * 256 + a

def ToBlock(color):
    if isinstance(color, int):
        return color
    elif isinstance(color, str):
        r, g, b, a = hextoints(color)
        return r * (256 ** 3) + g * (256 ** 2) + b * 256 + a

def GetBlock(x, y, z):
    r = requests.get("http://%s/?pos=%d,%d,%d" % (SERVER, x, y, z))
    return int(r.text)

def SetBlock(x, y, z, block):
    r = requests.post("http://%s/?pos=%d,%d,%d&blk=%d" % (SERVER, x, y, z, block))
    
def SetBlocks(x = None, y = None, z = None, block = None, bufferSize = 128):
    
    # X is None = Flush
    if x is not None:
        if not hasattr(SetBlocks, "buffer"):
            SetBlocks.buffer = []
        
        SetBlocks.buffer.append((x, y, z, block))

    # Should send cached requests
    if (x is None) or (len(SetBlocks.buffer) >= bufferSize):
        
        # Fill request text body
        reqText = ""
        for b in SetBlocks.buffer:
            reqText += "%d,%d,%d,%d\n" % (b[0], b[1], b[2], b[3])
        
        # Send
        r = requests.post("http://%s/batched" % SERVER, data = reqText, headers={'Content-Type': 'raw'})
        SetBlocks.buffer = []


def GetBlockWithin(xMin, yMin, zMin, xMax, yMax, zMax):
    r = requests.get("http://%s/numpy?min=%d,%d,%d&max=%d,%d,%d" % (SERVER, xMin, yMin, zMin, xMax+1, yMax+1, zMax+1))
    # print(r.text)
    y = np.load(BytesIO(r.content)).view(np.uint32)
    return y

# TODO
def NpGetChannels(arr):
    raise NotImplementedError

# TODO
def NpFromChannels(arr):
    raise NotImplementedError

if __name__ == "__main__":
    print(GetBlock(0, 0, 0))
    print(SetBlock(0, 50, 0, ToBlock("#FFCC00")))
    GetBlockWithin(0, 0, 0, 50, 50, 50)

    for i in range(200):
        SetBlocks(0, i, 0, ToBlock("#FFCC00"), 1)
    
    SetBlocks()
