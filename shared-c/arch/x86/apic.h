
#ifndef __APIC_H__
#define __APIC_H__


typedef void(*timer_callback_t)(execution_context_t *context);

void apic_timer_config(timer_callback_t);
void apic_timer_start(int interval, int prescaler);
void apic_timer_stop(void);
void apic_timer_trigger(void);
void apic_enable(void);
void apic_disable(void);
void apic_init(void);
void pic_enable(void);
void pic_disable(void);


#endif // __APIC_H__
