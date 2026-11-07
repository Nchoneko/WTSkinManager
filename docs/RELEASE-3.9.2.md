# v3.9.2 版本号变更清单

本文档是发 3.9.2 前的检查单。程序版本只有两个来源：`src/AssemblyInfo.cs`（四段）与 `package.json`（三段），其余文件由构建脚本校验或生成。

## 1. 必须同步修改

| 文件 | 当前值 | 目标值 | 说明 |
|---|---|---|---|
| `src/AssemblyInfo.cs` | `AssemblyVersion("3.9.1.0")` | `AssemblyVersion("3.9.2.0")` | 同时改 `AssemblyFileVersion` |
| `package.json` | `"version": "3.9.1"` | `"version": "3.9.2"` | `npm run check:version` 会校验 |
| `CHANGELOG.md` | 顶部 `## 3.9.1.0` | 新增 `## 3.9.2.0 — YYYY-MM-DD` 段 | 安装完成窗口/Release 说明的数据源 |
| `assets/data/updates.dat` | `[3.9.1]` | 顶部新增 `[3.9.2]` 段 | 与 CHANGELOG 的版本段必须一一对应，构建会校验 |

## 2. 由构建自动生成（不要手改）

- `out/generated/build-info.ini`：版本号来自 AssemblyInfo，构建时间取构建时刻；
- `out/generated/data-manifest.json`：数据文件版本清单；
- `out/WTSkinManager.exe.sha256`：CI 生成。

## 3. 建议写入 3.9.2 的变更（示例）

```markdown
## 3.9.2.0 — 2026-11-08

### 修复
- 修复 GitHub Actions 构建在 runner 上 `Get-FileHash` 不可用导致失败的问题，改用 .NET 计算 SHA-256 兜底。
- 修复 CI 签名验签在自签名证书链未受信任时被误判为失败的问题，改为校验签名者与可选指纹。

### 改进
- CI 构建失败时自动把 `build.log` 发布到 `ci-logs` 分支，便于公开排查。
- README 增加 Releases 徽章与「下载最新版本」按钮。
- 补充 `docs/PUBLISHING.md` 的 CI 签名与本地验签步骤。
```

对应 `updates.dat`：

```ini
[3.9.2]
date=2026-11-08
data=2026.11.08.1
fixed=修复 GitHub Actions 上 Get-FileHash 不可用导致构建失败的问题
fixed=修复 CI 签名验签在自签名证书链下误判失败的问题
improved=构建失败时自动把日志发布到 ci-logs 分支
improved=README 增加下载按钮与 Releases 徽章
```

## 4. 发版步骤

```powershell
# 1) 修改上表四个文件（AssemblyInfo、package.json、CHANGELOG、updates.dat）
npm run check:version
npm run build
npm run start          # 确认版本号显示 v3.9.2

# 2) 提交
git add -A
git commit -m "release: v3.9.2"
git push origin main

# 3) 等 main 的 build 通过后打 tag
git tag -a v3.9.2 -m "WTSkinManager v3.9.2"
git push origin v3.9.2
```

推送 tag 后 Actions 会自动构建、签名（若配置 Secrets）、生成校验和并创建 Release。

## 5. 发布后检查

- [ ] Release 附件：`WTSkinManager.exe`、`.sha256`、`updates.dat`
- [ ] 下载 exe，`certutil -hashfile` 与 `.sha256` 一致
- [ ] `Get-AuthenticodeSignature` 不是 `NotSigned`（若配置了签名 Secrets）
- [ ] 程序启动后标题栏显示 `v3.9.2 · 构建时间`
- [ ] 安装目录更新后 `settings.ini` 与用户数据保持不变
- [ ] `git status` 干净，未提交 `out/`、私钥、用户数据
