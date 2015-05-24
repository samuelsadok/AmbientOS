

typedef enum {
	APIC_SUCCESS,		// the operation was successful
	APIC_NOT_AVAILABLE	// the current platform has no APIC
} apic_status_t;


void apic_timer_start(int interval, int prescaler);
void apic_timer_stop(void);
apic_status_t apic_init(void);
