
/*
*
* Manages physical memory by keeping track of which memory areas are in use and which ones are free.
* The physical memory management is  fully thread and multiprocessing safe and lock-free.
*
* created: 27.12.14
*
*/

#include <system.h>
#include "memory.h"



//#define DBG_INIT(...)	LOGI(__VA_ARGS__)
#define DBG_INIT(...)
//#define DBG_ALLOC(...)	LOGI(__VA_ARGS__)
#define DBG_ALLOC(...)
//#define DBG_FREE(...)	LOGI(__VA_ARGS__)
#define DBG_FREE(...)



typedef struct memory_block_t
{
	uintptr_t startPage;					// number of the page where the block starts (derived from the physical start address)
	size_t pageCount;						// number of pages in this block
	struct memory_block_t * volatile next;
	volatile int flags;						// bit 0: allocated, bit 1: currently being modified
} memory_block_t; // size: ~36B


// Linked list of memory blocks, each of which is either marked "allocated" or "free"
// The list is sorted by address and spans the entire physical address space without
// overlaps or gaps.
// Blocks that are marked in use must not be accessed or edited, except for referencing
// the next element.
// The first block must always be allocated.
memory_block_t * volatile firstMemoryBlock = NULL;

// Linked list of memory block structures that are currently not in the memory list
// and can be recycled.
memory_block_t * volatile unusedBlocks = NULL;

// The maximum number of unused blocks that are kept when the list is cleaned up.
#define UNUSED_BLOCKS_MAX	(16)


typedef struct __attribute__((__packed__)) {
	uint16_t	entryLength;	// 0 for the first invalid entry
	uint64_t	baseAddress;
	uint64_t	length;
	uint32_t	type;			// 1: usable, anything else: consider reserved
	uint32_t	exAttr;			// ACPI 3.0 extended attributes (only valid if entryLength >= 24)
} bios_memory_block_t;



// Prints the specified list
void phy_dump_ex(memory_block_t *block) {
	int flags;
	for (; block; block = block->next)
		LOGI("  page %d: %d pages %s", block->startPage, block->pageCount, (flags = block->flags, (flags & 2 ? "in use" : (flags & 1 ? "allocated" : "free"))));
}

// Prints the current state of the allocated and free memory blocks.
void phy_dump(void) {
	LOGI("physical memory dump:");
	phy_dump_ex(firstMemoryBlock);
}



// Tests the physical memory manager by issuing some specially crafted
// alloc and free calls and dumping the memory list for inspection.
void phy_test(void) {
	phy_dump(); debug(0x10, 0);

	char *ptr1 = phy_page_alloc(12);

	// test various free patterns
	phy_dump(); debug(0x10, 1);
	phy_page_free(ptr1 + 3 * PAGE_SIZE, 2);
	phy_dump(); debug(0x10, 1);
	phy_page_free(ptr1 + 7 * PAGE_SIZE, 2);
	phy_dump(); debug(0x10, 1);
	phy_page_free(ptr1 + 1 * PAGE_SIZE, 2);
	phy_dump(); debug(0x10, 1);
	phy_page_free(ptr1 + 9 * PAGE_SIZE, 2);
	phy_dump(); debug(0x10, 1);
	phy_page_free(ptr1 + 5 * PAGE_SIZE, 2);
	phy_dump(); debug(0x10, 1);

	// test various alloc patterns
	char *ptr2 = phy_page_alloc(10);
	phy_dump(); debug(0x10, 2);
	phy_page_free(ptr2, 1);
	phy_page_free(ptr2 + 5 * PAGE_SIZE, 5);
	phy_dump(); debug(0x10, 2);
	char *ptr3 = phy_page_alloc(3);
	phy_dump(); debug(0x10, 2);
	char *ptr4 = phy_page_alloc(2);
	phy_dump(); debug(0x10, 2);
	char *ptr5 = phy_page_alloc(1);
	phy_dump(); debug(0x10, 2);

	// clean up
	phy_page_free(ptr1, 1);
	phy_page_free(ptr1 + 11 * PAGE_SIZE, 1);
	phy_page_free(ptr2 + 1 * PAGE_SIZE, 4);
	phy_page_free(ptr3, 3);
	phy_page_free(ptr4, 2);
	phy_page_free(ptr5, 1);

	// this dump should be equal to the first dump
	phy_dump(); debug(0x10, 3);
}







