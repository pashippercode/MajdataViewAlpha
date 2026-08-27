using System.Globalization;
using System.Text;
using MajdataEdit;

if (args.Length == 0 || args is ["-h"] or ["--help"])
{
    PrintHelp();
    return;
}

try
{
    var code = args[0] switch
    {
        "doctor" => RunDoctor(),
        "auto-onset" => await RunAutoOnset(args[1..]),
        "parse" => RunParse(args[1..]),
        _ => Unknown(args[0])
    };
    Environment.Exit(code);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.Exit(1);
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintHelp();
    return 2;
}

static void PrintHelp()
{
    Console.WriteLine("""
        majdata — Linux-native MajdataViewAlpha tooling

        Commands:
          doctor                         Verify Python, ffmpeg, and Maicaiyin assets
          auto-onset --audio PATH        Run Maicaiyin onset generation
            [--level 10] [--bpm 120] [--offset 0] [--threshold 0.55] [--title NAME]
            [--output DIR]
          parse --maidata PATH --diff N  Parse maidata to Majson JSON via simai_parser.py
            [--output PATH]

        Environment:
          MAJDATA_ROOT         Repository or release root (auto-detected when possible)
          MAJDATA_PYTHON       Python interpreter for Maicaiyin
          MAJDATA_MAICAIYIN    Override Maicaiyin tool directory
        """);
}

static int RunDoctor()
{
    var ok = true;
    ok &= Check("python3", "python3 --version");
    ok &= Check("ffmpeg", "ffmpeg -version");
    var tools = AutoOnsetRunner.ResolveToolDirectory();
    ok &= Exists("maicaiyin infer.py", Path.Combine(tools, "infer.py"));
    ok &= Exists("maicaiyin model", Path.Combine(tools, "joint-placement-numpy.npz"));
    Console.WriteLine(ok ? "doctor-ok" : "doctor-failed");
    return ok ? 0 : 1;
}

static bool Check(string name, string command)
{
    try
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "bash",
            ArgumentList = { "-lc", command + " >/dev/null 2>&1" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });
        process?.WaitForExit();
        var success = process is { ExitCode: 0 };
        Console.WriteLine(success ? $"[ok] {name}" : $"[missing] {name}");
        return success;
    }
    catch
    {
        Console.WriteLine($"[missing] {name}");
        return false;
    }
}

static async Task<int> RunAutoOnset(string[] args)
{
    var audio = Require(args, "--audio");
    var level = Get(args, "--level") ?? "10";
    var output = Get(args, "--output");
    var threshold = double.Parse(Get(args, "--threshold") ?? "0.55", CultureInfo.InvariantCulture);
    double? bpm = double.TryParse(Get(args, "--bpm"), NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
        ? b : null;
    double? offset = double.TryParse(Get(args, "--offset"), NumberStyles.Float, CultureInfo.InvariantCulture, out var o)
        ? o : null;
    var title = Get(args, "--title");

    Environment.SetEnvironmentVariable("MAJDATA_ROOT", Environment.GetEnvironmentVariable("MAJDATA_ROOT") ?? FindRepoRoot());

    var result = await AutoOnsetRunner.GenerateAsync(
        new AutoOnsetRequest(audio, level, bpm, offset, threshold, title),
        line => Console.WriteLine(line),
        CancellationToken.None);

    if (output != null)
    {
        Directory.CreateDirectory(output);
        var chartPath = Path.Combine(output, "generated-chart.txt");
        await File.WriteAllTextAsync(chartPath, result.Chart, Encoding.UTF8);
        var metaPath = Path.Combine(output, "generation.json");
        await File.WriteAllTextAsync(metaPath,
            $$"""{"bpm":{{result.Bpm}},"offset_seconds":{{result.First}},"predicted_onsets":{{result.PredictedOnsets}}}""",
            Encoding.UTF8);
        Console.WriteLine($"Wrote {chartPath}");
    }
    else
    {
        Console.WriteLine(result.Chart);
    }

    return 0;
}

static int RunParse(string[] args)
{
    var maidata = Require(args, "--maidata");
    var diff = Require(args, "--diff");
    var output = Get(args, "--output");
    var parser = ResolveParserScript();
    var python = ResolvePython();

    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = python,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    psi.ArgumentList.Add(parser);
    psi.ArgumentList.Add("--maidata");
    psi.ArgumentList.Add(maidata);
    psi.ArgumentList.Add("--diff");
    psi.ArgumentList.Add(diff);
    if (output != null)
    {
        psi.ArgumentList.Add("--output");
        psi.ArgumentList.Add(output);
    }

    using var process = System.Diagnostics.Process.Start(psi)
                        ?? throw new InvalidOperationException("Failed to start simai_parser.py");
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);

    if (output == null)
        Console.Write(stdout);
    else
        Console.WriteLine($"Wrote {output}");
    return 0;
}

static bool Exists(string name, string path)
{
    var success = File.Exists(path);
    Console.WriteLine(success ? $"[ok] {name}" : $"[missing] {name} ({path})");
    return success;
}

static string ResolveParserScript()
{
    var root = Environment.GetEnvironmentVariable("MAJDATA_ROOT") ?? FindRepoRoot();
    var path = Path.Combine(root, "simai_parser.py");
    if (!File.Exists(path))
        throw new FileNotFoundException("simai_parser.py not found", path);
    return path;
}

static string ResolvePython()
{
    var configured = Environment.GetEnvironmentVariable("MAJDATA_PYTHON");
    if (!string.IsNullOrWhiteSpace(configured))
        return configured;
    var venv = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".venvs", "majdataviewalpha", "bin", "python");
    return File.Exists(venv) ? venv : "python3";
}

static string FindRepoRoot()
{
    var dir = AppContext.BaseDirectory;
    for (var i = 0; i < 8; i++)
    {
        if (File.Exists(Path.Combine(dir, "simai_parser.py")))
            return dir;
        dir = Path.GetFullPath(Path.Combine(dir, ".."));
    }
    return Directory.GetCurrentDirectory();
}

static string? Get(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i] == name)
            return args[i + 1];
    return null;
}

static string Require(string[] args, string name) =>
    Get(args, name) ?? throw new ArgumentException($"Missing required flag {name}");
