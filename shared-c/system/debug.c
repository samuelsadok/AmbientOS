

#include <system.h>
#include "debug.h"


#define report_feature(str)		LOGI__("%s, ", str)

void debug_report_features() {

	LOGI("supported CPU features: ");

	uint32_t eax, ebx, ecx, edx;
	cpuid(0, &eax, &ebx, &ecx, &edx);
	uint32_t maxFunc = eax;

	if (maxFunc < 1) return;
	cpuid(1, &eax, &ebx, &ecx, &edx);

	if (edx & (1 << 0)) report_feature("onboard x87 FPU");
	if (edx & (1 << 1)) report_feature("v8086 extensions");
	if (edx & (1 << 2)) report_feature("debugging extensions");
	if (edx & (1 << 3)) report_feature("page size extensions");
	if (edx & (1 << 4)) report_feature("time stamp counter");
	if (edx & (1 << 5)) report_feature("model specific registers");
	if (edx & (1 << 6)) report_feature("physical address extension");
	if (edx & (1 << 7)) report_feature("machine check exception");
	if (edx & (1 << 8)) report_feature("CMPXCHG8 instruction");
	if (edx & (1 << 9)) report_feature("onboard APIC");
	if (edx & (1 << 11)) report_feature("SYSENTER, SYSEXIT instructions");
	if (edx & (1 << 12)) report_feature("memory type range registers");
	if (edx & (1 << 13)) report_feature("PGE bit in CR4");
	if (edx & (1 << 14)) report_feature("machine check architecture");
	if (edx & (1 << 15)) report_feature("conditional move and FCMOV instructions");
	if (edx & (1 << 16)) report_feature("page attribute table");
	if (edx & (1 << 17)) report_feature("36-bit page size extension");
	if (edx & (1 << 18)) report_feature("processor serial number");
	if (edx & (1 << 19)) report_feature("CLFLUSH instruction (SSE2)");
	if (edx & (1 << 21)) report_feature("32-bit debug store (save jump traces)");
	if (edx & (1 << 22)) report_feature("onboard thermal control MSRs for ACPI");
	if (edx & (1 << 23)) report_feature("MMX instructions");
	if (edx & (1 << 24)) report_feature("FXSAVE, FXRESTOR, CR4 bit 9");
	if (edx & (1 << 25)) report_feature("SSE support");
	if (edx & (1 << 26)) report_feature("SSE2 support");
	if (edx & (1 << 27)) report_feature("CPU cache supports self-snoopi");
	if (edx & (1 << 28)) report_feature("hyper threading");
	if (edx & (1 << 29)) report_feature("thermal monitor automatically limits temperature");
	if (edx & (1 << 30)) report_feature("IA64 emulating x86");
	if (edx & (1 << 31)) report_feature("PBE pin support");

	if (ecx & (1 << 0)) report_feature("SSE3 support");
	if (ecx & (1 << 1)) report_feature("PCLMULQDQ instruction");
	if (ecx & (1 << 2)) report_feature("64-bit debug store");
	if (ecx & (1 << 3)) report_feature("MONITOR and MWAIT instructions (SSE3)");
	if (ecx & (1 << 4)) report_feature("CPL qualified debug store");
	if (ecx & (1 << 5)) report_feature("VMX support");
	if (ecx & (1 << 6)) report_feature("TXT extensions");
	if (ecx & (1 << 7)) report_feature("enhanced SpeedStep");
	if (ecx & (1 << 8)) report_feature("thermal monitor 2");
	if (ecx & (1 << 9)) report_feature("supplemental SSE3 instructions");
	if (ecx & (1 << 10)) report_feature("L1 context ID");
	if (ecx & (1 << 12)) report_feature("FMA3 instructions");
	if (ecx & (1 << 13)) report_feature("CMPXCHG16B instruction");
	if (ecx & (1 << 14)) report_feature("can disable sending task priority messages");
	if (ecx & (1 << 15)) report_feature("perfmon & debug capabilities");
	if (ecx & (1 << 17)) report_feature("PCID support (CR4 bit 17)");
	if (ecx & (1 << 18)) report_feature("direct cache access for DMA writes");
	if (ecx & (1 << 19)) report_feature("SSE4.1 support");
	if (ecx & (1 << 20)) report_feature("SSE4.2 support");
	if (ecx & (1 << 21)) report_feature("x2APIC support");
	if (ecx & (1 << 22)) report_feature("MOVBE instruction");
	if (ecx & (1 << 23)) report_feature("POPCNT instruction");
	if (ecx & (1 << 24)) report_feature("APIC supports TSC deadline value");
	if (ecx & (1 << 25)) report_feature("AES instruction set");
	if (ecx & (1 << 26)) report_feature("XSAVE, XRESTOR, XSETBV, XGETBV instructions");
	if (ecx & (1 << 27)) report_feature("XSAVE enabled");
	if (ecx & (1 << 28)) report_feature("advanced vector extensions");
	if (ecx & (1 << 29)) report_feature("half precision floating point support");
	if (ecx & (1 << 30)) report_feature("RDRAND instruction");
	if (ecx & (1 << 31)) report_feature("WE ARE VIRUALIZED!!!");

	LOGI__("\n");
}