// initializes physical memory managment by using the list that
// was generated by the bootloader to generate a list of free and allocated memory regions
// that will then be used by phy_page_alloc and phy_page_free.
// This function uses malloc and free extensively, so these must already be initialized and
// have a large enough heap to not try to allocate a new heap. The required amount of memory
// depends on the memory map and can be estimated:
// ((memory map entries + 3) * sizeof(memory_block_t)) => ~320B for 6 entries
void phy_page_init() {
	//debug(5, (uint64_t)memoryMap);
	bios_memory_block_t *biosMemoryMap = *memoryMapPtr;

	memory_block_t *freeBlocks = NULL;
	memory_block_t *reservedBlocks = NULL;


	// step 1:
	// read all entries from the BIOS memory map and generate two separate lists for "free" and "allocated" regions
	for (int i = 0; biosMemoryMap[i].entryLength; i++) {
		//debug(9, (uint64_t)&biosMemoryMap[i]);
		if (!biosMemoryMap[i].length) continue;
		if (biosMemoryMap[i].entryLength >= 24 && !(biosMemoryMap[i].exAttr & 1)) continue;

		// "free" region: round up base address, round down length
		if (biosMemoryMap[i].type == 1) {
			//debug(7, (uint64_t)&biosMemoryMap[i].baseAddress);
			uint64_t delta = round_up(biosMemoryMap[i].baseAddress, PAGE_ALIGN_BITS) - biosMemoryMap[i].baseAddress;
			if (delta >= biosMemoryMap[i].length) continue; // "free" entry doen't span a whole page
			biosMemoryMap[i].baseAddress += delta;
			biosMemoryMap[i].length -= delta;

			biosMemoryMap[i].length = round_down(biosMemoryMap[i].length, PAGE_ALIGN_BITS);
			if (!biosMemoryMap[i].length) continue;
		} else { // "reserved" region: round down base address, round up length
			//debug(8, (uint64_t)&biosMemoryMap[i].baseAddress);
			uint64_t delta = biosMemoryMap[i].baseAddress - round_down(biosMemoryMap[i].baseAddress, PAGE_ALIGN_BITS);
			biosMemoryMap[i].baseAddress -= delta;
			biosMemoryMap[i].length += delta;

			biosMemoryMap[i].length = round_up(biosMemoryMap[i].length, PAGE_ALIGN_BITS);
		}

		// create a native data structure for the memory region
		memory_block_t *newBlock = (memory_block_t *)malloc(sizeof(memory_block_t));
		assert(newBlock);
		newBlock->startPage = biosMemoryMap[i].baseAddress >> PAGE_ALIGN_BITS;
		newBlock->pageCount = biosMemoryMap[i].length >> PAGE_ALIGN_BITS;
		newBlock->next = NULL;


		if (biosMemoryMap[i].type == 1) {
			// free blocks are inserted in address order and don't overlap

			memory_block_t *previous = NULL;
			memory_block_t *next = freeBlocks;

			// find the correct position within the free-blocks-list
			while (next) {
				if (next->startPage > newBlock->startPage) break;
				next = (previous = next)->next;
			}

			// insert new block into list (between previous and next)
			newBlock->next = next;
			if (previous)
				previous->next = newBlock;
			else
				freeBlocks = newBlock;

			// merge with preceding block if possible
			if (previous) {
				if (previous->startPage + previous->pageCount >= newBlock->startPage) {
					previous->pageCount = max(previous->pageCount, newBlock->pageCount + newBlock->startPage - previous->startPage);
					previous->next = next;
					free(newBlock);
					newBlock = previous;
				}
			}

			// merge with succeeding block(s) if possible
			while (next) {
				if (newBlock->startPage + newBlock->pageCount < next->startPage)
					break;

				newBlock->pageCount = max(newBlock->pageCount, next->pageCount + next->startPage - newBlock->startPage);
				newBlock->next = next->next;
				free(next);
				next = newBlock->next;
			}


		} else {
			// reserved blocks are not sorted
			newBlock->next = reservedBlocks;
			reservedBlocks = newBlock;
		}
	}

	//debug(6, (uint64_t)freeBlock);
	//debug(6, (uint64_t)reservedBlock);

	// at this point:
	// reservedBlock contains an unprocessed list of all reserved memory regions
	// freeBlock contains a non-overlapping list, sorted by start page, of all free memory regions

	//DBG_INIT("free blocks: ");
	//phy_dump_ex(freeBlocks);
	//DBG_INIT("reserved blocks: ");
	//phy_dump_ex(reservedBlocks);

	// step 2:
	// cut out all regions that are in the reserved-regions list

	while (reservedBlocks) {
		memory_block_t *previous = NULL;
		memory_block_t *next = freeBlocks;

		DBG_INIT("reserve block %d", reservedBlocks->startPage);

		// find the correct position within the free-blocks-list
		while (next) {
			if (next->startPage > reservedBlocks->startPage) break;
			next = (previous = next)->next;
		}

		if (previous)
			DBG_INIT("  after %d", previous->startPage);
		if (next)
			DBG_INIT("  before %d", next->startPage);


		// split previous free block if it ends after this reserved block
		if (previous) {
			if (previous->startPage + previous->pageCount > reservedBlocks->startPage + reservedBlocks->pageCount) {
				DBG_INIT("  split previous block");
				memory_block_t *newBlock = (memory_block_t *)malloc(sizeof(memory_block_t));
				assert(newBlock);
				newBlock->startPage = reservedBlocks->startPage + reservedBlocks->pageCount;
				newBlock->pageCount = (previous->startPage + previous->pageCount) - (reservedBlocks->startPage + reservedBlocks->pageCount);
				newBlock->next = previous->next;
				previous->next = next = newBlock;
			}
		}

		// prune/remove succeeding block(s) as long as they overlap
		while (next) {
			if (reservedBlocks->startPage + reservedBlocks->pageCount <= next->startPage)
				break;

			size_t delta = reservedBlocks->startPage + reservedBlocks->pageCount - next->startPage;
			if (delta < next->pageCount) { // prune
				DBG_INIT("  prune next block");
				next->pageCount -= delta;
				next->startPage += delta;
			} else { // remove
				DBG_INIT("  remove next block");
				if (previous) previous->next = next->next;
				else freeBlocks = next->next;
				memory_block_t *obsoleteBlock = next;
				next = next->next;
				free(obsoleteBlock);
			}
		}

		// prune preceding block if necessary
		if (previous) {
			if (previous->startPage + previous->pageCount > reservedBlocks->startPage) {
				DBG_INIT("  prune previous block");
				assert(previous->startPage <= reservedBlocks->startPage);
				previous->pageCount = reservedBlocks->startPage - previous->startPage;
			}
		}


		memory_block_t *obsoleteBlock = reservedBlocks;
		reservedBlocks = reservedBlocks->next;
		free(obsoleteBlock);
	}


	// step 3:
	// fill the gaps between free regions with blocks that are marked "allocated"

	uintptr_t currentPage = 0;
	memory_block_t *next = freeBlocks, *previous = NULL;

	while (next) {
		next->flags = 0;

		// remove empty blocks
		if (!(next->pageCount)) {
			next = next->next;
			if (previous)
				previous->next = next;
			else
				freeBlocks = next;
		}

		if (next->startPage > currentPage) {
			memory_block_t *newBlock = (memory_block_t *)malloc(sizeof(memory_block_t));
			assert(newBlock);
			newBlock->startPage = currentPage;
			newBlock->pageCount = next->startPage - currentPage;
			newBlock->flags = 1;
			newBlock->next = next;
			if (previous) previous->next = newBlock;
			else freeBlocks = newBlock;
			currentPage += newBlock->pageCount;
		}

		currentPage += next->pageCount;
		next = (previous = next)->next;
	}

	firstMemoryBlock = freeBlocks;
}




