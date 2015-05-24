/*
*
*
* created: 01.04.15
*
*/

#include <system.h>
#include <hardware/nvm.h>
#include <stdio.h>

CRITICAL_SECTION universalLock;

void __attribute__((__noreturn__)) __reset(int code) {
	//printf("\n");
	fflush(stdout);
	Sleep(100);
	ExitProcess(code);
}


// Reads until the end of the next argument.
// Returns the position of the end of the args string or the next space.
//	start: the search start position
void next_arg(const char *args, size_t *start, size_t *length) {
	*length = 0;

	// consume leading whitespace
	while (args[*start] == ' ')
		(*start)++;

	bool quote = 0;
	while (args[*start + *length] && (args[*start + *length] != ' '))
		if (args[*start + (*length)++] == '\"')
			quote = !quote;
}

int next_arg_as_int(const char **args) {
	size_t start = 0, length;
	next_arg(*args, &start, &length);
	*args += start;
	char str[length + 1];
	memcpy(str, *args, length);
	*args += length;
	str[length] = 0;
	//LOGE("have arg '%s', length %d", str, length);
	return atoi(str);
}


// todo: invoke on startup
void windows_init(void) {
	// init atomic blocks
	InitializeCriticalSection(&universalLock);
	
	// set empty buffer for stdout
	setvbuf(stdout, NULL, _IONBF, 0);

	// init virtual NVM
#ifdef USING_NVM
	nvm_init();
#endif


	LPCSTR args = GetCommandLine();

	// skip executable name
	size_t start = 0, length;
	next_arg(args, &start, &length);
	args += start + length;

	if (!*args) {
		LOGE("not enough args!");
		__reset(-1);
	}

	uintptr_t hOut = next_arg_as_int(&args);

	if (!*args) {
		LOGE("not enough args!");
		__reset(-1);
	}

	uintptr_t hIn = next_arg_as_int(&args);
	LOGE("have int %d and %d", hOut, hIn);

	sim_init((void *)hOut, (void *)hIn);

	/*char data[] = "hello\n";
	DWORD bla;
	DWORD ret = WriteFile((void *)hOut, data, sizeof(data), &bla, NULL);
	DWORD err = GetLastError();
	LOGE("ret: %d err : %d", ret, err);*/






#if defined(USING_NVM) && !defined(USING_BOOTLOADER)
	if (!nvmValid)
		nvm_data_init();
#endif

}

/*
void WinMain(void) {
	printf("hi\n");
}
*/

