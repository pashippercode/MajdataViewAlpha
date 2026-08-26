# MajdataViewAlpha — Linux

MajdataViewAlpha 在 Linux 上提供 **谱面工具链** 与 **Unity View 播放器**（需自行用 Unity 构建）的支持。WPF 组件 **MajdataEdit** 与 **MajdataLauncher** 仍为 Windows 专用。

## 快速开始（开发 / CI）

```bash
./scripts/linux-install.sh      # 安装 .NET SDK、ffmpeg、Python 依赖
./scripts/smoke-linux.sh        # 验证 Maicaiyin + simai_parser
./scripts/linux-release.sh      # 打包 dist/linux/MajdataViewAlpha-Linux-*.tar.gz
```

环境变量：

| 变量 | 说明 |
|------|------|
| `MAJDATA_PYTHON_VENV` | Python 虚拟环境路径（默认 `~/.venvs/majdataviewalpha`） |
| `MAJDATA_LINUX_VIEW` | 已构建的 Unity Linux 播放器目录，release 时会复制到 `App/MajdataView/` |

## Linux 发布包内容

```
MajdataViewAlpha-Linux-<version>/
  README.md
  VERSION
  simai_parser.py
  bin/
    maicaiyin-infer          # 自动踩音 CLI 包装
    setup-python-env         # 在目标机器创建 venv
  tools/Maicaiyin/           # NumPy 推理引擎 + 模型
  Skin/                      # 皮肤资源（如有）
  Assets/StreamingAssets/    # 不含 Windows ffmpeg.exe，使用系统 ffmpeg
  App/MajdataView/           # Unity Linux 播放器（可选）
```

## Unity View（Linux x86_64）

1. 在 Linux 或支持 Linux Build Support 的环境安装 **Unity 6000.4.2f1**。
2. 打开仓库根目录，选择 **Standalone Linux64**，执行 **Build**（不要用 Build And Run）。
3. 构建完成后，`AlphaReleasePostprocessor` 会组装 Linux 发布目录（不含 WPF Edit/Launcher）。
4. 或将构建输出路径设为 `MAJDATA_LINUX_VIEW` 后运行 `./scripts/linux-release.sh`。

## Windows 专用组件

以下组件依赖 WPF / Windows 原生库，**无法在 Linux 上编译或运行**：

- `MajdataEdit`（编辑器）
- `MajdataLauncher`（桌宠启动器）
- `bass.dll` / `bass_fx.dll`（音频；Edit 专用）

完整 Windows 发布包请按根目录 `README.md` 在 Windows + Unity 环境下构建。

## 自动踩音（Maicaiyin）

Linux 使用系统 Python 3.12+ 与 pip 安装的 NumPy 2.3.x 栈，而非 Windows 捆绑的 `tools/Maicaiyin/python/`：

```bash
source ~/.venvs/majdataviewalpha/bin/activate
./bin/maicaiyin-infer /path/to/track.wav --output ./out --bpm 120
```

## simai_parser

仓库根目录的 `simai_parser.py` 可将 maidata 转为 Majson JSON，供 MajdataView HTTP 接口（`:8013`）使用。在 Linux 上可直接运行，无需 Windows 依赖。
