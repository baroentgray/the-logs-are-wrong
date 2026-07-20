# SOFTWARE_REQUIREMENTS

**Project:** THE LOGS ARE WRONG  
**Platform:** Windows 11 x64  
**Verified:** 2026-07-19  
**Rule:** install tools by gate. Do not install future networking/audio stacks into the project early.

## 1. Required now — before Gate 1

### 1.1 Git for Windows

**Required:** yes  
**Purpose:** repository, branches, worktrees, commits and agent handoffs.

Recommended:
- current maintained x64 Git for Windows;
- Git Bash may remain installed;
- enable long paths if the repository later hits Windows path limits.

After installation:

```powershell
git --version
git config --global user.name "Sergey"
git config --global user.email "<your GitHub email>"
git config --global core.autocrlf false
git config --global init.defaultBranch main
```

`core.autocrlf=false` is intentional: the repository will define line endings through `.gitattributes`.

### 1.2 Git LFS

**Required:** yes before binary assets enter the repository  
**Purpose:** `.blend`, textures, audio and other large binary files.

Git for Windows may already include Git LFS. Check first:

```powershell
git lfs version
git lfs install
```

Do not track files manually before the repository `.gitattributes` is committed.

### 1.3 .NET 10 SDK x64

**Required:** yes  
**Purpose:** Gate 1 pure C# domain solution, build and tests.

Project baseline:
- target framework: `net10.0`;
- SDK: latest supported .NET 10 patch;
- repository must contain `global.json` after the first solution is created.

Check:

```powershell
dotnet --info
dotnet --list-sdks
```

The full **SDK** is required, not only `.NET Runtime`.

### 1.4 C# IDE — choose one primary

#### Recommended: Visual Studio Community 2026

Install workloads:

**Now:**
- `.NET desktop development`.

**Before Gate 2:**
- `Game development with Unity`;
- Visual Studio Tools for Unity.

Do not install C++, Unreal, mobile or Azure workloads for this project unless a future ticket requires them.

#### Acceptable alternatives

- JetBrains Rider — paid, strong Unity/C# support.
- Visual Studio Code — usable with C#, C# Dev Kit and Unity extensions, but not the preferred default for this project.

Use only one primary IDE in project instructions to avoid agents producing editor-specific setup noise.

### 1.5 PowerShell 7

**Required:** recommended/operationally required  
**Purpose:** setup scripts, checks, packaging and repeatable agent commands.

Check:

```powershell
pwsh --version
```

Windows PowerShell 5.1 remains installed side-by-side and is not the project shell.

### 1.6 AI clients already used by the project

**Required:** yes for the chosen workflow, but not a build dependency.

- Codex client/session access.
- Claude client/Claude Code access.
- ChatGPT access.

Rules:
- clients are updated independently;
- model names are not committed as permanent architecture;
- actual model/reasoning is recorded in `run-context.yaml`;
- AI credentials and local settings are never committed.

### 1.7 GitHub access

**Required:** yes  
**Installable app:** optional.

Choose:
- browser + Git command line; or
- GitHub Desktop for convenient repository and diff handling.

GitHub Desktop does not replace Git CLI for agent scripts.

### 1.8 Linear

**Required:** account/workspace, not a local installation.

Browser use is sufficient. Desktop app is optional.

---

## 2. Install before Gate 2 — local Unity prototype

### 2.1 Unity Hub

**Required at Gate 2.**

Use Unity Hub to install and manage the editor. Do not install preview/beta editors.

### 2.2 Unity 6.3 LTS

**Required at Gate 2.**

Policy:
- install the latest stable `6000.3.x` LTS patch available when the Unity project is created;
- record the exact editor version in `ProjectSettings/ProjectVersion.txt`;
- after project creation, upgrades require a separate ticket and clean backup/commit;
- Windows Build Support must be installed;
- Android, WebGL, Linux and dedicated-server modules are not required initially.

Gate 1 remains independent from Unity.

