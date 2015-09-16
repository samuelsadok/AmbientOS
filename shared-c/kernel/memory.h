

#ifndef __MEMORY_H__
#define __MEMORY_H__

void phy_dump(void);
void phy_test(void);
void phy_page_init(void);
void *phy_page_alloc(size_t count);
void phy_page_free(void *address, size_t count);
void phy_cleanup(void);

#endif // __MEMORY_H__
