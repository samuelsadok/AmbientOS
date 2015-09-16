/*
 * math_asm.S
 *
 * Created: 25.07.2013 21:27:04
 *  Author: cpuheater
 */ 


// this file contains code for converting a quaternion into yaw-pitch-roll angles (in this order)
// the mathematical operations given the quaternion 1 = w + xi + yj + zk are:
//		roll =	asin(2*y*z + 2*x*w)
//		pitch =	atan2(2*y*w - 2*z*x, 1 - 2*y*y - 2*x*x)
//		yaw =	atan2(2*z*w - 2*y*x, 1 - 2*z*z - 2*x*x)
//
// for a more efficient approach, we do instead:
//		roll =	asin_lookup(y*z + x*w)
//		pitch =	atan2_lookup(y*w - z*x, 0.5 - y*y - x*x)
//		yaw =	atan2_lookup(z*w - y*x, 0.5 - z*z - x*x)
// edit: we now calculate pitch more efficiently:
//		pitch =	asin_lookup(y*w - z*x), device is overhead when (0.5 - y*y - x*x) negative
//		abslute yaw data is not needed but could be calculated: yaw = asin_lookup(z*w - y*x) / (1 - roll/90°)
//
// for calculating the parameters used in the lookup functions, we have two multiplier functions:
//		x-multiplication:	temp = temp - x * var (temp is the intermediate result, var can be set to w, x, y or z)
//		y-multiplication:	temp = temp - y * var (temp is the intermediate result, var can be set to w, x, y or z)
// 
// We can load var with wisely chosen quaternion components and negate it when needed to end up with different multiplications.
// For the two last multiplications (z*w and z*z), we have to replace x by z, so that we get a z-multiplication function.


// the actual procedure is:
// 1. roll
//		w <= -w
//		res <= 0
//		var <= -z -> y-mult (res -= y*-z)
//		var <= w -> x-mult (res -= x*-w)
//
// 2a. pitch, 1st term
//		res <= 0
//		(nop) -> y-mult (res -= y*-w)
//		var <= z -> x-mult (res -= x*z)
// 2b. pitch, 2nd term
//		res <= 0.5 (represented by 0x0800)
//		var <= y -> y-mult (res -= y*y)
//		var <= x -> x-mult (res -= x*x) -> store x*x
// EDIT: only the sign of the 2b-result is needed, hence we can reduce it to two 8x8bit multiplications
//
// 3a. yaw, 1st term
//		res <= 0
//		(nop) -> y-mult (res -= y*x)
//		x <= z
//		var <= w -> x-mult (res -= z*-w)
//
// 3b. yaw, 2nd term
//		res <= 0.5 (represented by 0x0800)
//		var <= z -> x-mult (res -= z*z)
//		res -= cached x*x
// EDIT: we don't need absolute heading, hence we can prune 3a and 3b calculations

//#include <device.h>
#include <avr/io.h>

#ifdef USING_MATH_EXTENSIONS


//.extern MPU_yaw			; signed 16 bit
//.extern MPU_pitch		; signed 16 bit
//.extern MPU_roll		; signed 16 bit
//.extern MPU_overhead	; signed 8 bit
//.extern MPU_samples		; unsigned 8 bit

.global math_quaternion_to_ypr
//.global math_quaternion_data_to_ypr
.global math_kalman_filter

#ifdef MATH_ASM_PID
.global math_pid_controller
#endif





// r1:r0		8x8 mult result
// r2			0						(-> store!)
// r17:r16		second multiplicand		(-> store!)
// r19:r18		z
// r21:r20		y
// r23:r22		x
// r25:r24		w
// r27:r26 (X)	intermediate result
// r29:r28 (Y)	pointer to result data	(-> store!)
// r31:r30 (Z)	16x16 mult result


