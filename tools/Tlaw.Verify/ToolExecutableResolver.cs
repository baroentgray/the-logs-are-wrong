namespace Tlaw.Verify;

public static class ToolExecutableResolver
{
    public static string ResolveGitExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var installedGit = Path.Combine(programFiles, "Git", "bin", "git.exe");
            if (File.Exists(installedGit))
            {
                return installedGit;
            }
        }

        return "git";
    }
}