/*
//// Checks if the specified block has a length of zero, in which case it will be removed.
//// In this case, the two adjacent blocks are coalesced if possible
//void memory_block_validate(memory_block_t *block) {
//	if (!block->pageCount) {
//		if (block->previousBlock && block->nextBlock) {
//			if (block->previousBlock->allocated == block->nextBlock->allocated) {
//				// merge previous block with next block
//				block->previousBlock->pageCount += block->nextBlock->pageCount;
//				if (block->nextBlock->nextBlock)
//					block->nextBlock->nextBlock->previousBlock = block;
//				block->nextBlock = block->nextBlock->nextBlock;
//				memmgr_stage_free(block->nextBlock);
//			}
//			block->nextBlock->previousBlock = block->previousBlock;
//		}
//		block->previousBlock->nextBlock = block->nextBlock;
//		memmgr_stage_free(block);
//	}
//}

// Constructs and inserts a new empty block before the specified one if necessary.
// e.g. when an allocated block is requested and the block before the specified
// one is already marked allocated, no action is taken.
// Returns a non-zero value if the operation succeeded.
int memory_block_insert_before(memory_block_t *block, int allocated) {
	if (block->previousBlock->allocated != (allocated ? 1 : 0)) {
		memory_block_t *newBlock = (memory_block_t *)malloc(sizeof(memory_block_t));
		if (!newBlock) return 0;
		newBlock->startPage = block->startPage;
		newBlock->allocated = (allocated ? 1 : 0);
		newBlock->pageCount = 0;
		newBlock->previousBlock = block->previousBlock;
		newBlock->nextBlock = block;
		block->previousBlock->nextBlock = newBlock;
		block->previousBlock = newBlock;
	}
	return 1;
}

// Constructs and inserts a new empty block after the specified one if necessary.
// Returns a non-zero value if the operation succeeded.
int memory_block_insert_after(memory_block_t *block, int allocated) {
	if (block->nextBlock ? block->nextBlock->allocated != (allocated ? 1 : 0) : 1) {
		memory_block_t *newBlock = (memory_block_t *)malloc(sizeof(memory_block_t));
		if (!newBlock) return 0;
		newBlock->startPage = block->startPage + block->pageCount;
		newBlock->allocated = (allocated ? 1 : 0);
		newBlock->pageCount = 0;
		newBlock->previousBlock = block;
		newBlock->nextBlock = block->nextBlock;
		if (block->nextBlock)
			block->nextBlock->previousBlock = newBlock;
		block->nextBlock = newBlock;
	}
	return 1;
}

// Splits the specified block at the specified address into two blocks if necessary.
// i.e. when the blocks start address and the specified split address are equal, no action is taken.
// The address must be page aligned and within the block.
// The new block is inserted before the specified block.
int memory_block_split(memory_block_t *block, uintptr_t page) {
	if ((page != block->startPage) && (page != (block->startPage + block->pageCount))) {
		memory_block_t *newBlock = (memory_block_t *)malloc(sizeof(memory_block_t));
		if (!newBlock) return 0;

		newBlock->startPage = block->startPage;
		newBlock->pageCount = page - block->startPage;
		newBlock->allocated = block->allocated;
		block->startPage = page;
		block->pageCount -= newBlock->pageCount;
		newBlock->previousBlock = block->previousBlock;
		newBlock->nextBlock = block;
		if (block->previousBlock)
			block->previousBlock->nextBlock = newBlock;
		block->previousBlock = newBlock;
	}
	return 1;
}
*/



