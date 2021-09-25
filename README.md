# SC-WGRDemo
Please do `git clone --recurse-submodules` as the project contains submodules.

or `git submodule update --init --recursive` after cloned.

Unity version: 2021.1.11

------

Unity playground for voxel world generation / rendering.  
(Early development xD)

2021-09-24: Updated to Unity HDRP. (Partially done, this is only `Scenes/MoonlightForest.unity`)

Performance has degraded by the introduction of HDRP, mainly by its heavy lit shader and post effects. However the meshes etc. has not been changed, so you should get the same performance as demonstrated in the video if you use them in a simpler rendering pipeline.

Performance will be boosted in the future, but may not be the focus point now. I'd rather like to focus on content generation stuffs, sry!

![HDRP](https://i.imgur.com/cjzvvwm.png)

![demo img](https://i.imgur.com/tBCjE7o.png)
![demo img2](https://i.imgur.com/KRYmRnl.png)

## Environments
Only tested under:
* Windows 10
* Unity 2021.1.11
* 3950X / RTX 2080s
* DirectX 11  

It probably won't work (efficiently) for Graphics APIs other than DX11 ...

Disclaimer: some (SDF-based) generation stuffs (e.g. hashes, some voronoi stuffs etc.) are borrowed from shadertoy posts; some missing sources will be commented later ... sry
