using System;
using System.IO;
using System.Linq;

// Removes the public key token from the XNA AssemblyRefs of an XNA game exe,
// so unsigned FNA ABI stub assemblies bind on .NET Framework.
// Original file is backed up as <name>.orig.exe.
// Usage: patchexe <path-to-game.exe>

internal static class Program
{
    private static byte[] _data;
    private static int _peOff;
    private static bool _pe32Plus;
    private static int _numSections;
    private static int _sectionTableOff;

    private struct Section
    {
        public uint VirtualAddress;
        public uint VirtualSize;
        public uint RawSize;
        public uint RawOffset;
    }

    private static Section[] _sections;

    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: patchexe <game.exe>");
            return 1;
        }
        string path = Path.GetFullPath(args[0]);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine("File not found: " + path);
            return 1;
        }

        _data = File.ReadAllBytes(path);

        ParsePe();
        int comRva = ReadDataDirectory(14);
        if (comRva == 0)
        {
            Console.Error.WriteLine("No CLI header (not a .NET assembly?).");
            return 1;
        }

        int cliOff = RvaToOffset(comRva);
        int metaRva = (int)BitConverter.ToUInt32(_data, cliOff + 8);
        int metaOff = RvaToOffset(metaRva);
        (int tablesOff, int tablesSize, int stringsOff, int stringsSize, bool stringIndex4, bool blobIndex4) = ParseMetadataRoot(metaOff);

        int asmRefOff = FindTableOffset(tablesOff, tablesSize, 0x23, out uint rowCount);
        if (asmRefOff < 0)
        {
            Console.Error.WriteLine("AssemblyRef table not found.");
            return 1;
        }
        Console.WriteLine($"asmRefOff=0x{asmRefOff:X} rows={rowCount} stringsIndex4={stringIndex4} blobIndex4={blobIndex4}");

        int patched = 0;
        for (uint i = 0; i < rowCount; i++)
        {
            int rowOff = asmRefOff + (int)i * 20;
            int nameIdx = ReadIndex(rowOff + 12 + (blobIndex4 ? 4 : 2), stringIndex4);
            if (nameIdx <= 0 || nameIdx >= stringsSize)
            {
                continue;
            }
            string name = ReadAscii(stringsOff + nameIdx);
            if (name.StartsWith("Microsoft.Xna", StringComparison.Ordinal))
            {
                WriteUInt32(rowOff + 8, 0);   // Flags: clear PublicKey flag
                if (blobIndex4)
                {
                    WriteUInt32(rowOff + 12, 0); // PublicKeyOrToken: empty blob
                }
                else
                {
                    WriteUInt16(rowOff + 12, 0);
                }
                patched++;
                Console.WriteLine("patched: " + name);
            }
        }

        if (patched == 0)
        {
            Console.WriteLine("No XNA AssemblyRefs found; nothing to do.");
            return 0;
        }

        string outPath = Path.Combine(
            Path.GetDirectoryName(path),
            Path.GetFileNameWithoutExtension(path) + ".patched.exe");
        File.WriteAllBytes(outPath, _data);
        Console.WriteLine("wrote: " + outPath + " (" + patched + " references patched; original untouched)");
        return 0;
    }

    private static void ParsePe()
    {
        _peOff = BitConverter.ToInt32(_data, 0x3C);
        if (_data[_peOff] != 'P' || _data[_peOff + 1] != 'E')
        {
            throw new InvalidDataException("Bad PE signature");
        }
        _numSections = BitConverter.ToUInt16(_data, _peOff + 6);
        int optOff = _peOff + 24;
        ushort magic = BitConverter.ToUInt16(_data, optOff);
        _pe32Plus = magic == 0x20B;
        int optSize = BitConverter.ToUInt16(_data, _peOff + 20);
        _sectionTableOff = optOff + optSize;

        _sections = new Section[_numSections];
        for (int i = 0; i < _numSections; i++)
        {
            int s = _sectionTableOff + i * 40;
            _sections[i] = new Section
            {
                VirtualSize = BitConverter.ToUInt32(_data, s + 8),
                VirtualAddress = BitConverter.ToUInt32(_data, s + 12),
                RawSize = BitConverter.ToUInt32(_data, s + 16),
                RawOffset = BitConverter.ToUInt32(_data, s + 20),
            };
        }
    }

    private static int ReadDataDirectory(int index)
    {
        int optOff = _peOff + 24;
        int dirOff = optOff + (_pe32Plus ? 112 : 96) + index * 8;
        return (int)BitConverter.ToUInt32(_data, dirOff); // RVA
    }

    private static int RvaToOffset(int rva)
    {
        foreach (Section s in _sections)
        {
            uint size = Math.Max(s.VirtualSize, s.RawSize);
            if (rva >= s.VirtualAddress && rva < s.VirtualAddress + size)
            {
                return (int)(s.RawOffset + (uint)rva - s.VirtualAddress);
            }
        }
        throw new InvalidDataException($"RVA 0x{rva:X} not inside any section");
    }

    private static (int, int, int, int, bool, bool) ParseMetadataRoot(int off)
    {
        if (BitConverter.ToUInt32(_data, off) != 0x424A5342)
        {
            throw new InvalidDataException("Bad metadata signature");
        }
        int p = off + 4;
        p += 2 + 2 + 4; // major, minor, reserved
        int verLen = (int)BitConverter.ToUInt32(_data, p);
        p += 4;
        p += verLen; // version string
        p += 2; // flags
        int streams = BitConverter.ToUInt16(_data, p);
        p += 2;

        int tablesOff = -1, tablesSize = 0, stringsOff = -1, stringsSize = 0;
        bool stringIndex4 = false, blobIndex4 = false;

        for (int i = 0; i < streams; i++)
        {
            int streamOffset = (int)BitConverter.ToUInt32(_data, p);
            int streamSize = (int)BitConverter.ToUInt32(_data, p + 4);
            p += 8;
            string name = ReadAscii(p);
            p += (name.Length + 1 + 3) & ~3;

            if (name == "#~" || name == "#-")
            {
                tablesOff = off + streamOffset;
                tablesSize = streamSize;
                // heap sizes byte is at tablesOff + 6
                byte heapSizes = _data[tablesOff + 6];
                stringIndex4 = (heapSizes & 0x01) != 0;
                blobIndex4 = (heapSizes & 0x04) != 0;
            }
            else if (name == "#Strings")
            {
                stringsOff = off + streamOffset;
                stringsSize = streamSize;
            }
        }

        if (tablesOff < 0 || stringsOff < 0)
        {
            throw new InvalidDataException("#~ or #Strings stream missing");
        }
        return (tablesOff, tablesSize, stringsOff, stringsSize, stringIndex4, blobIndex4);
    }

    // Row sizes (bytes) for tables 0x00..0x23 (2-byte indexes, ECMA-335 II.22).
    private static readonly int[] RowSizes =
    {
        10, 6, 14, 4, 6, 4, 14, 4, 6, 4, 6, 6, 8, 4, 6, 8, 4, 2, 4, 4, 6, 4, 4, 6,
        6, 6, 4, 2, 8, 6, 8, 8, 22, 4, 12, 20,
    };

    private static int FindTableOffset(int tablesOff, int tablesSize, int tableId, out uint rowCount)
    {
        byte[] valid = new byte[8];
        Array.Copy(_data, tablesOff + 8, valid, 0, 8);
        int rowsPos = tablesOff + 24;
        uint[] rows = new uint[64];
        int offset = rowsPos;
        for (int t = 0; t < 64; t++)
        {
            if ((valid[t / 8] & (1 << (t % 8))) != 0)
            {
                rows[t] = BitConverter.ToUInt32(_data, offset);
                offset += 4;
            }
        }
        if (rows[tableId] == 0)
        {
            rowCount = 0;
            return -1;
        }
        int tableOff = offset;
        for (int t = 0; t < tableId; t++)
        {
            if (rows[t] > 0)
            {
                tableOff += (int)(rows[t] * (uint)RowSizes[t]);
            }
        }
        rowCount = rows[tableId];
        return tableOff;
    }

    private static int ReadIndex(int off, bool is4Bytes)
    {
        return is4Bytes ? (int)BitConverter.ToUInt32(_data, off) : BitConverter.ToUInt16(_data, off);
    }

    private static string ReadAscii(int off)
    {
        int end = off;
        while (end < _data.Length && _data[end] != 0)
        {
            end++;
        }
        return System.Text.Encoding.ASCII.GetString(_data, off, end - off);
    }

    private static void WriteUInt32(int off, uint value)
    {
        _data[off] = (byte)value;
        _data[off + 1] = (byte)(value >> 8);
        _data[off + 2] = (byte)(value >> 16);
        _data[off + 3] = (byte)(value >> 24);
    }

    private static void WriteUInt16(int off, ushort value)
    {
        _data[off] = (byte)value;
        _data[off + 1] = (byte)(value >> 8);
    }
}
