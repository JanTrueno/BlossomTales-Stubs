import dnfile
import collections

pe = dnfile.dnPE(r'C:\Users\Jblokstra\Downloads\btales\BlossomTales\Blossom Tales.exe')
md = pe.net.mdtables

print('=== AssemblyRefs ===')
ar_names = {}
for i, ar in enumerate(md.AssemblyRef.rows, 1):
    ver = getattr(ar, 'MajorVersion', None)
    vstr = f'v{ver}.{ar.MinorVersion}.{ar.BuildNumber}.{ar.RevisionNumber}' if ver is not None else ''
    ar_names[i] = str(ar.Name)
    print(f'{i}: {ar.Name} {vstr}')

print()
print('=== TypeRefs by assembly ===')
tre = collections.defaultdict(set)
for tr in md.TypeRef.rows:
    scope = tr.ResolutionScope
    if scope.table and scope.table.name == 'AssemblyRef':
        key = ar_names.get(scope.row_index, '?')
    else:
        key = scope.table.name if scope.table else '?'
    tre[key].add(f'{tr.TypeNamespace}.{tr.TypeName}')
for k in sorted(tre):
    print(f'--- {k} ({len(tre[k])})')
    for t in sorted(tre[k]):
        print('   ', t)

print()
print('=== TypeDef stats ===')
tds = list(md.TypeDef.rows)
print(f'TypeDefs: {len(tds)}')
shown = 0
for td in tds:
    if td.Namespace and not td.Namespace.startswith(('Microsoft', 'System')):
        print(f'   {td.Namespace}.{td.Name}')
        shown += 1
        if shown >= 40:
            print('   ...')
            break
