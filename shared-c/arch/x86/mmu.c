
/*
*
* Manages virtual (linear) memory space and maps it to physical memory by using the MMU utilities provided by the hardware.
* Physical pages that hold data or paging structures are allocated and freed dynamically.
*
* Memory organization:
*	0000000000000000 - 0000007FFFFFFFFF (1st 512GB)
*		never mapped (so referencing NULL fails)
*	0000008000000000 - 00007FFFFFFFFFFF
*		userspace (lower half)
*	0000800000000000 - FFFF7FFFFFFFFFFF
*		not accessible due to x86_64 architecture, may wrap around in some cases
*	FFFF800000000000 - FFFFFEFFFFFFFFFF
*		userspace (upper half)
*	FFFFFF0000000000 - FFFFFF8000000000
*		PML1T (ordered)
*	FFFFFF8000000000 - FFFFFF8040200FFF
*		paging structures PML2T, PML3T, PML4T (ordered)
*	FFFFFF8040201000 - FFFFFF8040201FFF
*		page for temporary mapping
*	FFFFFFFF80000000 - FFFFFFFF800FFFFF
*		bootloader/bios memory
*	FFFFFFFF80100000 - FFFFFFFFFFFFFFFF
*		kernel memory (initial stack is at the last page)
*
*
* created: 23.12.14
*
*/

#include <system.h>
#include "mmu.h"


//#define DBG_MAP(...)	LOGI(__VA_ARGS__)
#define DBG_MAP(...)
//#define DBG_FIND(...)	LOGI(__VA_ARGS__)
#define DBG_FIND(...)



// the paging structures occupy the last ~500GB of linear address space
uint64_t *PML1T = (uint64_t *)0xFFFFFF0000000000UL; // 512GB,  68'719'476'736 elements, each element maps a 4kB page (PT in AMD manuals)
uint64_t *PML2T = (uint64_t *)0xFFFFFF8000000000UL; // 1GB,       134'217'728 elements, each element controls a 2MB area (PDT in AMD manuals)
uint64_t *PML3T = (uint64_t *)0xFFFFFF8040000000UL; // 2MB,           262'144 elements, each element controls a 1GB area (PDPT in AMD manuals)
uint64_t *PML4T = (uint64_t *)0xFFFFFF8040200000UL; // 4kB,               512 elements, each element controls a 512GB area (PML4T in AMD manuals)

// the address of the virtual page reserved for temporary mapping
#define TEMP_PAGE_VA		(0xFFFFFF8040201000UL)

#define PAGE_TABLE_SIZE			(512)	// number of entries in each paging structure

// the 1st entry in PML4T is reserved,
// the next 509 entries control userspace memory,
// the last 2 entries control kernel memory
#define USERSPACE_START			(0x0000000000001000UL)
#define USERSPACE_END			(0xFFFFFEFFFFFFFFFFUL)
#define MAPSPACE_START			(0xFFFFFF0000000000UL)
#define MAPSPACE_END			(0xFFFFFF8040200FFFUL)
#define KERNELSPACE_START		(0xFFFFFFFF80000000UL)
#define KERNELSPACE_END			(0xFFFFFFFFFFFFEFFFUL)

#define MEMSPACE_START(userspace)	((userspace) ? USERSPACE_START : KERNELSPACE_START)
#define MEMSPACE_END(userspace)		((userspace) ? USERSPACE_END : KERNELSPACE_END)
#define PML4T_START(userspace)		((MEMSPACE_START(userspace) >> 39) & 0x1FF)
#define PML4T_END(userspace)		(((MEMSPACE_END(userspace) >> 39) & 0x1FF) + 1)
#define PML3T_START(userspace)		((MEMSPACE_START(userspace) >> 30) & 0x1FF)
#define PML3T_END(userspace)		(((MEMSPACE_END(userspace) >> 30) & 0x1FF) + 1)
#define PML2T_START(userspace)		((MEMSPACE_START(userspace) >> 21) & 0x1FF)
#define PML2T_END(userspace)		(((MEMSPACE_END(userspace) >> 21) & 0x1FF) + 1)
#define PML1T_START(userspace)		((MEMSPACE_START(userspace) >> 12) & 0x1FF)
#define PML1T_END(userspace)		(((MEMSPACE_END(userspace) >> 12) & 0x1FF) + 1)


