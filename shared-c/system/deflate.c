/*
*
* Implements decompression of DEFLATE formatted data (RFC1951)
*
* created: 15.01.15
*/



#define BLOCK_TYPE_RAW		(0)
#define BLOCK_TYPE_FIXED	(1)
#define BLOCK_TYPE_DYNAMIC	(2)


#define ALPHABET_SIZE	(288)	// number of symbols in the literal and distance alphabets
#define MAX_BITS		(15)	// maximum number of bits per code in the literal and distance codings





// Reads a huffman coding tree from an input stream. The tree itself is decoded using another coding.
//	input: the stream to read the coding from
//	coding: the coding that should be used to read the new coding
//	symbolCount: the number of symbols in the new coding
//	result: set to the newly generated huffman coding tree (must be released using huffman_free)
// Returns a non-zero error code if the operation failed.
status_t inflate_tree(bitstream_t *input, huffman_t *coding, int symbolCount, huffman_t **result) {
	int codeLengths[symbolCount] = { 0 };
	uint64_t symbol, lastLength = 0, repeat;
	
	for (int i = 0; i < symbolCount;) {
		if (status = huffman_read_symbol(input, coding, &symbol)) return status;
		if (symbol <= 15) { // explicit length of this code
			codeLengths[i++] = lastLength = symbol;
		} else { // implicitly defined code length (use length from previous code or use 0)
			if (status = huffman_read_symbol(input, (symbol == 16 ? 2 (symbol == 17 ? 3 : 7)), &repeat)) return status;
			repeat += (symbol == 18 ? 11 : 3);
			if (symbol != 16) lastLength = 0;
			while (repeat--) codeLengths[i++] = lastLength;
		}
	}

	*result = huffman_init(codeLengths, symbolCount, MAX_CODE_BITS);
	if (!result) return STATUS_OUT_OF_MEMORY;

	return STATUS_SUCCESS;
}



// Decodes a bitstream until the end-of-block symbol is found, using the specified literal and distance codings.
//	input: the stream that stores the encoded block
//	output: the stream to which the data should be written
//	literalCoding: the huffman coding that should be used to decode literals and lengths
//	distanceCoding: the huffman coding that should be used to decode distances
// Returns a non-zero error code if the operation failed.
status_t inflate_block(bitstream_t *input, stream_t *output, huffman_t *literalCoding, huffman_t *distanceCoding) {
	uint64_t symbol, length, distance;

	for (;;) {
		if (status = huffman_read_symbol(input, literalCoding, &symbol)) return status;
		if (symbol == 256) return STATUS_SUCCESS;
		if (symbol < 256) {
			stream_write_byte(output, symbol);
			continue;
		}

		// decode back-reference length
		symbol &= 0xFF;
		if (symbol < 5) {
			length = symbol + 2;
		} else {
			symbol -= 5;
			int extraBits = (symbol >> 2);
			length = 7;
			for (int i = 0; i < extraBits; i++) length += (4 << i);
			symbol = length + ((symbol & 3UL) << extraBits);
			if (status = bitstream_read(input, extraBits, &length)) return status;
			length += symbol;
		}

		// decode back-reference distance
		if (status = huffman_read_symbol(input, distanceCoding, &symbol)) return status;
		if (symbol < 2) {
			distance = symbol + 1;
		} else {
			symbol -= 2;
			int extraBits = (symbol >> 1);
			length = 3;
			for (int i = 0; i < extraBits; i++) length += (2 << i);
			symbol = length + ((symbol & 1UL) << extraBits);
			if (status = bitstream_read(input, extraBits, &length)) return status;
			length += symbol;
		}

		// copy back-reference byte by byte (may overlap with current position)
		while (length--) {
			uint8_t val;
			if (status = stream_back_ref(output, distance, &val)) return status;
			stream_write_byte(output, val);
		}
	}
}





