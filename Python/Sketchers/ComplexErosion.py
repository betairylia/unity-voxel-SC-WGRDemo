# Mainly from ErosionTest.ipynb

import os
import argparse

import numpy as np
from scipy import signal

from perlin_numpy import generate_fractal_noise_2d

'''
im: 2D array
x: [N,] float indices
y: [N,] float indices

from https://stackoverflow.com/questions/12729228/simple-efficient-bilinear-interpolation-of-images-in-numpy-and-python
'''
def bilinear_interpolate(im, x, y, period = False):
    x = np.asarray(x)
    y = np.asarray(y)

    x0 = np.floor(x).astype(int)
    x1 = x0 + 1
    y0 = np.floor(y).astype(int)
    y1 = y0 + 1

    if not period:
        x0 = np.clip(x0, 0, im.shape[0]-1);
        x1 = np.clip(x1, 0, im.shape[0]-1);
        y0 = np.clip(y0, 0, im.shape[1]-1);
        y1 = np.clip(y1, 0, im.shape[1]-1);

    if period:
        Ia = im[ x0 % im.shape[0], y0 % im.shape[1] ]
        Ib = im[ x0 % im.shape[0], y1 % im.shape[1] ]
        Ic = im[ x1 % im.shape[0], y0 % im.shape[1] ]
        Id = im[ x1 % im.shape[0], y1 % im.shape[1] ]
    else:
        Ia = im[ x0, y0 ]
        Ib = im[ x0, y1 ]
        Ic = im[ x1, y0 ]
        Id = im[ x1, y1 ]

    wa = (x1-x) * (y1-y)
    wb = (x1-x) * (y-y0)
    wc = (x-x0) * (y1-y)
    wd = (x-x0) * (y-y0)

    return wa*Ia + wb*Ib + wc*Ic + wd*Id

