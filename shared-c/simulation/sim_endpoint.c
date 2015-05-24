/*
*
* Provides access to endpoints that represent fields of simulated objects.
* The connection is provided by a pipe between the local application and the simulator.
*
* created: 01.04.15
*
*/

#include <system.h>
#include <stdio.h>
#include "sim_endpoint.h"


void ftoa(double value, char *str) {
	sprintf(str, "%e", value);
}


HANDLE sim_pipe_out, sim_pipe_in;

// Inits the simulation using a pipe that corresponds to the simulated device.
void sim_init(HANDLE pipeOut, HANDLE pipeIn) {
	sim_pipe_out = pipeOut;
	sim_pipe_in = pipeIn;
}


void sim_write_bytes(const char *data, size_t length) {
	//LOGI("writing %d bytes", length);

	DWORD dwNumberOfBytesWritten;
	if (!WriteFile(sim_pipe_out, data, length, &dwNumberOfBytesWritten, NULL))
		bug_check(STATUS_FILE_WRITE_ERROR, GetLastError());
	if (dwNumberOfBytesWritten != length)
		bug_check(STATUS_FILE_WRITE_ERROR, 0);

	//LOGI("write succeeded");
}


// Writes data to a field.
void sim_write_val(sim_object_t *obj, const char *field, size_t fieldLength, const char *val, size_t length) {
	//LOGI("waiting for lock in %d", GetCurrentThreadId());
	atomic() {
		//LOGI("got lock %d", GetCurrentThreadId());
		sim_write_bytes("set:", 4);
		sim_write_bytes(obj->name, obj->nameLength);
		sim_write_bytes(".", 1);
		sim_write_bytes(field, fieldLength);
		sim_write_bytes("\n", 1);
		sim_write_bytes(val, length);
		sim_write_bytes("\n", 1);
	}
	//LOGI("lock released");
}

// Reads data from a field.
// If the buffer is too small, the application will bug-check.
void sim_read_val(sim_object_t *obj, const char *field, size_t fieldLength, char *val, size_t *length) {
	atomic() {
		sim_write_bytes("get:", 4);
		sim_write_bytes(obj->name, obj->nameLength);
		sim_write_bytes(".", 1);
		sim_write_bytes(field, fieldLength);
		sim_write_bytes("\n", 1);

		bool lineTerminated = 0;
		do {
			DWORD dwNumberOfBytesRead;
			size_t offset = 0;

			if (!(*length - offset))
				bug_check(STATUS_OUT_OF_MEMORY, *length);

			if (!ReadFile(sim_pipe_in, val, *length - offset, &dwNumberOfBytesRead, NULL))
				bug_check(STATUS_FILE_WRITE_ERROR, GetLastError());

			while (dwNumberOfBytesRead--) {
				if (*(val++) == '\n') {
					*length = offset;
					lineTerminated = 1;
					break;
				}
				offset++;
			}
		} while (!lineTerminated);
	}
}




// Writes to a field that consists of 1 double value
void sim_write_d1(sim_object_t *obj, const char *field, size_t fieldLength, double val1) {
	char val[50];
	size_t offset = 0;

	// append val1
	ftoa(val1, val + offset);
	while (val[offset++]);
	offset--;

	sim_write_val(obj, field, fieldLength, val, offset);
}

// Reads a field that consists of 3 double values
void sim_read_d3(sim_object_t *obj, const char *field, size_t fieldLength, double *val1, double *val2, double *val3) {
	char val[75];
	size_t offset = 0, length = sizeof(val);

	sim_read_val(obj, field, fieldLength, val, &length);

	// parse val1
	*val1 = atof(val + offset);
	while (offset < length)
		if (val[offset++] == ' ')
			break;

	// parse val2
	*val2 = atof(val + offset);
	while (offset < length)
		if (val[offset++] == ' ')
			break;

	// parse val3
	*val3 = atof(val + offset);
	while (offset < length)
		if (val[offset++] == ' ')
			break;
}

// Reads a field that consists of 4 double values
void sim_read_d4(sim_object_t *obj, const char *field, size_t fieldLength, double *val1, double *val2, double *val3, double *val4) {
	char val[100];
	size_t offset = 0, length = sizeof(val);

	sim_read_val(obj, field, fieldLength, val, &length);

	// parse val1
	*val1 = atof(val + offset);
	while (offset < length)
		if (val[offset++] == ' ')
			break;

	// parse val2
	*val2 = atof(val + offset);
	while (offset < length)
		if (val[offset++] == ' ')
			break;

	// parse val3
	*val3 = atof(val + offset);
	while (offset < length)
		if (val[offset++] == ' ')
			break;

	// parse val4
	*val4 = atof(val + offset);
	while (offset < length)
		if (val[offset++] == ' ')
			break;

	//printf("read 4 values: %f, %f, %f, %f", *val1, *val2, *val3, *val4);
}

// Writes to a field that consists of 4 double values
void sim_write_d4(sim_object_t *obj, const char *field, size_t fieldLength, double val1, double val2, double val3, double val4) {
	char val[50];
	size_t offset = 0;

	// append val1
	ftoa(val1, val + offset);
	while (val[offset++]);
	val[offset - 1] = ' ';

	// append val2
	ftoa(val2, val + offset);
	while (val[offset++]);
	val[offset - 1] = ' ';

	// append val3
	ftoa(val3, val + offset);
	while (val[offset++]);
	val[offset - 1] = ' ';

	// append val4
	ftoa(val4, val + offset);
	while (val[offset++]);
	offset--;

	sim_write_val(obj, field, fieldLength, val, offset);
}