// retrieves a page table entry based on a page numer in linear address space (0 referring to the page at 0x0000000000000000 - 0x0000000000001000)
#define PML4T_VA_ENTRY(i)										(PML4T[((i) >> 27) & 0x00000000000001FFUL])
#define PML3T_VA_ENTRY(i)										(PML3T[((i) >> 18) & 0x000000000003FFFFUL])
#define PML2T_VA_ENTRY(i)										(PML2T[((i) >> 9) & 0x0000000007FFFFFFUL])
#define PML1T_VA_ENTRY(i)										(PML1T[((i) >> 0) & 0x0000000FFFFFFFFFUL])

// retrieves a page table enrty based on indices into all relevant paging structures
#define PML4T_GET_ENTRY(i_pml4t)								PML4T[((i_pml4t) << 0)]
#define PML3T_GET_ENTRY(i_pml4t, i_pml3t)						PML3T[((i_pml4t) << 9) | ((i_pml3t) << 0)]
#define PML2T_GET_ENTRY(i_pml4t, i_pml3t, i_pml2t)				PML2T[((i_pml4t) << 18) | ((i_pml3t) << 9) | ((i_pml2t) << 0)]
#define PML1T_GET_ENTRY(i_pml4t, i_pml3t, i_pml2t, i_pml1t)		PML1T[((i_pml4t) << 27) | ((i_pml3t) << 18) | ((i_pml2t) << 9) | ((i_pml1t) << 0)]


// page-table entry:
// 63		"no-execute" flag
// 62:53	"reference count" - counts the number of (at least partly) allocated entries in the table that this entry points to (for entries in PML1T this is always non-zero)
// 52		"full" flag (MAY be set if the entire address space controlled by this entry is allocated, always set for PML1T entries that are in use)
// 51:12	4kB aligned physical address of lower level paging structure
// 11:9		unused
// 8		"global" flag
// 7		cache control
// 6		"dirty" flag (set by CPU, only in lowest level table)
// 5		"acessed" flag (set by CPU)
// 4:3		cache control
// 2		"user mode" flag
// 1		"writable" flag
// 0		"present" flag



// Creates a page table entry for the specified physical address (returns 0 if phy_addr is NULL).
//	phy_addr: physical address of the referenced paging structure or page frame (must not exceed 52 bits)
//	user: set to TRUE to allow user mode access to any of the address space controlled by this entry
//	write: set to TRUE to allow write access to any of the address space controlled by this entry
//	execute: set to FALSE to allow instruction fetching from any of the address space controlled by this entry
// the entry is must subsequently be used
#define pte_create(phy_addr, user, write, execute)		((phy_addr) ? (((uint64_t)(phy_addr) & 0x000FFFFFFFFFF000UL) | (uint64_t)((user) ? (1 << 2) : 0) | (uint64_t)((write) ? (1 << 1) : 0) | (uint64_t)(nxSupport && !(execute) ? (1UL << 63) : 0) | 1UL) : 0UL)

// returns a non-zero value if any of the linear addresses space that the specified entry controls is allocated
#define pte_is_partly_allocated(entry)	(((entry) >> 53) & 0x3FFUL)

// returns zero value if not all of the linear addresses space that the specified entry controls is allocated (otherwise it may still return zero, except for PML1T entries)
#define pte_is_fully_allocated(entry)	(((entry) >> 52) & 1)

// returns a non-zero value if none of the linear addresses space that the specified entry controls is allocated
#define pte_is_completely_free(entry)	(!(pte_is_partly_allocated(entry)))

// swaps in the page table or page frame that the specified entry points to (currently not implemented)
#define pte_swap_in(entry)				(1)

// returns the physical address of the paging structure or page frame that this entry points to
#define pte_get_phy_addr(entry)			((void *)((entry) & 0x000FFFFFFFFFF000UL))

