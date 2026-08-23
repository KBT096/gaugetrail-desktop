# GaugeTrail Desktop v0.1.0

- 发布日期：2026-08-23
- 作者：KBT096
- 许可证：MIT

## 首个公开版本

v0.1.0 建立了一条可以完整体验的本地工作流：

`测量记录 → I 图 → 规则信号 → 解释卡片 → 行动闭环 → 报告导出`

### 已实现

- Windows 10/11 x64 原生 WPF 桌面界面；
- 58 条内置演示记录；
- 手工补录与 CSV 导入；
- I 图、中心线、控制限和规格限；
- 移动极差估计过程内标准差；
- Cp、Cpk、Pp、Ppk；
- 8 条透明规则；
- R2–R8 连续重叠窗口合并；
- 六阶段行动闭环与审计记录；
- CSV、Markdown、JSON 导出；
- JSON 原子保存和最近一次 `.bak`。

## 验证

- `dotnet build GaugeTrail.sln -c Release`：0 警告、0 错误；
- 自测覆盖统计量、8 条规则、窗口合并、CSV、JSON、Markdown 和无效规格；
- 在当前 Windows 主机实际启动窗口并检查总览和数据页面；
- 自包含发布包解压、文件清单、SHA-256 和启动检查。

GitHub Actions 会在 `windows-latest` 上重复执行 Release 构建和自测。

## 已知限制

- 仅支持连续数值型数据；
- 仅一个活动工作区；
- 没有独立 MR 子图；
- 没有其他控制图和分布检验；
- 未进行商业软件数值对标；
- 未购买 Windows 代码签名证书；
- 未完成生产环境和行业标准验证。

## 下载

下载 `GaugeTrail-Desktop-v0.1.0-win-x64.zip`，并用 Release 页面公布的 SHA-256 核对文件。
