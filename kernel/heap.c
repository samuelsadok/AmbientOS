
/*
*
* Manages the heap.
* The heap is splitted into several smaller heaps, each of which is stored in one or more virtual memory pages.
* The single parts of the heap are allocated and freed dynamically by calling page_alloc() and page_free().
*
*
* created: 27.12.14
*
*/

// Dependency list:
// malloc => page_alloc => phy_page_alloc => free
// free => page_free => phy_page_free => malloc

#include <system.h>
#include "heap.h"


//#define DBG_ALLOC(...)	LOGI(__VA_ARGS__)
#define DBG_ALLOC(...)


typedef struct free_block_t
{
	size_t size;
	struct free_block_t *nextFreeBlock;
	struct free_block_t *previousFreeBlock;
	size_t size2;
} free_block_t; // size: 32B on 64-bit system


typedef struct heap_t
{
	size_t largestFreeBlock;		// it is guaranteed that this heap has no larger block than this
	size_t freeSpace;				// total free space in this heap (sum of sizes of free blocks, excluding their size fields)
	size_t size;					// size of this heap (not including the header, the first free block and two size fields)
	struct heap_t *nextHeap;
	struct heap_t *previousHeap;
	free_block_t head[1];
} heap_t; // size: 72B on 64-bit system


heap_t *heap = NULL;
heap_t *lastHeap = NULL;


// Initializes an empty heap structure
void heap_init(heap_t *newHeap, size_t size) {
	assert(!((uintptr_t)newHeap & PAGE_SIZE_MASK));
	assert(size >= (sizeof(heap_t) + sizeof(free_block_t)));
	assert(!(size & 1));

	free_block_t *firstFreeBlock = (free_block_t *)((char *)newHeap + sizeof(heap_t));
	size -= sizeof(heap_t) + 2 * sizeof(size_t);
	newHeap->size = size;
	newHeap->largestFreeBlock = size;
	newHeap->freeSpace = size; // we never want to free this heap entirely
	newHeap->head[0].size = 0; // must be 0 so that free can always find the free-blocks-list
	newHeap->head[0].nextFreeBlock = firstFreeBlock;
	newHeap->head[0].previousFreeBlock = NULL;
	newHeap->head[0].size2 = 2 * sizeof(void *) | 1; // set "allocated" flag so the block doesn't get coalesced
	
	firstFreeBlock->size = size;
	firstFreeBlock->nextFreeBlock = NULL;
	firstFreeBlock->previousFreeBlock = newHeap->head;
	*(size_t *)((char *)firstFreeBlock + sizeof(size_t) + size) = size;
}



#ifdef USING_VIRTUAL_MEMORY
__attribute__((aligned(PAGE_SIZE)))
char initialHeap[PAGE_SIZE];
#endif


// Initializes the malloc and free functions using an reserved space that will be used as the initial heap.
void malloc_init(void) {
	heap = lastHeap = initialHeap;
	heap_init(heap, PAGE_SIZE);
	heap->freeSpace--; // we never want to free this heap entirely
	heap->nextHeap = NULL;
	heap->previousHeap = NULL;

#ifdef USING_VIRTUAL_MEMORY
	phy_page_init();
#endif
}

REGISTER_INIT4(malloc_init);



