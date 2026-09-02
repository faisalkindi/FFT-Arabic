# Pipeline notes (for contributors)

Working notes, not needed to play. Everything player-facing is in the README and the Releases page.

The full translation workspace and tooling live at `fft-arabic-pilot/pilot/` (glossary, editor prompts, wrap/box-fit calibration, batch scan tools, build scripts). This repo holds the installer source and the store texts; the built mod files ship via GitHub Releases / Nexus.

## What ships

| File | Where it goes | What it is |
|---|---|---|
| `dinput8.dll` | game root | FFT: The Ivalice Chronicles Mod Loader (Nenkai) |
| `data/enhanced/0004.en.pac` | `data\enhanced\` | translated text tables + font-type config, riding the English slot |
| `data/enhanced/0007.pac` | `data\enhanced\` | fonts incl. the cinematic-subtitle font |
| `data/enhanced/0008.pac` | `data\enhanced\` | main content pac (textures incl. Arabic title logo) |

Installer (`installer/`, .NET 8 WinForms): Steam AppID 1004640 auto-detect, backs every replaced file up with an `.arabic_backup` suffix, uninstall restores the backups. Blocks while `FFT_enhanced`/`FFT_classic` run.

## Engine facts (Square Enix "Faith" / FF16 engine — first Arabic shipped on it)

- No RTL/bidi tag exists in the engine's format-tag set, so **shaping + bidi are baked at inject time** (`tools/arabic_game_text.py::shape_for_game`): inline tags and printf specs are stashed into PUA codepoints (0xE000+), text passes through ArabicReshaper (harakat kept) + bidi `get_display()`, tags restored after; quotes normalized to «» pre-shaping. The TM/glossary stay logical Unicode.
- Fonts are SDF bmfont atlases rebuilt with FF16FontMaker. Key root cause: FontMaker never scales `xOffset` (only additive) while everything else is ×4 fixed-point — every glyph's fine placement was ~4× too small and cursive joins broke. Fix: pre-multiply each glyph's `xoffset` by 4 in the `.fnt`, then `CustomXadvance=4.0`. Locked UI config: 24px SDF atlas, `CustomYoffset=-88`, `CustomMultiYoffset=4.3`.
- Cinematic subtitles use their own font (Likurei) with separate locked params (`3.0/3.0`), rebuilt into `0007.pac`.
- The engine line-breaker char-wraps baked Arabic and splits cursive runs, so all long text is **pre-wrapped at build time** with per-table width budgets and line balancing (`tools/apply_tables_wrap.py`).
- Tools: [FF16Tools](https://github.com/Nenkai/FF16Tools) (pac/nxd), FF16FontMaker, the Mod Loader `dinput8.dll`.

## Rebuilding the installer

`installer/payload/` (gitignored, 158 MB) must contain the four shipped files above, zipped as `payload.zip` next to the csproj, then:

```
cd installer && dotnet publish -c Release -o publish
```

Requires the .NET 8 SDK. The published exe needs the .NET 8 Desktop Runtime on the player's machine.