//math_quaternion_data_to_ypr: // a more efficient way to call math_quaternion_to_ypr: takes the MPU data packet directly as argument
//	// CPU cycles (incl RCALL+RET): 265-273 (~34µs @ 8MHz)
//
//	movw r30, r24	; load parameters for math_quaternion_to_ypr - 25 CLK
//	ldd r25, Z+24	; load and store delta yaw data
//	ldd r24, Z+25
//	sts MPU_yaw, r24
//	sts MPU_yaw+1, r25
//	ld r25, Z	; load w
//	ldd r24, Z+1
//	ldd r23, Z+4	; load x
//	ldd r22, Z+5
//	ldd r21, Z+8	; load y
//	ldd r20, Z+9
//	ldd r19, Z+12	; load z
//	ldd r18, Z+13


math_quaternion_to_ypr: // see math.h for parameter description
	// CPU cycles (incl RCALL+RET): 240-248 (wrong)

	// prepare registers - 16 CLK
	in r0, _SFR_IO_ADDR(SREG)
	push r0
	push r2
	push r16
	push r17
	push r28
	push r29
	clr r2
	com r24 ; negate w (we use 1's instead of 2's complement to save 2 cycles)
	com r25
	movw r28, r16
	
	// temp test - ? CLK
	st Y, r24
	std Y+1, r25

	// roll - 99/103 CLK
	clr r26 ; load result with 0
	clr r27
	movw r16, r18 ; load var with z
	com r16 ; negate z
	com r17
	rcall Y_MULT
	movw r16, r24 ; load var with w
	rcall X_MULT
	rcall asin_lookup
	std Y+4, r26
	std Y+5, r27

	// pitch (1st part) - 96/100 CLK
	clr r26 ; load result with 0
	clr r27
	rcall Y_MULT ; var still contains w
	movw r16, r18 ; load var with z
	rcall X_MULT
	rcall asin_lookup
	std Y+2, r26
	std Y+3, r27

	// pitch (2nd part) - 12 CLK
	ldi r26, 0x00 ; load result with 0.5
	ldi r27, 0x08
	//movw r16, r20 ; load var with y
	//rcall Y_MULT
	//movw r16, r22 ; load var with x
	//rcall X_MULT
	//push 31 ; store x*x
	//push 30
	muls r21, r21
	sub r26, r0
	sbc r27, r1
	muls r23, r23
	sub r26, r0
	sbc r27, r1
	std Y+6, r27


	#ifdef MATH_CALC_YAW
	#error "not implemented"
	//// yaw (1st part) - 48+4 CLK
	//clr r26 ; load result with 0
	//clr r27
	//rcall Y_MULT ; var still contains x -> not if pitch 2nd part isn't executed
	//movw r22, r18 ; replace x with z
	//movw r16, r24 ; load var with w
	//rcall X_MULT
	//sts test_y1, r26
	//sts test_y1+1, r27
	//
	//// yaw (2nd part) - 30+4 CLK
	//ldi r26, 0x00 ; load result with 0.5
	//ldi r27, 0x08
	//movw r16, r18 ; load var with z
	//rcall X_MULT
	//pop r30 ; retrieve x*x
	//pop r31
	//sub r26, r30 ; subtract x*x from result
	//sbc r27, r31
	//sts test_y2, r26
	//sts test_y2+1, r27
	#endif

	// lds r24, MPU_samples ; increment sample counter - 6 CLK
	// inc r24
	// breq inc_MPU_samples_done ; don't update if there was an overflow
	// sts MPU_samples, r24
	// inc_MPU_samples_done:
	
	; restore registers - 14 CLK
	pop r29
	pop r28
	pop r17
	pop r16
	pop r2
	clr r1
	pop r0
	out _SFR_IO_ADDR(SREG), r0
ret