// Allocates a block of memory in the current processes linear address space.
// Returns NULL if not enough physical or virtual memory could be allocated or if the size argument is 0.
// The pointer is guaranteed to be 16-byte aligned for 8-byte address sizes. <= NO, NOT YET
void *malloc(size_t size) {
	//debug(1, size);
	if (!size)
		return NULL;
	if (size < 2 * sizeof(void *))
		size = 2 * sizeof(void *); // size must be at least that of a free block
	if (size & 1)
		size++;

	memmgr_enter_routine();

	heap_t *currentHeap = heap;

	for (;;) {
		while (currentHeap) {
			if (currentHeap->largestFreeBlock >= size && currentHeap->freeSpace >= size) {
				// this heap might be suitable (though this is not certain yet)

				free_block_t *currentBlock = currentHeap->head;
				size_t largestFreeBlock = 0;

				while (currentBlock) {
					if (currentBlock->size >= size) {
						// we found a suitable block

						// track memory usage
						currentHeap->freeSpace -= currentBlock->size;
						if (currentBlock->size >= currentHeap->largestFreeBlock)
							currentHeap->largestFreeBlock = SIZE_MAX; // invalidate largestFreeBlock field

						if (currentBlock->size >= size + 2 * sizeof(size_t) + 2 * sizeof(void *)) { // can this block be splitted?
							free_block_t *newFreeBlock = (free_block_t *)((char *)currentBlock + 2 * sizeof(size_t) + size);

							currentBlock->previousFreeBlock->nextFreeBlock = newFreeBlock;
							if (currentBlock->nextFreeBlock)
								currentBlock->nextFreeBlock->previousFreeBlock = newFreeBlock;
							newFreeBlock->previousFreeBlock = currentBlock->previousFreeBlock;
							newFreeBlock->nextFreeBlock = currentBlock->nextFreeBlock;

							newFreeBlock->size = currentBlock->size - size - 2 * sizeof(size_t);
							*(size_t *)((char *)newFreeBlock + sizeof(size_t) + newFreeBlock->size) = newFreeBlock->size;
							currentBlock->size = size;

							currentHeap->freeSpace += newFreeBlock->size;
						} else {
							// block can't be splitted - remove from linked list
							currentBlock->previousFreeBlock->nextFreeBlock = currentBlock->nextFreeBlock;
							if (currentBlock->nextFreeBlock)
								currentBlock->nextFreeBlock->previousFreeBlock = currentBlock->previousFreeBlock;
						}

						// set "allocated" flag in both size fields
						currentBlock->size = *(size_t *)((char *)currentBlock + sizeof(size_t) + currentBlock->size) = currentBlock->size | 1;

						uint64_t a = (uint64_t)((size_t *)currentBlock + 1);
						if ((a >> 40) != 0xFFFFFF)
							debug(0x41, a);
						return memmgr_exit_routine(), (void *)((size_t *)currentBlock + 1);
					}

					if (currentBlock->size > largestFreeBlock)
						largestFreeBlock = currentBlock->size;

					currentBlock = currentBlock->nextFreeBlock;
				}

				// we just scanned for the largest block, so let's update it
				currentHeap->largestFreeBlock = largestFreeBlock;
			}


			// no free block was found in the current heap - try next heap
			currentHeap = currentHeap->nextHeap;
		}
		//debug(3, (uint64_t)currentHeap);

		// no heap with enough free space was found

		assert(heap);
		assert(lastHeap);

		// allocate enough pages for the requested memory block plus the heap header structure
		size_t heapSize = sizeof(heap_t) + 2 * sizeof(size_t) + size;
		if (heapSize & PAGE_SIZE_MASK)
			heapSize += PAGE_SIZE;
		heapSize &= ~PAGE_SIZE_MASK;

		DBG_ALLOC("allocating heap of %d pages", (int)(heapSize >> PAGE_ALIGN_BITS));

		heap_t *newHeap = page_alloc(heapSize);
		if (!newHeap) return memmgr_exit_routine(), NULL; // page allocation failed

		// init the heap structure
		heap_init(newHeap, heapSize);

		// insert as the last element in linked list
		newHeap->previousHeap = lastHeap;
		newHeap->nextHeap = NULL;
		lastHeap->nextHeap = newHeap;
		lastHeap = newHeap;

		// allocate block from the new heap
		currentHeap = newHeap;
		//debug(4, (uint64_t)heap);
	}
}



// to do
void *realloc(void *block, size_t size) {
	if (!block) return NULL;

	memmgr_enter_routine();
	
	size_t *blockSize1 = (size_t *)block - 1;
	size_t *blockSize2 = (size_t *)((char *)block + *blockSize1);

	// find last free block before this one
	free_block_t *nextFreeBlock = (free_block_t *)blockSize1;
	do {
		nextFreeBlock = (free_block_t *)((char *)nextFreeBlock - (*((size_t *)nextFreeBlock - 1) & ~(1UL)) - 2 * sizeof(size_t)); // find first size field of preceding block
	} while (nextFreeBlock->size & 1);
	nextFreeBlock = nextFreeBlock->nextFreeBlock;

	int retainAddress = 0;

	if (nextFreeBlock)
		if (&(nextFreeBlock->size) == (blockSize2 + 1))
			if (*blockSize1 + nextFreeBlock->size + 2 * sizeof(size_t) >= size)
				retainAddress = 1;
	
	if (retainAddress) {
		// todo
	} else {
		void *newBlock = malloc(size);
		if (!newBlock) return NULL;
		memcpy(newBlock, block, *blockSize1);
		free(block);
		block = newBlock;
	}

	return block;
}



