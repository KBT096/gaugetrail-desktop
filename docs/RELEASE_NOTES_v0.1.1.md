# GaugeTrail Desktop v0.1.1

## 这次更新

v0.1.1 是一次日常维护版本，重点放在版本信息与发布路径的一致性：

- 同步桌面程序、README、快速上手和发布包的版本号；
- 发布脚本根据 `-Version` 自动选择对应的 Release Notes，减少手工改名遗漏；
- 保持统计算法、工作区 JSON 格式和离线运行方式不变。

## 下载与启动

打开 [最新 Release](https://github.com/KBT096/gaugetrail-desktop/releases/latest)，下载：

```text
GaugeTrail-Desktop-v0.1.1-win-x64.zip
```

解压后运行 `GaugeTrail.Desktop.exe`。发布页提供 SHA-256，下载后可以先核对再启动。

## 维护验证

发布前运行解决方案构建、自测和自包含 Windows x64 发布脚本；发布包包含程序、README、许可证、快速上手、版本说明和示例 CSV。

## 当前边界

GaugeTrail Desktop 仍是本地分析 Demo，不替代经过组织确认的质量系统、计量系统或产品放行流程。正式使用前，请由质量专业人员确认抽样方案、规则集、控制图类型和记录要求。
