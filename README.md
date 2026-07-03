# Duplicate Finder Pro / یابنده‌ی فایل‌های تکراری حرفه‌ای

A polished, bilingual (فارسی / English) Windows desktop app that scans one or
more folders recursively and finds duplicate files using several independent
strategies — exact content, similar names, and visual (perceptual) matching for
images and videos. Built with **.NET 10 + WPF** and **Material Design**.

![stack](https://img.shields.io/badge/.NET-10-512BD4) ![ui](https://img.shields.io/badge/WPF-Material%20Design-4CAF50)

## ✨ Features

- **Four detection methods**, mix and match freely:
  | Method | What it catches |
  |---|---|
  | **Exact content (hash)** | Byte-for-byte identical files even with different names (size → head/tail quick-hash → full SHA-256 funnel). |
  | **File-name similarity** | `Movie.mkv`, `Movie (1).mkv`, `Movie - Copy.mkv` via normalized Levenshtein clustering. |
  | **Look-alike images** | Same picture in a different format/resolution/quality via 64-bit dHash + Hamming distance. |
  | **Look-alike videos** | The same movie under a different name/quality — samples frames with **ffmpeg** and compares perceptual signatures. |
- **Recursive by default** — point it at one root folder and it walks *every* sub-folder. Duplicates are matched **across the whole tree**, so the same movie/file sitting in two different folders is found.
- **Bilingual UI with live switching** (English LTR ↔ فارسی RTL) — no restart. English is the default.
- **Material Design** light/dark themes.
- **Drag-and-drop folders**, extension/size/hidden filters, adjustable similarity thresholds.
- **One-click ffmpeg** — the video method needs ffmpeg; if it's missing, click *Download ffmpeg automatically* in Settings and the app fetches ffmpeg + ffprobe into its own data folder.
- **Smart auto-select** which copy to keep: newest, oldest, largest, smallest, shortest path, or cleanest name.
- **Image previews** — hover any image row for a thumbnail; type icons per file.
- **Results search**, per-group quick-select, right-click menu (open, reveal, copy path).
- **Safe actions**: send to Recycle Bin, move to a folder, or permanently delete (with confirmation); freed-space accounting.
- **Export** results to CSV or JSON for auditing.
- **Remembers your settings** (language, theme, folders, thresholds, window size) between runs.
- Resilient scanning (skips inaccessible paths as warnings), cancellable, parallel hashing, live progress, **gentle mode** to keep the machine responsive.

## 🗂 Structure

```
DuplicateFinderPro.sln
├─ src/
│  ├─ DuplicateFinderPro.Core/   # engine: scanning, hashing, detectors, actions (no UI deps)
│  └─ DuplicateFinderPro.App/    # WPF + Material Design, MVVM, localization
└─ tests/
   └─ DuplicateFinderPro.Core.Tests/   # xUnit tests for the detection logic
```

The **Core** library is UI-agnostic and independently testable. Detection
strategies implement `IDuplicateDetector` and are orchestrated by
`DuplicateScanEngine`, which enumerates files once and feeds every enabled
detector.

## 🚀 Run

```bash
dotnet run --project src/DuplicateFinderPro.App
```

Requires the **.NET 10 SDK** on Windows. For the *look-alike videos* method you
need ffmpeg — the easiest way is the **Download ffmpeg automatically** button in
the Advanced ▸ Videos settings, which fetches ffmpeg + ffprobe into the app's
data folder. Alternatively install [ffmpeg](https://ffmpeg.org/) yourself and add
it to `PATH` or point to it in settings.

## 🧪 Test

```bash
dotnet test
```

## 📦 Downloads & automated builds

Every push/PR is built and tested by **GitHub Actions** ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)).

To cut a release, just push a tag — CI produces **both** a portable and an installer build and attaches them to a GitHub Release automatically ([`.github/workflows/release.yml`](.github/workflows/release.yml)):

```bash
git tag v1.0.0
git push origin v1.0.0
```

That produces two Windows x64 artifacts, **no .NET install required** (self-contained):

| Artifact | What it is |
|---|---|
| `DuplicateFinderPro-<ver>-portable-win-x64.zip` | A single self-contained `.exe` — unzip and run, nothing to install. |
| `DuplicateFinderPro-<ver>-Setup.exe` | A standard installer (Start-menu + optional desktop shortcut), built with Inno Setup ([`installer/DuplicateFinderPro.iss`](installer/DuplicateFinderPro.iss)). |

You can also trigger the release workflow manually from the **Actions** tab (`workflow_dispatch`) and type a version.

### Build the same artifacts locally

```bash
# Portable single-file exe
dotnet publish src/DuplicateFinderPro.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/portable

# Installer (requires Inno Setup 6)
dotnet publish src/DuplicateFinderPro.App -c Release -r win-x64 --self-contained true -o publish/app
ISCC installer/DuplicateFinderPro.iss
```

## 🔧 Notes & tuning

- **Name similarity threshold** (0.5–1.0): higher = stricter.
- **Image threshold** (0–32 Hamming bits): lower = stricter; 8 is a good default.
- **Video frame samples**: more frames = more accurate but slower.
- Exact-content detection is always safe to run first — it never reports a false positive.
- Perceptual methods are heuristic; always review before permanently deleting.
