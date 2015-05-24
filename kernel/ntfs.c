/*
*
*
* thanks to http://bos.asmhackers.net/docs/filesystems/ntfs/index.html
*
* created: 02.02.15
*
*/

#include <system.h>
#include <system/unicode.h>
#include <system/drivers.h>
#include <system/filesystem.h>




//#define DBG_FILE(...)	LOGI(__VA_ARGS__)
#define DBG_FILE(...)
//#define DBG_INDEX(...)	LOGI(__VA_ARGS__)
#define DBG_INDEX(...)


#define MFT_CACHE_SIZE					(32)
#define MFT_CACHE_SIZE_MASK				(0x1FUL)
#define INDEX_BUFFER_CACHE_SIZE			(32)
#define INDEX_BUFFER_CACHE_SIZE_MASK	(0x1FUL)




// This header is used in any vital NTFS structures.
// It allows for detection of incomplete multi-sector writes.
typedef struct __attribute__((__packed__)) {
	uint32_t magicNumber;
	uint16_t updateSequenceOffset;
	uint16_t updateSequenceLength;
	uint64_t reserved;
} ntfs_fixup_header_t;

// The header of an MFT file record
typedef struct __attribute__((__packed__))
{
	ntfs_fixup_header_t header;
	uint16_t sequenceNumber;
	uint16_t referenceCount;
	uint16_t attributeSequenceOffset;
	uint16_t flags;
	uint32_t realSize;
	uint32_t allocatedSize;
	uint64_t baseRecordSegment;
	uint16_t maxAttributeType;
} ntfs_file_record_t;


// The header of an attribute in the file record
typedef struct __attribute__((__packed__))
{
	uint32_t type;
	uint32_t length;
	uint8_t nonResident;
	uint8_t nameLength;
	uint16_t nameOffset;
	uint16_t compressed;
	uint16_t id;
	union
	{
		struct
		{
			uint32_t length;
			uint16_t offset;
			uint16_t indexed;
		} residentHeader;
		struct
		{
			uint64_t startCluster;
			uint64_t lastCluster;
			uint16_t runlistOffset;
			uint16_t compressionEngine;
			uint32_t reserved;
			uint64_t allocatedSize;
			uint64_t realSize;
			uint64_t initializedSize;
		} nonResidentHeader;
	} extendedHeader;
} ntfs_attribute_t;


typedef struct __attribute__((__packed__)) {
	uint32_t sequenceOffset;
	uint32_t sequenceEndOffset;
	uint32_t bufferEndOffset;
	uint32_t hasChildren;
} ntfs_index_sequence_t;

typedef struct __attribute__((__packed__)) {
	uint32_t indexedType;
	uint32_t reserved;
	uint32_t bufferSize;
	uint32_t clustersPerBuffer;
	ntfs_index_sequence_t sequence;
} ntfs_index_root_t;

typedef struct __attribute__((__packed__)) {
	ntfs_fixup_header_t header;
	uint64_t indexBufferNumber;
	ntfs_index_sequence_t sequence;
} ntfs_index_buffer_t;

typedef struct __attribute__((__packed__)) {
	uint64_t reference;
	uint16_t length;
	uint16_t streamLength;
	uint32_t flags;
	char stream[1];
} ntfs_index_entry_t;

typedef struct __attribute__((__packed__)) {
	uint64_t parentReference;
	uint64_t creationTime;
	uint64_t writeTime;
	uint64_t mftEditTime;
	uint64_t readTime;
	uint64_t allocatedSize;
	uint64_t realSize;
	uint32_t flags;
	uint32_t reparseInfo;
	uint8_t fileNameLength;
	uint8_t nameSpace;
	wchar_t fileName[1];
} ntfs_file_name_t;




typedef struct
{
	ntfs_index_root_t *root;		// content of the index root attribute (resides in the owner's file record)
	char *bitmap;					// the index bitmap (only valid if the index root has children)
	ntfs_attribute_t *allocation;	// the index allocation attribute
	uint64_t allocatedBuffers;		// the number of allocated buffers
	ntfs_index_buffer_t *cache[INDEX_BUFFER_CACHE_SIZE];	// a cache for the index buffers
} ntfs_index_tree_t;


