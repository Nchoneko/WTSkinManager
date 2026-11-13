# 发布流程（只走 GitHub）

本文档给维护者用。**WTSkinManager 的发布只走 GitHub Releases**；Gitee 只保留仓库与历史存档，
不再自动同步，也不允许任何脚本 / CI 自动 push 或上传。

## 0. 硬性约定

1. **只发 GitHub**：唯一的发布目标是 <https://github.com/Nchoneko/WTSkinManager/releases>。
2. **禁止自动推 Gitee**：仓库里不得出现自动 `git push gitee`、自动上传 Gitee 附件、
   或调用 Gitee API/OpenAPI 的代码与脚本。`.github/workflows/` 里也不允许有 Gitee 相关步骤。
3. **保留 `gitee` remote**：本地仓库继续保留 `gitee` 远端（`git remote -v` 能看到即可），
   方便需要时**人工**查看历史，但任何发布流程都不使用它。
4. **版本号唯一来源**：`src/AssemblyInfo.cs` 与 `package.json` 必须一致，由
   `build/check-version.ps1` 校验；不要在别处再写一份版本号。
5. **不提交构建产物**：`out/` 已被 `.gitignore` 排除，Release 附件从本地 `out/` 上传，不进仓库。

## 1. 发版前自检

```powershell
npm run check:version                      # AssemblyInfo.cs ↔ package.json
npm run build                              # 校验资源 → 生成 vehicles.zip → csc 单文件 exe
powershell -NoProfile -ExecutionPolicy Bypass -File tools/security-tests.ps1 -Exe out/WTSkinManager.exe
npm run start                              # 双击产物跑一遍：导入 / 管理 / 设置 / 应用内更新
npm run pack                               # 生成 out/WTSkinManager-v<版本>-win64.zip
```

`out/WTSkinManager.exe` 需要满足：

- 版本号与 `package.json` 一致，界面标题栏显示同一版本；
- 设置页「更新与日志」可以检查更新、显示更新日志；
- 若上一版已发布，用上一版的**应用内自动更新**完整走一遍：下载 → 校验 → 就绪弹窗 → 替换 → 重启。

## 2. 发布命令序列（GitHub）

```powershell
# 0) 版本号与 CHANGELOG / updates.dat 已改好，工作区干净
git status

# 1) 本地构建单文件 exe（产物在 out/）
powershell -NoProfile -ExecutionPolicy Bypass -File build/build.ps1

# 2) 生成发布压缩包（含 exe/LICENSE/NOTICE/README）
powershell -NoProfile -ExecutionPolicy Bypass -File build/pack.ps1

# 3) 从 CHANGELOG.md 抽取本版说明 → out/release-notes.md
powershell -NoProfile -ExecutionPolicy Bypass -File build/release-notes.ps1 -Tag v3.9.13

# 4) 生成 Release 用的 SHA-256 附件（与 CI 一致的两空格格式）
$hash = (Get-FileHash 'out/WTSkinManager.exe' -Algorithm SHA256).Hash.ToLower()
"$hash  WTSkinManager.exe" | Out-File -FilePath 'out/WTSkinManager.exe.sha256' -Encoding ascii

# 5) 提交并只推 origin（GitHub）
git add -A
git commit -m "release: v3.9.13"
git push origin main
```

> **不要**执行 `git push gitee`，也不要在任何脚本里自动触发它。

## 3. 创建 GitHub Release

```powershell
# 需要已登录的 GitHub CLI（gh auth login）；Release 附件就三个文件
gh release create v3.9.13 `
  "out/WTSkinManager.exe" `
  "out/WTSkinManager.exe.sha256" `
  "out/release-notes.md" `
  --title "WTSkinManager v3.9.13" `
  --notes-file "out/release-notes.md"
```

- 附件必须包含 `WTSkinManager.exe`、`WTSkinManager.exe.sha256`；`release-notes.md` 同时作为
  Release 正文，正文里的 SHA-256 段会被应用内更新的校验逻辑优先读取。
- **不要**上传 `WTSkinManager-v*-win64.zip` 作为应用内更新源：应用内更新只下载单个 exe。
  zip 是给需要离线包的人手动下载用的，可以放到同一 Release 的额外附件，但不参与自动更新。
- `out/release-notes.md` 由 `build/release-notes.ps1` 从 `CHANGELOG.md` 生成，末尾自带
  `WTSkinManager.exe.sha256` 的校验说明。

> 另一条等价路径：打 tag（`git tag v3.9.13 && git push origin v3.9.13`）会触发
> `.github/workflows/build.yml` 里的 GitHub Actions 构建并创建 Release。两条路径都只作用于
> GitHub，工作流里没有任何 Gitee 步骤。

## 4. Gitee 的处理

| 项目 | 约定 |
| --- | --- |
| `gitee` remote | 保留，仅在需要时人工查看 |
| 自动同步 | **禁止**：不放脚本、不放 CI 步骤、不调用 Gitee API |
| 历史存档 | `out/Gitee-README.txt`、`out/Gitee-下载说明.txt` 已明确标注「历史存档」并指向 GitHub |
| 新版本发布 | 不在 Gitee 发布，也不把分卷上传到 Gitee |

如果确实需要人工补一份 Gitee 存档，必须手动操作，并在提交信息里注明「manual gitee archive」；
**不得**把它做成脚本或 workflow，以免形成自动同步。

## 5. 发布后检查

- [ ] Release 页面的 `WTSkinManager.exe` 大小正常（约 180 MB）且能下载；
- [ ] `certutil -hashfile WTSkinManager.exe SHA256` 与 `WTSkinManager.exe.sha256` 一致；
- [ ] Release 正文里有 `WTSkinManager.exe.sha256` 段（应用内更新优先读它）；
- [ ] 用**上一版**程序点「下载更新」完整走一遍：进度/速度 → 校验 → 就绪弹窗 → 立即更新 → 重启后版本号正确；
- [ ] `%LOCALAPPDATA%\WTSkinManager\updates\update.log` 里结果是「成功」，`backups\` 里有旧 exe；
- [ ] `git status` 干净，`out/` 与证书私钥没有进仓库；
- [ ] 本地 `gitee` remote 仍然存在，且没有任何自动推送记录。

## 6. 回滚

- 不要删除已发布的 tag；
- 严重问题时发布一个更高的补丁版本（应用内更新只接受更高版本）；
- 需要立刻止损时，可把 Release 标为 pre-release 或临时改为 draft，再补版本。
