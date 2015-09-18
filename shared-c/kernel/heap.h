

#ifndef __HEAP_H__
#define __HEAP_H__


// malloc, realloc and free are declared in stdlib.h

// all of these functions were commeted out for some reason -> find out why
// these functions are related to lazy free calls to prevent recursiveness of memory manager functions
#ifdef USING_VIRTUAL_MEMORY
void memmgr_enter_routine();
void memmgr_stage_free(void *address);
void memmgr_stage_page_free(void *address, size_t size);
void memmgr_stage_phy_page_free(void *address, size_t count);
void memmgr_exit_routine();
#endif


#endif // __HEAP_H__
