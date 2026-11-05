# MiniENVI Classic

一个参考 ENVI Classic 交互方式实现的课程作业级遥感图像处理工具。100% AI 生成。

## 功能

- 读写 ENVI 标准 HDR + DAT/IMG，支持 BSQ、BIL、BIP、大小端及常用数值类型。
- 灰度、单波段伪彩色和 RGB 三波段合成显示。
- 可独立拖动和缩放窗口尺寸的主图、Scroll 鸟瞰图、Zoom 局部放大图。
- 鸟瞰图红框控制主图范围；主图红框控制 Zoom 范围。
- Zoom 窗口左下角按钮调整局部放大倍率。
- 元信息、波段统计和直方图。
- 矩形裁剪，输出新的 HDR + DAT。
- 2% 线性、最小—最大、均值 ± 2σ 和无拉伸显示。

## 运行

直接运行提交包 `Run` 目录中的 `MiniEnvi.exe` 不需要预装 .NET。

从源码运行需要 Windows 10/11 和 .NET 10 SDK：

```powershell
dotnet run --project src/MiniEnvi/MiniEnvi.csproj
```

也可运行发布目录中的 `MiniEnvi.exe`。

## 基本操作

1. 在主菜单选择“文件 → 打开 ENVI 数据”，选择 `.hdr`。
2. 灰度或伪彩色模式下，双击波段立即显示。
3. RGB 模式下选择 R/G/B 单选项并双击波段；每次选择后自动切换到下一个通道，选择三个通道后刷新显示。
4. 在 Scroll 窗口点击或拖动红框移动主图；在主图点击或拖动红框移动 Zoom 范围。
5. 选择“基本工具 → 矩形裁剪”，在主图拖出矩形并保存。

示例数据可使用 `AVIRISReflectanceSubset.hdr` 与对应的 `.dat` 文件。
