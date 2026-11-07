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
cd WTSkinManager
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

## 7. CI 签名与验签

工作流（`.github/workflows/build.yml`）在配置了证书时会自动签名并验签；未配置则跳过，Release 为未签名 exe。

### 7.1 配置 Secrets

在仓库 Settings → Secrets and variables → Actions 新增：

| Secret | 内容 |
|---|---|
| `WTSM_SIGN_PFX_BASE64` | pfx 文件的 Base64（单行） |
| `WTSM_SIGN_PASSWORD` | pfx 密码 |
| `WTSM_SIGN_THUMBPRINT`（可选） | 证书指纹，验签时精确匹配 |

生成 Base64：

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes('C:\path\to\codesign.pfx')) | Set-Clipboard
```

> 私钥只放在 Secrets 里，绝不提交到仓库；`.gitignore` 已排除 `*.pfx`。

### 7.2 CI 里验证了什么

1. `Prepare signing certificate`：把 Base64 解到 `%RUNNER_TEMP%\codesign.pfx`，写入 `GITHUB_ENV`；
2. `Build`：`build.ps1` 检测到 `WTSM_SIGN_PFX` 后调用 `build/sign.ps1`，优先 `signtool`，找不到时回退 `Set-AuthenticodeSignature`；
3. `Verify signature`：读取 `Get-AuthenticodeSignature`，要求**不是 NotSigned**、签名者证书存在；配置了指纹则要求指纹一致。自签名证书链未受信任时状态为 `UnknownError`，这属于预期，不因此失败；
4. `Generate checksum` 与 Release 附件包含签名后的 exe。

### 7.3 本地验签（发布后自检）

```powershell
# 下载 Release 里的 exe 后
Unblock-File .\WTSkinManager.exe
$sig = Get-AuthenticodeSignature .\WTSkinManager.exe
$sig.Status            # Valid（本机已信任）或 UnknownError（链未受信任，但已签名）
$sig.SignerCertificate.Subject
$sig.TimeStamperCertificate.Subject

# 用 signtool 再验一次（需要 Windows SDK）
signtool verify /pa /v .\WTSkinManager.exe
```

同时核对 `.sha256`：

```powershell
certutil -hashfile .\WTSkinManager.exe SHA256
```

### 7.4 常见状态

| 状态 | 含义 | 处理 |
|---|---|---|
| `NotSigned` | 未配置 Secrets 或签名步骤被跳过 | 配置 Secrets 后重新发版 |
| `UnknownError` + 签名者为 CN=Nchoneko | 已签名，但证书链不在本机受信任根 | 自签名证书的正常表现 |
| `HashMismatch` | 签名后文件被改动 | 重新构建并签名 |
| 验签步骤失败 | 配置了 Secrets 但 exe 没有签名 | 看 `ci-logs/build.log` |

### 7.5 重新发布已签名版本

1. 配置/修正 Secrets；
2. 提升版本（见 `docs/RELEASE-3.9.2.md`）；
3. 打新 tag 并推送，工作流会重新构建、签名并创建 Release；
4. 已经存在的 Release 如需更新附件，可在 Actions 重新运行该 tag 的工作流，或手动上传替换。
