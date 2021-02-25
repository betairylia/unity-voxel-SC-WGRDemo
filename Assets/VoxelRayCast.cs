#define PROFILE

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis;

[RequireComponent(typeof(Camera))]
public class VoxelRayCast : MonoBehaviour
{
    public Transform pointed, next;
    public World world;

    public uint handblock;

    bool hitted = false;
    Vector3Int hit = new Vector3Int(0, 0, 0), dirc = new Vector3Int(0, 0, 0);

    // Update is called once per frame
    void LateUpdate()
    {
#if PROFILE
        UnityEngine.Profiling.Profiler.BeginSample($"VoxelRayCast");
#endif
        // RayCast for voxels
        Ray ray = GetComponent<Camera>().ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        float maxDistance = 10.0f;
        float _minD = maxDistance;
        Vector3 current = ray.origin;
        Vector3 terminal = ray.origin + ray.direction * maxDistance;

        Vector3Int minV = Vector3Int.RoundToInt(Vector3.Min(current, terminal)) - Vector3Int.one;
        Vector3Int maxV = Vector3Int.RoundToInt(Vector3.Max(current, terminal)) + Vector3Int.one;

        hitted = false;
        for (int x = minV.x; x <= maxV.x; x++)
        {
            for (int y = minV.y; y <= maxV.y; y++)
            {
                for (int z = minV.z; z <= maxV.z; z++)
                {
                    Vector3Int p = new Vector3Int(x, y, z);
                    if (!world.GetBlock(p).IsSolid()) // TODO: Selectable nonsolid blocks
                    {
                        continue;
                    }
                    Bounds b = new Bounds(p + Vector3.one * 0.5f, Vector3.one);

                    float d;
                    if (b.IntersectRay(ray, out d))
                    {
                        hitted = true;
                        if (d < _minD)
                        {
                            _minD = d;
                            hit = p;
                        }
                    }
                }
            }
        }

        // Actual cast
        if (hitted)
        {
            GameObject obj = new GameObject();
            obj.transform.position = Vector3.zero;
            obj.transform.rotation = Quaternion.identity;

            BoxCollider coll = obj.AddComponent<BoxCollider>();
            coll.center = hit + Vector3.one * 0.5f;
            coll.size = Vector3.one;

            RaycastHit info;
            if (coll.Raycast(ray, out info, maxDistance))
            {
                dirc = Vector3Int.RoundToInt(info.normal);
                pointed.gameObject.SetActive(true);
                //next.gameObject.SetActive(true);
                pointed.position = hit;
                pointed.rotation = Quaternion.identity;
                //next.position = hit + dirc;

                HandleInputs();
            }

            Destroy(coll);
            Destroy(obj);
        }
        else
        {
            pointed.gameObject.SetActive(false);
            //next.gameObject.SetActive(false);
        }
#if PROFILE
        UnityEngine.Profiling.Profiler.EndSample();
#endif
    }

    private void HandleInputs()
    {
        if(Input.GetMouseButtonDown(0))
        {
            world.SetBlock(hit, 0);
        }
        if(Input.GetMouseButtonDown(1))
        {
            world.SetBlock(hit + dirc, Block.From32bitColor(handblock));
        }
    }
}
