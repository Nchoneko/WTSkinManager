# 发布流程（GitHub）

本文档给维护者使用，覆盖从本地到 Release 的完整步骤。

## 0. 本地预检

```powershell
npm run check:version
npm run build
npm run start
npm run pack
```

确认 `out/WTSkinManager.exe` 能启动、版本号与 `package.json` 一致。

## 1. 创建仓库

在 GitHub 新建空仓库 `Nchoneko/WTSkinManager`（**不要**勾选初始化 README/License/.gitignore，避免冲突）。

## 2. 首次推送

```bash
cd "<local-path>"
git remote add origin https://github.com/Nchoneko/WTSkinManager.git
git branch -M main
git push -u origin main
```

仓库约 350 MB、2000+ 文件，首次 push 会比较慢，属正常。

## 3. 配置签名（可选但推荐）

工作流会在配置了证书时自动签名并校验，否则跳过签名直接构建。

1. 本地把代码签名证书转成 Base64（不要把 pfx 提交到仓库）：

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes('C:\path\to\codesign.pfx')) | Set-Clipboard
```

2. GitHub → Settings → Secrets and variables → Actions，新增：
   - `WTSM_SIGN_PFX_BASE64`：上一步的 Base64 字符串
   - `WTSM_SIGN_PASSWORD`：pfx 密码

> 自签名证书只能证明「文件未被篡改」，不能消除他人机器上的「未知发布者」。要公开发布建议使用 CA 签发的代码签名证书或 Azure Trusted Signing。

## 4. 发版

1. 修改 `src/AssemblyInfo.cs` 版本（例如 3.9.2.0）与 `package.json`（3.9.2），同步 `CHANGELOG.md` 与 `assets/data/updates.dat`；
2. 本地验证后提交；
3. 打 tag 并推送：

```bash
git tag v3.9.2
git push origin v3.9.2
```

推送 tag 会触发 Actions：构建 → （可选）签名校验 → 生成 SHA-256 → 创建 Release，附件包含：

- `WTSkinManager.exe`
- `WTSkinManager.exe.sha256`
- `updates.dat`

Release 说明由 `build/release-notes.ps1` 从 `CHANGELOG.md` 对应版本段生成。

## 5. 发布后检查

- [ ] Release 页面附件齐全、大小合理；
- [ ] 下载附件，核对 `sha256`；
- [ ] 在干净机器（或 VM）上运行一次安装/更新；
- [ ] 确认 `git status` 干净，没有把 `out/` 或证书私钥提交进去；
- [ ] 若配置了签名，确认文件属性里的发布者为 `CN=Nchoneko`。

## 6. 回滚

- 不要删除已发布的 tag；如需撤销，发布一个更高版本修复，或把 Release 标记为 pre-release；
- 严重问题可先把 Release 设为 draft，再补版本。