// Adds a block to the list of unused blocks.
//	thread-safe: yes
void mem_enqueue_unused_block(memory_block_t *block) {
	assert(block);
	memory_block_t *unusedBlocksFetch;
	do {
		unusedBlocksFetch = unusedBlocks;
		block->next = unusedBlocksFetch;
	} while (!(__sync_bool_compare_and_swap(&unusedBlocks, unusedBlocksFetch, block)));
}

// Removes and returns a block from the list of unused blocks.
// Returns NULL if the list was empty and no new block could be allocated.
//	allowAlloc: if set, if the list is empty, a new block is allocated.
//	thread-safe: yes
memory_block_t *mem_dequeue_unused_block(int allowAlloc) {
	memory_block_t *unusedBlocksFetch;
	do {
		unusedBlocksFetch = unusedBlocks;
		if (!unusedBlocksFetch)
			return (allowAlloc ? (memory_block_t *)malloc(sizeof(memory_block_t)) : NULL);
	} while (!(__sync_bool_compare_and_swap(&unusedBlocks, unusedBlocksFetch, unusedBlocksFetch->next)));
	return unusedBlocksFetch;
}


// Acquires the specified block by setting the in-use flag.
// Returns the flags before acquisition.
// A thread must never acquire a block that is before another block that it already owns.
//	thread-safe: yes
int mem_block_acquire(memory_block_t *block) {
	int oldFlags;
	while ((oldFlags = __sync_fetch_and_or(&(block->flags), 2)) & 2);
	return oldFlags;
}


