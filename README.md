## 下载

**最新版：v0.19.1（多世界观隔离 + 删除世界观修复 + 关系图/时间线修复）** → [GitHub Releases](https://github.com/nekibud/OC-editor/releases/latest)

- [便携版 OC-editor-0.19.1-portable-win64.zip](https://github.com/nekibud/OC-editor/releases/download/v0.19.1/OC-editor-0.19.1-portable-win64.zip) ← **推荐：解压即用，不需要安装**
- [安装程序 OC-editor-0.19.1-Setup.exe](https://github.com/nekibud/OC-editor/releases/download/v0.19.1/OC-editor-0.19.1-Setup.exe)
- [微软商店 MSIX 包 OC-editor-0.19.1.msix](https://github.com/nekibud/OC-editor/releases/download/v0.19.1/OC-editor-0.19.1.msix)（Microsoft Store 提交用）

> **0.19.1 修复**：「管理世界观」里删除按钮被遮罩层挡住、点不动的问题；静默安装
> （`--install`）在窗口创建前调用 `BeginInvoke` 崩溃的问题；至少保留一个世界观。

> **0.19.0 重点**：先选世界观再创作（角色/事件/时间线/关系图按世界观隔离）；
> 修复时间线超大年份卡死；修复同一对角色多条关系导致的闪屏与关系图空白页。
> 旧数据首次启动会自动归入「默认世界观」。

> **改名说明**：产品名已全面统一为 **OC editor**（程序 OCeditor.exe、快捷方式、安装目录、卸载项、
> 微软商店显示名）。0.18.1 及更早版本名为 OC Studio，其数据会在首次启动时自动从
> `%LOCALAPPDATA%\OC Studio\WebView2` 复制到 `%LOCALAPPDATA%\OC editor\WebView2`，不会丢。
>
> 安装版如果报“无法打开此安装程序包”（错误 1620）：那是系统里的 Windows Installer 被禁用/被拦截，
> 请用便携版 zip 或新版 Setup（二者都不经过 Windows Installer）。

---
# OC editor

本地优先的原创角色（OC）与世界观创作工作台。

当前版本：**v0.19.1 · local-first**

本仓库在原有角色 / 事件 / 国家 / 词条 / 时间线 / 关系图谱骨架上增量扩展，
新增了**多世界观隔离**、**程序化世界地图生成器**与**可折叠树形表格**。运行时完全离线：无账号、无云同步、
无遥测、无崩溃上报、无分析埋点、无自动更新。

---

## 功能

**多世界观（新增）**
启动时先选择世界观，进入后再编辑。角色、武器/宠物、事件、国家、词条、自定义分类、小说、地图、
时期、时间线、关系图、相册、搜索与回收站都按世界观隔离，互不混杂。顶栏/侧栏/移动端可随时切换、
新建、重命名或删除世界观。

**角色 / 事件 / 国家**
完整的角色信息卡（简介、生平、立绘），事件支持时间段和负数年份，国家可关联角色与事件。
同一对角色可以建立多条关系（如「朋友」「师徒」），关系图与「相关人物」双向同步。

**世界观百科**
树状词条编辑器，支持  `@` 引用，跨条目串联设定。

**时间线**
年/十年/五十年/百年聚合视图，事件以矩形条展示，支持拖拽调整时间。时期条标记历史阶段，6 色可选。

**关系图谱**
角色、事件、国家之间的可视化网络，6 种关系类型，点击节点跳转编辑。

**世界地图生成器（新增）**
- 1024 × 1024，纯前端 TypeScript + Web Worker，固定 seed 可复现。
- 高程：大陆掩码、分层 Simplex 噪声、domain warping、种子山带 + ridged noise 主脊/山麓。
- 水文：Priority-Flood 填洼、流向追踪、汇流加宽、湖泊蓄水、入海三角洲。
- 气候：纬度温度梯度、海拔递减、盛行风带、迎风坡降雨与背风坡雨影。
- 生物群系：温度 × 湿度 Whittaker 查表 + 海岸/湿地/高山修正。
- 视图：地形着色、等高线（Marching Squares）、温度、降水、生物群系、沿线地形剖面。
- 一次生成缓存底图图层；切换按钮只换渲染器。剖面视图可在地图上按住拖动画线，实时采样高程与群系。
- 地图参数与种子写入 Dexie，并随 JSON / ZIP 备份导出；相同 seed + 参数可重建完全相同的图层。

**树形表格（新增）**
- 可折叠层级、缩进、属性列、单元格内联编辑。
- 三个同源视图：词条树 / 国家树 / 按国家分组的角色。
- 直接读写 Dexie 的 `worldEntries`、`countries`、`characters`，不是独立数据集。
- 支持搜索、展开/折叠全部、新增根/子项、删除与子项提升。

**编辑器**
斜杠命令、Markdown 快捷输入、表格、待办列表、块拖拽排序。全局复用同一内核。

**全局相册**
所有图片按角色/事件/国家自动聚合，两级分类树，支持搜索过滤。

**暗黑模式**
白天 / 夜间 / 跟随系统，完整设计令牌覆盖。

**全局搜索**
`Ctrl+K` 跨模块模糊搜索，`Ctrl+S` 保存。

**回收站**
软删除，支持恢复和永久删除，30 天自动清理。

**数据导入/导出**
- JSON（`.ocbak`）或 ZIP（`oc-data.json` + `manifest.json` + `README.txt`）。
- 包含角色、事件、国家、词条、标签、嵌入关系、地图 seed 与参数。
- 4 种合并策略（跳过/覆盖/保留两者/清空导入）。
- 导入时递归剥离外部图片 URL。

---

## 截图

| 选择世界观 | 管理世界观 |
| --- | --- |
| ![选择世界观](docs/screenshots/world-select.png) | ![管理世界观](docs/screenshots/world-manage.png) |

| 地形 | 等高线 |
| --- | --- |
| ![地形](docs/screenshots/map-terrain.png) | ![等高线](docs/screenshots/map-contour.png) |

| 生物群系 | 地形剖面 |
| --- | --- |
| ![生物群系](docs/screenshots/map-biome.png) | ![剖面](docs/screenshots/map-profile.png) |

![树形表格](docs/screenshots/tree-table.png)

---

## 平台

| 平台 | 说明 |
|------|------|
| Web | 浏览器开发模式，`npm run dev` |
| Windows 桌面 | Tauri v2 打包，原生 Mica 半透明窗口效果 |
| Android | Capacitor v8 封装（首期地图/树表按桌面优先） |

---

## 快速开始

```bash
pnpm install   # 或 npm install
pnpm dev       # 或 npm run dev
```

打开 `http://localhost:3000`。

```bash
npm run build && npx tauri build   # Windows 桌面打包
npm run build && npx cap sync && npx cap open android  # Android 打包
```

> 本工作区也附带 pnpm 配置（`.npmrc` 使用 hoisted linker），可用 `pnpm install` / `pnpm build`。
> 如果 pnpm 提示 ignored build scripts（sharp / unrs-resolver），可执行 `pnpm approve-builds`；
> 这不影响 Next.js 静态导出与地图生成。

---

## 隐私

完整说明见 [PRIVACY.md](./PRIVACY.md)。

- Tauri CSP 以 `default-src 'self'` 为基线；`img-src` 仅允许本地、`data:`、`blob:` 与 AppData asset。
- 无 CDN、外部字体、在线底图或统计脚本。
- 地图计算、编辑器、图谱、导入导出全部在本机完成。
- 图片在桌面端复制到 `AppData/images/`，不引用外部 URL。
- 自动更新默认不做。

---

## 技术栈

Next.js 16 · TypeScript · Tailwind CSS v4 · Tiptap · Dexie.js (IndexedDB) · @xyflow/react · Tauri v2 · Capacitor v8

地图生成器为自研 TypeScript 模块（`src/features/map/generator/`），不依赖在线地图/地形库。

---

## 目录速览

```
src/features/map/            地图生成器、Worker、视图、图例、剖面
src/features/tree-table/     树形表格与内联编辑
src/lib/zip.ts               store-only ZIP 写入 + deflate 读取
src/lib/export.ts            JSON / ZIP 备份
src/lib/import.ts            导入、合并策略与远程 URL 清洗
src-tauri/tauri.conf.json    CSP、asset 协议、窗口配置
PRIVACY.md                   离线与隐私硬约束说明
```

---

## 许可

MIT
