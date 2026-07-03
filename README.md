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
- **Bilingual UI with live switching** (فارسی RTL ↔ English LTR) — no restart.
- **Material Design** light/dark themes.
- **Drag-and-drop folders**, extension/size/hidden filters, adjustable similarity thresholds.
- **Smart auto-select** which copy to keep: newest, oldest, largest, smallest, shortest path, or cleanest name.
- **Safe actions**: send to Recycle Bin, move to a folder, or permanently delete (with confirmation); freed-space accounting.
- **Export** results to CSV or JSON for auditing.
- Resilient scanning (skips inaccessible paths as warnings), cancellable, parallel hashing, live progress.

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

Requires the **.NET 10 SDK** on Windows. For the *look-alike videos* method,
install [ffmpeg](https://ffmpeg.org/) and either add it to `PATH` or set its
path in the Advanced settings (ffprobe is used for duration when present).

## 🧪 Test

```bash
dotnet test
```

## 🔧 Notes & tuning

- **Name similarity threshold** (0.5–1.0): higher = stricter.
- **Image threshold** (0–32 Hamming bits): lower = stricter; 8 is a good default.
- **Video frame samples**: more frames = more accurate but slower.
- Exact-content detection is always safe to run first — it never reports a false positive.
- Perceptual methods are heuristic; always review before permanently deleting.
