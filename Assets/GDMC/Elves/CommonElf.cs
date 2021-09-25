using System.Collections;
using UnityEngine;

namespace GDMC.Elves
{
    public class CommonElf : Elf
    {
        public CommonElf(ElfCity city) : base(city)
        {

        }

        public override bool Birth(Vector3Int gridPos)
        {
            // TODO: check surroundings and occpy grid
            return true;
        }

        public override void Loop()
        {
            throw new System.NotImplementedException();
        }
    }
}