// increments the "reference count" field of the specified entry
#define pte_alloc_child(entry)			((entry) += (1UL << 53))

// decrements the "reference count" field of the specified entry
#define pte_free_child(entry)			((entry) -= (1UL << 53))

// marks the specified entry as having no more free space
#define pte_mark_full(entry)			((entry) |= (1UL << 52))

// marks the specified entry as having some free space
#define pte_mark_not_full(entry)		((entry) &= ~(1UL << 52))

// flushes the translation lookaside buffer
#define flush_tlb()						write_cr3(read_cr3())

// creates a cannonical address from any 64-bit value copying the 48'th bit to the upper 16 bits
#define make_cannonical_va(address)		((void *)(((intptr_t)(address) << 16) >> 16))


int nxSupport = 0; // todo: check CPUID for no-execute support


uint64_t pml4tTemp;

uintptr_t realmodePML3TAddr;

void mmu_init(void) {
	realmodePML3TAddr = PML1T[((uintptr_t)TEMP_PAGE_VA & 0x0000FFFFFFFFFFFFUL) >> PAGE_ALIGN_BITS] & 0x000FFFFFFFFFF000;
}

REGISTER_INIT0(mmu_init);


// Maps a physical page to a well-known virtual page (writable, non-executable, kernel space) and clears it.
// This is guaranteed to work without recursive calls due
// to the way the bootloader sets up paging.
// Returns the virtual address that was used.
static void *page_map_temp(void *physicalAddress) {
	PML1T[((uintptr_t)TEMP_PAGE_VA & 0x0000FFFFFFFFFFFFUL) >> PAGE_ALIGN_BITS] = pte_create(physicalAddress, 0, 1, 0);
	flush_tlb();
	for (int i = 0; i < (PAGE_SIZE >> 3); i++)
		((uint64_t *)TEMP_PAGE_VA)[i] = 0;
	return (void *)TEMP_PAGE_VA;
}

// Restores a 1:1 mapping of the real mode memory to allow exiting paging
void page_map_realmode(void) {
	((uint64_t *)page_map_temp((void *)realmodePML3TAddr))[0] = PML3T[0x3FFFE];
	pml4tTemp = PML4T[0];
	PML4T[0] = pte_create(realmodePML3TAddr, 0, 1, 0);
	flush_tlb();
}

// Cleans up traces from real mode by making the virtual page 0 unavailable
void page_unmap_realmode(void) {
	PML4T[0] = pml4tTemp;
	flush_tlb();
}