typedef struct
{
	uint64_t segment;				// the segment that this file belongs to
	uint64_t references;			// the number of times this structure is used
	ntfs_file_record_t *record;		// a buffer that holds the MFT segment for this file (always valid if the file is opened)
	ntfs_attribute_t *data;			// pointer to the data attribute (NULL for directories) (don't free, resides in the record)
	ntfs_index_tree_t *i30;			// $I30 index of the directory (NULL for files)
} ntfs_file_t;


typedef struct
{
	volume_t *volume;

	uint64_t bytesPerSector;
	uint64_t bytesPerCluster;
	uint64_t bytesPerMftSegment;
	uint64_t bytesPerIndexBuffer;

	ntfs_file_t *cache[MFT_CACHE_SIZE];	// stores the MFT records of up to 32 files that were recently used
	ntfs_file_record_t *mftSegment0;	// MFT segment 0 (defines the MFT file itself)
	ntfs_attribute_t *mftData;			// data attribute of MFT segment 0
} ntfs_t;


enum
{
	NTFS_ATTRIBUTE_TYPE_STANDARD_INFORMATION = 0x10,
	NTFS_ATTRIBUTE_TYPE_ATTRIBUTE_LIST = 0x20,
	NTFS_ATTRIBUTE_TYPE_FILE_NAME = 0x30,
	NTFS_ATTRIBUTE_TYPE_VOLUME_VERSION = 0x40,
	NTFS_ATTRIBUTE_TYPE_SECURITY_DESCRIPTOR = 0x50,
	NTFS_ATTRIBUTE_TYPE_VOLUME_NAME = 0x60,
	NTFS_ATTRIBUTE_TYPE_VOLUME_INFORMATION = 0x70,
	NTFS_ATTRIBUTE_TYPE_DATA = 0x80,
	NTFS_ATTRIBUTE_TYPE_INDEX_ROOT = 0x90,
	NTFS_ATTRIBUTE_TYPE_INDEX_ALLOCATION = 0xA0,
	NTFS_ATTRIBUTE_TYPE_BITMAP = 0xB0,
	NTFS_ATTRIBUTE_TYPE_SYMBOLIC_LINK = 0xC0,
	NTFS_ATTRIBUTE_TYPE_EA_INFORMATION = 0xD0,
	NTFS_ATTRIBUTE_TYPE_EA = 0xE0,
	NTFS_ATTRIBUTE_TYPE_END_OF_LIST = -1
};


unicode_t directoryIndexName;



// Loads the dataruns defined by a runlist.
//	currentCluster: the starting cluster
//	dataruns: a pointer to the runlist
//	offset: an offset into the data defined by the runlist
//	count: the number of bytes to load
status_t ntfs_load_dataruns(ntfs_t *ntfs, int64_t currentCluster, char *dataruns, uint64_t offset, uint64_t count, char *buffer) {
	status_t status;

	while (*dataruns && count) {
		uint8_t offsetBytes = (*dataruns >> 4) & 0xF;
		uint8_t countBytes = *dataruns & 0xF;
		dataruns++;

		// load unsigned length field
		uint64_t clusterCount = 0;
		for (int i = 0; i < countBytes; i++)
			clusterCount |= ((*(dataruns++) & 0xFF) << (8 * i));

		// load signed offset field
		int64_t clusterOffset = 0;
		for (int i = 0; i < offsetBytes; i++)
			clusterOffset |= ((*(dataruns++) & 0xFF) << (8 * i));
		int offsetShift = 64 - 8 * offsetBytes;
		currentCluster += ((clusterOffset << offsetShift) >> offsetShift);

		if (offset >= clusterCount * ntfs->bytesPerCluster) {
			offset -= clusterCount * ntfs->bytesPerCluster;
			continue;
		}

		// load from current datarun
		uint64_t effectiveCount = min(count, clusterCount * ntfs->bytesPerCluster - offset);
		if ((status = volume_read(ntfs->volume, currentCluster * ntfs->bytesPerCluster + offset, effectiveCount, buffer)))
			return status;
		offset = 0;
		count -= effectiveCount;
		buffer += effectiveCount;
	}

	return STATUS_SUCCESS;
}


