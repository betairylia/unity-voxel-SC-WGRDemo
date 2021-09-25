using System.Collections;
using UnityEngine;

namespace GDMC
{
    public static class Utils
    {
        public static readonly Vector3Int[] directions = new Vector3Int[6]
        {
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.up,
            Vector3Int.down,
            new Vector3Int(0, 0, +1),
            new Vector3Int(0, 0, -1),
        };
    }
}