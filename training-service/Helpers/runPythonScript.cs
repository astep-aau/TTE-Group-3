using System.Diagnostics;
using System.IO;

namespace Helpers;

public class runPythonScript
{
    public string RunPythonScript(string scriptPath, string args = "")
    {
        scriptPath = $"\"{scriptPath}\"";
        
        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = string.IsNullOrWhiteSpace(args) ? scriptPath : $"{scriptPath} {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(psi)){
            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0){
                string errorMessage = errors.Trim();
                errorMessage = errorMessage.Replace("\"", "");
                throw new Exception($"{Path.GetFileName(scriptPath)} failed: {errorMessage}");
            }

            return output.Trim();
        }
    }  
}