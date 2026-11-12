# 图标来源与制作

用户最终选择：直接使用 SCP 基金会标志，在右下角写 rxsend tool。

- 原始矢量：[SCP Foundation (emblem)](https://commons.wikimedia.org/wiki/File:SCP_Foundation_(emblem).svg)
- 原标志作者 far2，高分辨率版 Aelanna；保留 CC BY-SA 3.0 署名，见项目 THIRD-PARTY-NOTICES.md。
- 最终可编辑母版 scp-rxsend-tool.svg；1024px PNG 为 scp-rxsend-tool.png，程序消费 assets/app.png 和多尺寸 assets/app.ico。
- make_icon.py 使用原 SVG 几何添加纯白圆角底板和蓝色角标，resvg 渲染、Pillow 封装 ICO。只在重新制作资源时需要 Python 依赖，程序运行不需要。
- 此前使用 imagegen 内置工具尝试生成与清理图标，图像有灰影和不连续边缘，未采用。按用户后续“直接使用 SCP 标志”的要求改为编辑已有矢量，避免改变标志。
- rxsend-wiki-source.svg / rxsend-wiki-reference.png 为前期参考，来源 https://wiki-cn.rxsend.cn/ ，原图 dx_seagulls；未用于最终图标。
