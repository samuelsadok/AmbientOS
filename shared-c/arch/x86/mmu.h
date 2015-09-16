

#ifndef __MMU_H__
#define __MMU_H__

#define PAGE_ALIGN_BITS		(12UL)
#define PAGE_SIZE_MASK		(0xFFFUL)
#define PAGE_SIZE			(1UL << PAGE_ALIGN_BITS)

void page_map_realmode(void);
void page_unmap_realmode(void);
uintptr_t page_find(size_t count, int userspace);
int page_map_single(void *physicalAddress, uintptr_t virtualPage, int userspace, int writable, int executable);
void *page_map(void *physicalAddress, size_t length, int userspace, int writable, int executable);
void *page_alloc_ex(size_t length, int userspace, int writable, int executable);
void *page_alloc(size_t length);
void page_free(void *address, size_t size);
void mmu_dump(int level);


#endif // __MMU_H__