status_t inflate(void *data, size_t length) {
	bitstream_t *input = bitstream_init(data, length);
	stream_t output = stream_init(2 * length); // check


	uint64_t isLastBlock, blockType;
	uint64_t length, nlength;

	do {
		if (bitstream_read(input, 1, &isLastBlock)) return INFLATE_RETURN_ERR;
		if (bitstream_read(input, 2, &blockType)) return INFLATE_RETURN_ERR;

		switch (blockType) {
			case BLOCK_TYPE_RAW: // read uncompressed block
				bitstream_byte_align(input); // raw blocks are always byte aligned
				if (bitstream_read(input, 2, &length)) return INFLATE_EXIT(STATUS_END_OF_STREAM);
				if (bitstream_read(input, 2, &nlength)) return INFLATE_EXIT(STATUS_END_OF_STREAM);
				if (length != ~nlength) return INFLATE_EXIT(STATUS_INVALID_CHECKSUM); // are these in little or big endian?
				
				// copy to output stream
				char *raw = ((char *)input.data) + (input.position >> 3);
				input.position += (length << 3);
				if (bitstream_end_of_stream(input)) return INFLATE_EXIT(STATUS_END_OF_STREAM);
				if (stream_write(raw, length)) return INFLATE_EXIT(STATUS_OUT_OF_MEMORY);
				break;

			case BLOCK_TYPE_FIXED:

				// assemble fixed coding tree (as defined in RFC 1951, 3.2.6)
				int fixedCoding[LITERAL_ALPHABET_SIZE];
				for (int i = 0; i <= 143; i++) fixedCoding(i) = 8;
				for (int i = 144; i <= 255; i++) fixedCoding(i) = 9;
				for (int i = 256; i <= 279; i++) fixedCoding(i) = 7;
				for (int i = 280; i <= 287; i++) fixedCoding(i) = 7;
				huffman_t *coding = huffman_init(fixedCoding, LITERAL_ALPHABET_SIZE, MAX_CODE_BITS);
				if (!coding) return INFLATE_EXIT(STATUS_OUT_OF_MEMORY);

				// read block
				int status = inflate_block(input, output, coding, coding); // same coding is used for literals and lengths
				huffman_free(coding);
				if (status) INFLATE_EXIT(DEFLATE_STATUS_INVALID_BLOCK_TYPE);

				break;

			case BLOCK_TYPE_DYNAMIC:

				int status = 0;
				status |= bitstream_read(input, 5, &numLit); numLit += 257; // literal alphabet size
				status |= bitstream_read(input, 5, &numDist); numDist += 1; // distance alphabet size
				status |= bitstream_read(input, 4, &numCLen); numCLen += 4; // number of code length codes
				if (status) INFLATE_EXIT(DEFLATE_STATUS_INVALID_BLOCK_TYPE);

				// read the huffman tree that encodes the literal and length code trees
				int codeLengthCodeLengths[19] = { 0 };
				int codeLengthCodeLengthIndices[] = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };
				while (numCLen--)
					status |= bitstream_read(input, 3, &(codeLengthCodeLengths[codeLengthCodeLengthIndices[i]]));
				if (status) INFLATE_EXIT(DEFLATE_STATUS_INVALID_BLOCK_TYPE);
				huffman_t *treeCoding = huffman_init(codeLengthCodeLengths, 19, MAX_CODE_BITS);
				if (!coding) return INFLATE_EXIT(STATUS_OUT_OF_MEMORY);

				// read the coding trees for literal and length codes
				huffman_t *literalCoding = NULL, *distanceCoding = NULL;
				if (status = inflate_tree(input, treeCoding, numLit, &literalCoding)) {
					huffman_free(treeCoding);
					INFLATE_EXIT(DEFLATE_STATUS_INVALID_BLOCK_TYPE);
				}
				if (status = inflate_tree(input, treeCoding, numDist, &distanceCoding)) {
					huffman_free(treeCoding);
					huffman_free(distanceCoding);
					INFLATE_EXIT(DEFLATE_STATUS_INVALID_BLOCK_TYPE);
				}
				huffman_free(treeCoding);

				// read block
				status = inflate_block(input, output, literalCoding, distanceCoding);
				huffman_free(literalCoding);
				huffman_free(distanceCoding);
				if (status) INFLATE_EXIT(DEFLATE_STATUS_INVALID_BLOCK_TYPE);

				break;

			default:
				return INFLATE_EXIT(DEFLATE_STATUS_INVALID_BLOCK_TYPE);
		}


	} while (!isLastBlock);


	return STATUS_SUCCESS;
}