// Returns a pointer to an attribute header inside an MFT record.
// Returns NULL if the attribute was not found.
// The returnd pointer points to somewhere in the file record, hence it must not be freed.
// File records larger than one MFT record are not supported (would need to read attribute 0x20).
//	type: the attribute type
//	name: the name of the attribute (NULL: don't care)
ntfs_attribute_t *ntfs_find_attribute(ntfs_file_record_t *fileRecord, uint32_t type, unicode_t *name) {

	// traverse the attribute list
	for (ntfs_attribute_t *attribute = (ntfs_attribute_t *)(((char *)fileRecord) + fileRecord->attributeSequenceOffset);
		 attribute->type <= type; // sorted by type
		 attribute = (ntfs_attribute_t *)(((char *)attribute) + attribute->length)) {

		if (attribute->type == type) {
			// multiple instances of the same attribute type may be present (but not with the same name)
			unicode_t attrName = { .length = attribute->nameLength, .data = (wchar_t *)((char *)attribute + attribute->nameOffset) };
			if (name) if (unicode_compare(name, &attrName, 0))
				continue;
			return attribute;
		}

		if (!attribute->length)
			return NULL;
	}
	return NULL;
}


// Returns the length of the underlying stream of the attibute
uint64_t ntfs_get_attribute_size(ntfs_attribute_t *attribute) {
	return (attribute->nonResident ? attribute->extendedHeader.nonResidentHeader.realSize : attribute->extendedHeader.residentHeader.length);
}


// Loads part of an attribute from the volume into a buffer.
//	type: the attribute type
//	name: the name of the attribute (NULL: don't care)
//	offset: the offset into the attribute
//	count: the number of bytes to read
//	buffer: the buffer (of sufficient size) into which the bytes should be loaded
status_t ntfs_load_attribute(ntfs_t *ntfs, ntfs_attribute_t *attribute, uint64_t offset, uint64_t count, char *buffer) {
	if (attribute->compressed) // can't read compressed, encrypted or sparse attributes
		return STATUS_NOT_IMPLEMENTED;

	if (attribute->nonResident) {
		if (offset + count > attribute->extendedHeader.nonResidentHeader.realSize)
			return STATUS_OUT_OF_RANGE;
		return ntfs_load_dataruns(ntfs, attribute->extendedHeader.nonResidentHeader.startCluster, ((char *)attribute) + attribute->extendedHeader.nonResidentHeader.runlistOffset, offset, count, buffer);
	} else {
		if (offset + count > attribute->extendedHeader.residentHeader.length)
			return STATUS_OUT_OF_RANGE;
		memcpy(buffer, ((char *)attribute) + attribute->extendedHeader.residentHeader.offset + offset, count);
	}

	return STATUS_SUCCESS;
}


// Validates and fixes a block of data that starts with the fixup header.
status_t ntfs_fixup(ntfs_t *ntfs, ntfs_fixup_header_t *block, uint32_t magicNumber) {
	if (block->magicNumber != magicNumber)
		return STATUS_DATA_CORRUPT;

	uint16_t *sequence = (uint16_t *)(((char *)block) + block->updateSequenceOffset);
	uint16_t count = block->updateSequenceLength;

	// replace the last word of each sector
	for (int i = 1; i < count; i++) {
		uint16_t *currentField = (uint16_t *)(((char *)block) + i * ntfs->bytesPerSector) - 1;
		if (sequence[0] && (sequence[0] != *currentField))
			return STATUS_DATA_CORRUPT;
		*currentField = sequence[i];
	}

	return STATUS_SUCCESS;
}


