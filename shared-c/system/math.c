/*
 * math.c
 *
 * Created: 27.04.2014
 *  Author: cpuheater
 */ 


#include <system.h>

#ifdef USING_MATH_EXTENSIONS

// Updates the state of a PID controller
float math_pid_controller(math_pid_t *pid, float value, float differential) {
	pid->sum = constrain(pid->sum + value, pid->maxI, -pid->maxI);
	return pid->P * value + pid->I * pid->sum + pid->D * differential;
}

#endif
