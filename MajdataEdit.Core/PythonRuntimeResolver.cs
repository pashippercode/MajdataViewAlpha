using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MajdataEdit;

public static class PythonRuntimeResolver
{
    public sealed record ResolvedRuntime(
        string FileName,
        IReadOnlyList<string> PrefixArguments,
        string? PackageDirectory,
        bool UseIsolatedRunner);

    public static ResolvedRuntime ResolveMaicaiyin(string toolDirectory)
    {
        var inferenceScript = Path.Combine(toolDirectory, "infer.py");
        if (!File.Exists(inferenceScript))
            throw new FileNotFoundException("Maicaiyin infer.py is missing.", inferenceScript);

        var modelPath = Path.Combine(toolDirectory, "joint-placement-numpy.npz");
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("Maicaiyin model is missing.", modelPath);

        var configured = Environment.GetEnvironmentVariable("MAJDATA_PYTHON");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return new ResolvedRuntime(configured, Array.Empty<string>(), null, false);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var bundled = Path.Combine(toolDirectory, "python", "python.exe");
            if (!File.Exists(bundled))
                throw new InvalidOperationException(
                    "Bundled Maicaiyin Python was not found. Reinstall the Windows release bundle.");

            var packages = Path.Combine(toolDirectory, "packages");
            if (!Directory.Exists(packages))
                throw new InvalidOperationException(
                    "Bundled Maicaiyin packages were not found. Reinstall the Windows release bundle.");

            return new ResolvedRuntime(bundled, Array.Empty<string>(), packages, true);
        }

        foreach (var candidate in LinuxPythonCandidates())
        {
            if (File.Exists(candidate))
                return new ResolvedRuntime(candidate, Array.Empty<string>(), null, false);
        }

        throw new InvalidOperationException(
            "No Python interpreter found. Run scripts/linux-install.sh or set MAJDATA_PYTHON.");
    }

    private static IEnumerable<string> LinuxPythonCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, ".venvs", "majdataviewalpha", "bin", "python");
        yield return Path.Combine(home, ".venvs", "majdataviewalpha", "bin", "python3");
        yield return "/usr/bin/python3";
        yield return "python3";
    }

    public static ProcessStartInfo CreateStartInfo(
        ResolvedRuntime runtime,
        string workingDirectory,
        string scriptPath,
        IEnumerable<string> scriptArguments)
    {
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.Environment["PYTHONNOUSERSITE"] = "1";
        startInfo.Environment["CUDA_VISIBLE_DEVICES"] = "";

        if (runtime.UseIsolatedRunner && runtime.PackageDirectory != null)
        {
            const string runner =
                "import os,runpy,sys; " +
                "packages=os.path.abspath(sys.argv[1]); script=os.path.abspath(sys.argv[2]); " +
                "sys.path=[packages,os.path.dirname(script)]+[p for p in sys.path if 'site-packages' not in p.lower() and 'dist-packages' not in p.lower()]; " +
                "sys.argv=sys.argv[2:]; runpy.run_path(script,run_name='__main__')";
            startInfo.FileName = runtime.FileName;
            startInfo.ArgumentList.Add("-I");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(runner);
            startInfo.ArgumentList.Add(runtime.PackageDirectory);
            startInfo.ArgumentList.Add(scriptPath);
            foreach (var argument in scriptArguments)
                startInfo.ArgumentList.Add(argument);
            startInfo.Environment["PIP_NO_INDEX"] = "1";
            startInfo.Environment["PIP_DISABLE_PIP_VERSION_CHECK"] = "1";
        }
        else
        {
            startInfo.FileName = runtime.FileName;
            startInfo.ArgumentList.Add(scriptPath);
            foreach (var argument in scriptArguments)
                startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
