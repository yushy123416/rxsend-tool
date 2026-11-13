# GMod NVIDIA 滤镜兼容边界

核查日期：2026-11-13。依据：本机只读检测、用户游戏内截图及启用/还原反馈、NVIDIA 官方资料，以及已安装 Overlay 的支持条件代码。

## 当前结论

目前确认的是一个可用组合，不是从某个最低版本到最新版的完整支持范围。驱动 621.14 + NVIDIA App 11.0.9.585 在本机 GMod 64 位上已收到启用和还原成功反馈；截图确认的具体效果是 RTX 动态亮丽。传统 Freestyle 的曝光、对比度、锐化等其他滤镜尚未确认可用，截图当时显示可用滤镜为 0、已激活 RTX 动态亮丽为 1。

## 已验证组合

| 项目 | 本机检测 / 验证结果 |
| --- | --- |
| 显卡 | NVIDIA GeForce RTX 5070 Laptop GPU |
| NVIDIA 驱动 | 621.14，Windows 驱动版本 32.0.16.2114；由 nvidia-smi 与 Win32_VideoController 交叉确认 |
| NVIDIA App | 11.0.9.585，注册表与安装文件信息一致 |
| NVIDIA Overlay | 产品版本 11.0.9.585，文件版本 128.4.13.34 |
| Windows | Windows 11 专业版，10.0.26200 |
| GMod | Steam App ID 4000，Build ID 25375506；gmod_win64.exe 产品版本 2026.09.16 |
| 工具操作 | 启用可用；修复驱动路径规范化比较后，用户确认还原可用 |
| 游戏内功能 | 截图确认 RTX 动态亮丽已激活；不据此认定其他滤镜全部开放 |

本次核查没有安装、卸载或切换驱动和 NVIDIA App，也没有再次修改驱动配置。因此不存在跨版本 A/B 实测数据。

## 可以引用的官方门槛

| 对象 | 官方条件 | 对本工具的含义 |
| --- | --- | --- |
| NVIDIA App 本体 | Windows 10 / 11，驱动 551.52 或更新；当前系统要求页列出 GeForce RTX 20 / 30 / 40 / 50 系列 | App 安装门槛不能当成滤镜保证；该页面也不是历史 GTX 兼容性列表 |
| RTX 动态亮丽 | Windows 10 22H2 或更新、RTX 20 系列及更新、非 SLI，驱动 551.61 或更新；支持 DX9–12 与 Vulkan；关闭游戏 HDR 与 Windows Auto HDR | 这是已观察到的效果的官方必要条件，不是本工具让 GMod 解锁成功的最低版本 |
| Game Ready 591.44 / R590 相关兼容要求 | NVIDIA 要求搭配 App 11.0.5.420 或更新以保证滤镜/照片模式正常 | 旧 App 搭配新驱动是明确的排查点。官方文章正文有一处写成 11.5.420，但检查步骤和 App 正式发行记录均为 11.0.5.420 |

551.61 是效果的通用功能门槛；具体显卡还要使用支持该型号的驱动，不能据此要求 RTX 50 系列回退到 551.61。

资料：

- [NVIDIA App 系统要求](https://www.nvidia.com/en-us/software/nvidia-app/system-requirements/)
- [NVIDIA App FAQ：RTX 动态亮丽与 RTX HDR 条件](https://nvidia.custhelp.com/app/answers/detail/a_id/5521)
- [R590 / 591.44 与 NVIDIA App 配套要求](https://nvidia.custhelp.com/app/answers/detail/a_id/5752)
- [NVIDIA App 正式版本记录，包含 11.0.5.420](https://www.nvidia.com/en-us/software/nvidia-app/release-highlights/)

## 驱动配置许可与具体滤镜开放是两层检查

当前实现只修改所选 GMod 驱动配置的三个公开 NVAPI 设置：

| 设置 | ID | 尝试设置值 |
| --- | --- | --- |
| ANSEL_ENABLE | 0x1075D972 | 1 |
| ANSEL_ALLOWLISTED | 0x1085DA8A | 1 |
| ANSEL_ALLOW | 0x1035DB89 | 1 |

[NVIDIA 的公开头文件](https://github.com/NVIDIA/nvapi/blob/main/NvApiDriverSettings.h)定义了这些设置及允许值，但没有承诺把它们设为 1 就能为所有游戏开放所有滤镜，也没有给出 GMod 兼容的最低/最高驱动版本。

本机安装的 App 11.0.9.585 的 `osc/main.61a92495293d6c3d.js` 中，普通滤镜的 `checkIfCurrentGameIsAllowlisted()` 读取 `nvidiaTech.FREESTYLE`，动态亮丽的 `isAIDVCSupportedInChroma()` 读取 `nvidiaTech.RTXDVC`；同时存在驱动许可 `drsWhitelist` 检查。因此这是本机代码直接支持的结论：驱动许可和 App 中的游戏功能支持不能混为一谈，两个效果也不能互相代替证明。

代码存在这些判断，不等于已经取得本机 GMod 当次会话的所有服务端返回值。当前没有足够证据把截图中传统滤镜缺失唯一归因到某个驱动、某个 App 版本或某个服务端标志。

当前工具没有修改 Overlay 文件或游戏二进制。由于还存在游戏支持数据这一条件，即使版本号相同，也不宜承诺永远能获得相同的滤镜列表。[NVIDIA 官方也说明滤镜需要游戏支持](https://nvidia.custhelp.com/app/answers/detail/a_id/5349)。

## 尚未建立的边界

- 未证明 551.61–621.14 之间所有驱动都能通过此方法为 GMod 开放效果，也未证明 621.14 是上限。
- 未证明所有 App 10.x / 11.x 都有效，也没有证据标出“从某一 App 版本起驱动配置方法彻底失效”。本机组合仍然有效。
- GTX、MX、专业卡，以及其他 RTX 型号未进行本工具实测。尤其 GTX 不能按 RTX 动态亮丽的条件承诺支持。
- 未对 Game Ready 和 Studio 两个驱动发行渠道进行对照；仅有驱动版本号不能证明两个发行包均已实测。
- GMod 的 32 位程序、旧 hl2.exe、其他分支、其他渲染接口未进行游戏内对照。工具有路径识别逻辑不等于这些组合已经验证。
- 更换游戏程序或安装路径，需要重新识别对应关联。更换驱动后需要复核配置是否保留；这次成功不能证明清洁安装后仍保留自定义配置。

## 产品支持口径

当前准确的说法是：“为 GMod 尝试开启 NVIDIA 驱动许可；已在 RTX 5070 Laptop、驱动 621.14、App 11.0.9.585、GMod Build 25375506 上验证 RTX 动态亮丽及配置还原；其他组合和滤镜仍属实验性。”

后续如果加入版本提示，应先显示真实驱动与 App 版本、明确官方必要条件和已实测组合，再由实际能力检测与游戏内验证判断结果。不能把“检测到任意 N 卡 + App 文件存在”直接显示成“全部滤镜受支持”，也不应凭单台机器的成功写死一个连续版本范围。
