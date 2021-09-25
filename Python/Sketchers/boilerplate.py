'''
Boilerplate script, do not modify.
Served as a template for copy-pasting.
'''

import os
import argparse

import numpy as np

def main():

    ##########################################################################
    ''' Argparse '''
    ##########################################################################

    parser = argparse.ArgumentParser(description='Python sketcher script')

    # Set-up
    parser.add_argument('--sizeX', type=int, default='X dimension of the map')
    parser.add_argument('--sizeY', type=int, default='Y dimension of the map')

    args = parser.parse_args()

    # Your program should be aware of sizeX & sizeY.
    arr = np.random.normal(loc = 0.5, scale = 0.2, size = (args.sizeX, args.sizeY, 4))

    # Match the outputs you have specified in your Python Sketcher.
    np.save("Sketch.npy", arr)

if __name__ == "__main__":
    main()
