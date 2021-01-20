#ifndef STRUCTURES
#define STRUCTURES

// Should be aligned with EcoSystemStructureDefinitions.cs

#define SU_SPHERE 0
#define SU_TREE 1
#define SU_HANG_MUSHROOM 2

typedef int StructureType;

struct StructureSeedDescriptor
{
	StructureType structureType;
	int3 worldPos;
};

#endif //STRUCTURES