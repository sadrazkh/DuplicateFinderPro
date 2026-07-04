<div align="center">

# 🔍 Duplicate Finder Pro

**A modern, bilingual (English / فارسی) Windows desktop app that finds duplicate files by _content_, _name_ and _visual similarity_ — including the same movie or photo saved under different names, formats or qualities.**

Built with **.NET 10 + WPF** and **Material Design**, with a clean, testable engine at its core.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF%20%2B%20Material%20Design-00897B)
![Platform](https://img.shields.io/badge/OS-Windows%2010%2F11-0078D6?logo=windows&logoColor=white)
![Languages](https://img.shields.io/badge/i18n-English%20%2F%20فارسی-2E7D32)
![License](https://img.shields.io/badge/license-MIT-green)

</div>

---

## 📖 فارسی — معرفی کوتاه

یک نرم‌افزار ویندوزی برای پیدا کردن **فایل‌های تکراری**. یک پوشه‌ی ریشه را می‌دهی و برنامه **همه‌ی زیرپوشه‌ها** را می‌گردد و تکراری‌ها را **در کل درخت** پیدا می‌کند — حتی اگر یک فیلم/عکس با **نام، فرمت یا کیفیت متفاوت** ذخیره شده باشد. چهار روش تشخیص دارد (محتوای دقیق، شباهت نام، تصویر هم‌شکل، ویدیوی هم‌شکل)، رابط کاربری **دوزبانه (RTL/LTR)** و **Material Design**، دانلود **خودکار ffmpeg** برای بخش ویدیو، و عملیات امن (سطل بازیافت / انتقال / حذف). راهنمای کامل در ادامه (انگلیسی).

---

## 📑 Table of contents

- [Features](#-features)
- [How the detection works](#-how-the-detection-works)
- [Install & run](#-install--run)
- [Usage guide](#-usage-guide)
- [Tuning the Advanced options](#-tuning-the-advanced-options)
- [ffmpeg (for video matching)](#-ffmpeg-for-video-matching)
- [Where settings are stored](#-where-settings-are-stored)
- [Project structure](#-project-structure)
- [Build from source](#-build-from-source)
- [Automated builds & releases (CI/CD)](#-automated-builds--releases-cicd)
- [Testing](#-testing)
- [Add a new language](#-add-a-new-language)
- [Safety notes](#-safety-notes)
- [Troubleshooting / FAQ](#-troubleshooting--faq)
- [Tech stack & credits](#-tech-stack--credits)
- [License](#-license)

---

## ✨ Features

- **Recursive by default** — point it at one root folder and it walks **every** sub-folder. Duplicates are matched **across the whole tree**, so the same file sitting in two different folders is still found.
- **Four detection methods**, mix and match freely (see [below](#-how-the-detection-works)):
  - Exact content (hash) · File-name similarity · Look-alike images · Look-alike videos.
- **Photo Cleanup mode** — analyses your images and flags **blurry**, **dark**, **overexposed**, **low-resolution** and **screenshot** photos, so tidying a phone gallery takes seconds. Each photo shows a thumbnail, a keep-worthiness score and its issues, with copyable paths and one-click Recycle/Move/Delete.
- **Real previews everywhere** — inline thumbnails in every list, an enlarged preview on hover, and for videos a **frame grabbed from the middle of the film** so you can tell what it is at a glance.
- **Statistics dashboard** — an attractive tab with overview cards and bar charts: size by file type, reclaimable space by method, photo-quality breakdown, and your largest duplicate groups.
- **Modern Material Design UI** with a teal→green theme, light/dark toggle, tabbed layout and readable typography.
- **Fully bilingual, live switching** — English (LTR) ⇄ فارسی (RTL) with no restart. English is the default.
- **Drag-and-drop folders**, plus filters for extensions, min/max size, and hidden/system files.
- **One-click ffmpeg** — the video method needs ffmpeg; if it's missing, the app can **download it automatically** into its own data folder.
- **Smart auto-select** which copy to keep: newest, oldest, largest, smallest, shortest path, or cleanest name — then act on the rest in one go.
- **Image thumbnails on hover**, file-type icons, per-group quick-select, and a right-click menu (open, reveal, copy path).
- **Search box** to filter large result sets instantly.
- **Safe actions**: send to Recycle Bin, move to a folder, or permanently delete — always with a confirmation and freed-space accounting.
- **Export** the full result set to **CSV** or **JSON** for auditing.
- **Remembers everything** between runs (language, theme, folders, thresholds, window size).
- **Gentle mode** keeps the machine responsive during heavy media scans (low priority, capped concurrency).
- **Resilient & cancellable** — inaccessible paths become warnings instead of crashes; long scans can be cancelled anytime.

---

## 🧠 How the detection works

Each method is an independent strategy (`IDuplicateDetector`) orchestrated by `DuplicateScanEngine`, which enumerates the files **once** and feeds every enabled detector. Turn on as many as you like.

### 1. Exact content (hash) — _byte-for-byte identical_
A three-stage funnel keeps it both **fast** and **exact**:
1. Group files by **size** (different sizes can't be identical).
2. Within each size group, compute a cheap **head + tail quick-hash** to split obvious non-matches without reading whole files.
3. Only for survivors, compute a full **SHA-256** and confirm.

> ✔ Never produces a false positive — matches are truly identical, even with different names.
> Great first pass for any folder.

### 2. File-name similarity — _renamed copies_
Names are **normalized** (lower-cased, copy markers like `Copy`, `(1)`, `- Copy` stripped, punctuation flattened) and compared with a **normalized Levenshtein ratio**. Matches above your threshold are clustered with a union-find structure.

> ✔ Finds `Movie.mkv`, `Movie (1).mkv`, `Movie - Copy.mkv`.
> ⚠ Purely name-based — different files with similar names can land together, so review before deleting.

### 3. Look-alike images — _same picture, different file_
Each image is reduced to a 64-bit **perceptual hash (dHash)**; images within a small **Hamming distance** are grouped. This survives resizing, re-compression and format changes.

> ✔ Catches the same photo saved as JPG vs PNG, or at a different resolution/quality.
> ⚠ A loose threshold can group merely-similar images — tune it (lower = stricter).

### 4. Look-alike videos — _the same movie, differently encoded_
Uses **ffmpeg** to sample frames and reduce each to a perceptual signature, then matches videos whose frame signatures overlap.

- **Samples random points across the _whole_ runtime** (start, middle **and** end), not just the edges — so two different films that share an intro/recap aren't merged by accident.
- The random positions use a **seed shared across one scan**, so they differ run-to-run but stay identical for every file in the same scan — which keeps two copies of the same movie **aligned** and matchable.
- Extraction is **downscaled, single-threaded and low-priority** by default so a big library scan stays civil.

> ✔ Finds the same movie under a different name, container or quality.
> ⚠ Requires ffmpeg and is the heaviest method (CPU + time).

### Photo Cleanup — _is this photo worth keeping?_
A separate pass (tick **Photo quality analysis**) scores every image and flags the junk that clutters phone galleries:
- **Blurry** — via the variance of the Laplacian (low = out of focus).
- **Dark** / **Overexposed** — mean luminance too low/high.
- **Low-resolution** — tiny images (thumbnails/junk).
- **Screenshot** — recognised by filename and common screen resolutions.

Results land in the **Photo Cleanup** tab with a thumbnail, a 0–100 keep score and the detected issues. Filter to *only flagged*, auto-select them all, and Recycle/Move/Delete.

> ⚠ Quality is subjective — the score is a helper, not a verdict. Review (the thumbnails make it quick) before deleting.

---

## 📦 Install & run

### Option A — Download a release (recommended, no .NET needed)
From the repository's **Releases** page, grab one of the **self-contained Windows x64** builds:

| Download | What it is |
|---|---|
| `DuplicateFinderPro-<ver>-portable-win-x64.zip` | **Portable** — a single `.exe`. Unzip and run, nothing to install. |
| `DuplicateFinderPro-<ver>-Setup.exe` | **Installer** — Start-menu entry + optional desktop shortcut, standard uninstall. |

Both bundle the .NET runtime, so no separate install is required.

### Option B — Run from source
Requires the **.NET 10 SDK** on Windows:

```bash
dotnet run --project src/DuplicateFinderPro.App
```

---

## 🖱 Usage guide

1. **Add folders** — drag folders onto the drop zone, or click **Add folder**. You can add several roots.
2. **Choose detection methods** — tick any combination of the four methods. Exact content is on by default and always safe.
3. **(Optional) Filters** — recurse into sub-folders (on by default), include hidden/system files, and set min/max size or an extension allow/ignore list.
4. **(Optional) Advanced** — each method's fine-tuning appears here and **activates only when its method is ticked** (see [tuning](#-tuning-the-advanced-options)).
5. **Start scan** — watch live progress; cancel anytime.
6. **Review results** — each duplicate group is an expandable card showing every copy, its folder, size and date. Hover an image row for a **thumbnail**. Use the **search box** to filter.
7. **Select what to remove** — pick a *Keep by* rule (newest, largest, cleanest name, …) and click **Auto-select**, or tick rows manually. Per-group quick-select buttons are in each group header.
8. **Act** — **Move to folder**, **Recycle Bin**, or **Delete permanently** (with confirmation). The status bar shows how many files and how much space are selected.
9. **Export** — save the full report as **CSV** or **JSON**.

Right-click any file for **Open**, **Open file location**, **Copy full path**, **Copy folder path**.

---

## 🎛 Tuning the Advanced options

Each sub-section is **greyed out until you tick its method** in *Detection methods*, and shows a short **pro/con** so you know what it does.

| Option | Meaning | Guidance |
|---|---|---|
| **Name similarity threshold** | How similar names must be to group (0.5–1.0). | Higher = stricter. 0.85 is a good start. |
| **Image similarity threshold** | Max Hamming distance between image fingerprints (0–32). | Lower = stricter. 8 is a good default. |
| **Sample frames per video** | How many frames to fingerprint per video. | More = more accurate but slower. |
| **Skip from start / end (%)** | Trims intros/credits from the sampling window. | Helps avoid matching on shared logos/recaps. |
| **Gentle mode** | Low priority + capped concurrency + single-threaded ffmpeg. | Leave on unless you want maximum speed. |

---

## 🎞 ffmpeg (for video matching)

The **Look-alike videos** method needs `ffmpeg` (and `ffprobe`). Easiest path:

- In **Advanced ▸ Videos**, click **Download ffmpeg automatically**. The app downloads a current Windows build and extracts `ffmpeg.exe` + `ffprobe.exe` into its data folder, then uses it automatically.

Or provide your own:

- Install [ffmpeg](https://ffmpeg.org/) and either add it to your `PATH`, or paste the path to `ffmpeg.exe` into the settings field. `ffprobe` next to it is used to read durations.

If ffmpeg isn't available, the video method is simply skipped (with a warning) — the other methods still run.

---

## 💾 Where settings are stored

Everything you configure is saved to:

```
%AppData%\DuplicateFinderPro\settings.json
```

and reloaded on the next launch (language, theme, folders, methods, thresholds, keep-rule, window size). A downloaded ffmpeg lives in `%AppData%\DuplicateFinderPro\ffmpeg\`.

---

## 🗂 Project structure

```
DuplicateFinderPro.sln
├─ src/
│  ├─ DuplicateFinderPro.Core/        # UI-agnostic engine (unit-tested)
│  │  ├─ Models/                      # FileItem, DuplicateGroup, ScanOptions, ScanResult, …
│  │  ├─ Services/
│  │  │  ├─ FileScanner.cs            # resilient recursive enumeration + filters
│  │  │  ├─ ContentHasher.cs          # quick-hash + SHA-256
│  │  │  ├─ Detectors/                # the four IDuplicateDetector strategies
│  │  │  ├─ FfmpegVideoHasher.cs      # frame sampling + perceptual signatures
│  │  │  ├─ DuplicateScanEngine.cs    # orchestrates enumeration + detectors
│  │  │  ├─ DuplicateSelector.cs      # "keep by" rules
│  │  │  ├─ FileActionService.cs      # recycle / delete / move
│  │  │  └─ ReportExporter.cs         # CSV / JSON
│  │  └─ Utils/                       # StringSimilarity, UnionFind, PerceptualHasher, …
│  └─ DuplicateFinderPro.App/         # WPF + Material Design (MVVM)
│     ├─ ViewModels/                  # MainViewModel + item VMs
│     ├─ Views/MainWindow.xaml        # the UI
│     ├─ Localization/                # Strings (fa/en) + live Loc engine
│     ├─ Services/                    # dialogs, theme, settings, ffmpeg installer
│     └─ Converters/                  # value converters (bytes, thumbnails, …)
├─ tests/DuplicateFinderPro.Core.Tests/   # xUnit tests for the engine
├─ installer/DuplicateFinderPro.iss       # Inno Setup script
└─ .github/workflows/                      # ci.yml + release.yml
```

The **Core** library has no UI dependencies, so the detection logic is fully unit-testable and reusable.

---

## 🛠 Build from source

```bash
# Restore, build, test
dotnet build -c Release
dotnet test  -c Release

# Portable single-file exe (self-contained)
dotnet publish src/DuplicateFinderPro.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/portable

# Installer input (self-contained folder), then compile with Inno Setup 6
dotnet publish src/DuplicateFinderPro.App -c Release -r win-x64 --self-contained true -o publish/app
ISCC installer/DuplicateFinderPro.iss
```

A `global.json` pins the SDK to .NET 10 for reproducible builds.

---

## 🚀 Automated builds & releases (CI/CD)

- **CI** — every push/PR is built and tested on `windows-latest` ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)).
- **Release** — pushing a tag builds **both** artifacts and attaches them to a GitHub Release ([`.github/workflows/release.yml`](.github/workflows/release.yml)):

```bash
git push
git tag v1.0.0
git push origin v1.0.0
```

You can also run the release workflow manually from the **Actions** tab (`workflow_dispatch`) and type a version.

> **Two one-time GitHub settings for releases to work:**
> 1. **Settings → Actions → General → Workflow permissions → Read and write** (so the workflow can create the Release and upload files).
> 2. Make sure the **tag points at a commit that already contains the workflow** — if you changed the workflow, push first, then (re-)create the tag.

---

## 🧪 Testing

```bash
dotnet test
```

The xUnit suite covers the engine: exact-content matching (including **across deeply nested folders**), same-size-but-different-content rejection, name-similarity clustering, name normalization, keep-rule selection and byte formatting.

---

## 🌍 Add a new language

1. Open [`src/DuplicateFinderPro.App/Localization/Strings.cs`](src/DuplicateFinderPro.App/Localization/Strings.cs) and add a new dictionary alongside `Fa` and `En` (same keys).
2. Register it in `Localization.Apply(...)` and add the enum value in `AppLanguage`.
3. Add a language button in the top bar.

The UI binds strings through a live `{loc:Loc Key}` markup extension, so switching updates every label — and the window `FlowDirection` — instantly.

---

## 🛡 Safety notes

- **Exact content** never yields false positives — it's always safe to delete extras from those groups.
- **Name** and **perceptual** methods are heuristic. Always glance at a group (and the thumbnails) before a permanent delete.
- Prefer **Recycle Bin** over permanent delete; the Recycle option is fully recoverable.
- The app never deletes anything without an explicit action and a confirmation dialog.

---

## ❓ Troubleshooting / FAQ

**The video method finds nothing / is skipped.**
ffmpeg isn't available. Use **Download ffmpeg automatically** in Advanced ▸ Videos, or set the path manually.

**Two copies of the same movie weren't matched.**
Increase *Sample frames per video*, lower the *Image similarity threshold*, and reduce the *Skip from start/end* percentages so more of the film is sampled.

**A scan is using too much CPU.**
Keep **Gentle mode** on. You can also scan fewer methods at once, or narrow the file-size/extension filters.

**Nothing was built after I pushed a tag.**
Check the two GitHub settings in [CI/CD](#-automated-builds--releases-cicd): workflow write permission, and that the tag includes the workflow commit. The Actions tab shows the run logs.

**Does it change or move my files during a scan?**
No. Scanning is read-only. Files change only when you click Move / Recycle / Delete and confirm.

---

## 🧰 Tech stack & credits

- [.NET 10](https://dotnet.microsoft.com/) + **WPF** (MVVM, no external MVVM framework)
- [Material Design In XAML Toolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit)
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) — cross-platform image decoding for perceptual hashing
- [ffmpeg](https://ffmpeg.org/) — video frame sampling (optional, user-provided or auto-downloaded)
- [Inno Setup](https://jrsoftware.org/isinfo.php) — installer packaging
- [xUnit](https://xunit.net/) — tests

---

## 📄 License

Released under the **MIT License**. See [`LICENSE`](LICENSE) if present, or treat this as MIT.

---

<div align="center">
Made with ❤ for tidy disks.
</div>
