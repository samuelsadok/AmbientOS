/*
* log.h
*
* Created: 24.07.2013
*  Author: cpuheater (innovation-labs@appinstall.ch)
*/


#ifndef __LOG_H__
#define __LOG_H__

#include <config.h>
#include <stddef.h>

// LOG_VERBOSITY:
// 0: no print functions
// 1: only errors
// 2: errors, warnings
// 3: errors, warnings, infos

#ifndef LOG_VERBOSITY
#	define LOG_VERBOSITY 0
#endif

#if LOG_VERBOSITY > 2
#define LOGI__(...)	do {__fprintf(__stdout, __VA_ARGS__);} while (0)
#define LOGI(...)	do {print_file_name(__stdout, __FILE__, sizeof(__FILE__)); __fprintf(__stdout, log_str_info); __fprintf(__stdout, __VA_ARGS__); __fprintf(__stdout, "\n");} while (0)				// logs an information
#else
#define LOGI__(...)
#define LOGI(...)
#endif
#if LOG_VERBOSITY > 1
#define LOGW(...)	do {print_file_name(__stdout, __FILE__, sizeof(__FILE__)); __fprintf(__stdout, log_str_warning); __fprintf(__stdout, __VA_ARGS__); __fprintf(__stdout, "\n");} while (0)				// logs a warning
#else
#define LOGW(...)
#endif
#if LOG_VERBOSITY > 0
#define LOGE__(...)	do {__fprintf(__stderr, __VA_ARGS__);} while (0)
#define LOGE(...)	do {print_file_name(__stderr, __FILE__, sizeof(__FILE__)); __fprintf(__stderr, log_str_error, __LINE__); __fprintf(__stderr, __VA_ARGS__); __fprintf(__stderr, "\n");} while (0)		// logs an error
#else
#define LOGE__(...)
#define LOGE(...)
#endif


typedef struct stream_t
{
	status_t(*readByte)(struct stream_t *, char *);
	status_t(*writeByte)(struct stream_t *, char);
} stream_t;


void print_file_name(stream_t *stream, const char *fileName, int fileNameLength);
void __fprintf(stream_t *stream, const char *__fmt, ...);


extern const char log_str_info[];
extern const char log_str_warning[];
extern const char log_str_error[];
extern stream_t *__stdout;
extern stream_t *__stderr;


// For each platform, this macro must be used in exactly one position. It
// configures the standard functions used for outputting log messages.
#define REGISTER_OUTPUT(__stdoutFunc, __stderrFunc)			\
stream_t __stdoutStruct = { .writeByte = (__stdoutFunc) };	\
stream_t __stderrStruct = { .writeByte = (__stderrFunc) };	\
stream_t *__stdout = &__stdoutStruct;						\
stream_t *__stderr = &__stderrStruct


#endif /* __LOG_H__ */
