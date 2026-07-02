using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


internal static class ImageUtils
{
    public static void MakeIcon(string file)
    {
        int input = 0;
        do
        {
            Console.WriteLine("Chọn icon muốn tạo:");
            Console.WriteLine("1. Android");
            Console.WriteLine("2. iOS");
            Console.Write("Nhập lựa chọn của bạn: ");
        }
        while (!int.TryParse(Console.ReadLine(), out input) && input < 1 && input > 2);
        switch (input)
        {
            case 1:
                CreateAndroidIcon(file); break;
            case 2:
                CreateIOSIcon(file); break;
        }
    }

    static void CreateAndroidIcon(string file)
    {
        var path = Path.GetDirectoryName(file);
        var data = new Dictionary<string, int>();
        data["mipmap-hdpi"] = 72;
        data["mipmap-mdpi"] = 48;
        data["mipmap-xhdpi"] = 96;
        data["mipmap-xxhdpi"] = 144;
        data["mipmap-xxxhdpi"] = 196;
        foreach (var entry in data)
        {
            var saveFile = Path.Combine(path, "Android", entry.Key, "ic_launcher.png");
            Directory.CreateDirectory(Path.GetDirectoryName(saveFile));
            var command = $"magick {file} -resize {entry.Value}x{entry.Value} {saveFile}";
            Utils.RunCommand(command);
        }
    }

    static void CreateIOSIcon(string file)
    {
        string names = "Icon-iPad-152.png;Icon-iPad-167.png;Icon-iPad-76.png;Icon-iPad-Notification-20.png;Icon-iPad-Notification-40.png;Icon-iPad-Settings-29.png;Icon-iPad-Settings-58.png;Icon-iPad-Spotlight-40.png;Icon-iPad-Spotlight-80.png;Icon-iPhone-120.png;Icon-iPhone-180.png;Icon-iPhone-Notification-40.png;Icon-iPhone-Notification-60.png;Icon-iPhone-Settings-29.png;Icon-iPhone-Settings-58.png;Icon-iPhone-Settings-87.png;Icon-iPhone-Spotlight-120.png;Icon-iPhone-Spotlight-80.png;Icon-Store-1024.png";

        var path = Path.GetDirectoryName(file);
        var savePath = Path.Combine(path, "IOS");
        Directory.CreateDirectory(savePath);
        foreach (var item in names.Split(";"))
        {
            int size = int.Parse(item.Split("-").Last().Replace(".png", ""));
            var command = $"magick {file} -resize {size}x{size} {Path.Combine(savePath, item)}";
            Utils.RunCommand(command);
        }
        //To Do
        var resourceName = "RightMenu.Resources.Contents.json"; // Adjust namespace if necessary
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                Console.WriteLine("Không tìm thấy contents.json trong resources.");
                return;
            }
            var destinationPath = Path.Combine(savePath, "Contents.json");
            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fileStream);
            }
        }
    }
}

