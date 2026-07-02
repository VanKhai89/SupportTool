using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


internal static class StringUtils
{
    public static void CopyToClipboard(this string text)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c clip",
                RedirectStandardInput = true,
                UseShellExecute = false
            };

            using (var process = Process.Start(psi))
            {
                process.StandardInput.Write(text);
                process.StandardInput.Close();
            }
        }
        catch (Exception ex)
        {
        }
    }
}

