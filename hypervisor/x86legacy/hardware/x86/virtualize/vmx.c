

#include <stdint.h>
#include <hardware\x86\io.h>

#define	IA32_FEATURE_CONTROL	0x3A


enum {
	VMX_SUCCESS = 0,
	VMX_NOT_SUPPORTED,
	VMX_NOT_FULLY_SUPPORTED
};

#define PAGE_SIZE 4096

__attribute__((align(PAGE_SIZE)))
char * vmxOnRegion[PAGE_SIZE];


typedef struct {
	__attribute__((align(PAGE_SIZE))) char vmcs[PAGE_SIZE];
	__attribute__((align(PAGE_SIZE))) char vmIoBitmap[PAGE_SIZE * 2];
	__attribute__((align(PAGE_SIZE))) char vmMsrBitmapLegacy[PAGE_SIZE];
	__attribute__((align(PAGE_SIZE))) char vmMsrBitmapLongmode[PAGE_SIZE];
	__attribute__((align(PAGE_SIZE))) char vmReadBitmap[PAGE_SIZE];
	__attribute__((align(PAGE_SIZE))) char vmWriteBitmap[PAGE_SIZE];
} vm_t;


// todo: maintain data in writeback cacheable memory

// Loads a VM control structure on the current processor.
// The VMCS will become active as well as current.
//	vmcs: A pointer to a VM control structure (4k memory page)
static inline void vmx_load_vmcs(void *vmcs) {
	__asm volatile ("vmptrld %0" : : "r" (vmcs) : "memory");
}

// Unloads a VM control structure from the current processor.
// The VMCS will become inactive and non-current.
// This should also be used to initialize a new VMCS.
//	vmcs: The VMCS to unload
static inline void vmx_clear_vmcs(void *vmcs) {
	__asm volatile ("vmclear %0" : : "r" (vmcs) : "memory");
}



// Inits a virtual machine.
void vmx_init_vm(vm_t *vm) {
	// todo: write revision field (offset 0) in vmcs according to IA32_VMX_BASIC
	
	memset(vm, 0xFF, sizeof(vm_t)); // set all bits in bitmaps to 1 (intercept all I/O, intercept all MSRs)

	// set bit 0 in vmx_vpid_bitmap??


	// todo: init mmu


}


// Disables interception of the specified I/O port
void vmx_io_intercept_disable(vm_t *vm, int port) { clear_bit(port, vm->vmIoBitmap); }
// Enables interception of the specified I/O port
void vmx_io_intercept_enable(vm_t *vm, int port) { set_bit(port, vm->vmIoBitmap); }


// Disables interception of the specified MSR register
void vmx_msr_intercept_disable(vm_t *vm, int msr) {
	clear_bit((msr & 0x1FFF) + (msr > 0x1FFF ? 0x2000 : 0), vm->vmMsrLegacy);
	clear_bit((msr & 0x1FFF) + (msr > 0x1FFF ? 0x2000 : 0), vm->vmMsrLongmode);
}
// Enables interception of the specified I/O port
void vmx_msr_intercept_enable(vm_t *vm, int msr) {
	set_bit((msr & 0x1FFF) + (msr > 0x1FFF ? 0x2000 : 0), vm->vmMsrLegacy);
	set_bit((msr & 0x1FFF) + (msr > 0x1FFF ? 0x2000 : 0), vm->vmMsrLongmode);
}



// Checks if VMX is supported by this processor, prepares the VMXON-region and issues the VMXON instruction
int vmx_init(void) {
	uint32_t eax, ebx, ecx, edx;
	cpuid(1, &eax, &ebx, &ecx, &edx);
	if (!((ecx >> 5) & 1)) return VMX_NOT_SUPPORTED;

	// todo: check if unrestricted guest is supported

	write_msr(IA32_FEATURE_CONTROL, read_msr(IA32_FEATURE_CONTROL) | 7); // enable VMX in/outside SMX operation, lock register


	// todo: init vmxOnRegion by setting the vmxRevisionIdentifier

	write_cr4(read_cr4() | (1 << 13));
	__asm volatile ("vmxon %0" : : "m" (vmxOnRegion) : "memory");


	return VMX_SUCCESS;
}




// Exits VMX mode
void vmx_exit(void) {
	__asm volatile ("vmxoff" : : : "memory");
	write_cr4(read_cr4() & ~(1 << 13));
}

