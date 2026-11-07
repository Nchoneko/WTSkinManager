# 素材来源与版权说明（assets/SOURCES.md）

本文件逐项记录仓库内 `assets/` 的素材来源、处理方式与授权状态。总原则：

- 程序代码与自制图形以 **Apache-2.0** 授权（见根目录 `LICENSE`）。
- 载具图标、游戏名称与 Wiki 数据属于各自权利人，**不在 Apache-2.0 授权范围内**，
  仅用于识别、展示对应的《战争雷霆》载具。
- 本项目是非官方工具，与 Gaijin Entertainment 无隶属、合作或背书关系。

---

## 1. `assets/icons/`

| 文件 | 内容 | 来源 / 作者 | 授权 |
| --- | --- | --- | --- |
| `app.ico` | 应用图标，多尺寸 ICO（16–256） | Nchoneko 自制，由项目内的几何绘图脚本生成（深色圆角底 + 琥珀色涂装笔触 + 战机剪影） | Apache-2.0 |
| `icon-256.png` | 界面左上角图标 / 关于页图标，256×256 PNG | Nchoneko 自制，与 `app.ico` 同源 | Apache-2.0 |

图标不包含任何游戏截图、官方 Logo 或第三方字体文件。界面中使用的
「Microsoft YaHei UI」为 Windows 系统字体，不随仓库分发。

---

## 2. `assets/vehicles/`

| 内容 | 说明 |
| --- | --- |
| `<slug>.png`（2027 张，含 `_placeholder.png`） | 载具识别展示图 |
| `manifest.json` | 图标索引：slug、文件名、字节数、SHA-256、国家、分支、等级、采集状态 |

### 来源

- 主来源：War Thunder 官方 Wiki 科技树页面
  <https://wiki.warthunder.com/>，覆盖 10 个国家 × 3 条分支（陆战 / 空战 / 直升机）
  共 30 个科技树页面。
- 图片实际地址：官方百科图床
  <https://static.encyclopedia.warthunder.com/>（Wiki 页面内引用的载具渲染图）。
- 采集时间：2026-11-07。
- 采集方式：按科技树页面的载具链接逐台请求；请求间隔 ≥ 1 秒、超时 10 秒、
  单条最多重试 2 次；按载具内部 ID（slug）保存为 `<slug>.png`。
- 后处理：等比缩放/重编码到单张 ≤ 120KB；相同 SHA-256 的图片只保留一份，
  清单中复用同一文件名。`_placeholder.png` 是统一的「暂无图标」占位图。

### 版权

- 载具渲染图、名称、科技树结构与《战争雷霆》游戏素材的版权归
  **Gaijin Entertainment** 及各自权利人所有。
- 本仓库对这些图片**不授予** Apache-2.0 或任何再许可；它们仅作为本工具
  识别、展示载具的附随素材随程序分发。
- 权利人如认为某张图片不宜再分发，请通过
  <https://github.com/Nchoneko/WTSkinManager/issues> 联系，我们会移除对应文件
  并在 `manifest.json` 中将该条目标记为不可用（界面回退到占位图）。

### `manifest.json` 字段

`format` / `schemaVersion` / `dataVersion` / `version` / `updated` / `generator` /
`source` / `count` / `ok` / `retrying` / `permanent` / `entries[]`。
每条 entry 含 `slug`、`file`、`bytes`、`sha256`、`nation`、`branch`、`rank`、
`name`、`collected`、`status`、`attemptCount`、`lastError`、`lastAttemptAt`，
以及与等级/权重采集共用的 `meta*` 字段。构建脚本会逐条校验
`status=ok` 的文件存在、大小与 SHA-256 一致。

---

## 3. `assets/data/`（构建期内嵌，不直接展示）

| 文件 | 内容 | 来源 |
| --- | --- | --- |
| `vehicles.dat` | 载具 ID → 中文名 / 英文名对照表 | 参考公开数据项目 [War-Thunder-Datamine](https://github.com/gszabi99/War-Thunder-Datamine) 的 `units.csv`，经本项目脚本清洗、去重、补充中文名 |
| `nations.dat` | 载具 ID → 国家 | 同上 |
| `vehicle_meta.dat` | 等级 / 权重（AB/RB/SB）基础数据 | 程序从 War Thunder Wiki 采集后固化；运行时可用「更新数据」刷新到本地缓存 |
| `shortnames.ini` | 国家显示名、载具简称与规则开关 | Nchoneko 整理 |
| `updates.dat` | 程序内更新日志（与 `CHANGELOG.md` 对应） | Nchoneko 编写 |
| `data-version.ini` | 数据格式版本 `schemaVersion` / 数据版本 `dataVersion` | Nchoneko 维护 |

数据仅用于功能展示，可能存在滞后或误差；详见 README 的免责声明。
`data-manifest.json` 由 `build/build.ps1` 在构建时生成到 `out/generated/`，
不纳入版本控制。

---

## 4. `assets/certs/publisher.cer`

- 项目自签的代码签名证书，**只含公钥**，用于 Windows「受信任的发布者」导入。
- 不含私钥；仓库中不存在任何 `.pfx` / `.pvk` / `.p12` / `.key` 私钥文件。
- 构建脚本会校验其 SHA-256，并在编译时作为资源内嵌；用户可在
  「设置 → 发布者签名」中导入或移除。

---

## 5. `docs/` 截图

- 均为本项目在隔离沙盒中运行时的窗口截图，使用合成/示例数据，
  不含真实用户名、绝对路径、聊天窗口或第三方个人信息。
- 截图中的《战争雷霆》载具名称、图标版权归权利人所有，仅用于功能说明。

---

## 6. 未纳入仓库的素材

以下内容属于构建产物、个人研究资料或临时素材，已被 `.gitignore` 排除：

- `assets/vehicles.zip`：构建时从 `assets/vehicles/*.png` 重新生成并内嵌进 exe。
- `dist/`、`out/`：构建产物。
- `.research/`、`tools/signer/`、`setup_payload/`、`video_frames/`、
  `m7_frames/`、`demo_*`、`m7_*`、`*.mp4`：抓取/录制/签名工作目录。
- 任何证书私钥、Token、账号密码、个人绝对路径。
