/*
* XMEGA TWI slave drive.
*
* The code in this file is mostly inspired by the code found in the ASF.
*
* original file: ASF/xmega/drivers/twi/twis.c (revision 2660, created 11.08.09 by Atmel)
*
*
* Config options:
*	USING_BUILTIN_TWI[...]_MASTER	enables one of the I2C slave controllers.
*	BUILTIN_TWI[...]_ADDRESS	the address that the local slave controller listens to
*	BUILTIN_TWI[...]_ADDR_BYTES	number of bytes used for register addressing (1 - 4)
*	BUILTIN_TWI[...]_FAST_MODE	enables up to 1MHz bus speed
*	BUILTIN_TWI[...]_INT_LEVEL	interrupt priority (all transfers are interrupt driven)
*
* created: 02.03.15
*
*/


#include <system.h>



#ifdef USING_BUILTIN_TWI_SLAVE


// Some devices support a bridge mode.
// In this mode, a single TWI controller can act both as master and slave
// at the same time on two different ports. The location of the alternative port
// (which is always the slave port) can be found in the datasheet.
#if defined(USING_BUILTIN_TWIC_MASTER) && defined(USING_BUILTIN_TWIC_SLAVE)
#	define	TWIC_BRIDGE(module)	((module) == &TWIC)
#else
#	define	TWIC_BRIDGE(module) (0)
#endif
#if defined(USING_BUILTIN_TWID_MASTER) && defined(USING_BUILTIN_TWID_SLAVE)
#	define	TWID_BRIDGE(module)	((module) == &TWID)
#else
#	define	TWID_BRIDGE(module) (0)
#endif
#if defined(USING_BUILTIN_TWIE_MASTER) && defined(USING_BUILTIN_TWIE_SLAVE)
#	define	TWIE_BRIDGE(module)	((module) == &TWIE)
#else
#	define	TWIE_BRIDGE(module) (0)
#endif
#if defined(USING_BUILTIN_TWIF_MASTER) && defined(USING_BUILTIN_TWIF_SLAVE)
#	define	TWIF_BRIDGE(module)	((module) == &TWIF)
#else
#	define	TWIF_BRIDGE(module) (0)
#endif

#define	TWI_DUAL_MODE(module)	(TWIC_BRIDGE(module) || TWID_BRIDGE(module) || TWIE_BRIDGE(module) || TWIF_BRIDGE(module))


// Buffer size defines. (could be moved to platform header)
#define TWIS_BUFFER_SIZE			(8)


// Context of an I2C slave controller
typedef struct
{
	TWI_t *interface;                               // pointer to the controller hardware
	i2c_endpoint_t *endpoints;						// an array of endpoints on this slave interface
	size_t endpointCount;							// the number of elements in endpoints
	endpoint_t *currentEndpoint;					// the endpoint corresponding to the currently selected register address
	size_t bufLen;									// number of valid bytes in buffer (if sending) or buffer length (if receiving)
	size_t bufPos;									// position in the buffer in the current transaction
	char buffer[TWIS_BUFFER_SIZE];					// incoming or outgoing data
	uint32_t addr;									// currently selected register address
	size_t addrBytes;								// number of address bytes expected by this device (up to 4)
	size_t offset;									// offset relative to the register address (used when a large transaction is split into several transactions)
	struct
	{
		unsigned int busy : 1;						// a transaction is in progress
		unsigned int rwn : 1;						// 1: read (send), 0: write (receive)
		unsigned int receivingAddr : 1;				// currently receiving the register address bytes (only applicable if receiving)
	} state;
} twi_slave_t;


void(*i2cSlaveErrorCallback)(status_t status) = NULL; // This callback will be invoked for every error that is detected on any I2C bus


// Starts a new transaction and fills the out-buffer with new data (if neccessary)
//	rwn: if set to 1, the new transaction is a read transaction
//	extendedTransaction: if set to 1, the previous register address will be retained and the offset will be increased by the buffer position
status_t EXTENDED_TEXT twis_start_transaction(twi_slave_t *twi, bool rwn, bool extendedTransaction) {
	twi->state.rwn = rwn;
	twi->state.busy = 1;

	if (extendedTransaction) {
		if (!twi->state.receivingAddr)
			twi->offset += twi->bufPos;
		twi->state.receivingAddr = 0;
	} else {
		twi->state.receivingAddr = 1;
		twi->addr = 0;
		twi->offset = 0;
		twi->currentEndpoint = NULL;
	}

	twi->bufLen = TWIS_BUFFER_SIZE;
	twi->bufPos = 0;
	if (twi->state.rwn) {
		if (!twi->currentEndpoint) return STATUS_ERROR;
		if (!twi->currentEndpoint->read) return STATUS_ERROR;
		return twi->currentEndpoint->read(twi, twi->offset, twi->buffer, &(twi->bufLen));
	}
	return STATUS_SUCCESS;
}


