/*
*
*
* created: 01.04.15
*
*/
#ifndef __SIM_ENDPOINT_H__
#define __SIM_ENDPOINT_H__


typedef struct
{
	const char *name;
	size_t nameLength;
} sim_object_t;

#define CREATE_SIM_OBJ(_name) {	\
	.name = _name,				\
	.nameLength = sizeof(_name) - 1	\
}


void sim_init(HANDLE pipeOut, HANDLE pipeIn);

void sim_write_d1(sim_object_t *obj, const char *field, size_t fieldLength, double val1);
void sim_read_d1(sim_object_t *obj, const char *field, size_t fieldLength, double *val1);
void sim_write_d2(sim_object_t *obj, const char *field, size_t fieldLength, double val1, double val2);
void sim_read_d2(sim_object_t *obj, const char *field, size_t fieldLength, double *val1, double *val2);
void sim_write_d3(sim_object_t *obj, const char *field, size_t fieldLength, double val1, double val2, double val3);
void sim_read_d3(sim_object_t *obj, const char *field, size_t fieldLength, double *val1, double *val2, double *val3);
void sim_write_d4(sim_object_t *obj, const char *field, size_t fieldLength, double val1, double val2, double val3, double val4);
void sim_read_d4(sim_object_t *obj, const char *field, size_t fieldLength, double *val1, double *val2, double *val3, double *val4);



#endif // __SIM_ENDPOINT_H__