X_MULT:	// multiplies x (r23:r22) by var (r17:r16) and subtracts the result's two most significant bytes from the intermediate result (r27:r26)
	// CPU cycles (incl RCALL+RET): 22
	// note that this multiplication is an approximation only

	// 16x16=>32(16)bit multiplication
	muls r23, r17; (signed)ah * (signed)bh
	movw r30, r0
	mulsu r23, r16	; (signed)ah * bl
	sbc r31, r2
	add r30, r1
	adc r31, r2
	mulsu r17, r22	; (signed)bh * al
	sbc r31, r2
	add r30, r1
	adc r31, r2

	// subtract multiplication result from intermediate result
	sub r26, r30
	sbc r27, r31
	
ret


Y_MULT:	// multiplies y (r21:r20) by var (r17:r16) and subtracts the result's two most significant bytes from the intermediate result (r27:r26)
	// CPU cycles (incl RCALL+RET): 22
	// note that this multiplication is an approximation only

	// 16x16=>32(16)bit multiplication
	muls r21, r17; (signed)ah * (signed)bh
	movw r30, r0
	mulsu r21, r16	; (signed)ah * bl
	sbc r31, r2
	add r30, r1
	adc r31, r2
	mulsu r17, r20	; (signed)bh * al
	sbc r31, r2
	add r30, r1
	adc r31, r2

	// subtract multiplication result from intermediate result
	sub r26, r30
	sbc r27, r31
	
ret







asin_lookup: // returns the asin of r27:r26
	// CPU cycles (incl RCALL+RET): 49 (for neg inputs) / 45 (for pos inputs)
	// input: 0xF800: -1, 0x0000: 0, 0x0800: 1
	// output: 0x8000: -180°, 0x0000: 0°, 0x7FFF: 180° (the actual range is only -90°...+90°)
	
	// r1:r0		temp delta correction data
	// r2			remembers if the input was negative (must be 0 on entry)
	// r27:r26		input, table offset, output
	// r31:r30		pointer for array lookup


	sbrs r27, 7	; make input positive (and remember) - 3/5CLK
	rjmp asin_make_positive_done
	com r26
	com r27
	com r2
	asin_make_positive_done:
	
	cpi r27, 0x08 ; check input bounds - exit with max output if input too large - 3CLK (6CLK if exiting)
	brlo asin_check_bounds
	ldi r26, 0xFF ; exit with +-90°
	ldi r27, 0x3F
	rjmp asin_almost_done
	asin_check_bounds:

	mov r31, r26 ; load r27:r26 with table offset and r0 with the delta correction multiplier - 7CLK
	andi r31, 0x0F
	mov r0, r31
	andi r26, 0xF0
	or r26, r27
	swap r26
	clr r27

	ldi r30, lo8(asin_lookup_table_delta)	; load r1 with delta correction value from table - 7CLK
	ldi r31, hi8(asin_lookup_table_delta)
	add r30, r26
	adc r31, r27
	lpm r1, Z

	; only 7 bits are required as table index
	lsl r26 ; load r27:r26 with approximate asin value - 11CLK
	ldi r30, lo8(asin_lookup_table)
	ldi r31, hi8(asin_lookup_table)
	add r30, r26
	adc r31, r27
	lpm r26, Z+
	lpm r27, Z

	mul r0, r1	; calculate and apply delta correction - 4CLK
	add r26, r0
	adc r27, r1

	asin_almost_done:
	sbrs r2, 0 ; make output negative if input was negative - 3/5CLK
	rjmp asin_make_negative_done
	com r26
	com r27
	com r2
	asin_make_negative_done:
	
ret








// r1:r0	8x8 multiplication result
// r2		zero register
// r16		temp register
// r23:r22	input value
// r27:r24	32-bit scratchpad
// r31:r30	Kalman structure pointer

