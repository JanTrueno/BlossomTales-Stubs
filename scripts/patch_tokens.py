import dnfile
import struct

SRC = r'C:\Users\Jblokstra\Downloads\btales\BlossomTales\Blossom Tales.exe'
ORIG = r'C:\Users\Jblokstra\Downloads\btales\BlossomTales\Blossom Tales.orig.exe'

pe = dnfile.dnPE(ORIG)
md = pe.net.mdtables
ar = md.tables[35]
print('row_size:', ar.row_size, 'num_rows:', ar.num_rows, 'file_offset:', hex(ar.file_offset))
assert ar.row_size == 20, 'unexpected AssemblyRef row size'

targets = []
for i, row in enumerate(ar.rows):
    name = str(row.Name)
    if name.startswith('Microsoft.Xna'):
        targets.append((i, name))

print('targets:', targets)

with open(ORIG, 'rb') as f:
    data = bytearray(f.read())

for i, name in targets:
    off = ar.file_offset + i * 20
    struct.pack_into('<I', data, off + 8, 0x0)   # Flags = 0 (no PublicKey)
    struct.pack_into('<H', data, off + 12, 0x0)  # PublicKeyOrToken blob index = 0 (empty)

with open(SRC, 'wb') as f:
    f.write(data)
print('patched and written')

pe.close()

pe2 = dnfile.dnPE(SRC)
for row in pe2.net.mdtables.AssemblyRef.rows:
    if str(row.Name).startswith('Microsoft.Xna'):
        print(row.Name, 'flags=0x%x' % row.struct.Flags, 'blob=', row.PublicKey.size)
