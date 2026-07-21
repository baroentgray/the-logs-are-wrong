using System.Diagnostics;
using System.Text;

namespace Tlaw.Verify;

public sealed record CommandExecution(CommandEvidence Evidence, string Output);

public static class CommandRunner
{
    public static async Task<CommandExecution> RunAsync(string executable, IReadOnlyList<string> arguments, string workingDirectory, string logPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var startedAt = DateTimeOffset.UtcNow;
        var exitCode = -1;
        string stdout = string.Empty;
        string stderr = string.Empty;

        try
        {
            using var process = new Process();
            process.StartInfo.FileName = executable;
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync());
            stdout = stdoutTask.Result;
            stderr = stderrTask.Result;
            exitCode = process.ExitCode;
        }
        catch (Exception exception)
        {
            stderr = $"{exception.GetType().Name}: {exception.Message}";
        }

        var finishedAt = DateTimeOffset.UtcNow;
        var output = BuildLog(executable, arguments, workingDirectory, startedAt, finishedAt, exitCode, stdout, stderr);
        await File.WriteAllTextAsync(logPath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return new CommandExecution(
            new CommandEvidence(executable, arguments.ToArray(), workingDirectory, startedAt, finishedAt, exitCode, logPath),
            output);
    }

    private static string BuildLog(string executable, IReadOnlyList<string> arguments, string workingDirectory, DateTimeOffset startedAt, DateTimeOffset finishedAt, int exitCode, string stdout, string stderr) =>
        $"executable: {executable}{Environment.NewLine}" +
        $"arguments: {string.Join(" ", arguments)}{Environment.NewLine}" +
        $"workingDirectory: {workingDirectory}{Environment.NewLine}" +
        $"startedAtUtc: {startedAt:O}{Environment.NewLine}" +
        $"finishedAtUtc: {finishedAt:O}{Environment.NewLine}" +
        $"exitCode: {exitCode}{Environment.NewLine}" +
        $"[stdout]{Environment.NewLine}{stdout}{Environment.NewLine}" +
        $"[stderr]{Environment.NewLine}{stderr}";
}
