using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static System.IO.Path;

namespace TotalImage.FileSystems.FAT
{
    public class FatRootDirectory : Directory
    {
        Fat12 fat;

        public FatRootDirectory(Fat12 fat) 
        {
            this.fat = fat;
        }

        public override IEnumerable<FileSystemObject> EnumerateFileSystemObjects()
        {
            var rootDirOffset = (uint)(fat.BiosParameterBlock.BytesPerLogicalSector + (fat.BiosParameterBlock.BytesPerLogicalSector * fat.BiosParameterBlock.LogicalSectorsPerFAT * 2));
            var stream = fat.GetStream();
            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                stream.Seek(rootDirOffset, SeekOrigin.Begin);

                //Read the entries top to bottom
                for (int i = 0; i < fat.BiosParameterBlock.RootDirectoryEntries; i++)
                {
                    stream.Seek(rootDirOffset + i * 0x20, SeekOrigin.Begin);
                    if (reader.ReadByte() == 0x00)
                    {
                        break; //Empty entry, no entries after this one
                    }

                    stream.Seek(-0x01, SeekOrigin.Current);
                    var entry = DirectoryEntry.Parse(reader.ReadBytes(32));

                    //Ignore deleted entries for now
                    if (entry.name[0] != 0xE5)
                    {
                        //Skip hidden, LFN and volume label entries for now
                        if (Convert.ToBoolean(entry.attr & 0x02) || entry.attr == 0x0F || Convert.ToBoolean(entry.attr & 0x08))
                        {
                            continue;
                        }

                        //Folder entry
                        if (Convert.ToBoolean(entry.attr & 0x10))
                        {
                            yield return new FatDirectory(fat, entry, this, this);
                        }
                        //File entry
                        else if (!Convert.ToBoolean(entry.attr & 0x10))
                        {
                            yield return new FatFile(fat, entry, this);
                        }
                    }
                }
            }
        }

        public override Directory CreateSubdirectory(string path)
        {
            throw new NotImplementedException();
        }

        public override void Delete()
            => throw new InvalidOperationException();

        public override void MoveTo(string path)
            => throw new InvalidOperationException();

        public override string Name
        {
            get => string.Empty;
            set => throw new InvalidOperationException();
        }

        public override Directory Root => this;
        public override Directory Parent => null;

        public override FileAttributes Attributes
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override DateTime LastAccessTime
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override DateTime LastWriteTime
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override DateTime CreationTime
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override long Length
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }
}