// Acquires the head of the memory list.
// A thread must never acquire the head if it already owns an element of the list.
// The head can be released by setting it to a non-null value.
memory_block_t *mem_list_acquire() {
	memory_block_t *head;
	while (!(head = __sync_fetch_and_and(&firstMemoryBlock, 0)));
	return head;
}



// Removes the current memory block from the list and merges the two adjacent blocks if possible.
// It is assumed that the previous and current blocks are already owned by the current thread,
// and that the current block has a size of 0.
//	thread-safe: yes
void mem_block_remove(memory_block_t *previous, memory_block_t *current) {
	memory_block_t *next = previous->next = current->next;
	mem_enqueue_unused_block(current);

	// acquire next block
	if (!next)
		return;
	int oldFlags = mem_block_acquire(next);

	// check if previous and next block can be merged
	if ((oldFlags & 1) != (previous->flags & 1)) {
		next->flags = oldFlags;
		return;
	}

	assert(previous->startPage + previous->pageCount == next->startPage);
	previous->pageCount += next->pageCount;
	previous->next = next->next;
	mem_enqueue_unused_block(next);
}



// Allocates the specified number of contiguous pages in physical address space.
// If clear is set, the allocated pages are filled with zeros
// After this call, phy_cleanup should be called.
void *phy_page_alloc(size_t count) {
	memory_block_t *previous, *current;
	int oldFlagsP, oldFlagsC, wasInUse;

	do {
		previous = NULL;
		memory_block_t *firstMemoryBlockTemp = current = mem_list_acquire();
		oldFlagsP = 0; oldFlagsC = 0; wasInUse = 0;

		for (; current; current = current->next) {
			if (previous) {
				// a free block can only be allocated if the previous one was already allocated

				if ((oldFlagsC = __sync_val_compare_and_swap(&(current->flags), 0, 3)) == 0) { // find and acquire a block that is free and not in use
					if (current->pageCount >= count) {

						// At this point we found a suitable free block that is preceeded by an allocated block.
						// Grow previous block and shrink current block.
						void *addr = (void *)((previous->startPage + previous->pageCount) << PAGE_ALIGN_BITS);
						previous->pageCount += count;
						current->startPage += count;
						if (!(current->pageCount -= count))
							mem_block_remove(previous, current);
						else
							current->flags = oldFlagsC;
						
						previous->flags = oldFlagsP;
						return addr;
					}

					// release current block
					current->flags = oldFlagsC;
				}

				// release previous block
				previous->flags = oldFlagsP;
				previous = NULL;
			} else if (firstMemoryBlockTemp) {
				// release head of the list
				firstMemoryBlock = firstMemoryBlockTemp;
				firstMemoryBlockTemp = NULL;
			}

			if ((oldFlagsP = __sync_val_compare_and_swap(&(current->flags), 1, 3)) == 1) // find and acquire a block that is allocated and not in use
				previous = current;
			else
				previous = NULL;

			wasInUse |= (oldFlagsP & 2) | (oldFlagsC & 2);
		}

		if (previous)
			previous->flags = oldFlagsP;
	} while (wasInUse); // repeat the whole scan if any of the blocks were being edited by another thread.

	return NULL;




	/*
	memmgr_enter_routine();

	//LOGI("phy page alloc\n");

	for (memory_block_t *currentBlock = firstMemoryBlock; currentBlock; currentBlock = currentBlock->nextBlock) {
		//LOGI("check block\n");
		if (currentBlock->allocated) continue;
		if (currentBlock->pageCount < count) continue;
	
		// if the block before this one was not allocated, insert an empty one
		// todo: assert(block->previousBlock->allocated);
		// if (!memory_block_insert_before(currentBlock, 1)) must not use malloc here
		// 	return memmgr_exit_routine(), NULL;
	
		// expand previous (allocated) block and shrink this one
		uintptr_t result = currentBlock->startPage << PAGE_ALIGN_BITS;
		currentBlock->previousBlock->pageCount += count;
		currentBlock->pageCount -= count;
		currentBlock->startPage += count;
	
		// if this block became empty, remove it
		//memory_block_validate_block(currentBlock);
	
		//LOGI("phy page alloc complete\n");
		return memmgr_exit_routine(), (void *)result;
	}
	
	LOGE("phy page alloc failed\n");
	
	// all memory has been scanned without success
	return memmgr_exit_routine(), NULL;
	*/
}