// Frees the memory block that is associated with a pointer that was obtained through a malloc() call.
void free(void *block) {
	if (!block) return;

	memmgr_enter_routine();

	size_t *blockSize1 = (size_t *)block - 1;
	assert((*blockSize1) & 1); // cannot free a block that wasn't allocated
	*blockSize1 &= (~(size_t)1); // clear "allocated" flag
	size_t *blockSize2 = (size_t *)((char *)block + *blockSize1);
	*blockSize2 &= (~(size_t)1); // clear "allocated" flag
	assert(*blockSize1 == *blockSize2);
	size_t yield = *blockSize1;

	//debug(0x22, blockSize1);
	//debug(0x22, blockSize2);

	// find last free block before this one
	free_block_t *precedingFreeBlock = (free_block_t *)blockSize1;
	do {
		precedingFreeBlock = (free_block_t *)((char *)precedingFreeBlock - (*((size_t *)precedingFreeBlock - 1) & ~(1UL)) - 2 * sizeof(size_t)); // find first size field of preceding block
	} while (precedingFreeBlock->size & 1);

	//debug(0x23, precedingFreeBlock);

	// insert this block into linked list of free blocks
	free_block_t *currentBlock = (free_block_t *)blockSize1;
	currentBlock->previousFreeBlock = precedingFreeBlock;
	currentBlock->nextFreeBlock = precedingFreeBlock->nextFreeBlock;
	if (precedingFreeBlock->nextFreeBlock) {
		//debug(0x23, precedingFreeBlock->nextFreeBlock);
		precedingFreeBlock->nextFreeBlock->previousFreeBlock = currentBlock;
	}
	precedingFreeBlock->nextFreeBlock = currentBlock;

	// find the heap we're in
	while (precedingFreeBlock->previousFreeBlock)
		precedingFreeBlock = precedingFreeBlock->previousFreeBlock;
	heap_t *currentHeap = (heap_t *)((char *)precedingFreeBlock - offsetof(heap_t, head[0]));
	//heap_t *currentHeap = (heap_t *)((char *)precedingFreeBlock - (sizeof(heap_t) - sizeof(free_block_t)));

	//debug(0x24, currentHeap);
	assert(!((uintptr_t)currentHeap & PAGE_SIZE_MASK));

	// coalesce with preceding block
	if (!(*(blockSize1 - 1) & 1)) {
		assert(*(blockSize1 - 1));
		free_block_t *block2 = (free_block_t *)blockSize1;
		blockSize1 = (size_t *)((char *)(blockSize1 - 2) - *(blockSize1 - 1)); // find first size field of preceding block
		*blockSize1 = *blockSize2 = *blockSize1 + *blockSize2 + 2 * sizeof(size_t);
		free_block_t *block1 = (free_block_t *)blockSize1;

		// update linked list
		block1->nextFreeBlock = block2->nextFreeBlock;
		if (block2->nextFreeBlock) block2->nextFreeBlock->previousFreeBlock = block1;

		yield += 2 * sizeof(size_t);
	}

	// coalesce with succeeding block (if this is not the last in the heap)
	if ((char *)(blockSize2 + 1) < ((char *)currentHeap + sizeof(heap_t) + currentHeap->size + 2 * sizeof(void *))) {
		if (!(*(blockSize2 + 1) & 1)) {
			assert(*(blockSize2 + 1));
			free_block_t *block1 = (free_block_t *)blockSize1;
			free_block_t *block2 = (free_block_t *)(blockSize2 + 1);
			blockSize2 = (size_t *)((char *)(blockSize2 + 2) + *(blockSize2 + 1)); // find second size field of succeeding block
			*blockSize1 = *blockSize2 = *blockSize1 + *blockSize2 + 2 * sizeof(size_t);

			// update linked list
			block1->nextFreeBlock = block2->nextFreeBlock;
			if (block2->nextFreeBlock) block2->nextFreeBlock->previousFreeBlock = block1;

			yield += 2 * sizeof(size_t);
		}
	}

	// track memory usage
	currentHeap->freeSpace += yield;
	if (currentHeap->largestFreeBlock < yield)
		currentHeap->largestFreeBlock = yield;

	if (currentHeap->freeSpace == currentHeap->size) {

		// remove this heap from linked list and free the associated pages
		if (currentHeap->previousHeap)
			currentHeap->previousHeap->nextHeap = currentHeap->nextHeap;
		else
			heap = currentHeap->nextHeap;
		if (currentHeap->nextHeap)
			currentHeap->nextHeap->previousHeap = currentHeap->previousHeap;
		else
			lastHeap = currentHeap->previousHeap;

		// the heap is now in a consistent state, so this call is safe
		page_free(currentHeap, currentHeap->size + sizeof(heap_t) + 2 * sizeof(size_t));
	}

	memmgr_exit_routine();
}










