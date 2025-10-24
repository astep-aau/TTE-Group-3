using System.Diagnostics;

namespace Helpers;

public class runPythonScript
{
    public string RunPythonScript(string scriptPath, string args = "")
    {
        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = string.IsNullOrWhiteSpace(args) ? scriptPath : $"{scriptPath} {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(psi))
        {
            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(errors))
                Console.WriteLine("Python errors: " + errors);

            return output.Trim();
        }
    }  
}