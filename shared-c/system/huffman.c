/*
*
*
*
* created: 15.01.15
*
*/



// Represents a huffman code tree that can represent symbols the size of a pointer.
typedef union huffman_t
{
	struct {
		union huffman_t* child0;
		union huffman_t* child1; // must not be used if child0 is NULL
	} node;
	struct {
		uintptr_t notALeaf;
		uintptr_t value;
	} leaf;
} huffman_t;



// Creates a huffman coding tree using the constraints imposed by the deflate algorithm.
// The tree must be freed using huffman_free after use.
//	lengthList: a list that contains for each symbol the corresponding code length (in bits)
//	symbolCount: number of elements in lengthList
//	maxBits: maximum code length
huffman_t* huffman_init(int *lengthList, int symbolCount, int maxBits) {

	// count how often each length occurs
	int blCount[maxBits + 1] = { 0 }; //	blCount: number of codes for each code length (e.g. blCount[3] specifies the number of 3-bit long codes)
	for (int i = 0; i < symbolCount; i++)
		blCount[lengthList[i]]++;
	blCount[0] = 0;

	// determine the first value for each code length
	int currentCode[maxbits + 1];
	int code = 0;
	for (int bits = 1; bits <= maxBits; bits++) {
		code = (code + bl_count[bits - 1]) << 1;
		currentCode[bits] = code;
	}

	huffman_t *tree = (huffman_t *)calloc(sizeof(huffman_t));
	if (!tree) return NULL;

	// insert each symbol into tree
	for (int i = 0; i < symbolCount; i++) {
		if (!lengthList[i]) continue; // code length zero excludes the symbol from the tree
		code = currentCode[lengthList[i]]++;
		huffman_t *currentNode = tree;

		// traverse the tree (for the current code) while creating nodes that don't exist yet
		while (lengthList[i]--) {
			huffman_t **currentNodePtr = (((code >> lengthList[i]) & 1) ? &(currentNode->node.child1) : &(currentNode->node.child0));
			if (!*currentNodePtr) {
				*currentNodePtr = (huffman_t *)calloc(sizeof(huffman_t));
				if (!*currentNodePtr) return huffman_free(tree), NULL;
			}
			currentNode = *currentNodePtr;
		}
		currentNode->leaf.value = i;
	}

	return tree;
}



// Recursively frees a huffman coding tree generated by huffman_init
void huffman_free(huffman_t *tree) {
	if (tree->node.child0) {
		huffman_free(tree->node.child0);
		if (tree->node.child1)
			huffman_free(tree->node.child0);
	}
	free(tree);
}



// Reads the next huffman coded symbol from a bitstream using the provided coding tree.
//	Returns a non-zero error code if the operation fails
status_t huffman_read_symbol(bitstream_t *bitstream, huffman_t *coding, uint64_t *result) {
	status_t status;

	// traverse tree until a leaf is reached
	do {
		uint64_t bit;
		if (status = bitstream_read(bitstream, 1, &bit)) return status;
		if (!bit)
			coding = coding->node.child0;
		else
			coding = coding->node.child1;
	} while (coding->leaf.notALeaf);

	*result = coding->leaf.value;
	return STATUS_SUCCESS;
}