### 2.3 Blender 4.5 LTS

**Required before original 3D asset work.**

Use one fixed Blender LTS version for the project. Do not switch `.blend` files between experimental/daily builds.

Initial use:
- blockout;
- character comparison;
- low-poly props;
- simple rigs/animations;
- FBX/glTF export tests.

Blender is not required to start Gate 1.

### 2.4 2D image editor — choose one

**Optional until UI/texture work:**
- Krita;
- GIMP;
- Affinity Photo/Designer if already owned.

No project dependency should require Adobe software.

### 2.5 Audio editor — choose one

**Optional until audio work:**
- Audacity for basic editing;
- REAPER if more complex routing, batch processing or mastering is needed.

FMOD/Wwise are not approved and must not be installed into the Unity project without an ADR.

---

## 3. Install before Gate 3 — Steam networking

### 3.1 Steam desktop client

**Required at Gate 3.**

Needed for:
- Steam login;
- lobby/session tests;
- two-account smoke tests;
- Steam overlay/invites later.

### 3.2 Second Steam test identity and second client environment

**Required for the real two-player smoke test.**

Acceptable:
- second physical Windows PC;
- trusted partner's PC;
- later, a proven isolated second Windows environment.

Do not assume that launching two Steam clients under one normal Windows user is a valid final test.

### 3.3 Networking packages

These are **project packages**, not global software:

- FishNet;
- FishySteamworks;
- Steamworks.NET.

Rules:
- do not add them during Gate 1 or Gate 2;
- first install them in an isolated Unity smoke-test project;
- pin exact tags/commits only after the two-account test;
- never depend on floating Git branches.

### 3.4 Steamworks SDK / partner access

Not required for Gate 1 or Gate 2.

Add only when needed for Steam integration/upload/onboarding. Keep the SDK outside the Git repository except for explicitly permitted redistributable files.

---

## 4. Later or optional

### Local model stack

Optional:
- Ollama;
- approved local coding model;
- local model launcher/profile script.

Not needed to compile or test the game. Do not delay Gate 1 for local-model setup.

### Useful optional tools

- GitHub CLI (`gh`) — PRs/issues from terminal.
- 7-Zip — archives.
- Everything — fast file search.
- OBS Studio — capture playtests.
- RenderDoc — graphics debugging later.
- Sysinternals Process Explorer — process/debug diagnostics.

### Only when a real requirement appears

- Docker Desktop;
- WSL2;
- Python;
- Node.js/npm;
- databases;
- cloud SDKs;
- FMOD/Wwise;
- Perforce;
- Plastic SCM/Unity Version Control.

None of these is a Gate 1 requirement.

---

## 5. Explicitly not required now

Do **not** install or configure for Gate 1:

- Unity editor;
- FishNet;
- Steamworks.NET;
- Steamworks SDK;
- Blender;
- voice SDK;
- Docker;
- database;
- dedicated server tools;
- Android/iOS build modules.

Gate 1 requires only:

```text
Windows 11
+ Git/Git LFS
+ .NET 10 SDK
+ C# IDE
+ PowerShell 7
+ GitHub
+ Codex/Claude/ChatGPT workflow
```

---

## 6. Environment verification

Run:

```powershell
pwsh -File .\scripts\check-environment.ps1
```

Expected Gate 1 minimum:
- Git found;
- Git LFS found;
- .NET 10 SDK found;
- PowerShell 7 found.

Unity, Blender and Steam are reported but do not block Gate 1.

## 7. Version policy

- Global tools: current stable supported releases.
- Project-critical tools: exact versions pinned in repository after first successful use.
- Unity editor: `ProjectVersion.txt`.
- .NET SDK: `global.json`.
- NuGet packages: central or project package versions committed.
- Unity packages: `Packages/manifest.json` and lock file.
- Blender: version recorded in this document and asset pipeline notes.
- Network stack: exact tags/commits accepted only after Gate 3 smoke-test.