math_kalman_filter:
	
	in r0, _SFR_IO_ADDR(SREG)	; store and set up registers
	push r0
	push r2
	push r16
	clr r2
	movw r30, r24 ; structure pointer
	
	// r27:r24 = currentValue - lastPrediction (limited to 16-bit)
	movw r24, r22
	ldd r18, Z+2 ; Kalman.lastPrediction
	ldd r19, Z+3 ; Kalman.lastPrediction
	sub r24, r18
	sbc r25, r19
	brvc Kalman_delta_bounds_OK ; check if an overflow occured
	mov r0, r25 ; set max pos/neg val (ignore low byte)
	ldi r25, 0x7F
	sbrs r0, 7
	ldi r25, 0x80
	Kalman_delta_bounds_OK:
	clr r26 ; sign extend to 24-bit
	sbrc r25, 7
	com r26
	
	// r27:r24 *= reactiveness + 1 (implicitly limited to 24-bit)
	movw r18, r24
	ld r16, Z ; Kalman.reactiveness
	rcall ADD_WITH_COEF

	// lastSlope/r26:r24 = lastSlope + r26:r24 (limited to 24-bit)
	ldd r16, Z+4 ; Kalman.lastSlope
	ldd r17, Z+5 ; Kalman.lastSlope
	ldd r18, Z+6 ; Kalman.lastSlope
	add r24, r16
	adc r25, r17
	adc r26, r18
	brvc Kalman_addition_A_OK
	mov r0, r26 ; set max pos/neg val on overflow (ignore low byte)
	ldi r26, 0x7F
	sbrs r0, 7
	ldi r26, 0x80
	Kalman_addition_A_OK:
	std Z+4, r24 ; Kalman.lastSlope
	std Z+5, r25 ; Kalman.lastSlope
	std Z+6, r26 ; Kalman.lastSlope
	mov r18, r25
	mov r19, r26

	// lastPrediction = currentValue + lastSlope (limited to 16-bit)
	add r25, r22
	adc r26, r23
	brvc Kalman_addition_B_OK
	mov r0, r26 ; set max pos/neg val on overflow (ignore low byte)
	ldi r26, 0x7F
	sbrs r0, 7
	ldi r26, 0x80
	Kalman_addition_B_OK:
	std Z+2, r25 ; Kalman.lastPrediction
	std Z+3, r26 ; Kalman.lastPrediction

	// result = currentValue + predictionTime * lastSlope (implicitly limited to 24-bit)
	movw r24, r22
	clr r26
	ldd r16, Z+1 ; Kalman.predictionTime
	rcall ADD_WITH_COEF

	// // limit result to 16-bit
	// ; if it's a negative result, make positive and remember
	// mov r1, r26
	// sbrs r26, 7
	// rjmp Kalman_finish_negate_A_done
	// com r24
	// com r25
	// com r26
	// Kalman_finish_negate_A_done:
	// ; make sure that the result is smaller than 0x008000
	// cpi r25, 0x80
	// cpc r26, r2
	// brlo Kalman_finish_bounds_OK
	// ldi r24, 0xFF
	// ldi r25, 0x7F
	// Kalman_finish_bounds_OK:
	// ; if it was a negative result, make negative again
	// sbrs r1, 7
	// rjmp Kalman_finish_negate_B_done
	// com r24
	// com r25
	// Kalman_finish_negate_B_done:


	pop r16 ; restore registers
	pop r2
	clr r1
	pop r0
	out _SFR_IO_ADDR(SREG), r0

ret






// r1:r0	8x8 multiplication result
// r2		zero register
// r16		temp register (loaded with PID parameters)
// r21:r18	32-bit scratchpad
// r23:r22	input error
// r27:r24	intermediate 32-bit result
// r31:r30	PID structure pointer