// Maps the specified page in physical address space to the specified page in virtual address space using the specified settings
// Returns zero if the operation succeeded.
int page_map_single(void *physicalAddress, uintptr_t virtualPage, int userspace, int writable, int executable) {
	struct
	{
		void *physicalAddress;
		uintptr_t virtualPage;
	} newTables[3];
	int newTablesCount = 0;

	DBG_MAP("map physical address %xp to virtual address %xp", (uintptr_t)physicalAddress, virtualPage << 12);

	// ensure that page is within limits of the specified usage
	uintptr_t va = (uintptr_t)make_cannonical_va(virtualPage << 12);
	if (userspace)
		assert(va >= USERSPACE_START && va <= USERSPACE_END);
	else
		assert((va >= KERNELSPACE_START && va <= KERNELSPACE_END) || (va >= MAPSPACE_START && va <= MAPSPACE_END));


	virtualPage &= 0x0000000FFFFFFFFFUL;

	size_t PML1TIndex = virtualPage & 0x1FFUL;
	uint64_t *currentPML1T = &PML1T[virtualPage & ~(0x1FFUL)];
	size_t PML2TIndex = (virtualPage >>= 9) & 0x1FFUL;
	uint64_t *currentPML2T = &PML2T[virtualPage & ~(0x1FFUL)];
	size_t PML3TIndex = (virtualPage >>= 9) & 0x1FFUL;
	uint64_t *currentPML3T = &PML3T[virtualPage & ~(0x1FFUL)];
	size_t PML4TIndex = (virtualPage >>= 9) & 0x1FFUL;
	uint64_t *currentPML4T = PML4T;


	// ensure that PML3T is available
	if (pte_is_completely_free(currentPML4T[PML4TIndex])) {
		//LOGI("PML3T missing\n");
		// allocate new PML3T and reference it in PML4TE
		if (!(newTables[newTablesCount].physicalAddress = phy_page_alloc(1))) return 1;
		currentPML4T[PML4TIndex] = pte_create(newTables[newTablesCount].physicalAddress, 1, 1, 1);


		// remember where to map the new table in the end
		newTables[newTablesCount].virtualPage = (uintptr_t)currentPML3T >> PAGE_ALIGN_BITS;

		// temporarily map new PML3T so that we can access it
		currentPML3T = (uint64_t *)page_map_temp(newTables[newTablesCount].physicalAddress);

		// remember how many new tables need to be mapped
		newTablesCount++;
	}

	// ensure that PML2T is available
	if (pte_is_completely_free(currentPML3T[PML3TIndex])) {
		//LOGI("PML2T missing\n");
		if (!(newTables[newTablesCount].physicalAddress = phy_page_alloc(1))) return 1;
		pte_alloc_child(currentPML4T[PML4TIndex]); // the PML3T has now one more entry
		currentPML3T[PML3TIndex] = pte_create(newTables[newTablesCount].physicalAddress, 1, 1, 1);
		newTables[newTablesCount].virtualPage = (uintptr_t)currentPML2T >> PAGE_ALIGN_BITS;
		currentPML2T = (uint64_t *)page_map_temp(newTables[newTablesCount].physicalAddress);
		newTablesCount++;
	}

	// ensure that PML1T is available
	if (pte_is_completely_free(currentPML2T[PML2TIndex])) {
		//LOGI("PML1T missing\n");
		if (!(newTables[newTablesCount].physicalAddress = phy_page_alloc(1))) return 1;
		pte_alloc_child(currentPML3T[PML3TIndex]);
		currentPML2T[PML2TIndex] = pte_create(newTables[newTablesCount].physicalAddress, 1, 1, 1);
		newTables[newTablesCount].virtualPage = (uintptr_t)currentPML1T >> PAGE_ALIGN_BITS;
		//debug(0x36, newTables[newTablesCount].physicalAddress);
		currentPML1T = (uint64_t *)page_map_temp(newTables[newTablesCount].physicalAddress);
		//debug(0x37, currentPML1T);
		newTablesCount++;
	}

	pte_alloc_child(currentPML2T[PML2TIndex]);

	// ensure that the virtual page wasn't already allocated
	assert(pte_is_completely_free(currentPML1T[PML1TIndex]));
	
	//LOGI("creating page frame\n");

	currentPML1T[PML1TIndex] = pte_create(physicalAddress, userspace, writable, executable);
	pte_alloc_child(currentPML1T[PML1TIndex]);
	pte_mark_full(currentPML1T[PML1TIndex]);

	flush_tlb();

	// map all newly created tables
	if (newTablesCount)
		DBG_MAP("map %d tables", newTablesCount);
	while (newTablesCount--)
		if (page_map_single(newTables[newTablesCount].physicalAddress, newTables[newTablesCount].virtualPage, 0, 1, 0))
			return 1;


	//LOGI("page map complete\n");
	return 0;
}



