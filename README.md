# VulcanMemoryManager
The video memory manager for the Vulcan GAPI implemented for the another game engine

The memory manager main task is to provide convenient means of allocating and freeing arbitrary chunks of VRAM within huge blocks limited amount of those can be requested from the GAPI itself
The memory controller consists of 3 different types of bins for memory blocks organized in 3 sequential regions: 
1)The region of numbered bins - small bins with each one having a strict size for blocks to store
2)The region of linear bins - medium sized bins where each one accomodates blocks in range of allowed sizes
3)The region of exponential bins - large bins where the memory range of accepted blocks sizes of a latter bin is N times wider then the one of a former

The memory manager also utilizes the linked list and small DB to keep track of continuous allocated and free blocks with the ability of defragmentation - merging sequential free chunks
