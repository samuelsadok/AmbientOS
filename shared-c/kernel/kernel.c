/*
* todo: rename to bugcheck
*
* created: 08.02.15
*
*/


#include <system.h>
#include "kernel.h"

#ifdef USING_STACKTRACE
#include <execinfo.h>
#define MAX_BACKTRACE_SIZE	20
#endif


#ifdef USING_NVM

#include <hardware/nvm.h>

typedef struct
__attribute__((__packed__))
{
	status_t reason;
	uintptr_t info;
	const char *file;
	int line;
} nvm_bug_log_t;

STATIC_ASSERT(NVM_BUG_LOG_LENGTH >= sizeof(nvm_bug_log_t) * WORDSIZE, "not enough space in NVM allocated for bugcheck data");
#endif

volatile int shuttingDown = 0;

volatile int didBugcheck = 0;

void __attribute__((__noreturn__)) bug_check_ex(status_t reason, uintptr_t info, const char *file, int line) {
	interrupts_off();
	if (!didBugcheck) {	// prevent recursive bugchecking
		didBugcheck = 1;


#ifdef USING_NVM
		nvm_bug_log_t nvmBugLog = {
			.reason = reason,
			.info = info,
			.file = file,	// the assumption that string is in the .text section and that the image does not change until reboot
			.line = line
		};
		nvm_write(NVM_BUG_LOG_OFFSET, (char *)&nvmBugLog, sizeof(nvmBugLog) * WORDSIZE);
#endif

		LOGE("FATAL ERROR");
		LOGE("  in file %s, line %d", file, line);
		LOGE("  status code: %d, info: %xp", (int)reason, info);
		LOGE("execution halted");

#ifdef USING_STACKTRACE
		void *callStack[MAX_BACKTRACE_SIZE];
		int callStackSize = backtrace(callStack, MAX_BACKTRACE_SIZE);
		LOGE__("call stack: bug check");
		for (int i = 0; i < callStackSize; i++)
			LOGE__(" <- %xp", (uintptr_t)callStack[i]);
#endif
	}

#ifdef AUTO_RESET
	__reset(reason);
#else
	for (;;)
		/*cpu_halt()*/;
#endif
}