// Finds the first contiguous memory block of the requested size in virtual address space
//	count: the number of contiguous virtual pages to find
// Returns the virtual page number of the block that was found or 0 if no large enough block was found.
uintptr_t page_find(size_t count, int userspace) {
	DBG_FIND("searching %d free pages for %s", count, (userspace ? "userspace" : "kernelspace"));

	// We do a depth-search through the page table hierarchy for each entry, while
	// skipping entries if we already know that they're fully allocated/free.
	// The search is aborted once enough contiguous free space is found.
	// Also, if we detect that an entry is completely allocated, we mark it as full to save time in the next search.
	size_t freePages = 0;
	size_t nonFreePages = MEMSPACE_START(userspace) >> PAGE_ALIGN_BITS;
	int pml3t_full, pml2t_full, pml1t_full;


	size_t pml4t_start = PML4T_START(userspace), pml4t_end = PML4T_END(userspace);
	size_t pml3t_start = PML3T_START(userspace), pml3t_end = PML3T_END(userspace);
	size_t pml2t_start = PML2T_START(userspace), pml2t_end = PML2T_END(userspace);
	size_t pml1t_start = PML1T_START(userspace), pml1t_end = PML1T_END(userspace);

	for (size_t i4 = pml4t_start; i4 < pml4t_end && freePages < count; i4++) {
		DBG_FIND("  looking at PTE4T[%d]", i4);
		if (pte_is_completely_free(PML4T_GET_ENTRY(i4)) || pte_is_fully_allocated(PML4T_GET_ENTRY(i4))) {
			freePages += 512 * 512 * 512 - (pml3t_start << 18) - (pml2t_start << 9) - pml1t_start;
			if (pte_is_fully_allocated(PML4T_GET_ENTRY(i4))) {
				nonFreePages += freePages;
				freePages = 0;
			}
		} else {
			pml3t_full = 1;
			for (size_t i3 = pml3t_start; i3 < (((i4 == pml4t_end - 1)) ? pml3t_end : PAGE_TABLE_SIZE) && freePages < count; i3++) {
				DBG_FIND("    looking at PTE3T[%d]", i3);
				if (pte_is_completely_free(PML3T_GET_ENTRY(i4, i3)) || pte_is_fully_allocated(PML3T_GET_ENTRY(i4, i3))) {
					freePages += 512 * 512 - (pml2t_start << 9) - pml1t_start;
					if (pte_is_fully_allocated(PML3T_GET_ENTRY(i4, i3))) {
						nonFreePages += freePages;
						freePages = 0;
					} else {
						pml3t_full = pml2t_full = 0;
					}
				} else {
					pml2t_full = 1;
					for (size_t i2 = pml2t_start; i2 < (((i4 == pml4t_end - 1) && (i3 == pml3t_end - 1)) ? pml2t_end : PAGE_TABLE_SIZE) && freePages < count; i2++) {
						DBG_FIND("      looking at PTE2T[%d]", i2);
						if (pte_is_completely_free(PML2T_GET_ENTRY(i4, i3, i2)) || pte_is_fully_allocated(PML2T_GET_ENTRY(i4, i3, i2))) {
							//debug(0x35, PML2T_GET_ENTRY(i4, i3, i2));
							freePages += 512 - pml1t_start;
							if (pte_is_fully_allocated(PML2T_GET_ENTRY(i4, i3, i2))) {
								nonFreePages += freePages;
								freePages = 0;
							} else {
								pml3t_full = pml2t_full = 0;
							}
						} else {
							pml1t_full = 1;
							for (size_t i1 = pml1t_start; i1 < (((i4 == pml4t_end - 1) && (i3 == pml3t_end - 1) && (i2 == pml2t_end - 1)) ? pml1t_end : PAGE_TABLE_SIZE) && freePages < count; i1++) {
								DBG_FIND("        looking at PTE1T[%d]", i1);
								freePages++;
								//debug(0x36, (uint64_t)&(PML1T_GET_ENTRY(i4, i3, i2, i1)));
								if (pte_is_fully_allocated(PML1T_GET_ENTRY(i4, i3, i2, i1))) {
									nonFreePages += freePages;
									freePages = 0;
								} else {
									pml3t_full = pml2t_full = pml1t_full = 0;
								}
							}
							if (pml1t_full) pte_mark_full(PML2T_GET_ENTRY(i4, i3, i2));
						}
						pml1t_start = 0;
					}
					if (pml2t_full) pte_mark_full(PML3T_GET_ENTRY(i4, i3));
				}
				pml2t_start = 0; pml1t_start = 0;
			}
			if (pml3t_full) pte_mark_full(PML4T_GET_ENTRY(i4));
		}
		pml3t_start = 0; pml2t_start = 0; pml1t_start = 0;
	}

	DBG_FIND("found %d free pages at %xp", freePages, (void *)(nonFreePages << PAGE_ALIGN_BITS));

	return (freePages < count ? 0 : nonFreePages);
}