// Frees the specified number of contiguous pages in physical address space.
// After this call, phy_cleanup should be called.
void phy_page_free(void *address, size_t count) {
	assert(!((uintptr_t)address & PAGE_SIZE_MASK));
	assert(count);
	uintptr_t page = ((uintptr_t)address >> PAGE_ALIGN_BITS);

	// Before we mess around with the memory list, make some 
	// blocks available to be used if neccessary.
	memory_block_t *dequeuedBlock1 = mem_dequeue_unused_block(1);
	memory_block_t *dequeuedBlock2 = mem_dequeue_unused_block(1);
	if (!dequeuedBlock1 || !dequeuedBlock2)
		bug_check(STATUS_OUT_OF_MEMORY, 0);


	memory_block_t *previous, *current, *next;
	int oldFlagsP = 0, oldFlagsC = 0, oldFlagsN = 0;

	previous = NULL;
	memory_block_t *firstMemoryBlockTemp = current = mem_list_acquire();

	// find the memory block that contains the pages to be freed
	for (;;) {
		assert(current);
		oldFlagsC = mem_block_acquire(current);

		if ((current->startPage <= page) && (current->startPage + current->pageCount > page)) {
			assert(current->startPage + current->pageCount >= page + count); // ensure that the pages are fully within the current block
			next = current->next;
			if (next) oldFlagsN = mem_block_acquire(next);
			break;
		}

		if (previous)
			previous->flags = oldFlagsP;
		else
			firstMemoryBlock = firstMemoryBlockTemp;
		previous = current;
		current = current->next;
		oldFlagsP = oldFlagsC;
	}


	// The pages to be freed are now in current, while
	// previous and next point to the adjacent blocks or are NULL,
	// oldFlagsP, oldFlagsC, oldFlagsN hold their old flags.
	// All three blocks (if not NULL) are owned by the current thread.
	// If previous is NULL, that means that the head of the list has been acquired.
	
	DBG_FREE("free %d", page);

	// ensure that the pages to be freed are aligned with the start of current block
	if (current->startPage != page) {
		size_t delta = page - current->startPage;
		if (previous && ((oldFlagsP & 1) == (current->flags & 1))) {
			DBG_FREE("  shift before (delta %d)", delta);
			previous->pageCount += delta;
		} else {
			DBG_FREE("  split start of %d, %d (delta %d)", current->startPage, current->pageCount, delta);
			memory_block_t *newBlock = dequeuedBlock1;
			dequeuedBlock1 = NULL;
			newBlock->pageCount = delta;
			newBlock->startPage = current->startPage;
			newBlock->next = current;
			newBlock->flags = current->flags;
			if (previous) {
				previous->next = newBlock;
				previous->flags = oldFlagsP;
			} else {
				firstMemoryBlock = newBlock;
			}
			previous = newBlock;
			oldFlagsP = oldFlagsC;
		}
		current->startPage = page;
		current->pageCount -= delta;
	}

	// ensure that the pages to be freed are aligned with the end of the current block
	if (current->pageCount != count) {
		size_t delta = current->pageCount - count;
		if (next && ((oldFlagsN & 1) == (current->flags & 1))) {
			DBG_FREE("  shift after (delta %d)", delta);
			next->startPage -= delta;
			next->pageCount += delta;
		} else {
			DBG_FREE("  split end of %d, %d (delta %d)", current->startPage, current->pageCount, delta);
			memory_block_t *newBlock = dequeuedBlock2;
			dequeuedBlock2 = NULL;
			newBlock->pageCount = delta;
			newBlock->startPage = current->startPage + count;
			newBlock->next = next;
			newBlock->flags = current->flags;
			current->next = newBlock;
			if (next)
				next->flags = oldFlagsN;
			next = newBlock;
			oldFlagsN = oldFlagsC;
		}
		current->pageCount -= delta;
	}

	if (previous) DBG_FREE("  previous: %d, %d", previous->startPage, previous->pageCount);
	DBG_FREE("  current: %d, %d", current->startPage, current->pageCount);
	if (next) DBG_FREE("  next: %d, %d", next->startPage, next->pageCount);

	// clear the allocated flag
	current->flags &= ~(1);

	// try to coalesce with previous block and release previous block
	if (previous) {
		if ((previous->flags & 1) == (current->flags & 1)) {
			previous->pageCount += current->pageCount;
			previous->next = current->next;
			mem_enqueue_unused_block(current);
			current = previous;
		} else {
			previous->flags = oldFlagsP;
		}
	} else {
		firstMemoryBlock = firstMemoryBlockTemp;
	}

	// try to coalesce with next block and release next block
	if (next) {
		if ((next->flags & 1) == (current->flags & 1)) {
			current->pageCount += next->pageCount;
			current->next = next->next;
			mem_enqueue_unused_block(next);
		} else {
			next->flags = oldFlagsN;
		}
	}

	// release current block
	current->flags &= ~(2);


	// recycle blocks that were dequeued but not used
	if (dequeuedBlock1)
		mem_enqueue_unused_block(dequeuedBlock1);
	if (dequeuedBlock2)
		mem_enqueue_unused_block(dequeuedBlock2);











	/*

	memmgr_enter_routine();

	memory_block_t *currentBlock = firstMemoryBlock;
	while (currentBlock->nextBlock) {
		if (currentBlock->nextBlock->startPage > ((uintptr_t)address >> PAGE_ALIGN_BITS)) break;
		currentBlock = currentBlock->nextBlock;
	}

	// the pages to be freed are somewhere in currentBlock

	if (currentBlock->startPage + currentBlock->pageCount == ((uintptr_t)address >> PAGE_ALIGN_BITS) + count) {

		// case 1: the pages to be freed are at the end of currentBlock or equal
		memory_block_insert_after(currentBlock, 0);

		currentBlock->nextBlock->pageCount += count;
		currentBlock->nextBlock->startPage -= count;
		currentBlock->pageCount -= count;

	} else {

		// case 2: the pages to be freed are somewhere in the middle of currentBlock
		memory_block_split(currentBlock, (uintptr_t)address);

		// case 3: the pages to be freed are at the beginning of currentBlock
		memory_block_insert_before(currentBlock, 0);

		currentBlock->previousBlock->pageCount += count;
		currentBlock->pageCount -= count;
		currentBlock->startPage += count;
	}

	memmgr_exit_routine();*/
}


// Removes or coaleces blocks whenever possible. (NO)
// Limits the size of the list of unused blocks.
// This should only be called while the heap is in a consistent state.
void phy_cleanup(void) {
	memory_block_t *blocks[UNUSED_BLOCKS_MAX];
	memory_block_t *block;
	size_t i = 0;

	// unqueue all blocks and keep some
	while ((block = mem_dequeue_unused_block(0))) {
		if (i < UNUSED_BLOCKS_MAX)
			blocks[i++] = block;
		else
			free(block);
	}

	// enqueue the blocks we kept
	while (i--)
		mem_enqueue_unused_block(blocks[i]);


	/*
	for (memory_block_t *currentBlock = firstMemoryBlock->nextBlock; currentBlock; currentBlock = currentBlock->nextBlock) {
		if (currentBlock->pageCount && (currentBlock->allocated != currentBlock->previousBlock->allocated))
			continue;
		currentBlock->previousBlock->nextBlock = currentBlock->nextBlock;
		if (currentBlock->nextBlock)
			currentBlock->nextBlock->previousBlock = currentBlock->previousBlock;
		free(currentBlock);
	}
	*/
}