math_pid_controller:
	
	in r0, _SFR_IO_ADDR(SREG)	; store and set up registers
	push r0
	push r2
	push r16
	clr r2
	movw r30, r24 ; structure pointer
	clr r24 ; set result to zero
	clr r25
	clr r26
	clr r27

	/***** PROPORTIONAL COMPONENT *****/
	
	movw r18, r22	; add proportional component
	ld r16, Z ; PID.Kp
	rcall ADD_WITH_COEF

	/***** INTEGRAL COMPONENT *****/
	
	; add current error to integral
	ldd r18, Z+4 ; PID.errSum
	ldd r19, Z+5 ; PID.errSum
	ldd r20, Z+6 ; PID.errSum
	ldd r21, Z+7 ; PID.errSum
	sbrc r23, 7 ; sign-extend error?
	com r2
	add r18, r22
	adc r19, r23
	adc r20, r2
	adc r21, r2
	clr r2

	; limit integral amplitude
	ldd r16, Z+3 ; PID.I_limit
	mov r1, r21
	sbrs r21, 7	; if this is a negative number, make positive and remember
	rjmp PID_sum_negate_A_done
	com r18
	com r19
	com r20
	com r21
	PID_sum_negate_A_done:
	cp r16, r20 ; compare custom limit to actual sum
	cpc r2, r21
	brsh PID_sum_bounds_OK
	ser r18 ; out of bounds - set to max val
	ser r19
	mov r20, r16
	clr r21
	PID_sum_bounds_OK:
	sbrs r1, 7 ; if it was a negative number, make negative again
	rjmp PID_sum_negate_B_done
	com r18
	com r19
	com r20
	com r21
	PID_sum_negate_B_done:

	; store new integral
	std Z+4, r18 ; PID.errSum
	std Z+5, r19 ; PID.errSum
	std Z+6, r20 ; PID.errSum
	std Z+7, r21 ; PID.errSum

	; right shift by four and add to result
	mov r21, r19
	andi r21, 0x0F
	andi r20, 0x0F
	andi r19, 0xF0
	andi r18, 0xF0
	or r18, r21
	or r19, r20
	swap r18
	swap r19
	ldd r16, Z+1 ; PID.Ki
	rcall ADD_WITH_COEF
	

	/***** DERIVATIVE COMPONENT *****/
	
	; load previous error and store current error
	ldd r20, Z+8 ; PID.errPrev
	ldd r21, Z+9 ; PID.errPrev
	std Z+8, r22 ; PID.errPrev
	std Z+9, r23 ; PID.errPrev

	; subtract error from previous error and prevent overflows
	sub r22, r20
	sbc r23, r21
	brvc PID_dev_checked_overflow
	andi r23, 1 ; toggle the sign since we had an overflow and make sure the high byte isn't "empty" (0x00 or 0xFF)
	com r23
	andi r23, 2
	PID_dev_checked_overflow:

	; limit the value between -128 (0x0080) and 127 (0x007F) and left-shift it by 8 bits
	mov r1, r23
	sbrs r23, 7 ; if this is a negative number, make positive and remember
	rjmp PID_dev_negate_A_done
	com r22
	com r23
	PID_dev_negate_A_done:
	cpi r22, 0x80
	cpc r23, r2
	brsh PID_dev_set_max_val
	mov r19, r22 ; the low byte becomes the high byte
	rjmp PID_dev_bounds_OK
	PID_dev_set_max_val:
	ldi r19, 0x7F
	PID_dev_bounds_OK:
	clr r18
	sbrs r1, 7 ; if it was a negative number, make negative again
	rjmp PID_dev_negate_B_done
	com r18
	com r19
	PID_dev_negate_B_done:

	; add to result
	ldd r16, Z+2 ; PID.Kd
	rcall ADD_WITH_COEF
	

	/***** RETURN RESULT *****/
	
	; if it's a negative result, make positive and remember
	mov r1, r27
	sbrs r27, 7
	rjmp PID_finish_negate_A_done
	com r24
	com r25
	com r26
	com r27
	PID_finish_negate_A_done:
	
	; make sure that the result is smaller than 0x00080000 (since we'll be right-shifting it by 4)
	cpi r26, 0x08
	cpc r27, r2
	brlo PID_finish_bounds_OK
	ldi r24, 0xFF
	ldi r25, 0xFF
	ldi r26, 0x07
	PID_finish_bounds_OK:

	; if it was a negative result, make negative again
	sbrs r1, 7
	rjmp PID_finish_negate_B_done
	com r24
	com r25
	com r26
	com r27
	PID_finish_negate_B_done:

	; right-shift result by 4 bits
	mov r27, r25
	andi r27, 0x0F
	andi r26, 0x0F
	andi r25, 0xF0
	andi r24, 0xF0
	or r24, r27
	or r25, r26
	swap r24
	swap r25


	pop r16 ; restore registers
	pop r2
	clr r1
	pop r0
	out _SFR_IO_ADDR(SREG), r0
ret


ADD_WITH_COEF:	// multiplies a signed 16-bit value (r19:r18) by an unsigned 8-bit value (r16) and adds the 24-bit result to a signed 32-bit intermediate result (r27:r24)
	// r2 must be zero
	// CPU cycles (incl RCALL+RET): 19

	mulsu r19, r16 ; (signed)ah * b
	sbc r27, r2
	add r25, r0
	adc r26, r1
	adc r27, r2
	mul r18, r16	; al * b
	add r24, r0
	adc r25, r1
	adc r26, r2
	adc r27, r2
	
ret




.section .progmem

asin_lookup_table: // 128 asin values for the asin function
.word	0x0000, 0x0051, 0x00A3, 0x00F4, 0x0146, 0x0198, 0x01E9, 0x023B, 0x028C, 0x02DE, 0x0330, 0x0381, 0x03D3, 0x0425, 0x0477, 0x04C9
.word	0x051B, 0x056D, 0x05C0, 0x0612, 0x0664, 0x06B7, 0x070A, 0x075C, 0x07AF, 0x0802, 0x0855, 0x08A9, 0x08FC, 0x0950, 0x09A3, 0x09F7
.word	0x0A4B, 0x0AA0, 0x0AF4, 0x0B49, 0x0B9E, 0x0BF3, 0x0C48, 0x0C9D, 0x0CF3, 0x0D49, 0x0D9F, 0x0DF5, 0x0E4C, 0x0EA3, 0x0EFA, 0x0F52
.word	0x0FA9, 0x1001, 0x105A, 0x10B2, 0x110B, 0x1165, 0x11BE, 0x1218, 0x1273, 0x12CE, 0x1329, 0x1385, 0x13E1, 0x143D, 0x149A, 0x14F7
.word	0x1555, 0x15B4, 0x1612, 0x1672, 0x16D2, 0x1732, 0x1793, 0x17F5, 0x1857, 0x18BA, 0x191D, 0x1982, 0x19E7, 0x1A4C, 0x1AB3, 0x1B1A
.word	0x1B82, 0x1BEA, 0x1C54, 0x1CBF, 0x1D2A, 0x1D97, 0x1E04, 0x1E73, 0x1EE2, 0x1F53, 0x1FC5, 0x2038, 0x20AD, 0x2123, 0x219A, 0x2213
.word	0x228D, 0x2309, 0x2387, 0x2407, 0x2488, 0x250C, 0x2592, 0x261A, 0x26A4, 0x2731, 0x27C1, 0x2854, 0x28EA, 0x2984, 0x2A21, 0x2AC3
.word	0x2B69, 0x2C13, 0x2CC4, 0x2D7A, 0x2E37, 0x2EFC, 0x2FC9, 0x30A1, 0x3184, 0x3276, 0x3379, 0x3493, 0x35C9, 0x3729, 0x38C9, 0x3AE7

asin_lookup_table_delta: // 128 asin delta values fot the asin function. These values represent the step size to the next table value, divided by 16. They are used to archive higher than 128-step precision.
.byte 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05
.byte 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05
.byte 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05
.byte 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06
.byte 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06
.byte 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x08, 0x08
.byte 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x0A, 0x0A, 0x0A, 0x0A
.byte 0x0B, 0x0B, 0x0B, 0x0C, 0x0C, 0x0D, 0x0D, 0x0E, 0x0F, 0x10, 0x12, 0x13, 0x16, 0x1A, 0x22, 0x52


#endif // USING_MATH_EXTENSIONS