// Maps the pages starting at the specified physical address to an appropriate block of writable, kernelspace, non-executable virtual pages.
//	physicalAddress: the physical address where the block starts (must be page aligned)
//	length: the length in bytes of the block to be mapped (must be a multiple of PAGE_SIZE)
// Returns the page aligned virtual address that was mapped or NULL if the operation failed.
void *page_map(void *physicalAddress, size_t length, int userspace, int writable, int executable) {
	assert(!((uintptr_t)physicalAddress & PAGE_SIZE_MASK));
	assert(!(length & PAGE_SIZE_MASK));
	uintptr_t page = page_find(length >>= PAGE_ALIGN_BITS, 0);
	if (!page)
		return NULL;
	for (int i = 0; i < length; i++)
		if (page_map_single((char *)physicalAddress + (i << PAGE_ALIGN_BITS), page + i, userspace, writable, executable))
			return NULL;
	return make_cannonical_va(page << PAGE_ALIGN_BITS);
}



// Allocates a large contiguous block of memory in linear address space
// and maps it to somewhere in physical memory using the specified access modifiers
// The returned address will be page aligned.
//	length: the length in bytes of the block to be allocated (must be a multiple of PAGE_SIZE)
//	userspace: the page can be accessed from user space
//	writable: the page can be written to
//	executable: instructions can be fetched from this page
void *page_alloc_ex(size_t length, int userspace, int writable, int executable) {
	assert(!(length & PAGE_SIZE_MASK));

	// first, try to allocate the physical memory as a single chunk
	void *phyAddr = phy_page_alloc(length >> PAGE_ALIGN_BITS);
	if (phyAddr)
		return page_map(phyAddr, length, userspace, writable, executable);

	// if the chunk is too large, alloc and map pages one by one
	uintptr_t page = page_find(length >>= PAGE_ALIGN_BITS, userspace);
	if (!page)
		return NULL;

	for (int i = 0; i < length; i++) {
		phyAddr = phy_page_alloc(1);
		if (!phyAddr)
			return NULL;
		if (page_map_single(phyAddr, page + i, userspace, writable, executable))
			return NULL;
	}
	return make_cannonical_va(page << PAGE_ALIGN_BITS);
}



// Allocates a writable, non-executable kernelspace page
void *page_alloc(size_t length) {
	return page_alloc_ex(length, 0, 1, 1);
}



// Frees a virtual page and it's underlying physical page in linear address space.
// The address and size parameters should be page aligned.
void page_free(void *address, size_t size) {
	assert(!((uintptr_t)address & PAGE_SIZE_MASK));
	assert(!(size & PAGE_SIZE_MASK));

	memmgr_enter_routine();

	uintptr_t page = ((uintptr_t)address >> PAGE_ALIGN_BITS) & 0x0000000FFFFFFFFFUL;
	size >>= PAGE_ALIGN_BITS;

	void *freeTables[4];
	int freeTableCount = 0;

	// Free every page separately.
	// If freeing a page makes the table that controls it free, it is also freed.
	// This chain of freeing is propagated up to the topmost page table
	while (size--) {
		//debug(0x11, page);
		//debug(0x11, size);

		//debug(0x11, ((page) >> 0) & 0x0000000FFFFFFFFFUL);
		freeTableCount = 0;

		void *phyPageAddr = pte_get_phy_addr(PML1T_VA_ENTRY(page));
		pte_mark_not_full(PML1T_VA_ENTRY(page));
		pte_free_child(PML1T_VA_ENTRY(page));

		pte_mark_not_full(PML2T_VA_ENTRY(page));
		pte_free_child(PML2T_VA_ENTRY(page));

		if (pte_is_completely_free(PML2T_VA_ENTRY(page))) {
			freeTables[freeTableCount++] = &PML1T[(page >> 0) & ~(0x1FFUL)];
			pte_mark_not_full(PML3T_VA_ENTRY(page));
			pte_free_child(PML3T_VA_ENTRY(page));

			if (pte_is_completely_free(PML3T_VA_ENTRY(page))) {
				freeTables[freeTableCount++] = &PML2T[(page >> 9) & ~(0x1FFUL)];
				pte_mark_not_full(PML4T_VA_ENTRY(page));
				pte_free_child(PML4T_VA_ENTRY(page));

				if (pte_is_completely_free(PML4T_VA_ENTRY(page))) {
					freeTables[freeTableCount++] = &PML3T[(page >> 18) & ~(0x1FFUL)];
				}
			}
		}

		phy_page_free(phyPageAddr, 1);
		for (int i = 0; i < freeTableCount; i++)
			page_free(freeTables[i], PAGE_SIZE);

		page++;
	}

	// the page tables are now in a consistent state, so this call is safe
	phy_cleanup();

	memmgr_exit_routine();
}





