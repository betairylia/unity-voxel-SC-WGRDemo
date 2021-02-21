#ifndef STRUCTURES
#define STRUCTURES

// Should be aligned with EcoSystemStructureDefinitions.cs

#define SU_SPHERE 0
#define SU_TREE 1
#define SU_HANG_MUSHROOM 2

#define SU_MOONFOREST_GIANTTREE 3
#define SU_MOONFOREST_TREE 4
#define SU_MOONFOREST_FLOWER 5
#define SU_MOONFOREST_FLOWERVINE 6

typedef int StructureType;

struct StructureSeedDescriptor
{
	StructureType structureType;
	int3 worldPos;
};

#endif //STRUCTURES