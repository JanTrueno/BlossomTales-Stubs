import dnfile

pe = dnfile.dnPE(r'C:\Users\Jblokstra\Downloads\btales\BlossomTales\Blossom Tales.exe')
md = pe.net.mdtables

# coded index helpers
def decode_cidx(v):
    tag = v & 3
    return (v >> 2, tag)

def type_ref_or_def(cidx):
    idx, tag = decode_cidx(cidx)
    if tag == 0:
        r = md.TypeDef.rows[idx - 1]
        return f'{r.TypeNamespace}.{r.TypeName}'
    elif tag == 1:
        r = md.TypeRef.rows[idx - 1]
        return f'{r.TypeNamespace}.{r.TypeName}'
    elif tag == 2:
        r = md.TypeSpec.rows[idx - 1]
        return f'typespec#{idx}'
    return '?'

class BlobReader:
    def __init__(self, data):
        self.data = data
        self.pos = 0
    def u8(self):
        b = self.data[self.pos]; self.pos += 1; return b
    def u16(self):
        v = int.from_bytes(self.data[self.pos:self.pos+2], 'little'); self.pos += 2; return v
    def compressed(self):
        b = self.u8()
        if (b & 0x80) == 0:
            return b
        if (b & 0xC0) == 0x80:
            return ((b & 0x3F) << 8) | self.u8()
        return ((b & 0x1F) << 24) | (self.u8() << 16) | (self.u8() << 8) | self.u8()

ET = {0x01:'void',0x02:'bool',0x03:'char',0x04:'sbyte',0x05:'byte',0x06:'short',0x07:'ushort',
      0x08:'int',0x09:'uint',0x0a:'long',0x0b:'ulong',0x0c:'float',0x0d:'double',0x0e:'string',
      0x0f:'ptr',0x10:'ref',0x11:'valuetype',0x12:'class',0x13:'var',0x14:'array',0x15:'genericinst',
      0x16:'typedbyref',0x18:'nativeint',0x19:'nativeuint',0x1b:'fnptr',0x1c:'object',0x1d:'szarray',
      0x1f:'mvar',0x1e:'pinned'}

def read_type(br, seen_var=None):
    e = br.u8()
    while e in (0x10, 0x0f, 0x1e, 0x41, 0x45):  # byref/ptr/pinned/cmod opt/req
        if e == 0x41 or e == 0x45:
            br.compressed()
        return ('ref' if e == 0x10 else ('ptr' if e == 0x0f else '')) + read_type(br)
    if e == 0x1d:
        return read_type(br) + '[]'
    if e == 0x11 or e == 0x12:
        return ET[e] + ':' + type_ref_or_def(br.compressed())
    if e == 0x15:
        inner = read_type(br)
        n = br.compressed()
        args = [read_type(br) for _ in range(n)]
        return f'{inner}<{",".join(args)}>'
    if e == 0x14:
        t = read_type(br)
        br.compressed()
        n = br.compressed()
        for _ in range(n):
            br.compressed()
        return t + '[...]'
    if e in (0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b,
             0x0c, 0x0d, 0x0e, 0x16, 0x18, 0x19, 0x1b, 0x1c):
        return ET.get(e, '?')
    if e == 0x13 or e == 0x1f:
        return '!' + str(br.compressed())
    return f'ET0x{e:02x}'

def member_ref_sig(row):
    sig_idx = row.struct.Signature_BlobIndex
    blob, size = row._blobs.get_with_size(sig_idx)
    br = BlobReader(bytes(blob[:size]))
    cc = br.u8() & 0x0F
    if cc == 0x6:  # FIELD sig
        return 'field:' + read_type(br)
    n = br.compressed()
    ret = read_type(br)
    params = [read_type(br) for _ in range(n)]
    return f'{ret} ({", ".join(params)})'

# resolve MemberRef class
for row in md.MemberRef.rows:
    cls = row.Class
    name = str(row.Name)
    parent = None
    if cls.table and cls.table.name == 'TypeRef':
        parent = f'{cls.row.TypeNamespace}.{cls.row.TypeName}'
    else:
        continue
    if parent.startswith('SlimDX') or parent.startswith('Steamworks'):
        try:
            sig = member_ref_sig(row)
        except Exception as ex:
            sig = f'<parse-error {ex}>'
        print(f'{parent} :: {name} :: {sig}')
