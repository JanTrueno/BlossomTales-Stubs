# Blossom Tales (The Sleeping King) on FNA

Run the XNA 4.0 PC build of *Blossom Tales* on [FNA](https://github.com/FNA-XNA/FNA) without the XNA redistributable — on Windows and on Linux. The game binary is never modified in-place (the token patch is applied to a copy) and the game runs through FNA's ABI stubs.

## What's here

- `scripts/patch_tokens.py` — Python version of the exe patch.
- `scripts/patcher/` — C# port (`dotnet build`, then `dotnet patchexe.dll <game.exe>`). Writes `<game>.patched.exe`; the original exe is never modified. Required on .NET Framework, which rejects unsigned ABI stubs (Mono ignores this anyway).
- `scripts/bt_analyze.py` — dumps the game exe's AssemblyRefs / XNA TypeRefs / method usage (analysis helper).
- `scripts/dump_memberrefs.py` — dumps exact member signatures the game expects from SlimDX/Steamworks (used to build the shims below).
- `stubs/SlimDXStub.cs` — pure-managed replacement for `SlimDX.DirectInput.dll` (the real one is C++/CLI, unloadable on Mono/Linux). Returns an empty device list; safe because the game's XNA GamePad path (SDL-backed under FNA) takes precedence — see analysis below.
- `stubs/SteamworksStub.cs` — pure-managed replacement for `Steamworks.NET.dll` (`SteamAPI.Init()` → false; all stats/cloud calls no-op). Avoids the native `steam_api` dependency on Linux.
- `linux/run.sh` — launches the game under your Mono runtime with WSLg.

## The game (facts established during analysis)

- `BlossomTales.exe` is an **Enigma Virtual Box** single-file bundle; extract with `evbunpack --legacy-fs`.
- Real exe: .NET Framework 4.0, x86, `Blossom Tales v1.0.0.17`, not obfuscated, ships with a PDB.
- References exactly: `Microsoft.Xna.Framework`, `.Game`, `.Graphics`, `.Xact` (v4.0.0.0), `SlimDX.DirectInput` (4.0.10.43), `Steamworks.NET` (7.0.0.0), WinForms.
- XNA assemblies are *not* bundled — they come from the XNA 4.0 redist GAC, so app-dir replacements bind cleanly.
- Content: standard XNB sprites + 2 SpriteFonts + 1 compiled Effect, XACT3 banks, Tiled `.tmx` maps parsed via `XmlDocument` (no custom XNB readers).
- Steam: plain Steamworks (achievements/cloud), all guarded by `IsSteamInitialized` — no Steam DRM; runs fine without Steam.
- SlimDX is used *only* in `DInput.cs` for DirectInput joystick enumeration and is short-circuited whenever the XNA GamePad path is active (`DInput.update()` returns immediately if `Input.UsingController`).

## Windows setup

1. Extract the game (`pip install evbunpack`, then `python -m evbunpack --legacy-fs BlossomTales.exe game\`).
2. Into the game folder drop your FNA ABI libs (`FNA.dll`, `FNA.dll.config`, `FNA.NetStub.dll`, the 4 `Microsoft.Xna.Framework*.dll` stubs) plus x86 natives: `SDL3.dll` (official SDL release), `FNA3D.dll`, `FAudio.dll` (FNA fnalibs).
3. Patch a copy of the exe: `dotnet run --project scripts/patcher -- <game.exe>` (produces `Blossom Tales.patched.exe`; keep the original). .NET Framework requires this. Keep `SlimDX.DirectInput.dll`, `Steamworks.NET.dll`, `steam_api.dll` as-is on Windows.
4. Launch the patched exe. No XNA redist needed.

## Linux setup

Same managed stack, plus:

- Your Mono runtime (any recent 6.x is fine).
- Swap natives for `libSDL3.so.0`, `libFNA3D.so.0`, `libFAudio.so.0` (fnalibs lib64).
- Replace `SlimDX.DirectInput.dll` and `Steamworks.NET.dll` with the managed stubs from `stubs/` (compile with csc; versions must match 4.0.10.43 / 7.0.0.0 — the `AssemblyVersion` attributes are already set in the sources).
- `MONO_BIN=/path/to/mono sh linux/run.sh` — see the env vars at the top of the script.

Known caveats: `SDL_AUDIODRIVER=dummy` (silent) until libpulse/libjack are available; Mono's WinForms dialogs are only used by the built-in level editor; saves go to FNA's StorageDevice path (no XNA device dialog).

## Legal

Scripts/stubs in this repo are written for personal compatibility use. The game itself remains the property of its rightsholders; this repo contains no game files.
