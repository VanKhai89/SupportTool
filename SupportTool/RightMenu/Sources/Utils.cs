using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


internal static class Utils
{
    public static void RunCommand(string command)
    {
        try
        {
            Console.WriteLine(command);
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",    // Chạy command prompt
                Arguments = $"/c {command}", // /c để thực thi command sau đó đóng cmd
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processStartInfo))
            {
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd(); // Đọc kết quả từ output
                    Console.WriteLine(output); // In ra kết quả (nếu có)
                    process.WaitForExit(); // Đợi đến khi quá trình hoàn tất
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi chạy command: " + ex.Message);
        }
    }
}

