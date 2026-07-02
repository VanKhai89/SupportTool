using System;
using System.Windows.Forms;
using MemoryViewer.Sources.Views;

namespace MemoryViewer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
