# Contributing

感谢你愿意改进 GaugeTrail Desktop。

## 开始之前

- Issue 和 PR 请描述真实问题，不制造使用量或维护记录；
- 统计公式或规则变更必须同时更新代码、自测和 README；
- 不把统计信号描述为已经确认的根因；
- 不提交真实生产数据、个人信息或凭据。

## 本地验证

```powershell
dotnet build GaugeTrail.sln -c Release
dotnet run --project tests/GaugeTrail.SelfTest/GaugeTrail.SelfTest.csproj -c Release
```

桌面布局变更还应在真实 Windows 窗口中检查。

## 代码风格

- C# 启用 Nullable；
- 核心计算放在 `GaugeTrail.Core`；
- Windows 界面和文件对话框放在 `GaugeTrail.Desktop`；
- 用户可见文本优先使用清晰的中文，并保留必要的统计符号。
