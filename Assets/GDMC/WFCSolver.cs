using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GDMC
{
    public class WFCRule
    {
        public int blockTypeCount { get; protected set; }
        public List<List<int>[]> legality;

        public WFCRule(int blockTypeCount)
        {
            this.blockTypeCount = blockTypeCount;

            for(int i = 0; i < this.blockTypeCount; i++)
            {
                legality.Add(new List<int>[6]);
            }
        }
    }

    public class WFCSolver
    {

    }
}