using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal sealed class AlphaReleasePostprocessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 1000;

    public void OnPostprocessBuild(BuildReport report)
    {
        var summary = report.summary;
        if (summary.platform == BuildTarget.StandaloneLinux64)
        {
            CollectLinuxReleaseFiles(summary.outputPath);
            return;
        }

        if (summary.platform != BuildTarget.StandaloneWindows && summary.platform != BuildTarget.StandaloneWindows64)
            return;

        CollectWindowsReleaseFiles(summary.outputPath);

        if ((summary.options & BuildOptions.AutoRunPlayer) != 0)
            UnityEngine.Debug.LogWarning(
                "[MajdataViewAlpha] 请使用 Build 而不是 Build And Run:播放器已移入 App\\MajdataView," +
                "Unity 的自动运行会报“找不到文件”(构建本身已成功,可忽略该弹窗,手动运行 MajdataLauncher.exe)。");
    }

    private static void CollectWindowsReleaseFiles(string builtPlayerPath)
    {
        var projectRoot = RequireProjectRoot();
        var releaseRoot = RequireReleaseRoot(builtPlayerPath);
        var publishRoot = Path.Combine(projectRoot, "Library", "ReleasePublish");
        var tempRoot = Path.Combine(publishRoot, "temp");
        Directory.CreateDirectory(publishRoot);
        Directory.CreateDirectory(tempRoot);

        var appRoot = Path.Combine(releaseRoot, "App");
        var viewRoot = Path.Combine(appRoot, "MajdataView");
        var editRoot = Path.Combine(appRoot, "MajdataEdit");
        var playerName = Path.GetFileNameWithoutExtension(builtPlayerPath);

        MoveWindowsPlayerIntoAppFolder(releaseRoot, builtPlayerPath, viewRoot, playerName);

        var editOutput = Publish(projectRoot, "MajdataEdit", publishRoot, tempRoot,
            "-p:PublishReadyToRun=true");
        var launcherOutput = Publish(projectRoot, "MajdataLauncher", publishRoot, tempRoot,
            "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true");
        CopyDirectory(editOutput, editRoot);
        CopyLauncherToRoot(launcherOutput, releaseRoot);
        CopyDirectory(Path.Combine(projectRoot, "MajdataLauncher", "Pets"), Path.Combine(releaseRoot, "Pets"));

        var skinSource = Path.Combine(projectRoot, "Skin");
        if (Directory.Exists(skinSource))
            CopyDirectory(skinSource, Path.Combine(editRoot, "Skin"));
        CollectChartLibrary(projectRoot, editRoot);
        CopyIfPresent(Path.Combine(projectRoot, "Assets", "StreamingAssets", "ffmpeg.exe"),
            Path.Combine(editRoot, "ffmpeg.exe"));
        RemoveObsoleteReleaseNotes(releaseRoot);
        CopyIfPresent(Path.Combine(projectRoot, "README.md"), Path.Combine(releaseRoot, "README.md"));

        ValidateWindowsRelease(viewRoot, editRoot, releaseRoot, playerName, !EditorUserBuildSettings.development);
        UnityEngine.Debug.Log($"[MajdataViewAlpha] Windows release collected in {releaseRoot}");
    }

    private static void CollectLinuxReleaseFiles(string builtPlayerPath)
    {
        var projectRoot = RequireProjectRoot();
        var releaseRoot = RequireReleaseRoot(builtPlayerPath);
        var appRoot = Path.Combine(releaseRoot, "App");
        var viewRoot = Path.Combine(appRoot, "MajdataView");
        var toolsRoot = Path.Combine(releaseRoot, "tools", "Maicaiyin");
        var playerName = Path.GetFileNameWithoutExtension(builtPlayerPath);

        MoveLinuxPlayerIntoAppFolder(releaseRoot, builtPlayerPath, viewRoot, playerName);

        var publishRoot = Path.Combine(projectRoot, "Library", "ReleasePublish");
        var tempRoot = Path.Combine(publishRoot, "temp");
        Directory.CreateDirectory(publishRoot);
        Directory.CreateDirectory(tempRoot);

        var editRoot = Path.Combine(appRoot, "MajdataEdit");
        var cliRoot = Path.Combine(releaseRoot, "bin");
        Directory.CreateDirectory(cliRoot);
        PublishLinux(projectRoot, "MajdataEdit.Avalonia", publishRoot, tempRoot, editRoot);
        PublishLinux(projectRoot, "MajdataEdit.Cli", publishRoot, tempRoot, cliRoot);

        Directory.CreateDirectory(toolsRoot);
        CopyIfPresent(Path.Combine(projectRoot, "simai_parser.py"), Path.Combine(releaseRoot, "simai_parser.py"));
        CopyIfPresent(Path.Combine(projectRoot, "README-LINUX.md"), Path.Combine(releaseRoot, "README.md"));
        CopyIfPresent(Path.Combine(projectRoot, "README.md"), Path.Combine(releaseRoot, "README.full.md"));

        CopyIfPresent(Path.Combine(projectRoot, "MajdataEdit", "tools", "Maicaiyin", "infer.py"),
            Path.Combine(toolsRoot, "infer.py"));
        CopyIfPresent(Path.Combine(projectRoot, "MajdataEdit", "tools", "Maicaiyin", "joint-placement-numpy.npz"),
            Path.Combine(toolsRoot, "joint-placement-numpy.npz"));
        CopyIfPresent(Path.Combine(projectRoot, "MajdataEdit", "tools", "Maicaiyin", "requirements.txt"),
            Path.Combine(toolsRoot, "requirements.txt"));
        CopyDirectory(Path.Combine(projectRoot, "MajdataEdit", "tools", "Maicaiyin", "maicaiyin"), Path.Combine(toolsRoot, "maicaiyin"));

        var skinSource = Path.Combine(projectRoot, "Skin");
        if (Directory.Exists(skinSource))
            CopyDirectory(skinSource, Path.Combine(releaseRoot, "Skin"));

        CopyStreamingAssetsForLinux(projectRoot, releaseRoot);
        WriteLinuxHelperScripts(releaseRoot);
        ValidateLinuxRelease(viewRoot, editRoot, toolsRoot, releaseRoot, playerName);
        UnityEngine.Debug.Log($"[MajdataViewAlpha] Linux release collected in {releaseRoot}");
    }

    private static string RequireProjectRoot() =>
        Directory.GetParent(Application.dataPath)?.FullName
        ?? throw new BuildFailedException("Cannot resolve the Unity project directory.");

    private static string RequireReleaseRoot(string builtPlayerPath) =>
        Path.GetDirectoryName(builtPlayerPath)
        ?? throw new BuildFailedException("Cannot resolve the player output directory.");

    private static void MoveWindowsPlayerIntoAppFolder(
        string releaseRoot, string builtPlayerPath, string viewRoot, string playerName)
    {
        if (Directory.Exists(viewRoot))
            Directory.Delete(viewRoot, true);
        Directory.CreateDirectory(viewRoot);

        foreach (var burstDebug in Directory.GetDirectories(releaseRoot,
                     playerName + "_BurstDebugInformation*", SearchOption.TopDirectoryOnly))
            Directory.Delete(burstDebug, true);

        var entries = new[]
        {
            Path.GetFileName(builtPlayerPath),
            playerName + "_Data",
            "UnityPlayer.dll",
            "UnityCrashHandler64.exe",
            "MonoBleedingEdge",
            "D3D12",
            "baselib.dll",
            "WinPixEventRuntime.dll",
            "dstorage.dll",
            "dstoragecore.dll"
        };
        foreach (var entry in entries)
            MoveIfPresent(Path.Combine(releaseRoot, entry), Path.Combine(viewRoot, entry));
    }

    private static void MoveLinuxPlayerIntoAppFolder(
        string releaseRoot, string builtPlayerPath, string viewRoot, string playerName)
    {
        if (Directory.Exists(viewRoot))
            Directory.Delete(viewRoot, true);
        Directory.CreateDirectory(viewRoot);

        foreach (var burstDebug in Directory.GetDirectories(releaseRoot,
                     playerName + "_BurstDebugInformation*", SearchOption.TopDirectoryOnly))
            Directory.Delete(burstDebug, true);

        MoveIfPresent(builtPlayerPath, Path.Combine(viewRoot, Path.GetFileName(builtPlayerPath)));
        MoveIfPresent(Path.Combine(releaseRoot, playerName + "_Data"), Path.Combine(viewRoot, playerName + "_Data"));

        foreach (var sharedObject in Directory.GetFiles(releaseRoot, "*.so", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(sharedObject);
            MoveIfPresent(sharedObject, Path.Combine(viewRoot, name));
        }

        var unityFolder = Path.Combine(releaseRoot, "UnityPlayer.so");
        MoveIfPresent(unityFolder, Path.Combine(viewRoot, "UnityPlayer.so"));
    }

    private static void MoveIfPresent(string source, string destination)
    {
        if (Directory.Exists(source))
            Directory.Move(source, destination);
        else if (File.Exists(source))
            File.Move(source, destination);
    }

    private static void CopyStreamingAssetsForLinux(string projectRoot, string releaseRoot)
    {
        var source = Path.Combine(projectRoot, "Assets", "StreamingAssets");
        if (!Directory.Exists(source))
            return;

        var destination = Path.Combine(releaseRoot, "Assets", "StreamingAssets");
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith("ffmpeg.exe.meta", StringComparison.OrdinalIgnoreCase))
                continue;
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static void WriteLinuxHelperScripts(string releaseRoot)
    {
        var binRoot = Path.Combine(releaseRoot, "bin");
        Directory.CreateDirectory(binRoot);

        File.WriteAllText(Path.Combine(binRoot, "maicaiyin-infer"), """
            #!/usr/bin/env bash
            set -euo pipefail
            ROOT="$(cd "$(dirname "$0")/.." && pwd)"
            VENV="${MAJDATA_PYTHON_VENV:-$HOME/.venvs/majdataviewalpha}"
            exec "$VENV/bin/python" "$ROOT/tools/Maicaiyin/infer.py" "$@"
            """);

        File.WriteAllText(Path.Combine(binRoot, "setup-python-env"), """
            #!/usr/bin/env bash
            set -euo pipefail
            ROOT="$(cd "$(dirname "$0")/.." && pwd)"
            VENV="${MAJDATA_PYTHON_VENV:-$HOME/.venvs/majdataviewalpha}"
            python3 -m venv "$VENV"
            "$VENV/bin/pip" install --upgrade pip
            "$VENV/bin/pip" install -r "$ROOT/tools/Maicaiyin/requirements.txt"
            echo "Python env ready: $VENV"
            """);

        File.WriteAllText(Path.Combine(binRoot, "majdata-edit"), """
            #!/usr/bin/env bash
            set -euo pipefail
            ROOT="$(cd "$(dirname "$0")/.." && pwd)"
            export MAJDATA_ROOT="$ROOT"
            export MAJDATA_PYTHON="${MAJDATA_PYTHON:-${MAJDATA_PYTHON_VENV:-$HOME/.venvs/majdataviewalpha}/bin/python}"
            exec "$ROOT/App/MajdataEdit/MajdataEdit" "$@"
            """);

        foreach (var script in Directory.GetFiles(binRoot))
        {
            try
            {
                var chmod = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    ArgumentList = { "+x", script },
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                chmod?.WaitForExit();
            }
            catch
            {
                // chmod may be unavailable on some build hosts; scripts remain usable via bash.
            }
        }
    }

    private static void CopyLauncherToRoot(string source, string releaseRoot)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(file).Equals(".pdb", StringComparison.OrdinalIgnoreCase))
                continue;
            var target = Path.Combine(releaseRoot, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static void ValidateWindowsRelease(
        string viewRoot, string editRoot, string releaseRoot, string playerName, bool fullRelease)
    {
        var dataRoot = Path.Combine(viewRoot, playerName + "_Data");
        var required = new[]
        {
            Path.Combine(viewRoot, playerName + ".exe"),
            Path.Combine(dataRoot, "StreamingAssets", "ffmpeg.exe"),
            Path.Combine(dataRoot, "StreamingAssets", "ffarguments.txt"),
            Path.Combine(dataRoot, "StreamingAssets", "Skin"),
            Path.Combine(dataRoot, "StreamingAssets", "Background"),
            Path.Combine(editRoot, "MajdataEdit.exe"),
            Path.Combine(editRoot, "MajdataEdit.runtimeconfig.json"),
            Path.Combine(editRoot, "ffmpeg.exe"),
            Path.Combine(editRoot, "bass.dll"),
            Path.Combine(editRoot, "bass_fx.dll"),
            Path.Combine(editRoot, "ICSharpCode.AvalonEdit.dll"),
            Path.Combine(editRoot, "EditorSetting.json"),
            Path.Combine(editRoot, "slide_time.json"),
            Path.Combine(editRoot, "SFX"),
            Path.Combine(editRoot, "Themes"),
            Path.Combine(editRoot, "Skin"),
            Path.Combine(editRoot, "tools", "MaiMuriDX", "lib", "python.exe"),
            Path.Combine(editRoot, "tools", "MaiMuriDX", "lib", "cli.py"),
            Path.Combine(editRoot, "tools", "Maicaiyin", "infer.py"),
            Path.Combine(editRoot, "tools", "Maicaiyin", "joint-placement-numpy.npz"),
            Path.Combine(editRoot, "tools", "Maicaiyin", "python", "python.exe"),
            Path.Combine(editRoot, "tools", "Maicaiyin", "python", "python312.dll"),
            Path.Combine(editRoot, "tools", "Maicaiyin", "packages", "numpy-2.3.5.dist-info"),
            Path.Combine(editRoot, "tools", "Maicaiyin", "packages", "scipy-1.17.1.dist-info"),
            Path.Combine(editRoot, "tools", "Maicaiyin", "packages", "librosa-0.11.0.dist-info"),
            Path.Combine(editRoot, "tools", "Maicaiyin", "packages", "soundfile-0.14.0.dist-info"),
            Path.Combine(releaseRoot, "MajdataLauncher.exe"),
            Path.Combine(releaseRoot, "README.md"),
            Path.Combine(releaseRoot, "Pets", "dilaxiong", "pet.json"),
            Path.Combine(releaseRoot, "Pets", "dilaxiong", "spritesheet.png")
        };
        foreach (var path in required)
            RequirePath(path);

        if (fullRelease)
            RequirePath(Path.Combine(editRoot, "charts"));
    }

    private static void ValidateLinuxRelease(
        string viewRoot, string editRoot, string toolsRoot, string releaseRoot, string playerName)
    {
        var playerCandidates = new[]
        {
            Path.Combine(viewRoot, playerName + ".x86_64"),
            Path.Combine(viewRoot, playerName),
            Directory.GetFiles(viewRoot, "*.x86_64", SearchOption.TopDirectoryOnly).FirstOrDefault() ?? string.Empty
        };
        if (!playerCandidates.Any(File.Exists))
            throw new BuildFailedException($"Linux player binary is missing under {viewRoot}");

        var required = new[]
        {
            Path.Combine(viewRoot, playerName + "_Data"),
            Path.Combine(editRoot, "MajdataEdit"),
            Path.Combine(toolsRoot, "infer.py"),
            Path.Combine(toolsRoot, "joint-placement-numpy.npz"),
            Path.Combine(toolsRoot, "requirements.txt"),
            Path.Combine(releaseRoot, "simai_parser.py"),
            Path.Combine(releaseRoot, "README.md"),
            Path.Combine(releaseRoot, "bin", "maicaiyin-infer"),
            Path.Combine(releaseRoot, "bin", "setup-python-env"),
            Path.Combine(releaseRoot, "bin", "majdata-edit"),
            Path.Combine(releaseRoot, "bin", "majdata")
        };
        foreach (var path in required)
            RequirePath(path);
    }

    private static void RequirePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new BuildFailedException($"Release dependency is missing: {path}");
    }

    private static void CollectChartLibrary(string projectRoot, string editRoot)
    {
        var configured = Environment.GetEnvironmentVariable("MAJDATA_CHART_LIBRARY");
        var repositoryRoot = Directory.GetParent(projectRoot)?.FullName;
        var candidates = new[]
        {
            configured,
            Path.Combine(projectRoot, "charts"),
            repositoryRoot == null
                ? null
                : Path.Combine(repositoryRoot, "release", "MaiChartAssistant", "charts")
        };
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
                continue;
            CopyDirectory(candidate, Path.Combine(editRoot, "charts"));
            return;
        }

        UnityEngine.Debug.LogWarning(
            "[MajdataViewAlpha] Chart library was not found. " +
            "Set MAJDATA_CHART_LIBRARY before building to include charts in the release.");
    }

    private static string Publish(
        string projectRoot, string projectName, string publishRoot, string tempRoot,
        params string[] extraArguments)
    {
        var projectFile = Path.Combine(projectRoot, projectName, projectName + ".csproj");
        if (!File.Exists(projectFile))
            throw new BuildFailedException($"Missing release project: {projectFile}");

        var workRoot = Path.Combine(publishRoot, projectName);
        var output = Path.Combine(workRoot, "publish");
        if (Directory.Exists(workRoot))
            Directory.Delete(workRoot, true);
        Directory.CreateDirectory(output);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var intermediateRoot = Path.Combine(workRoot, "obj") + Path.DirectorySeparatorChar;
        var buildOutputRoot = Path.Combine(workRoot, "bin") + Path.DirectorySeparatorChar;
        foreach (var argument in new[]
                 {
                     "publish", projectFile, "-c", "Release", "-r", "win-x64", "--self-contained", "true",
                     "--nologo", "-o", output,
                     $"-p:BaseIntermediateOutputPath={intermediateRoot}",
                     $"-p:BaseOutputPath={buildOutputRoot}"
                 })
            startInfo.ArgumentList.Add(argument);
        foreach (var argument in extraArguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment["TEMP"] = tempRoot;
        startInfo.Environment["TMP"] = tempRoot;
        startInfo.Environment["DOTNET_CLI_HOME"] = Path.Combine(tempRoot, "dotnet-home");

        using var process = Process.Start(startInfo)
                            ?? throw new BuildFailedException($"Failed to start dotnet publish for {projectName}.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var standardOutput = outputTask.GetAwaiter().GetResult();
        var standardError = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new BuildFailedException(
                $"dotnet publish failed for {projectName}.\n{standardOutput}\n{standardError}");
        return output;
    }

    private static string PublishLinux(
        string projectRoot, string projectName, string publishRoot, string tempRoot, string output)
    {
        var projectFile = Path.Combine(projectRoot, projectName, projectName + ".csproj");
        if (!File.Exists(projectFile))
            throw new BuildFailedException($"Missing release project: {projectFile}");

        var workRoot = Path.Combine(publishRoot, projectName);
        if (Directory.Exists(workRoot))
            Directory.Delete(workRoot, true);
        if (Directory.Exists(output))
            Directory.Delete(output, true);
        Directory.CreateDirectory(output);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var intermediateRoot = Path.Combine(workRoot, "obj") + Path.DirectorySeparatorChar;
        var buildOutputRoot = Path.Combine(workRoot, "bin") + Path.DirectorySeparatorChar;
        foreach (var argument in new[]
                 {
                     "publish", projectFile, "-c", "Release", "-r", "linux-x64", "--self-contained", "false",
                     "--nologo", "-o", output,
                     $"-p:BaseIntermediateOutputPath={intermediateRoot}",
                     $"-p:BaseOutputPath={buildOutputRoot}"
                 })
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment["TEMP"] = tempRoot;
        startInfo.Environment["TMP"] = tempRoot;
        startInfo.Environment["DOTNET_CLI_HOME"] = Path.Combine(tempRoot, "dotnet-home");

        using var process = Process.Start(startInfo)
                            ?? throw new BuildFailedException($"Failed to start dotnet publish for {projectName}.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var standardOutput = outputTask.GetAwaiter().GetResult();
        var standardError = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new BuildFailedException(
                $"dotnet publish failed for {projectName}.\n{standardOutput}\n{standardError}");
        return output;
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static void CopyIfPresent(string source, string destination)
    {
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, true);
        }
    }

    private static void RemoveObsoleteReleaseNotes(string releaseRoot)
    {
        foreach (var path in Directory.GetFiles(
                     releaseRoot, "RELEASE_NOTES*.md", SearchOption.TopDirectoryOnly))
            File.Delete(path);
    }
}