// Allocates and loads an index tree from the volume.
// In case the call succeeds, the tree must be freed at some point.
status_t ntfs_load_index(ntfs_t *ntfs, ntfs_file_t *file, unicode_t *name, ntfs_index_tree_t **treePtr) {
	status_t status;
	*treePtr = NULL;

	DBG_INDEX("load index");

	// clear memory (to fill the cache with NULL)
	ntfs_index_tree_t *tree = (ntfs_index_tree_t *)calloc(sizeof(ntfs_index_tree_t), 1);
	if (!tree) return STATUS_OUT_OF_MEMORY;

	// load index root (always resident)
	ntfs_attribute_t *indexRootAttr = ntfs_find_attribute(file->record, NTFS_ATTRIBUTE_TYPE_INDEX_ROOT, name);
	if (!indexRootAttr)
		return free(tree), STATUS_DATA_CORRUPT;
	tree->root = (ntfs_index_root_t *)(((char *)indexRootAttr) + indexRootAttr->extendedHeader.residentHeader.offset);
	
	if (tree->root->sequence.hasChildren) {
		// load index allocation
		tree->allocation = ntfs_find_attribute(file->record, NTFS_ATTRIBUTE_TYPE_INDEX_ALLOCATION, name);
		if (!tree->allocation)
			return free(tree), STATUS_DATA_CORRUPT;
		tree->allocatedBuffers = ntfs_get_attribute_size(tree->allocation) / tree->root->bufferSize;

		// load bitmap
		ntfs_attribute_t *bitmapAttr = ntfs_find_attribute(file->record, NTFS_ATTRIBUTE_TYPE_BITMAP, name);
		uint64_t bitmapSize = ntfs_get_attribute_size(bitmapAttr);
		tree->bitmap = (char *)malloc(bitmapSize);
		if (!tree->bitmap)
			return free(tree), STATUS_OUT_OF_MEMORY;
		if ((status = ntfs_load_attribute(ntfs, bitmapAttr, 0, bitmapSize, tree->bitmap)))
			return free(tree->bitmap), free(tree), status;
	} else {
		tree->allocatedBuffers = 0;
	}

	DBG_INDEX("index loaded at %x64", (uint64_t)tree);

	*treePtr = tree;
	return STATUS_SUCCESS;
}


// Frees a tree that was allocated by ntfs_load_index
void ntfs_index_tree_free(ntfs_index_tree_t *tree) {
	DBG_INDEX("free index tree");
	for (int i = 0; i < INDEX_BUFFER_CACHE_SIZE; i++) {
		if (tree->cache[i])
			free(tree->cache[i]);
	}
	if (tree->bitmap)
		free(tree->bitmap);
	free(tree);
	debug(0x40, (uint64_t)tree);
}


// Loads the specified index buffer from the index allocation of the tree.
// If the index is not available (as specified in the bitmap), the next available index buffer is returned.
// The returned buffer is only valid until another index buffer is loaded.
// The returned buffer should not be freed as this is done when it is evicted from cache or the file is freed.
// Returns STATUS_END_OF_STREAM if no valid buffer was found.
status_t ntfs_load_index_buffer(ntfs_t *ntfs, ntfs_index_tree_t *tree, uint64_t *number, ntfs_index_buffer_t **bufferPtr) {
	status_t status;
	*bufferPtr = NULL;

	DBG_INDEX("load index buffer %d", (int)*number);

	// find the next index buffer in use
	for (;;) {
		if (*number >= tree->allocatedBuffers)
			return STATUS_END_OF_STREAM;
		if ((tree->bitmap[*number >> 3] >> (*number & 7)) & 1)
			break;
		(*number)++;
	}	
	
	// try to get buffer from cache
	ntfs_index_buffer_t *buffer = tree->cache[*number & INDEX_BUFFER_CACHE_SIZE_MASK];
	if (buffer) if (buffer->indexBufferNumber != *number) {
		free(buffer);
		buffer = NULL;
	}

	// load buffer if it wasn't in cache
	if (!buffer) {
		buffer = (ntfs_index_buffer_t *)malloc(tree->root->bufferSize);
		if (!buffer)
			return STATUS_OUT_OF_MEMORY;
		if ((status = ntfs_load_attribute(ntfs, tree->allocation, *number * tree->root->bufferSize, tree->root->bufferSize, (char *)buffer)))
			return free(buffer), status;
		if ((status = ntfs_fixup(ntfs, &(buffer->header), *(uint32_t *)"INDX")))
			return free(buffer), status;
		tree->cache[*number & INDEX_BUFFER_CACHE_SIZE_MASK] = buffer;
	}

	DBG_INDEX("index buffer %d loaded at %x64", (int)*number, (uint64_t)buffer);
	*bufferPtr = buffer;
	return STATUS_SUCCESS;
}


