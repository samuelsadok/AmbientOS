using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmbientOS.Environment;
using AmbientOS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbientOS.FileSystem.NTFS
{
    partial class NTFSService
    {
        public class NTFSTest
        {
            public enum TestFlags
            {
                IgnoreLogFile = 1,

                /// <summary>
                /// If there are differences, they are ignored if, when read as an 8-byte aligned date value,
                /// they result in a valid date in the year 2XXX and differ by less than 100 days.
                /// </summary>
                IgnoreDates = 2
            }

            /// <summary>
            /// Compares two NTFS attributes and outputs the index of every index where there is a difference.
            /// The output indices are 8-byte aligned.
            /// If they have different length, only the common part is compared.
            /// </summary>
            private static IEnumerable<long> Diff(NTFSAttribute attr1, NTFSAttribute attr2, TestFlags options)
            {
                const long BUFFER_SIZE = 16777216;
                long offset = 0;
                var size = Math.Min(attr1.GetSize(), attr2.GetSize());
                var buffer1 = new byte[Math.Min(size, BUFFER_SIZE)];
                var buffer2 = new byte[Math.Min(size, BUFFER_SIZE)];

                while (size > 0) {
                    var blockSize = Math.Min(size, BUFFER_SIZE);
                    attr1.Read(offset, blockSize, buffer1, 0);
                    attr2.Read(offset, blockSize, buffer2, 0);

                    for (int i = 0; i < blockSize - 7; i += 8) {
                        var val1 = buffer1.ReadInt64(i, Endianness.LittleEndian);
                        var val2 = buffer2.ReadInt64(i, Endianness.LittleEndian);
                        if (val1 != val2) {
                            if (options.HasFlag(TestFlags.IgnoreDates)) {
                                long i2 = i; var date1 = buffer1.ReadDateTime(ref i2, DateFormat.NTFS, Endianness.LittleEndian);
                                i2 = i; var date2 = buffer1.ReadDateTime(ref i2, DateFormat.NTFS, Endianness.LittleEndian);
                                if (date1.Year / 1000 == 2 && date2.Year / 1000 == 2 && (date1 - date2).Duration().TotalDays < 100)
                                    continue;
                            }

                            yield return i;
                        }
                    }

                    var pos = (blockSize / 8) * 8;
                    for (long i = pos; i < blockSize; i++) {
                        if (buffer1[i] != buffer2[i]) {
                            yield return pos;
                            break;
                        }
                    }

                    offset += blockSize;
                    size -= blockSize;
                }
            }

            /// <summary>
            /// Compares two volumes by comparing all non-resident attributes of all files that exist in both volumes.
            /// This effectively finds all differences in allocated disk space, since the MFT is its own non-resident data stream.
            /// </summary>
            private static IEnumerable<Tuple<NTFSAttribute, long>> Diff(NTFSVolume volume1, NTFSVolume volume2, TestFlags options)
            {
                var bitmap1 = volume1.MFT.File.FileRecord.ReadAttribute(NTFSAttributeType.Bitmap, "");
                var bitmap2 = volume2.MFT.File.FileRecord.ReadAttribute(NTFSAttributeType.Bitmap, "");
                var recordCount1 = Math.Min(volume1.MFT.File.Data.GetSize() / volume1.bytesPerMFTRecord, bitmap1.Length * 8);
                var recordCount2 = Math.Min(volume2.MFT.File.Data.GetSize() / volume2.bytesPerMFTRecord, bitmap2.Length * 8);

                for (long i = 0; i < Math.Min(recordCount1, recordCount2); i++) {
                    if (((bitmap1[i / 8] >> (byte)(i % 8)) & 1) == 0 || ((bitmap1[i / 8] >> (byte)(i % 8)) & 1) == 0)
                        continue;

                    if (i == 2)
                        if (options.HasFlag(TestFlags.IgnoreLogFile))
                            continue;

                    var attrList1 = volume1.MFT.GetFile(i, null).FileRecord.GetAttributes(NTFSAttributeType.DontCare, null).Where(a => !a.Resident).ToArray();
                    var attrList2 = volume2.MFT.GetFile(i, null).FileRecord.GetAttributes(NTFSAttributeType.DontCare, null).Where(a => !a.Resident).ToArray();

                    foreach (var attr1 in attrList1) {
                        var attr2 = attrList2.FirstOrDefault(a => a.type == attr1.type && a.name == attr1.name);
                        if (attr2 == null)
                            continue;

                        foreach (var index in Diff(attr1, attr2, options))
                            yield return new Tuple<NTFSAttribute, long>(attr1, index);
                    }
                }
            }



            public static void DoTest(IFile vhd, Context context)
            {
                context.Log.Log("loading temp folder");
                var destination = context.Environment.GetTempFolder();

                // mount test VHD using AmbientOS services and the NTFS driver we want to test
                context.Log.Log("copying test VHD");
                var tstVHD = vhd.Copy(destination, "vhd - test.vhd", MergeMode.Evict);
                context.Log.Log("mounting test file system");
                var tstVol = ObjectStore.Action<IFile, IVolume>(tstVHD, context);
                if (tstVol == null)
                    throw new Exception("The test disk must contain one single volume");
                var tstFS = new NTFSService().Mount(tstVol, context).AsSingle();

                context.Log.Log("running test on test file system");
                TestFS(tstFS, true, false);

                // mount reference VHD using Windows services (i.e. native Windows NTFS driver)
                context.Log.Log("copying reference VHD");
                var refVHD = vhd.Copy(destination, "vhd - reference.vhd", MergeMode.Evict);
                context.Log.Log("mounting reference disk");
                var refDisk = new WindowsVHDService().Mount(refVHD, false, context).AsSingle();
                if (refDisk == null)
                    throw new Exception("The reference VHD must contain one single disk");
                context.Log.Log("mounting reference volume");
                var refVol = new WindowsDiskService().Mount(refDisk, context).AsSingle();
                if (refVol == null)
                    throw new Exception("The reference disk must contain one single volume");
                context.Log.Log("mounting reference file system");
                var refFS = new WindowsVolumeService().Mount(refVol, context).AsSingle();
                if (refFS == null)
                    throw new Exception("The reference volume must contain one single file system");

                context.Log.Log("running test on reference file system");
                TestFS(refFS, false, false);

                context.Log.Log("all tests passed");
            }





            public static void TestFolderCreateDeleteSimple(IFolder folder, string childName)
            {
                if (folder.GetChildren().Any())
                    throw new InternalTestFailureException("folder must be empty");

                var newFileA = folder.GetChild(childName + ".txt", true, OpenMode.New) as IFile;
                var newFolderA = folder.GetChild(childName, false, OpenMode.New) as IFolder;

                Assert.IsNotNull(newFileA);
                Assert.IsNotNull(newFolderA);

                var newFileB = folder.GetChild(childName + ".txt", true, OpenMode.Existing) as IFile;
                var newFolderB = folder.GetChild(childName, false, OpenMode.Existing) as IFolder;

                Assert.AreEqual(newFileA, newFileB);
                Assert.AreEqual(newFolderA, newFolderB);

                var content = folder.GetChildren().ToArray();
                Assert.IsTrue(content.Contains(newFileA));
                Assert.IsTrue(content.Contains(newFolderA));
                Assert.AreEqual(content.Count(), 2);

                newFileA.Delete(DeleteMode.Permanent);
                newFolderA.Delete(DeleteMode.Permanent);

                content = folder.GetChildren().ToArray();
                Assert.AreEqual(content.Count(), 0);
            }

            public static void TestFolderCreateDeleteSimpleEasy(IFolder folder)
            {
                TestFolderCreateDeleteSimple(folder, "abc");
                TestFolderCreateDeleteSimple(folder, "abc");
            }

            public static void TestFolderCreateDeleteSimpleHardNames(IFolder folder)
            {
                if (folder.GetFileSystem().GetNamingConventions().Complies("°+`\"*ç%&/()=?"))
                    TestFolderCreateDeleteSimple(folder, "°+`\"*ç%&/()=?");
                if (folder.GetFileSystem().GetNamingConventions().Complies(""))
                    TestFolderCreateDeleteSimple(folder, "");
                if (folder.GetFileSystem().GetNamingConventions().Complies(" "))
                    TestFolderCreateDeleteSimple(folder, " ");
                if (folder.GetFileSystem().GetNamingConventions().Complies(".."))
                    TestFolderCreateDeleteSimple(folder, "..");

                // todo: use information of filesystem on what names and lengths are allowed

                var maxVal = 65535;
                var veryMeanName = new string(Enumerable.Range(maxVal - 250, 250).Select(i => Convert.ToChar(i)).ToArray());
                if (folder.GetFileSystem().GetNamingConventions().Complies(veryMeanName))
                    TestFolderCreateDeleteSimple(folder, veryMeanName);
            }

            public static void TestFolderCreateDeleteSequence(IFolder folder, string[] names, int[] createSequence, int[] deleteSequence)
            {
                if (names.Count() != createSequence.Count())
                    throw new InternalTestFailureException();
                //if (createSequence.Count() != deleteSequence.Count())
                //    throw new InternalTestFailureException();

                var shadowCopy = new List<string>();
                var content = new List<IFileSystemObject>();

                foreach (var i in createSequence) {
                    shadowCopy.Add(names[i]);
                    content.Add(folder.GetChild(names[i], false, OpenMode.New));
                }

                foreach (var name in shadowCopy)
                    Assert.IsTrue(folder.ChildExists(name, false));
                Assert.AreEqual(folder.GetChildren().Count(), shadowCopy.Count());

                foreach (var i in deleteSequence) {
                    shadowCopy.Remove(names[i]);
                    folder.GetChild(names[i], false, OpenMode.Existing).Delete(DeleteMode.Permanent);
                }

                foreach (var name in shadowCopy)
                    Assert.IsTrue(folder.ChildExists(name, false));
                Assert.AreEqual(folder.GetChildren().Count(), shadowCopy.Count());
            }

            public static void TestFolder(IFolder folder, bool skipMeanNames, int depth)
            {
                while (depth-- > 0) {
                    // test basic folder operations
                    TestFolderCreateDeleteSimpleEasy(folder);
                    if (!skipMeanNames)
                        TestFolderCreateDeleteSimpleHardNames(folder);

                    // test different create/delete patterns
                    var namingConventions = folder.GetFileSystem().GetNamingConventions();
                    var alternativeSet = skipMeanNames || !namingConventions.Complies("") || !namingConventions.Complies(" ") || !namingConventions.CaseSensitive;
                    var names = new string[] { alternativeSet ? "b" : "", alternativeSet ? "-" : " ", "1", "2", "3", "abc", "a", "ab", "abcdef", alternativeSet ? "Abcd" : "A", alternativeSet ? "Ba" : "Ab", alternativeSet ? "bca" : "aB", "ac" };
                    var createSequence = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
                    var deleteSequence = new int[] { 8, 4, 6, 7, 2, 3, 0, 11, 10, 1, 12, 5, 9 };
                    TestFolderCreateDeleteSequence(folder, names, createSequence, deleteSequence);

                    // test a large number of children
                    var numbers = Enumerable.Range(0, 1025).ToArray();
                    TestFolderCreateDeleteSequence(folder, numbers.Select(i => i.ToString()).ToArray(), numbers.Reverse().ToArray(), numbers.Skip(65).ToArray());

                    // test subfolder
                    folder = folder.GetChild("34", false, OpenMode.Existing) as IFolder;
                }
            }

            public static void TestFS(IFileSystem fs, bool skipMeanNames, bool cleanUp)
            {
                var testFolder = fs.GetRoot().NavigateToFolder("test", OpenMode.NewOrExisting); // todo: change to "new"

                //TestFolder(testFolder, skipMeanNames, 3);

                var testFile = testFolder.GetFile("test.txt", OpenMode.Existing);
                var buf = new byte[] { 0x45, 0x46, 0x47 };
                testFile.Write(2, buf.Count(), buf, 0);

                if (cleanUp)
                    testFolder.Delete(DeleteMode.Permanent);
            }
        }

        public void Test(IFile vhd, Context context)
        {
            NTFSTest.DoTest(vhd, context);
        }
    }
}
