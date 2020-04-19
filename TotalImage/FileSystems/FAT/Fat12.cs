using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TotalImage.FileSystems.FAT
{
    public class Fat12 : FileSystem
    {
        private frmMain main;
        private Stream stream;
        private BiosParameterBlock bpb;
        public BiosParameterBlock BiosParameterBlock => bpb;
        private Directory rootDirectory;

        public override string Format => "FAT12";

        public override string VolumeLabel
        {
            get => bpb is BiosParameterBlock40 bpb40 && bpb40.BpbVersion == BiosParameterBlockVersion.Dos40 ? bpb40.VolumeLabel : "UNSUPPORTED";
            set => ChangeVolLabel(value);
        }

        public override Directory RootDirectory => rootDirectory;

        public override long AvailableFreeSpace => throw new NotImplementedException();

        public override long TotalFreeSpace => throw new NotImplementedException();

        public override long TotalSize => throw new NotImplementedException();

        protected Fat12()
        {
            main = (frmMain)Application.OpenForms["frmMain"];
        }

        public Fat12(Stream stream) : this()
        {
            this.stream = stream;
            bpb = Parse();
            rootDirectory = new FatRootDirectory(this);
        }

        public Stream GetStream()
        {
            return stream;
        }

        public BiosParameterBlock Parse()
        {
            var bpb = new BiosParameterBlock();
            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                stream.Seek(0x0B, SeekOrigin.Begin); //BPB offset

                bpb.BytesPerLogicalSector = reader.ReadUInt16();
                bpb.LogicalSectorsPerCluster = reader.ReadByte();
                bpb.ReservedLogicalSectors = reader.ReadUInt16();
                bpb.NumberOfFATs = reader.ReadByte();
                bpb.RootDirectoryEntries = reader.ReadUInt16();
                bpb.TotalLogicalSectors = reader.ReadUInt16();
                bpb.MediaDescriptor = reader.ReadByte();
                bpb.LogicalSectorsPerFAT = reader.ReadUInt16();
                bpb.PhysicalSectorsPerTrack = reader.ReadUInt16();
                bpb.NumberOfHeads = reader.ReadUInt16();
                bpb.HiddenSectors = reader.ReadUInt32();
                bpb.LargeTotalLogicalSectors = reader.ReadUInt32();

                /* At this point it's worth checking if there even is a valid BPB at the standard offset (0x0B).
                 * 
                 * If there isn't, then additional checks should be performed for the exotic disk formats that may have
                 * a BPB elsewhere (e.g. Apricot disks have it at 0x50...)
                 */
                if(bpb.NumberOfHeads == 0 || bpb.PhysicalSectorsPerTrack == 0 || bpb.BytesPerLogicalSector == 0)
                {
                    throw new Exception("Non-standard BPB");
                }
                if(bpb.TotalLogicalSectors / bpb.NumberOfHeads / bpb.PhysicalSectorsPerTrack != stream.Length / bpb.NumberOfHeads / bpb.PhysicalSectorsPerTrack / bpb.BytesPerLogicalSector)
                {
                    throw new Exception("Non-standard BPB");
                }

                // Try to read the BPB further as a DOS 4.0 BPB.
                var bpb40 = new BiosParameterBlock40(bpb);
                bpb40.PhysicalDriveNumber = reader.ReadByte();
                bpb40.Flags = reader.ReadByte();

                switch(reader.ReadByte())
                {
                    case 40:
                        bpb40.BpbVersion = BiosParameterBlockVersion.Dos34;
                        break;
                    case 41:
                        bpb40.BpbVersion = BiosParameterBlockVersion.Dos40;
                        break;
                    default:
                        return bpb; // it's not a DOS 4.0 BPB, don't bother any further
                }

                bpb40.VolumeSerialNumber = reader.ReadUInt32();

                if(bpb40.BpbVersion == BiosParameterBlockVersion.Dos40)
                {
                    bpb40.VolumeLabel = new string(reader.ReadChars(11));
                    bpb40.FileSystemType = new string(reader.ReadChars(8));
                }

                return bpb;
            }
        }

        //Formats a volume with FAT12 file system - currently assumes it's a floppy disk...
        public static Fat12 Create(Stream stream, BiosParameterBlock bpb, byte tracks)
        {
            var fat = new Fat12();
            fat.stream = stream;

            uint totalSize = (uint)stream.Length;
            uint rootDirSize = (uint)(bpb.RootDirectoryEntries << 5);
            uint fatSize = (uint)(bpb.LogicalSectorsPerFAT * bpb.BytesPerLogicalSector);
            uint fat1Offset = (uint)(bpb.ReservedLogicalSectors * bpb.BytesPerLogicalSector);
            uint fat2Offset = fat1Offset + fatSize;
            uint dataAreaOffset = fat2Offset + fatSize + rootDirSize;

            using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                stream.Seek(0, SeekOrigin.Begin);
                for (uint i = 0; i < totalSize; i++)
                {
                    writer.Write((byte)0);
                }

                //Fille the data area with 0xF6
                stream.Seek(dataAreaOffset, SeekOrigin.Begin);
                for (uint i = 0; i < totalSize - dataAreaOffset; i++)
                {
                    writer.Write((byte)0xF6);
                }

                stream.Seek(0, SeekOrigin.Begin);
                writer.Write(bpb.BootJump, 0, 3);
                writer.Write(bpb.OemId.PadRight(8, ' ').ToCharArray());
                writer.Write(bpb.BytesPerLogicalSector);
                writer.Write(bpb.LogicalSectorsPerCluster);
                writer.Write(bpb.ReservedLogicalSectors);
                writer.Write(bpb.NumberOfFATs);
                writer.Write(bpb.RootDirectoryEntries);
                writer.Write(bpb.TotalLogicalSectors);
                writer.Write(bpb.MediaDescriptor);
                writer.Write(bpb.LogicalSectorsPerFAT);
                writer.Write(bpb.PhysicalSectorsPerTrack);
                writer.Write(bpb.NumberOfHeads);
                writer.Write(bpb.HiddenSectors);
                writer.Write(bpb.LargeTotalLogicalSectors);

                //DOS 3.4+ specific values
                {
                    if (bpb is BiosParameterBlock40 bpb40)
                    {
                        writer.Write(bpb40.PhysicalDriveNumber);
                        writer.Write(bpb40.Flags);
                        
                        if (bpb.BpbVersion == BiosParameterBlockVersion.Dos34)
                            writer.Write((byte)40);
                        else if (bpb.BpbVersion == BiosParameterBlockVersion.Dos40)
                            writer.Write((byte)41);
                        else
                            throw new Exception("PANIIIIIIC");

                        writer.Write(bpb40.VolumeSerialNumber);

                        //DOS 4.0 adds volume label and FS type as well
                        if (bpb40.BpbVersion == BiosParameterBlockVersion.Dos40)
                        {
                            if (string.IsNullOrEmpty(bpb40.VolumeLabel))
                                writer.Write("NO NAME    ".ToCharArray());
                            else
                                writer.Write(bpb40.VolumeLabel.PadRight(11, ' ').ToCharArray());
                            writer.Write(bpb40.FileSystemType.PadRight(8, ' ').ToCharArray());
                        }
                    }
                }

                //Boot signature
                stream.Seek(0x1FE, SeekOrigin.Begin);
                writer.Write((byte)0x55);
                writer.Write((byte)0xAA);

                /* Media descriptor needs to be written to each FAT as well
                 * It takes up the first cluster entry (0), and the second entry (1) is also reserved */
                stream.Seek(fat1Offset, SeekOrigin.Begin);
                writer.Write(bpb.MediaDescriptor);
                writer.Write((byte)0xFF);
                writer.Write((byte)0xFF);
                stream.Seek(fat2Offset, SeekOrigin.Begin);
                writer.Write(bpb.MediaDescriptor);
                writer.Write((byte)0xFF);
                writer.Write((byte)0xFF);

                //Volume label needs to be written to the root directory as well
                stream.Seek(fat2Offset + fatSize, SeekOrigin.Begin);

                //First 11 bytes (8.3 space-padded filename without the period) are the label itself
                {
                    if(bpb is BiosParameterBlock40 bpb40 && !string.IsNullOrEmpty(bpb40.VolumeLabel))
                    {
                        writer.Write(bpb40.VolumeLabel.PadRight(11, ' ').ToCharArray());
                        writer.Write((byte)0x08); //Volume label attribute
                    }
                }
            }

            fat.bpb = fat.Parse();
            fat.rootDirectory = new FatRootDirectory(fat);
            return fat;
        }

        //Reads the root directory entries and any subdirectories and adds them to the treeview
        public void ReadRootDir()
        {
            uint rootDirOffset = (uint)(bpb.BytesPerLogicalSector + (bpb.BytesPerLogicalSector * bpb.LogicalSectorsPerFAT * 2));
            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                stream.Seek(rootDirOffset, SeekOrigin.Begin);

                //Read the entries top to bottom
                for (int i = 0; i < bpb.RootDirectoryEntries; i++)
                {
                    stream.Seek(rootDirOffset + i * 0x20, SeekOrigin.Begin);
                    if (reader.ReadByte() == 0x00)
                    {
                        break; //Empty entry, no entries after this one
                    }

                    stream.Seek(-0x01, SeekOrigin.Current);
                    DirectoryEntry entry = new DirectoryEntry
                    {
                        name = Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd(' ').ToUpper() + "." +
                               Encoding.ASCII.GetString(reader.ReadBytes(3)).TrimEnd(' ').ToUpper(),
                        attr = reader.ReadByte(),
                        ntRes = reader.ReadByte(),
                        crtTimeTenth = reader.ReadByte(),
                        crtTime = reader.ReadUInt16(),
                        crtDate = reader.ReadUInt16(),
                        lstAccDate = reader.ReadUInt16(),
                        fstClusHI = reader.ReadUInt16(),
                        wrtTime = reader.ReadUInt16(),
                        wrtDate = reader.ReadUInt16(),
                        fstClusLO = reader.ReadUInt16(),
                        fileSize = reader.ReadUInt32()
                    };

                    //Ignore deleted entries for now
                    if (entry.name[0] != 0xE5)
                    {
                        //Skip hidden, LFN and volume label entries for now
                        if (Convert.ToBoolean(entry.attr & 0x02) || Convert.ToBoolean(entry.attr & 0x08))
                        {
                            continue;
                        }

                        //Folder entry
                        if (Convert.ToBoolean(entry.attr & 0x10))
                        {
                            main.AddToRootDir(entry);
                            main.AddToFileList(entry);
                            ReadSubdir(entry);
                        }
                        //File entry
                        else if (!Convert.ToBoolean(entry.attr & 0x10))
                        {
                            main.AddToFileList(entry);
                        }
                    }
                }
            }
        }

        //Reads the subdirectory entries and adds subdirectories to the treeview
        public void ReadSubdir(DirectoryEntry parent)
        {
            uint dataAreaOffset = (uint)(bpb.BytesPerLogicalSector + (bpb.BytesPerLogicalSector * bpb.LogicalSectorsPerFAT * 2) + 
                (bpb.RootDirectoryEntries << 5));
            ushort fat1Offset = bpb.BytesPerLogicalSector;

            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                uint cluster = (((uint)parent.fstClusHI) << 16) + parent.fstClusLO;

                do
                {
                    uint clusterOffset = (cluster - 2) * bpb.LogicalSectorsPerCluster * bpb.BytesPerLogicalSector;

                    //No. of entries that fit in one cluster = BPS * SPC / 32 bytes per entry
                    for (int i = 0; i < (bpb.LogicalSectorsPerCluster * bpb.BytesPerLogicalSector / 32); i++)
                    {
                        stream.Seek(dataAreaOffset + clusterOffset + (i * 32), SeekOrigin.Begin);
                        byte firstByte = reader.ReadByte();
                        if (firstByte == 0x00)
                        {
                            break; //Directory is empty, go up one level and onto the next
                        }
                        if(firstByte == 0x2E)
                        {
                            continue; //Ignore . and .. entries
                        }

                        stream.Seek(-0x01, SeekOrigin.Current);
                        DirectoryEntry entry = new DirectoryEntry
                        {
                            name = Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd(' ').ToUpper() + "." +
                                   Encoding.ASCII.GetString(reader.ReadBytes(3)).TrimEnd(' ').ToUpper(),
                            attr = reader.ReadByte(),
                            ntRes = reader.ReadByte(),
                            crtTimeTenth = reader.ReadByte(),
                            crtTime = reader.ReadUInt16(),
                            crtDate = reader.ReadUInt16(),
                            lstAccDate = reader.ReadUInt16(),
                            fstClusHI = reader.ReadUInt16(),
                            wrtTime = reader.ReadUInt16(),
                            wrtDate = reader.ReadUInt16(),
                            fstClusLO = reader.ReadUInt16(),
                            fileSize = reader.ReadUInt32()
                        };

                        //Ignore deleted entries for now
                        if (entry.name[0] != (char)0xE5)
                        {
                            //Skip hidden, LFN and volume label entries for now
                            if (Convert.ToBoolean(entry.attr & 0x02) || Convert.ToBoolean(entry.attr & 0x08))
                            {
                                continue;
                            }

                            //Folder entry
                            if (Convert.ToBoolean(entry.attr & 0x10))
                            {
                                main.AddToDir(parent, entry);
                                ReadSubdir(entry);
                            }
                        }
                    }
                    if(cluster % 2 == 0)
                    {
                        stream.Seek(fat1Offset + (ushort)(cluster * 1.5), SeekOrigin.Begin);
                        ushort lower8 = reader.ReadByte();
                        ushort upper4 = (ushort)((reader.ReadByte() & 0x0F) << 8);
                        cluster = (ushort)(upper4 + lower8);
                    }
                    else
                    {
                        stream.Seek(fat1Offset + (ushort)Math.Floor(cluster * 1.5), SeekOrigin.Begin);
                        ushort lower4 = (ushort)(reader.ReadByte() >> 4);
                        ushort upper8 = (ushort)(reader.ReadByte() << 4);
                        cluster = (ushort)(upper8 + lower4);
                    }
                }
                while (cluster <= 0x0FEF);
            }
        }

        //Lists the contents of the directory without traversing subdirectories and adding them to the treeview
        public void ListDir(DirectoryEntry parent)
        {
            uint dirSize = 0;
            ushort fat1Offset = bpb.BytesPerLogicalSector;
            ushort fatSize = (ushort)(bpb.BytesPerLogicalSector * bpb.LogicalSectorsPerFAT);
            uint rootDirOffset = (uint)(fat1Offset + (fatSize * 2));
            uint dataAreaOffset = (uint)(rootDirOffset + (bpb.RootDirectoryEntries << 5));

            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                uint cluster = ((uint)parent.fstClusHI << 16) + parent.fstClusLO;
                do
                {
                    uint clusterOffset = (cluster - 2) * bpb.LogicalSectorsPerCluster * bpb.BytesPerLogicalSector;

                    //No. of entries that fit in one cluster = BPS * SPC / 32 bytes per entry
                    for (int i = 0; i < (bpb.LogicalSectorsPerCluster * bpb.BytesPerLogicalSector / 32); i++)
                    {
                        stream.Seek(dataAreaOffset + clusterOffset + (i * 32), SeekOrigin.Begin);
                        byte[] filename = reader.ReadBytes(8);
                        if (filename[0] == 0x00)
                        {
                            break; //No entries will follow after this one, stop reading
                        }
                        if (Encoding.ASCII.GetString(filename).TrimEnd(' ') == ".")
                        {
                            continue; //Ignore the root directory (".") entry
                        }

                        stream.Seek(-0x08, SeekOrigin.Current);
                        DirectoryEntry entry = new DirectoryEntry
                        {
                            name = Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd(' ').ToUpper() + "." +
                                   Encoding.ASCII.GetString(reader.ReadBytes(3)).TrimEnd(' ').ToUpper(),
                            attr = reader.ReadByte(),
                            ntRes = reader.ReadByte(),
                            crtTimeTenth = reader.ReadByte(),
                            crtTime = reader.ReadUInt16(),
                            crtDate = reader.ReadUInt16(),
                            lstAccDate = reader.ReadUInt16(),
                            fstClusHI = reader.ReadUInt16(),
                            wrtTime = reader.ReadUInt16(),
                            wrtDate = reader.ReadUInt16(),
                            fstClusLO = reader.ReadUInt16(),
                            fileSize = reader.ReadUInt32()
                        };

                        //Ignore deleted entries for now
                        if (entry.name[0] != (char)0xE5)
                        {
                            //Skip hidden, LFN and volume label entries for now
                            if (Convert.ToBoolean(entry.attr & 0x02) || Convert.ToBoolean(entry.attr & 0x08))
                            {
                                continue;
                            }

                            dirSize += entry.fileSize;
                            main.AddToFileList(entry);
                        }
                    }
                    if (cluster % 2 == 0)
                    {
                        stream.Seek(fat1Offset + (ushort)(cluster * 1.5), SeekOrigin.Begin);
                        ushort lower8 = reader.ReadByte();
                        ushort upper4 = (ushort)((reader.ReadByte() & 0x0F) << 8);
                        cluster = (ushort)(upper4 + lower8);
                    }
                    else
                    {
                        stream.Seek(fat1Offset + (ushort)Math.Floor(cluster * 1.5), SeekOrigin.Begin);
                        ushort lower4 = (ushort)(reader.ReadByte() >> 4);
                        ushort upper8 = (ushort)(reader.ReadByte() << 4);
                        cluster = (ushort)(upper8 + lower4);
                    }
                }
                while (cluster <= 0x0FEF);
            }
        }

        //Lists the contents of the root directory without traversing subdirectories and adding them to the treeview
        public void ListRootDir()
        {
            uint rootDirOffset = (uint)(bpb.BytesPerLogicalSector + (bpb.BytesPerLogicalSector * bpb.LogicalSectorsPerFAT * 2));

            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                stream.Seek(rootDirOffset, SeekOrigin.Begin);

                //Read the entries top to bottom
                for (int i = 0; i < bpb.RootDirectoryEntries; i++)
                {
                    stream.Seek(rootDirOffset + i * 0x20, SeekOrigin.Begin);
                    if (reader.ReadByte() == 0x00)
                    {
                        break; //Empty entry, no entries after this one
                    }

                    stream.Seek(-0x01, SeekOrigin.Current);
                    DirectoryEntry entry = new DirectoryEntry
                    {
                        name = Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd(' ').ToUpper() + "." +
                               Encoding.ASCII.GetString(reader.ReadBytes(3)).TrimEnd(' ').ToUpper(),
                        attr = reader.ReadByte(),
                        ntRes = reader.ReadByte(),
                        crtTimeTenth = reader.ReadByte(),
                        crtTime = reader.ReadUInt16(),
                        crtDate = reader.ReadUInt16(),
                        lstAccDate = reader.ReadUInt16(),
                        fstClusHI = reader.ReadUInt16(),
                        wrtTime = reader.ReadUInt16(),
                        wrtDate = reader.ReadUInt16(),
                        fstClusLO = reader.ReadUInt16(),
                        fileSize = reader.ReadUInt32()
                    };

                    //Ignore deleted entries for now
                    if (entry.name[0] != (char)0xE5)
                    {
                        //Skip hidden, LFN and volume label entries for now
                        if (Convert.ToBoolean(entry.attr & 0x02) || Convert.ToBoolean(entry.attr & 0x08))
                        {
                            continue;
                        }

                        main.AddToFileList(entry);
                    }
                }
            }
        }

        /* Changes the volume label
         * 
         * If BPB version <= 3.31, only the root dir label is changed
         * If BPB version >= 3.40, both the root dir and BPB label are changed
         */
        public void ChangeVolLabel(string label)
        {
            uint rootDirOffset = (uint)(bpb.BytesPerLogicalSector + (bpb.BytesPerLogicalSector * bpb.LogicalSectorsPerFAT * 2));
            bool rootDirFull = false;

            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                stream.Seek(rootDirOffset, SeekOrigin.Begin);

                for (int i = 0; i < bpb.RootDirectoryEntries; i++)
                {
                    stream.Seek(rootDirOffset + i * 0x20, SeekOrigin.Begin);
                    if (reader.ReadByte() == 0x00)
                    {
                        //Root dir is empty, so let's add the first entry
                        stream.Seek(-0x01, SeekOrigin.Current);
                        writer.Write(label.ToCharArray());
                        writer.Write((byte)0x08); //Volume label attribute

                        break;
                    }

                    /* Root directory is not empty, we need to find the volume label, as it may not be the first entry or it may not exist at all
                     * 
                     * Currently, this code will only look for empty entries after the last entry in the directory (first byte 0x00), and then give up.
                     * What it should do if no free entries after the last one exist, is to go through the directory again and search for any deleted
                     * entries (first byte 0xE5) that can be overwritten and use the first one it finds. The reaseon these deleted entries are not
                     * used immediately when found is to facilitate the UNDELETE functionality.
                     */
                    stream.Seek(-0x01, SeekOrigin.Current);
                    DirectoryEntry entry = new DirectoryEntry
                    {
                        name = Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd(' ').ToUpper() + "." +
                               Encoding.ASCII.GetString(reader.ReadBytes(3)).TrimEnd(' ').ToUpper(),
                        attr = reader.ReadByte(),
                        ntRes = reader.ReadByte(),
                        crtTimeTenth = reader.ReadByte(),
                        crtTime = reader.ReadUInt16(),
                        crtDate = reader.ReadUInt16(),
                        lstAccDate = reader.ReadUInt16(),
                        fstClusHI = reader.ReadUInt16(),
                        wrtTime = reader.ReadUInt16(),
                        wrtDate = reader.ReadUInt16(),
                        fstClusLO = reader.ReadUInt16(),
                        fileSize = reader.ReadUInt32()
                    };

                    if(entry.attr == 0x08)
                    {
                        stream.Seek(-0x20, SeekOrigin.Current);
                        writer.Write(label.ToCharArray());
                        writer.Write(0x08);
                        break;
                    }

                    if(i == bpb.RootDirectoryEntries - 1)
                    {
                        rootDirFull = true;
                    }
                }

                if (bpb is BiosParameterBlock40 && bpb.BpbVersion == BiosParameterBlockVersion.Dos40)
                {
                    stream.Seek(0x2B, SeekOrigin.Begin);
                    writer.Write(label.ToCharArray());
                }

                if (rootDirFull)
                {
                    throw new Exception("Root directory is full, volume label cannot be written");
                }
            }
        }
    }
}