// Terminates an ongoing transaction (if there was one) and commits the data
// in the in-buffer (if any)
void EXTENDED_TEXT twis_end_transaction(twi_slave_t *twi, status_t status) {
	// If we successfully received data, signal a write complete event
	if (!status && twi->state.busy && !twi->state.rwn && !twi->state.receivingAddr) {
		if (twi->currentEndpoint)
			if (twi->currentEndpoint->write)
				twi->currentEndpoint->write(twi, twi->offset, twi->buffer, twi->bufPos);
	}

	twi->state.busy = 0;
	if (status)
		if (i2cSlaveErrorCallback)
			i2cSlaveErrorCallback(status);
}


// Restarts an ongoing transaction or starts a new transaction. This can be used to:
//	- refresh buffers if an overflow or underflow occurs
//	- change the read/write mode
static inline status_t twis_restart_transaction(twi_slave_t *twi, bool rwn, bool extendedTransaction) {
	twis_end_transaction(twi, STATUS_SUCCESS);
	return twis_start_transaction(twi, rwn, extendedTransaction);
}


// Handles an address match condition (a start or repeated start condition
// followed by the address of this slave) by preparing the controller for
// data transfer.
static inline void twis_start_handler(twi_slave_t *twi) {

	// Ensure a clean termination in case we were already receiving data (repeated start).
	bool rwn = twi->interface->SLAVE.STATUS & TWI_SLAVE_DIR_bm;
	status_t status = twis_restart_transaction(twi, rwn, rwn);

	// Disable stop interrupt
	twi->interface->SLAVE.CTRLA &= ~TWI_SLAVE_PIEN_bm;

	if (status) {
		// Send NACK, wait for next START condition
		twi->interface->SLAVE.CTRLB = TWI_SLAVE_CMD_COMPTRANS_gc;
		twis_end_transaction(twi, status);
	} else {
		// Send ACK, wait for data interrupt (either send or receive)
		twi->interface->SLAVE.CTRLB = TWI_SLAVE_CMD_RESPONSE_gc;
	}
}


// Handles a STOP condition.
static inline void twis_stop_handler(twi_slave_t *twi) {
	// Disable stop interrupt.
	twi->interface->SLAVE.CTRLA &= ~TWI_SLAVE_PIEN_bm;

	// Clear APIF, according to flowchart don't ACK or NACK
	twi->interface->SLAVE.STATUS |= TWI_SLAVE_APIF_bm;

	twis_end_transaction(twi, STATUS_SUCCESS);

	if (!twi->state.rwn) // this should always hold
		if (twi->currentEndpoint)
			if (twi->currentEndpoint->writeComplete)
				twi->currentEndpoint->writeComplete(twi);
}


// Selects the best fitting endpoint for the current address
static inline status_t twi_select_endpoint(twi_slave_t *twi) {
	uint32_t nearestAddr = 0;
	i2c_endpoint_t *nearestEndpoint = NULL;

	for (size_t i = 0; i < twi->endpointCount; i++)
		if ((!twi->endpoints[i].randomAccess && twi->endpoints[i].addr == twi->addr) || (twi->endpoints[i].addr <= twi->addr && twi->endpoints[i].addr >= nearestAddr))
			nearestAddr = (nearestEndpoint = twi->endpoints + i)->addr;

	if (!nearestEndpoint)
		return STATUS_ERROR;

	twi->currentEndpoint = nearestEndpoint->endpoint;
	return STATUS_SUCCESS;
}


// Handles an incoming TWI byte.
static inline void twis_write_handler(twi_slave_t *twi) {
	// Enable stop interrupt.
	twi->interface->SLAVE.CTRLA |= TWI_SLAVE_PIEN_bm;

	status_t status = STATUS_SUCCESS;

	if (twi->state.receivingAddr) {
		/* receiving address byte */
		twi->addr = (twi->addr << 8) | twi->interface->SLAVE.DATA;
		if (++twi->bufPos >= twi->addrBytes) {
			twi->bufPos = 0;
			twi->state.receivingAddr = 0;
			status = twi_select_endpoint(twi);
		}

	} else {
		/* receiving data byte */
		if (twi->bufPos >= twi->bufLen) // buffer overflow -> flush buffer
			status = twis_restart_transaction(twi, 0, 1);

		twi->buffer[twi->bufPos++] = twi->interface->SLAVE.DATA;
	}

	if (status) {
		/* receive fail: send NACK and wait for next start condition */
		twi->interface->SLAVE.CTRLB = TWI_SLAVE_ACKACT_bm | TWI_SLAVE_CMD_COMPTRANS_gc;
		twis_end_transaction(twi, status);
	} else {
		/* receive success: send ACK and wait for next byte */
		twi->interface->SLAVE.CTRLB = TWI_SLAVE_CMD_RESPONSE_gc;
	}
}


