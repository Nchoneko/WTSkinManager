# 贡献指南

感谢你愿意参与。这个项目是单文件 WinForms 工具，构建链刻意保持最小：不需要 .NET SDK、不需要 Visual Studio。

## 环境

- Windows 10/11
- .NET Framework 4.x（系统自带，`csc.exe` 位于 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319`）
- Node.js 18+（仅用于 `npm run ...` 包装脚本，不引入运行时依赖）
- PowerShell 5.1

## 构建与运行

```powershell
npm run check:version
npm run build
npm run start
```

产物在 `out/`（已忽略，不会提交）。`npm run pack` 生成发布 zip。

## 提交前

- [ ] `npm run check:version` 通过
- [ ] `npm run build` 通过，`out/WTSkinManager.exe` 能启动
- [ ] 不提交 `out/`、`dist/`、日志、缓存、`settings.ini`、证书私钥（`*.pfx/*.pvk/*.p12`）
- [ ] 不写入个人绝对路径；路径从 `Environment` / `Config` 动态取
- [ ] 涉及数据的改动同步更新 `assets/data` 的 `schemaVersion` / `dataVersion` 与 `CHANGELOG.md`
- [ ] 界面改动附截图

## 代码风格

- C# 5 语法（与 `csc.exe` 4.x 兼容），不引入第三方运行时库；
- 沿用 `Ui` 里的主题与控件，不另起一套配色；
- 文件操作必须遵守 `README` 的安全说明：只读 UserSkins 顶层目录，禁用只做 `.BAN` 重命名。

## 提交信息

使用简短祈使句，例如：

```
fix: 修正更新按钮在长版本号下越界
feat: 支持拖动窗口四角缩放
docs: 更新构建说明
```

## 协议

提交代码即表示同意以 Apache-2.0 协议分发你的贡献。
