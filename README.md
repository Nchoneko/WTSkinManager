# 战雷涂装管理器（WTSkinManager）

一个 Windows 小工具：把《战争雷霆》第三方自定义涂装包拖进来，自动识别所属载具，导入到游戏 `UserSkins` 的载具目录下；同时提供涂装备份/还原、删除、重命名、占用统计与载具图标显示。

[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![Latest Release](https://img.shields.io/github/v/release/Nchoneko/WTSkinManager)](https://github.com/Nchoneko/WTSkinManager/releases/latest)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-0078D6.svg)](#运行要求)

> 当前版本 **v3.9.29**（程序内版本 `3.9.29.0`）。

---

**语言 / Language：** [![简体中文](https://img.shields.io/badge/%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87-current-2f81f7?style=for-the-badge)](#战雷涂装管理器wtskinmanager) [![English](https://img.shields.io/badge/English-switch-24292f?style=for-the-badge)](#wtskinmanager-english)

## 功能

- **识别**：读取涂装包中的 `.blk` 文件名与目录结构，匹配具体载具。
- **两级结构**：写入 `UserSkins\template_<机型代号>\<涂装名>\`，同一载具可放多个涂装，互不覆盖；旧结构兼容。
- **自定义名称**：导入时可修改涂装文件夹名，实时校验、路径预览，同名冲突支持改名/覆盖/跳过，覆盖前自动备份。
- **管理**：右栏展开载具下的涂装，支持勾选、全选、备份、删除、重命名、排序、打开文件夹；删除只删子目录，保留 `template_` 载具目录。
- **占用统计**：后台扫描涂装大小，左栏显示涂装数量与总占用。
- **缺失目录处理**：可设置「每次询问 / 自动创建 / 关闭」，确认后创建空 `template_` 目录。
- **更新**：应用内检查更新，可选国内镜像（`gh-proxy.com`，推荐）或 GitHub 源；延迟 2 秒重启，避免单实例冲突。
- **图标**：载具图标与等级/权重离线显示；图标包独立分发，不随程序发布。

## 运行要求

- Windows 10 / 11（x64）
- .NET Framework 4.x（系统自带）
- 已安装《战争雷霆》，并能定位到包含 `UserSkins` 的游戏目录

## 下载与安装

1. 从 [Releases](https://github.com/Nchoneko/WTSkinManager/releases/latest) 下载 `WTSkinManager.exe`；
2. 运行后选择安装目录与游戏目录；
3. 如需载具图标，单独下载图标包（见下）并把 `vehicles.zip` 放到 `WTSkinManager.exe` 同级目录或 `assets` 子目录，重启程序生效。

### 图标包（单独下载）

图标属于第三方素材，**不随 exe、发行 zip 或 GitHub Release 提供**。可通过以下网盘获取，解压后得到 `vehicles.zip`：

- 百度网盘：https://pan.baidu.com/s/1xxmhGCyle5F7KNd9OykdXA?pwd=97yx
- 夸克网盘：https://pan.quark.cn/s/dad0fa59501a
- 123 云盘：https://1816316610.share.123pan.cn/123pan/mqhqVv-fDAG3?pwd=amUI
- Google Drive: https://drive.google.com/file/d/1Xgo_oAew1j6c37l1pP0UnnIR8sXJ6fiU/view?usp=sharing

图标包约 283 MB（单文件，非分卷），下载后解压即可得到 `vehicles.zip`。也可以在「设置 → 涂装包」里把 `vehicles.zip` 直接拖入导入。

## 从源码构建

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/build.ps1
```

- 使用 Windows 自带的 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 编译；
- 不引入任何 NuGet / 第三方运行时依赖；
- 仓库**不包含**第三方载具图标；缺少 `assets/vehicles/*.png` 时构建仍可成功，只是不生成 `vehicles.zip`；
- 产物：`out/WTSkinManager.exe`（约 1.1 MB）。

## 开源与素材许可

- 代码：Apache License 2.0，见 [LICENSE](LICENSE)；
- 第三方组件与素材说明：见 [NOTICE](NOTICE)；
- 本仓库**不包含**《战争雷霆》载具图标、游戏原始资源或任何 Gaijin Entertainment 的二进制文件；
- 载具图标版权归 Gaijin Entertainment 及各权利人所有，仅由用户自行从第三方网盘获取，**不在 Apache-2.0 授权范围内**；
- 本项目为非官方第三方工具，与 Gaijin Entertainment 无隶属、合作或背书关系。

## 数据来源

- 载具 ID / 名称参考公开数据项目 [War-Thunder-Datamine](https://github.com/gszabi99/War-Thunder-Datamine)；
- 等级/权重、图标地址参考 [War Thunder Wiki](https://wiki.warthunder.com/)；
- 数据可能存在误差，以游戏内为准。

## 免责声明

本工具只读写 `UserSkins` 目录与程序自身数据目录，不修改游戏本体文件。使用前请自行备份重要涂装；因使用本工具造成的任何损失，作者不承担责任。

War Thunder 是 Gaijin Entertainment 的商标。

---

# WTSkinManager (English)

**Language：** [![简体中文](https://img.shields.io/badge/%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87-switch-24292f?style=for-the-badge)](#战雷涂装管理器wtskinmanager) [![English](https://img.shields.io/badge/English-current-2f81f7?style=for-the-badge)](#wtskinmanager-english)


A small Windows tool for *War Thunder* third-party custom skins: drag skin packages in, it identifies the vehicle, imports them into the game's `UserSkins\template_<vehicle-id>\` folders, and provides backup/restore, delete, rename, disk-usage stats and offline vehicle icons.

[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![Latest Release](https://img.shields.io/github/v/release/Nchoneko/WTSkinManager)](https://github.com/Nchoneko/WTSkinManager/releases/latest)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-0078D6.svg)](#requirements)

> Current version **v3.9.29** (internal version `3.9.29.0`).

## Features

- **Detection**: reads `.blk` filenames and folder structure inside a skin package to match the vehicle.
- **Two-level layout**: writes to `UserSkins\template_<vehicle-id>\<skin-name>\`, so multiple skins per vehicle coexist; legacy layout is still recognized.
- **Custom folder name**: edit the skin folder name before importing, with live validation, path preview, and rename/overwrite/skip on conflicts; overwrite backs up the old folder first.
- **Management**: expand a vehicle in the right panel to select, select-all, back up, delete, rename, sort and open skin folders. Deleting removes only sub-folders and never the `template_` vehicle folder.
- **Disk usage**: background size scan; the left sidebar shows skin count and total size.
- **Missing folders**: policy of "ask every time / create automatically / off"; after confirmation the app creates an empty `template_` folder.
- **Updates**: in-app update check with a choice between a China mirror (`gh-proxy.com`, recommended) and GitHub; restart is delayed by 2 seconds to avoid single-instance conflicts.
- **Icons**: offline vehicle icons and rank/battle-rating; the icon pack is distributed separately and is not bundled.

## Requirements

- Windows 10 / 11 (x64)
- .NET Framework 4.x (included with Windows)
- *War Thunder* installed; the app must locate a game folder containing `UserSkins`

## Download and install

1. Download `WTSkinManager.exe` from [Releases](https://github.com/Nchoneko/WTSkinManager/releases/latest).
2. Run it and choose the install folder and game folder.
3. For vehicle icons, download the icon pack separately (see below) and place `vehicles.zip` next to `WTSkinManager.exe` (or in an `assets` sub-folder), then restart the app.

### Icon pack (separate download)

Vehicle icons are third-party artwork and are **not** included in the exe, the release zip or GitHub Releases. Download the pack from one of these cloud drives:

- Baidu Pan: https://pan.baidu.com/s/1xxmhGCyle5F7KNd9OykdXA?pwd=97yx
- Quark: https://pan.quark.cn/s/dad0fa59501a
- 123 Pan: https://1816316610.share.123pan.cn/123pan/mqhqVv-fDAG3?pwd=amUI
- Google Drive: https://drive.google.com/file/d/1Xgo_oAew1j6c37l1pP0UnnIR8sXJ6fiU/view?usp=sharing

The icon pack is about 283 MB as a single archive (not split); extract it to get `vehicles.zip`. You can also drag `vehicles.zip` into "Settings → Icon pack" to import it.

## Build from source

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/build.ps1
```

- Uses the Windows built-in `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`;
- No NuGet packages or third-party runtime dependencies;
- The repository does **not** contain third-party vehicle icons. The build still succeeds without `assets/vehicles/*.png`; it just skips generating `vehicles.zip`;
- Output: `out/WTSkinManager.exe` (about 1.1 MB).

## Open-source and artwork licensing

- Code: Apache License 2.0, see [LICENSE](LICENSE).
- Third-party components and artwork: see [NOTICE](NOTICE).
- This repository does **not** contain War Thunder vehicle icons, game assets, or any Gaijin Entertainment binary files.
- Vehicle icon copyrights belong to Gaijin Entertainment and their respective owners. Icons are obtained by users from third-party cloud drives and are **not** covered by Apache-2.0.
- This is an unofficial third-party tool and is not affiliated with, endorsed by, or sponsored by Gaijin Entertainment.

## Data sources

- Vehicle IDs / names are based on the public [War-Thunder-Datamine](https://github.com/gszabi99/War-Thunder-Datamine) project.
- Rank / battle rating and icon URLs are based on the [War Thunder Wiki](https://wiki.warthunder.com/).
- Data may contain errors; the in-game values are authoritative.

## Disclaimer

The tool only reads and writes the `UserSkins` folder and its own data folder; it does not modify game files. Back up important skins before use. The author is not liable for any loss caused by using this tool.

War Thunder is a trademark of Gaijin Entertainment.