// Frees a cached file that is no longer in memory or in cache
void ntfs_file_free(ntfs_file_t *file) {
	if (file->record)
		free(file->record);
	if (file->i30)
		ntfs_index_tree_free(file->i30);
	free(file);
}


// Loads a file record (and index buffer in case it is a directory),
// stores it in cache and keeps track of how often it was opened.
status_t ntfs_open(ntfs_t *ntfs, uint64_t reference, ntfs_file_t **filePtr) {
	status_t status;
	*filePtr = NULL;
	uint64_t segment = reference & 0xFFFFFFFFFFFFUL;
	uint16_t sequenceNumber = reference >> 48;

	ntfs_file_t *file = ntfs->cache[segment & MFT_CACHE_SIZE_MASK];

	// if the cache location is occupied by another file, it is evicted.
	if (file) if (file->segment != segment) {
		if (!file->references)
			ntfs_file_free(file);
		ntfs->cache[segment & MFT_CACHE_SIZE_MASK] = file = NULL;
	}

	// load the file from disk if necessary and store in cache
	if (!file) {
		file = (ntfs_file_t *)calloc(sizeof(ntfs_file_t), 1);
		if (!file)
			return STATUS_OUT_OF_MEMORY;

		file->segment = segment;
		file->references = 0;

		// load MFT record
		file->record = (ntfs_file_record_t *)malloc(ntfs->bytesPerMftSegment);
		if ((status = ntfs_load_dataruns(ntfs,
			ntfs->mftData->extendedHeader.nonResidentHeader.startCluster,
			((char *)ntfs->mftData) + ntfs->mftData->extendedHeader.nonResidentHeader.runlistOffset, segment * ntfs->bytesPerMftSegment,
			ntfs->bytesPerMftSegment,
			(char *)(file->record))))
			return ntfs_file_free(file), status;
		if ((status = ntfs_fixup(ntfs, &(file->record->header), *(uint32_t *)"FILE")))
			return ntfs_file_free(file), status;
		if (sequenceNumber && (file->record->sequenceNumber != sequenceNumber))
			return ntfs_file_free(file), STATUS_DATA_CORRUPT;

		// ensure that this segment is in use
		if (!(file->record->flags & 1))
			return ntfs_file_free(file), STATUS_DATA_CORRUPT;

		// load index or find data attribute
		if (file->record->flags & 2) {
			if ((status = ntfs_load_index(ntfs, file, &directoryIndexName, &(file->i30))))
				return ntfs_file_free(file), status;
		} else {
			if (!(file->data = ntfs_find_attribute(file->record, NTFS_ATTRIBUTE_TYPE_DATA, NULL)))
				return ntfs_file_free(file), STATUS_DATA_CORRUPT;
		}


		ntfs->cache[segment & MFT_CACHE_SIZE_MASK] = file;
	}

	file->references++;
	DBG_FILE("file opened at %x64 ", (uint64_t)file);
	*filePtr = file;
	return STATUS_SUCCESS;
}


// Releases the resources associated with an open file. The memory is freed
// if the last reference to the file is closed and it was evicted from cache.
status_t ntfs_close(ntfs_t *ntfs, ntfs_file_t *file) {
	DBG_FILE("close file at %x64 ", (uint64_t)file);
	if (!--(file->references))
		if (file != ntfs->cache[file->segment & MFT_CACHE_SIZE_MASK])
			ntfs_file_free(file);
	return STATUS_SUCCESS;
}