#define is_va_mapped(addr)	((PML4T_VA_ENTRY((uintptr_t)addr >> 12) & 1) ? ((PML3T_VA_ENTRY((uintptr_t)addr >> 12) & 1) ? ((PML2T_VA_ENTRY((uintptr_t)addr >> 12) & 1) ? ((PML1T_VA_ENTRY((uintptr_t)addr >> 12) & 1) ? 1 : 0) : 0) : 0) : 0)

// Prints the current state of the paging structures.
//	level: the desired depth (1: only print PML4T entries, 4: print even PML1T entries)
void mmu_dump(int level) {
	LOGI("page map dump:");
	for (uint64_t i4 = 0; i4 < PAGE_TABLE_SIZE; i4++) {
		uint64_t pte4 = PML4T[i4];
		if (pte_is_completely_free(pte4)) continue;
		LOGI("PML4T[%x16], ref count: %d%s", (int)i4, (int)(pte4 >> 53) & 0x3FF, ((pte4 >> 52) & 1) ? " (full)" : "");
		if (level <= 1) continue;
		for (uint64_t i3 = 0; i3 < PAGE_TABLE_SIZE; i3++) {
			uint64_t *pte3 = &PML3T[(i4 << 9) + i3];
			if (!is_va_mapped(pte3)) {
				LOGE("  PML3T not mapped! address: 0x%x64", (uint64_t)pte3);
				break;
			}
			if (pte_is_completely_free(*pte3)) continue;
			LOGI("  PML3T[%x16], ref count: %d%s", (int)i3, (int)(*pte3 >> 53) & 0x3FF, ((*pte3 >> 52) & 1) ? " (full)" : "");
			if (level <= 2) continue;
			for (uint64_t i2 = 0; i2 < PAGE_TABLE_SIZE; i2++) {
				uint64_t *pte2 = &PML2T[(i4 << 18) + (i3 << 9) + i2];
				if (!is_va_mapped(pte2)) {
					LOGE("    PML2T not mapped! address: 0x%x64", (uint64_t)pte2);
					break;
				}
				if (pte_is_completely_free(*pte2)) continue;
				LOGI("    PML2T[%x16], ref count: %d%s", (int)i2, (int)(*pte2 >> 53) & 0x3FF, ((*pte2 >> 52) & 1) ? " (full)" : "");
				if (level <= 3) continue;
				LOGI__("           PML1T: ");
				for (uint64_t i1 = 0; i1 < PAGE_TABLE_SIZE; i1++) {
					//LOGI__("start at %x64 ", (uint64_t)PML1T);
					uint64_t *pte1 = &PML1T[(i4 << 27) + (i3 << 18) + (i2 << 9) + i1];
					if (!is_va_mapped(pte1)) {
						LOGE__("not mapped! address: 0x%x64", (uint64_t)pte1);
						break;
					}
					if (pte_is_completely_free(*pte1)) continue;
					LOGI__("%x16 (%d), ", (int)i1, (*pte1 >> 53) & 0x3FF);
				}
				LOGI__("\n");
			}
		}
	}
}