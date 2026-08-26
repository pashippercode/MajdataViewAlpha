using System.Diagnostics;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MajdataEdit;

public sealed record AutoOnsetRequest(
    string AudioPath,
    string Level,
    double? Bpm,
    double? First,
    double Threshold,
    string? Title);

public sealed record AutoOnsetResult(
    string Chart,
    double Bpm,
    double First,
    int PredictedOnsets);

public static class AutoOnsetRunner
{
    public static async Task<AutoOnsetResult> GenerateAsync(
        AutoOnsetRequest request,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var toolDirectory = ResolveToolDirectory();
        var inferenceScript = Path.Combine(toolDirectory, "infer.py");
        var runtime = PythonRuntimeResolver.ResolveMaicaiyin(toolDirectory);

        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "MajdataEdit-AutoOnset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        try
        {
            reportProgress?.Invoke("Running Maicaiyin onset engine...");
            var arguments = new List<string>
            {
                request.AudioPath,
                "--output", outputDirectory,
                "--level", request.Level,
                "--threshold", request.Threshold.ToString("R", CultureInfo.InvariantCulture),
                "--model", Path.Combine(toolDirectory, "joint-placement-numpy.npz")
            };
            if (request.Bpm.HasValue)
            {
                arguments.Add("--bpm");
                arguments.Add(request.Bpm.Value.ToString("R", CultureInfo.InvariantCulture));
            }
            if (request.First.HasValue)
            {
                arguments.Add("--offset");
                arguments.Add(request.First.Value.ToString("R", CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                arguments.Add("--title");
                arguments.Add(request.Title);
            }

            var processResult = await RunProcessAsync(
                PythonRuntimeResolver.CreateStartInfo(runtime, toolDirectory, inferenceScript, arguments),
                reportProgress,
                cancellationToken);
            if (processResult.ExitCode != 0)
                throw new InvalidOperationException(BuildProcessError(processResult));

            var maidataPath = Path.Combine(outputDirectory, "maidata.txt");
            var reportPath = Path.Combine(outputDirectory, "generation.json");
            if (!File.Exists(maidataPath) || !File.Exists(reportPath))
                throw new InvalidOperationException("Maicaiyin did not produce maidata.txt or generation.json.");

            var maidata = await File.ReadAllTextAsync(maidataPath, Encoding.UTF8, cancellationToken);
            var report = JObject.Parse(await File.ReadAllTextAsync(reportPath, Encoding.UTF8, cancellationToken));
            var chart = ExtractGeneratedChart(maidata);
            var bpm = report.Value<double?>("bpm")
                      ?? throw new InvalidOperationException("Maicaiyin report is missing bpm.");
            var first = report.Value<double?>("offset_seconds")
                        ?? throw new InvalidOperationException("Maicaiyin report is missing offset_seconds.");
            var predictedOnsets = report.Value<int?>("predicted_onsets") ?? 0;
            return new AutoOnsetResult(chart, bpm, first, predictedOnsets);
        }
        finally
        {
            try
            {
                if (Directory.Exists(outputDirectory))
                    Directory.Delete(outputDirectory, true);
            }
            catch
            {
                // ponytail: temp dir cleanup is best-effort
            }
        }
    }

    public static string ResolveToolDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("MAJDATA_MAICAIYIN");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return configured;

        var root = Environment.GetEnvironmentVariable("MAJDATA_ROOT");
        if (!string.IsNullOrWhiteSpace(root))
        {
            var fromRoot = Path.Combine(root, "MajdataEdit", "tools", "Maicaiyin");
            if (Directory.Exists(fromRoot))
                return fromRoot;
            var fromRelease = Path.Combine(root, "tools", "Maicaiyin");
            if (Directory.Exists(fromRelease))
                return fromRelease;
        }

        var fromBase = Path.Combine(AppContext.BaseDirectory, "tools", "Maicaiyin");
        if (Directory.Exists(fromBase))
            return fromBase;

        foreach (var relative in new[] { "..", "../.." })
        {
            var sibling = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relative, "tools", "Maicaiyin"));
            if (Directory.Exists(sibling))
                return sibling;
        }

        return fromBase;
    }

    private static string ExtractGeneratedChart(string maidata)
    {
        const string marker = "&inote_1=";
        var start = maidata.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException("Generated maidata is missing &inote_1=.");
        start += marker.Length;
        while (start < maidata.Length && maidata[start] is '\r' or '\n')
            start++;
        var chart = maidata[start..].Trim();
        if (!chart.EndsWith("E", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Generated chart does not end with E.");
        return chart;
    }

    private static string BuildProcessError(ProcessResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return $"Maicaiyin failed with exit code {result.ExitCode}: {message.Trim()}";
    }

    private static async Task<ProcessResult> RunProcessAsync(
        ProcessStartInfo startInfo,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch
            {
                // Cancellation is best effort.
            }
        });

        var output = new StringBuilder();
        var error = new StringBuilder();
        var outputTask = ReadLinesAsync(process.StandardOutput, output, reportProgress);
        var errorTask = ReadLinesAsync(process.StandardError, error, reportProgress);
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(outputTask, errorTask);
        return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        StringBuilder destination,
        Action<string>? reportProgress)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            destination.AppendLine(line);
            if (!string.IsNullOrWhiteSpace(line))
                reportProgress?.Invoke(line);
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