// Returns the name of the file or directory.
// The buffer returned in the unicode string must be freed.
status_t ntfs_get_name(ntfs_t *ntfs, ntfs_file_t *file, unicode_t *name) {
	status_t status;
	name->data = NULL;

	// load file name attribute
	ntfs_attribute_t *attribute = ntfs_find_attribute(file->record, NTFS_ATTRIBUTE_TYPE_FILE_NAME, NULL);
	if (!attribute)
		return STATUS_DATA_CORRUPT;
	uint64_t fileNameSize = ntfs_get_attribute_size(attribute);
	ntfs_file_name_t *fileName = (ntfs_file_name_t *)malloc(fileNameSize);
	if (!fileName)
		return STATUS_OUT_OF_MEMORY;
	if ((status = ntfs_load_attribute(ntfs, attribute, 0, fileNameSize, (char *)fileName)))
		return free(fileName), status;

	// copy file name
	name->data = (wchar_t *)malloc(fileName->fileNameLength * sizeof(wchar_t));
	if (!(name->data))
		return free(fileName), STATUS_OUT_OF_MEMORY;
	name->length = fileName->fileNameLength;
	memcpy((char *)name->data, (char *)fileName->fileName, fileName->fileNameLength * sizeof(wchar_t));
	free(fileName);

	return STATUS_SUCCESS;
}


// Walks the B+ tree of the I30-index of the specified directory to find the file with the specified name.
status_t ntfs_get_child(ntfs_t *ntfs, ntfs_file_t *dir, unicode_t *name, uint64_t *childReference, uint64_t *childSize, int isDir) {
	isDir = (isDir ? 1 : 0);
	*childSize = 0;

	ntfs_index_entry_t *currentEntry = (ntfs_index_entry_t *)((char *)&(dir->i30->root->sequence) + dir->i30->root->sequence.sequenceOffset);
	uint64_t currentNode = -1L;

	int after;
	status_t status;

	do {
		// check last-entry flag
		after = (currentEntry->flags & 2);

		// if this is not the last entry, compare to requested entry
		if (!after) {
			ntfs_file_name_t *testAttr = (ntfs_file_name_t *)(currentEntry->stream);
			unicode_t testName = (unicode_t) { .length = testAttr->fileNameLength, .data = testAttr->fileName };
			// where do we bring in case sensitivity? the order is case insensitive even if the name space is case sensive (insensitive = (testAttr->nameSpace ? 1 : 0))
			int relation = unicode_compare(&testName, name, 1);
			if (!relation && !(isDir ^ ((testAttr->flags >> 28) & 1))) {
				*childReference = currentEntry->reference;
				*childSize = testAttr->realSize;
				return STATUS_SUCCESS;
			}
			if (relation > 0)
				after = 1;
		}

		if (!after) {
			currentEntry = (ntfs_index_entry_t *)((char *)currentEntry + currentEntry->length);
		} else {
			// switch to subnode if present, else move on to next index buffer (if this was the last entry)
			if (currentEntry->flags & 1)
				currentNode = *(((uint64_t *)((char *)currentEntry + currentEntry->length)) - 1);
			else if (currentEntry->flags & 2)
				currentNode++;
			else
				return STATUS_FILE_NOT_FOUND;

			// load new index buffer
			ntfs_index_buffer_t *subnode;
			status = ntfs_load_index_buffer(ntfs, dir->i30, &currentNode, &subnode);
			if (status != STATUS_END_OF_STREAM)
				after = 0;
			else if (status)
				return status;
			currentEntry = (ntfs_index_entry_t *)((char *)&(subnode->sequence) + subnode->sequence.sequenceOffset);
		}
	} while (!after);

	return STATUS_FILE_NOT_FOUND;
}


// Loads the specified portion of the file's data attribute.
status_t ntfs_read(ntfs_t *ntfs, ntfs_file_t *file, uint64_t offset, uint64_t count, char *buffer) {
	return ntfs_load_attribute(ntfs, file->data, offset, count, buffer);
}




// structure of the first sector of an NTFS volume
typedef struct __attribute__((__packed__))
{
	char reserved1[3];
	char fsType[8];
	uint16_t bytesPerSector;
	uint8_t sectorsPerCluster;
	char reserved2[34];
	uint64_t mftCluster;
	uint64_t mftMirCluster;
	int8_t clustersPerMftSegment;
	char reserved3[3];
	uint8_t clustersPerIndexBuffer;
} ntfs_vbr_t;


