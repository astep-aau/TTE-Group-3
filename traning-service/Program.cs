using System;
using System.Diagnostics;
namespace TrainingService
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Training Service is running...");

            var TrainingProcessParams = new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = "/Users/emilskov/Desktop/TTE-Group-3/traning-service/LSTMTraining.py",
                    RedirectStandardOutput = true,  //Redirect output from python to C#.
                    RedirectStandardError = true,   //Redirect errors from python to C#.
                    UseShellExecute = false         //Tells C#, the process doesnt need to start from the Terminal.
                };

            using (var process = new Process())
            {
                process.StartInfo = TrainingProcessParams; //Inherit the start paramters
                process.Start();        //Starts the process
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();  //Waits untill the process is done

                Console.WriteLine("Output: " + output); //Logs the output

                if (!string.IsNullOrEmpty(error))   //Logs errors
                    Console.WriteLine("Error: " + error);
            }   
        }
    }
}