// Handles a TWI read request.
static inline void twis_read_handler(twi_slave_t *twi) {
	/* If NACK, transaction finished. */
	if ((twi->bufPos || twi->offset) && (twi->interface->SLAVE.STATUS & TWI_SLAVE_RXACK_bm)) {
		twi->interface->SLAVE.CTRLB = TWI_SLAVE_CMD_COMPTRANS_gc;
		twis_end_transaction(twi, STATUS_SUCCESS);
		return;
	}

	/* If ACK, master expects more data. */

	status_t status = STATUS_SUCCESS;

	if (twi->bufPos >= twi->bufLen) // buffer underflow -> restart transaction to fetch new data
		status = twis_restart_transaction(twi, 1, 1);

	if (!status && !twi->bufLen) // no more data -> signal error
		status = STATUS_OUT_OF_RANGE;

	if (status) {
		twi->interface->SLAVE.CTRLB = TWI_SLAVE_CMD_COMPTRANS_gc;
		twis_end_transaction(twi, status);
	} else {
		twi->interface->SLAVE.CTRLB = TWI_SLAVE_CMD_RESPONSE_gc;
		twi->interface->SLAVE.DATA = twi->buffer[twi->bufPos++];
	}
}



// TWI Slave interrupt service routine.
// Handles all TWI transactions and responses to address match, data reception,
// data transmission, bus error and data collision.
void EXTENDED_TEXT twi_slave_interrupt(twi_slave_t *twi)
{
	uint8_t currentStatus = twi->interface->SLAVE.STATUS;

	if (currentStatus & (TWI_SLAVE_BUSERR_bm | TWI_SLAVE_COLL_bm)) {
		twis_end_transaction(twi, STATUS_I2C_PROTOCOL); // bus error
	} else if (currentStatus & TWI_SLAVE_APIF_bm) {
		if (currentStatus & TWI_SLAVE_AP_bm) { // address match (start or repeated start condition plus address)
			twis_start_handler(twi);
		} else { // stop condition
			twis_stop_handler(twi);
		}
	} else if (currentStatus & TWI_SLAVE_DIF_bm) {
		if (currentStatus & TWI_SLAVE_DIR_bm) {
			twis_read_handler(twi); // byte requested by master
		} else {
			twis_write_handler(twi); // byte received from master
		}
	} else {
		twis_end_transaction(twi, STATUS_ERROR); // unexpected state
	}
}


/* TWIC Slave */

#ifdef USING_BUILTIN_TWIC_SLAVE
i2c_endpoint_t twic_endpoints[] = TWIC_ENDPOINTS; // defined in the platform header
twi_slave_t twic_slave = { .interface = &TWIC, .endpoints = twic_endpoints, .endpointCount = sizeof(twic_endpoints) / sizeof(i2c_endpoint_t) };
EXTENDED_TEXT ISR(TWIC_TWIS_vect) {
	twi_slave_interrupt(&twic_slave);
}
#endif



// Inits a built in I2C controller in slave mode
//	address: the I2C address that this device should respond to
//	fastMode: if set to 1, the slave supports 1MHz bus speeds
void builtin_i2c_slave_init_ex(twi_slave_t *twi, char intLevel, bool fastMode, char address, size_t addrBytes) {
	sysclk_enable_peripheral_clock(twi->interface);

	if (TWI_DUAL_MODE(twi->interface)) {
		twi_bridge_enable(twi->interface);
		if (fastMode)
			twi_slave_fast_mode_enable(twi->interface);
	} else {
		if (fastMode)
			twi_fast_mode_enable(twi->interface);
	}

	twi->addrBytes = addrBytes;
	twi->state.busy = 0;

	// init and enable I2C slave controller
	twi->interface->SLAVE.CTRLA = intLevel |
		TWI_SLAVE_DIEN_bm |
		TWI_SLAVE_APIEN_bm |
		TWI_SLAVE_ENABLE_bm;
	twi->interface->SLAVE.ADDR = (address << 1);
}


// Inits all built in I2C slave modules that are used by the application
void builtin_i2c_slave_init(void) {
#ifdef USING_BUILTIN_TWIC_SLAVE
	builtin_i2c_slave_init_ex(&twic_slave, BUILTIN_TWIC_INT_LEVEL, BUILTIN_TWIC_FAST_MODE, BUILTIN_TWIC_ADDRESS, BUILTIN_TWIC_ADDR_BYTES);
#endif
	// todo: extend
}


#endif