void *memcpy(void *restrict __dest, const void *restrict __src, size_t __n) {
	char *dest = __dest;
	const char *src = __src;
	while (__n--) *dest++ = *src++;
	return __dest;
}


int memcmp(const void *ptr1, const void *ptr2, size_t num) {
	const char *__ptr1 = ptr1, *__ptr2 = ptr2;
	while (num--) {
		if (*__ptr1 > *__ptr2) return 1;
		if (*(__ptr1++) < *(__ptr2++)) return -1;
	}
	return 0;
}


void *memset(void *ptr, int value, size_t num) {
	char *__ptr = ptr;
	while (num--)
		*(__ptr++) = (unsigned char)value;
	return ptr;
}





/*

#define MEMMGR_FREE				(1)
#define MEMMGR_PAGE_FREE		(2)
#define MEMMGR_PHY_PAGE_FREE	(3)

typedef struct
{
	void *arg1;
	size_t arg2;
	int type;
} memmgr_lazy_call_t;

memmgr_lazy_call_t memmgrStagedCalls[20];
int memmgrStagedCallCount = 0;
int memmgrNestedLevel = 0;


// marks the beginning of a memory management related routine
void memmgr_enter_routine(void) {
	memmgrNestedLevel++;
}

// stages a free(address) call to be committed when appropriate
void memmgr_stage_free(void *address) {
	memmgrStagedCalls[memmgrStagedCallCount++] = (memmgr_lazy_call_t) { .arg1 = address, .type = MEMMGR_FREE };
}

// stages a page_free(address) call to be committed when appropriate
void memmgr_stage_page_free(void *address, size_t size) {
	memmgrStagedCalls[memmgrStagedCallCount++] = (memmgr_lazy_call_t) { .arg1 = address, .arg2 = size, .type = MEMMGR_PAGE_FREE };
}

// stages a phy_page_free(address, 1) call to be committed when appropriate
void memmgr_stage_phy_page_free(void *address, size_t count) {
	memmgrStagedCalls[memmgrStagedCallCount++] = (memmgr_lazy_call_t) { .arg1 = address, .arg2 = count, .type = MEMMGR_PHY_PAGE_FREE };
}

// Marks the end of a memory management related routine.
// Commits all staged calls if this was the outer-most nested level.
void memmgr_exit_routine(void) {
	if (--memmgrNestedLevel)
		return;

	memmgrNestedLevel++;

	while (memmgrStagedCallCount) {
		memmgrStagedCallCount--;
		debug(0x10, memmgrStagedCallCount);
		switch (memmgrStagedCalls[memmgrStagedCallCount].type) {
			case MEMMGR_FREE: free(memmgrStagedCalls[memmgrStagedCallCount].arg1); break;
			case MEMMGR_PAGE_FREE: page_free(memmgrStagedCalls[memmgrStagedCallCount].arg1, memmgrStagedCalls[memmgrStagedCallCount].arg2); break;
			case MEMMGR_PHY_PAGE_FREE: phy_page_free(memmgrStagedCalls[memmgrStagedCallCount].arg1, memmgrStagedCalls[memmgrStagedCallCount].arg2); break;
		}
	}

	memmgrNestedLevel--;
}


*/