def main():
    return

    ##########################################################################
    ''' Argparse '''
    ##########################################################################

    parser = argparse.ArgumentParser(description='Python sketcher script')

    # Set-up
    parser.add_argument('--sizeX', type=int, default='X dimension of the map')
    parser.add_argument('--sizeY', type=int, default='Y dimension of the map')

    args = parser.parse_args()
    
    assert args.sizeX == args.sizeY
    mapSize = args.sizeX

    terrain = generate_fractal_noise_2d((mapSize, mapSize), (2, 2), 8, tileable = (True, True), persistence = 0.38)
    terrain = terrain * 0.5 + 0.5

    # Fast Hydraulic Erosion Simulation and Visualization on GPU, featureing a shallow water model and no particles were used.

    # Constant / parameters
    hMax = mapSize / 2
    dt = 0.05

    A = 1.0 # Cross-sectional area of the pipe model for flow simulation
    lPipe = 1.0 # Length of virtual pipe
    lXY = 1.0 # Length of grid
    g = 9.8 # Gravity acc
    Kc = 0.1 # Sediment capacity constant
    Ks = 0.02 # Dissolving constant
    Kd = 0.013 # Deposition constant
    Ke = 0.3 # Evaporation constant
    alphaMin = 0.01 # Lower bound for surface slope

    # Buffers
    b = np.copy(terrain) * hMax # Terrain height
    d = np.zeros_like(b) # Water height
    s = np.zeros_like(d) # Sediment
    f = np.zeros((mapSize, mapSize, 4)) # Out-flux in 4 directions: X+, X-, Y+, Y-
    v = np.zeros((mapSize, mapSize, 2)) # Velocity in x, y

    d1 = np.zeros_like(d) # for calculation

    # r = np.ones_like(b) * 0.02 # Water amount per 1.0 time unit
    r = np.power(b / hMax, 2.0) * 0.4 # Water amount per 1.0 time unit
    # r = np.zeros_like(b)
    rainAmount = 15.0
    waterSourceAmount = 8.0

    # Helper functions
    _xs = np.linspace(0, mapSize * lXY, mapSize, endpoint = False)
    _ys = np.linspace(0, mapSize * lXY, mapSize, endpoint = False)
    cellPos = np.stack(np.meshgrid(_xs, _ys), axis = -1) # [mapSize, mapSize, 2] Cell positions

    # Used to smooth the velocity
    alpha = 5
    normalizing = (alpha + 4.0)
    veloSmoothKernel = np.array(
        [[0.0, 1.0 / normalizing, 0.0],
        [1.0 / normalizing, alpha / normalizing, 1.0 / normalizing],
        [0.0, 1.0 / normalizing, 0.0]])

    # Water source
    waterSrc = np.asarray(np.unravel_index(b.argmax(), b.shape))
    dist_to_src = np.linalg.norm((cellPos - waterSrc[np.newaxis, np.newaxis, :]) / mapSize, axis = -1)
    # waterSrc = (mapSize // 2, mapSize // 2)
    # r[dist_to_src < 0.02] = waterSourceAmount
    print("Water source: %s" % repr(waterSrc))

    def PhysicalPosToNumpyIndex(x, y):
        return x / lXY, y / lXY

    # Iterative update
    iters = 2000
    for it in range(iters):
        
        ##### 1. Water Increment
        d = d + dt * r
        
        # Rain
    #     rndidx = np.random.randint(mapSize, size = (1, 1, 2))
    #     dist_to_rain = np.linalg.norm((cellPos - rndidx) / mapSize, axis = -1)
    #     d[dist_to_rain < 0.008] += dt * rainAmount # Raindrop
        np.copyto(d1, d)
        
        ##### 2. Flow Simulation
        ### Flux
        h = b + d # Water surface height
        
        # Height differences
        dh_x = np.diff(h, n = 1, axis = 0, append = 0) # dh_x[i, j] = h[i+1, j] - h[i, j]
        dh_y = np.diff(h, n = 1, axis = 1, append = 0) # dh_y[i, j] = h[i, j+1] - h[i, j]
        
        dh_xp = - (dh_x) # i, j - i+1, j
        dh_xn = + np.roll(dh_x, +1, axis = 0)
        dh_yp = - (dh_y)
        dh_yn = + np.roll(dh_y, +1, axis = 1)
        
        dh = np.stack([dh_xp, dh_xn, dh_yp, dh_yn], axis = -1)
        
        # Compute flux
        f = np.maximum(0, (f * 0.9999) + dt * A * (g * dh) / lPipe)
        
        # Boundary conditions
        f[mapSize - 1, :, 0] = 0
        f[0, :, 1] = 0
        f[:, mapSize - 1, 2] = 0
        f[:, 0, 3] = 0
        
        _f = np.copy(f)
        
        # Flux scaling to avoid negative water amount
        K = np.minimum(1.0, d * lXY * lXY / (dt * (np.sum(f, axis = -1)) + 1e-10))
        f = f * np.expand_dims(K, axis = -1)
        
        ### Water surface & Velocity
        # TODO: np.roll -> array slice / just use torch or cuda lmao
        # Change in volume
        dv = dt * (
            # In-flux
            # Boundary conditions already handled so we are good
            np.roll(f[:, :, 0], +1, axis = 0) +
            np.roll(f[:, :, 1], -1, axis = 0) +
            np.roll(f[:, :, 2], +1, axis = 1) +
            np.roll(f[:, :, 3], -1, axis = 1) -
            
            # Out-flux
            np.sum(f, axis = -1)
        )
        
        # Adjust water height
        d = d + dv / (lXY * lXY)
        
        # Velocity
        dWx = np.roll(f[:, :, 0], +1, axis = 0) - f[:, :, 1] + f[:, :, 0] - np.roll(f[:, :, 1], -1, axis = 0)
        dWy = np.roll(f[:, :, 2], +1, axis = 1) - f[:, :, 3] + f[:, :, 2] - np.roll(f[:, :, 3], -1, axis = 1)
        d_avg = (d + d1) / 2
        
        v[:, :, 0] = dWx / (lXY * d_avg + 1e-10)
        v[:, :, 1] = dWy / (lXY * d_avg + 1e-10)
        
        # Dirty trick to smooth out the velocity (gooey erosion OoO)
        # from https://github.com/LanLou123/Webgl-Erosion/blob/master/src/shaders/sediment-frag.glsl
        v[:, :, 0] = signal.convolve2d(v[:, :, 0], veloSmoothKernel, boundary = 'symm', mode = 'same')
        v[:, :, 1] = signal.convolve2d(v[:, :, 1], veloSmoothKernel, boundary = 'symm', mode = 'same')
        
        ##### 3. Erosion / Deposition
        ### Sediment transport capacity
        grad = np.linalg.norm(np.stack(np.gradient(b), axis = -1), axis = -1)
        sinAlpha = grad / np.sqrt(grad ** 2 + 1 ** 2) # Here the np.gradient is independent of our step l
        sinAlpha = np.maximum(sinAlpha, alphaMin)
        C = Kc * sinAlpha * np.linalg.norm(v, axis = -1)
        
        ### Transport soil
        dissolve = C > s
        deposite = C <= s
        
        # Use different constants for dissolve & deposition
        b[dissolve] = b[dissolve] - dt * Ks * (C[dissolve] - s[dissolve])
        s[dissolve] = s[dissolve] + dt * Ks * (C[dissolve] - s[dissolve])
        
        b[deposite] = b[deposite] - dt * Kd * (C[deposite] - s[deposite])
        s[deposite] = s[deposite] + dt * Kd * (C[deposite] - s[deposite])
        
        ##### 4. Sediment Transportation
        backward_pos = cellPos - dt * v
        npXs, npYs = PhysicalPosToNumpyIndex(backward_pos[:, :, 0], backward_pos[:, :, 1])
        npXs = npXs.reshape(-1)
        npYs = npYs.reshape(-1)
        result = bilinear_interpolate(s, npXs, npYs, True).reshape(mapSize, mapSize)

        s = result
        
        ##### 5. Evaporation
        d = d * (1 - Ke * dt)

    result_map = np.stack([b, b - terrain * hMax, d], axis = -1) / hMax

    # Match the outputs you have specified in your Python Sketcher.
    np.save("Sketch.npy", result_map)

if __name__ == "__main__":
    main()