// Initializes an instance of an NTFS driver on an NFTS formatted volume.
// Returns NULL if the initialization fails or the volume is not NTFS formatted.
status_t ntfs_init(volume_t *volume, char *vbr, fs_t **fsPtr, uint64_t *rootReference) {
	status_t status;
	*fsPtr = NULL;


	ntfs_vbr_t *ntfsVbr = (ntfs_vbr_t *)vbr;
	if (memcmp(ntfsVbr->fsType, "NTFS    ", 8))
		return STATUS_INCOMPATIBLE;

	// clear memory (to fill cache with NULL)
	fs_t *fs = calloc(offsetof(fs_t, context) + sizeof(ntfs_t), 1);
	if (!fs) return STATUS_OUT_OF_MEMORY;

	// set up filesystem
	fs->open = (file_open_proc_t)ntfs_open;
	fs->close = (file_close_proc_t)ntfs_close;
	fs->getName = (file_get_name_proc_t)ntfs_get_name;
	fs->getChild = (file_get_child_proc_t)ntfs_get_child;
	fs->read = (file_read_proc_t)ntfs_read;
	ntfs_t *ntfs = (ntfs_t *)(fs->context);

	// load file system metrics
	ntfs->bytesPerSector = ntfsVbr->bytesPerSector;
	ntfs->bytesPerCluster = ntfs->bytesPerSector * (uint64_t)ntfsVbr->sectorsPerCluster;
	ntfs->bytesPerIndexBuffer = ntfs->bytesPerCluster * (uint64_t)ntfsVbr->clustersPerIndexBuffer;
	if (ntfsVbr->clustersPerMftSegment < 0)
		ntfs->bytesPerMftSegment = (1 << -(ntfsVbr->clustersPerMftSegment));
	else
		ntfs->bytesPerMftSegment = ntfs->bytesPerCluster * (uint64_t)ntfsVbr->clustersPerMftSegment;

	// load MFT
	ntfs->volume = volume;
	ntfs->mftSegment0 = (ntfs_file_record_t *)malloc(ntfs->bytesPerMftSegment);
	if (!ntfs->mftSegment0)
		return free(fs), STATUS_OUT_OF_MEMORY;
	if ((status = volume_read(volume, ntfsVbr->mftCluster * ntfs->bytesPerCluster, ntfs->bytesPerMftSegment, (char *)ntfs->mftSegment0)))
		return free(ntfs->mftSegment0), free(fs), STATUS_OUT_OF_MEMORY;
	if ((status = ntfs_fixup(ntfs, &(ntfs->mftSegment0->header), *(uint32_t *)"FILE")))
		return free(ntfs->mftSegment0), free(fs), status;
	ntfs->mftData = ntfs_find_attribute(ntfs->mftSegment0, NTFS_ATTRIBUTE_TYPE_DATA, NULL);
	if (!ntfs->mftData)
		return free(ntfs->mftSegment0), free(fs), STATUS_DATA_CORRUPT;

	// load unicode uppercase mapping (for file name comparision)
	// todo: load only if not already set up
	ntfs_file_t *upcase;
	wchar_t *mapping = (wchar_t *)malloc(1 << 17);
	if (!mapping)
		return free(ntfs->mftSegment0), free(fs), STATUS_OUT_OF_MEMORY;
	if ((status = ntfs_open(ntfs, 0xA, &upcase))) // $UpCase is always at 0xA
		return free(ntfs->mftSegment0), free(fs), free(mapping), status;
	status = ntfs_read(ntfs, upcase, 0, (1 << 17), (char *)mapping);
	ntfs_close(ntfs, upcase);
	if (status)
		return free(ntfs->mftSegment0), free(fs), free(mapping), status;
	unicode_set_uppercase(mapping);

	directoryIndexName = UNICODE("$I30");

	*fsPtr = fs;
	*rootReference = 0x5;
	return STATUS_SUCCESS;
}


driver_t ntfsDriver = { .initProc = (status_t(*)(void))ntfs_init };

// Registers the NTFS driver. Subsequent calls to fs_init will be able to initialize volumes that are NTFS formatted.
void ntfs_register(void) {
	driver_register(&ntfsDriver, DRIVER_TYPE_VOLUME);
}

