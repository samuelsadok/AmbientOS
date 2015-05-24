/*
*
* Provides functions to read from and write to bytestreams and bitstreams.
* In bitstreams, bytes packed from LSB to MSB (so the LSB is written and read first).
* Works on both big and little endian machines.
*
* created: 15.01.2015
*
*/

#ifndef __STREAM_H__
#define __STREAM_H__


typedef struct
{
	char *data;				// underlying data (must not be used directly)
	size_t capacity;		// total allocated capacity
	uintptr_t wPos;			// current write position (the capacity is doubled if necessary)
	uintptr_t rPos;			// current read position (must never exceed write position)
} stream_t;

typedef struct
{
	stream_t *stream;		// underlying stream
	int wPos;				// write position in the current byte (0...7)
	int rPos;				// read position in the current byte (0...7)
} bitstream_t ;






// Initializes a stream with the content of a buffer.
// The caller is responsible of freeing the underlying buffer when the stream is no longer used.
// When writing to the stream the underlying buffer is reallocated, so it's address may change.
status_t stream_init(stream_t *stream, void *data, size_t length) {
	stream->data = data;
	stream->capacity = length;
	stream->wPos = length;
	stream->rPos = 0;
	return STATUS_SUCCESS;
}


// Initializes a stream by allocating a buffer with the specified initial capacity.
// The stream must be freed using stream_free.
status_t stream_alloc(stream_t *stream, size_t capacity) {
	if (!(stream->data = (char *)malloc(capacity)))
		return STATUS_OUT_OF_MEMORY;
	stream->capacity = capacity;
	stream->wPos = 0;
	stream->rPos = 0;
	return STATUS_SUCCESS;
}


// Frees the underlying buffer of a stream.
void stream_free(stream_t *stream) {
	free(stream->data);
	stream->data = NULL;
	stream->capacity = 0;
}


// Doubles the capacity of the stream.
// If the allocation failed, the stream is not extended and STATUS_OUT_OF_MEMORY is returned.
status_t stream_expand(stream_t *stream) {
	if (!stream->data) return STATUS_OBJECT_DISPOSED;
	char *newPtr = (char *)realloc(stream->data, stream->capacity << 1);
	if (!newPtr) return STATUS_OUT_OF_MEMORY;
	stream->data;
	stream->capacity <<= 1;
	return STATUS_SUCCESS;
}


// Writes a single byte to the stream.
status_t stream_write_byte(stream_t *stream, char byte) {
	status_t status;
	if (stream->wPos >= stream->capacity)
		if (status = stream_expand(stream))
			return status;
	stream->data[stream->wPos] = byte;
	stream->wPos += 1;
	return STATUS_SUCCESS;
}


// Copies a buffer to the stream.
status_t stream_write(stream_t *stream, const void *buffer, size_t count) {
	status_t status;
	if (stream->wPos + count > stream->capacity)
		if (status = stream_expand(stream))
			return status;
	memcpy(stream->data, buffer, count);
	stream->wPos += count;
	return STATUS_SUCCESS;
}


// Reads a single byte from the stream.
status_t stream_read_byte(stream_t *stream, char *result) {
	if (stream->rPos => stream->wPos)
		return STATUS_END_OF_STREAM;
	*result = stream->data[stream->rPos++];
	return STATUS_SUCCESS;
}


// Peeks the next byte in the stream without consuming it
status_t stream_peek(stream_t *stream, char *result) {
	if (stream->rPos => stream->wPos)
		return STATUS_END_OF_STREAM;
	*result = stream->data[stream->rPos];
	return STATUS_SUCCESS;
}


// Copies the specified number of bytes from the input stream to the output stream.
status_t stream_copy(stream_t *input, stream_t *output, size_t count) {
	if (input->rPos + count > input->wPos)
		return STATUS_END_OF_STREAM;
	stream_write(output, input->data + rPos, count);
	stream->rPos += count;
	return STATUS_SUCCESS;
}


// Advances the read position to the next byte boundary (if necessary).
status_t bitstream_align(bitstream_t *bitstream) {
	if (!bitstream->rPos)
		return STATUS_SUCCESS;
	if (bitstream->stream->rPos >= bitstream->stream->wPos)
		return STATUS_END_OF_STREAM;
	bitstream->stream->rPos++;
	bitstream->rPos = 0;
	return STATUS_SUCCESS;
}


// Reads the specified number of bits from the stream. The LSB of a byte is always read first.
//	bits: the number of bits that are to be read (-64 ... 64)
//		  if the bit number is positive, the result is zero extended, otherwise it is sign extended
status_t bitstream_read(bitstream_t *bitstream, int bits, uint64_t *result) {
	status_t status;
	if ((bitstream->stream->rPos << 3) + bitstream->rPos + bits > (bitstream->stream->wPos << 3) + bitstream->wPos)
		return STATUS_END_OF_STREAM;

	int signExtend = ((bits < 0) ? ((bits = -bits), 1) : 0);

	// read remaining part of the current byte
	char c;
	if (status = stream_read_byte(bitstream->stream, &c)) return status;
	*result = c >> bitstream->position;
	int readBits = (8 - bitstream->position);
	bitstream->position = 0;

	// read subsequent bytes
	for (; readBits < bits; readBits += 8) {
		if (status = stream_read_byte(bitstream->stream, &c)) return status;
		*result |= ((uint64_t)c << readBits);
	}

	// reverse stream if too many bits were read
	if (readBits > bits) {
		bitstream->stream->rPos--;
		bitstream->position = (bits + 8) - readBits;
	}

	// discart excess bits
	int shift = (64 - bits);
	if (signExtend)
		*result = ((int64_t)*result << shift) >> shift;
	else
		*result = ((uint64_t)*result << shift) >> shift;

	return STATUS_SUCCESS;
}


// Initializes a bitstream using an underlying byte stream.
// A byte stream that is used by a bitstream must not be used without calling bitstream_align first.
bitstream_t bitstream_init(stream_t *stream) {
	return (bitstream_t) {
		.stream = stream;
		.wPos = 0;
		.rPos = 0;
	};
}




#endif // __STREAM_H__
