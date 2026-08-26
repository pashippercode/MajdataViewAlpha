# MajdataViewAlpha — Linux

MajdataViewAlpha 在 Linux 上提供 **Avalonia 轻量编辑器**、**原生 CLI 工具链**、**Maicaiyin 自动踩音**、**simai_parser**，以及 **Unity View 播放器**（需 Unity 构建）。完整 WPF 编辑器仍为 Windows 专用；Wine 仅作可选后备。

## 快速开始（开发 / CI）

```bash
./scripts/linux-install.sh      # .NET SDK、ffmpeg、Python 依赖
./scripts/smoke-linux.sh        # 验证 Maicaiyin + simai_parser
./scripts/linux-release.sh      # 打包 dist/linux/MajdataViewAlpha-Linux-*.tar.gz
```

### Avalonia 编辑器（推荐 GUI）

无需 Wine 或 WindowsDesktop SDK，框架依赖发布体积极小：

```bash
# 开发
dotnet run --project MajdataEdit.Avalonia

# 发布包
./bin/majdata-edit              # 或 App/MajdataEdit/MajdataEdit
```

功能：打开/保存谱面、Maicaiyin 自动踩音（Level/BPM）。完整 WPF 功能（BASS 预览、Discord RPC、皮肤编辑等）仍仅在 Windows 版提供。

### 原生 CLI

Linux 上可直接使用 **`majdata`** 命令（发布包在 `bin/majdata`，开发时 `dotnet run --project MajdataEdit.Cli`）：

```bash
export MAJDATA_ROOT=/path/to/repo-or-release
export MAJDATA_PYTHON=~/.venvs/majdataviewalpha/bin/python

majdata doctor
majdata auto-onset --audio track.wav --level 10 --bpm 120 --output ./out
majdata parse --maidata ./out/maidata.txt --diff 1 --output chart.json
```

`MajdataEdit.Core` 中的 `AutoOnsetRunner` 被 WPF、Avalonia 与 CLI 共用；Linux 上自动使用 `MAJDATA_PYTHON` 或 `~/.venvs/majdataviewalpha`。

### Wine 运行 Windows 版 MajdataEdit（可选）

1. Windows CI 或本地 `dotnet publish` 产出 `MajdataEdit/`（见 `.github/workflows/windows-edit.yml` artifact）。
2. Linux 上：

```bash
bash scripts/wine-install.sh
MAJDATA_EDIT_WIN=/path/to/MajdataEdit bash scripts/run-edit-wine.sh
```

### Linux 启动器

```bash
bash scripts/majdata-launcher.sh   # 优先 View → Avalonia Edit → majdata doctor
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
    majdata                  # CLI
    majdata-edit             # Avalonia 编辑器启动脚本
    maicaiyin-infer          # 自动踩音 CLI 包装
    setup-python-env         # 在目标机器创建 venv
  App/
    MajdataEdit/             # Avalonia 编辑器（框架依赖 publish）
    MajdataView/             # Unity Linux 播放器（可选）
  tools/Maicaiyin/           # NumPy 推理引擎 + 模型
  Skin/                      # 皮肤资源（如有）
  Assets/StreamingAssets/    # 不含 Windows ffmpeg.exe，使用系统 ffmpeg
```

## 项目结构（.NET）

| 项目 | 平台 | 说明 |
|------|------|------|
| `MajdataEdit` | Windows WPF | 完整编辑器 |
| `MajdataEdit.Avalonia` | Linux/macOS/Windows | 轻量跨平台 GUI |
| `MajdataEdit.Cli` | Linux | `majdata` 命令行 |
| `MajdataEdit.Core` | 共享 | Auto-onset / Python 运行时 |

## Unity View（Linux x86_64）

1. 在 Linux 或支持 Linux Build Support 的环境安装 **Unity 6000.4.2f1**。
2. 打开仓库根目录，选择 **Standalone Linux64**，执行 **Build**（不要用 Build And Run）。
3. 构建完成后，`AlphaReleasePostprocessor` 会组装 Linux 发布目录（含 Avalonia Edit + majdata CLI）。
4. 或将构建输出路径设为 `MAJDATA_LINUX_VIEW` 后运行 `./scripts/linux-release.sh`。

## Windows 专用组件

以下组件依赖 WPF / Windows 原生库，**无法在 Linux 上编译**：

- `MajdataEdit`（WPF 完整版）
- `MajdataLauncher`（桌宠启动器）
- `bass.dll` / `bass_fx.dll`（音频；WPF Edit 专用）

完整 Windows 发布包请按根目录 `README.md` 在 Windows + Unity 环境下构建。

## 自动踩音（Maicaiyin）

Linux 使用系统 Python 3.12+ 与 pip 安装的 NumPy 2.3.x 栈，而非 Windows 捆绑的 `tools/Maicaiyin/python/`：

```bash
source ~/.venvs/majdataviewalpha/bin/activate
./bin/maicaiyin-infer /path/to/track.wav --output ./out --bpm 120
```

## simai_parser

仓库根目录的 `simai_parser.py` 可将 maidata 转为 Majson JSON，供 MajdataView HTTP 接口（`:8013`）使用。在 Linux 上可直接运行，无需 Windows 依赖。
