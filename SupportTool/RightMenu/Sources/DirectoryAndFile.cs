using NaturalSort.Extension;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal static class DirectoryAndFile
{
    static readonly string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".heic", ".heif" };
    static readonly string[] videoExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".flv", ".3gp" };
    static readonly string[] audioExtensions = { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a" };

    public static void RemoveFileInfos(string path)
    {
        var allExtension = imageExtensions.Concat(videoExtensions).Concat(audioExtensions).ToHashSet();
        var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(file => allExtension.Contains(Path.GetExtension(file).ToLower()));
        foreach (var file in files)
        {
            RemoveFileInfo(file);
        }
    }

    public static void RemoveFileInfo(string file)
    {
        var extension = Path.GetExtension(file);
        var directory = Path.Combine(Path.GetDirectoryName(file), "WWW");
        Directory.CreateDirectory(directory);
        var saveFile = Path.Combine(directory, $"{Path.GetFileName(file)}");

        string command = null;
        Console.WriteLine($"Đã xóa file info: {file}");
        if (imageExtensions.Contains(extension))
        {
            command = $"magick \"{file}\" -strip \"{saveFile}\"";
        }
        else if (videoExtensions.Contains(extension) || audioExtensions.Contains(extension))
        {
            command = $"ffmpeg -i \"{file}\" -map_metadata -1 -c copy \"{saveFile}\"";
        }
        if (command != null)
        {
            Utils.RunCommand(command);
        }
        else
        {
            Console.WriteLine($"❌ Không hỗ trợ định dạng: {extension}");
        }
    }

    public static void RemoveMetas(string path)
    {
        var files = Directory.EnumerateFiles(path, "*.meta", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            File.Delete(file);
            Console.WriteLine($"Đã xóa file meta: {file}");
        }
    }

    public static void ChangeName(string paths)
    {
        int input = 0;
        do
        {
            Console.WriteLine("Chọn cách thay đổi tên:");
            Console.WriteLine("1. Đặt theo thứ tự");
            Console.WriteLine("2. Replace");
            Console.Write("Nhập lựa chọn của bạn: ");
        }
        while (!int.TryParse(Console.ReadLine(), out input) && input < 1 && input > 2);
        if (input == 1)
        {
            ChangeNameByOrder(paths);
        }
        else if (input == 2)
        {
            ChangeNameByReplace(paths);
        }
    }

    public static void ChangeNameByOrder(string paths)
    {
        Console.Write("Nhập tiêu đề: ");
        var header = Console.ReadLine();
        var files = Directory.GetFiles(paths, "*.*")
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase.WithNaturalSort())
            .ToArray();

        int totalFiles = files.Length;

        // Tự động xác định số chữ số cần thiết (vd: 100 -> 3, 1000 -> 4)
        int digits = totalFiles.ToString().Length;

        int index = 1;
        foreach (var file in files)
        {
            var directory = Path.GetDirectoryName(file);
            var extension = Path.GetExtension(file);
            var fileNumber = index.ToString($"D{digits}"); // Format thay thế PadLeft
            var newFileName = $"{header}{fileNumber}{extension}";
            var newFilePath = Path.Combine(directory, newFileName);
            File.Move(file, newFilePath);
            Console.WriteLine($"Đã đổi tên: {file} -> {newFilePath}");
            index++;
        }
    }

    public static void ChangeNameByReplace(string paths)
    {
        Console.Write("Nhập từ cần thay thế: ");
        var oldName = Console.ReadLine();
        Console.Write("Nhập từ mới: ");
        var newName = Console.ReadLine();
        var files = Directory.GetFiles(paths, "*.*");
        foreach (var file in files)
        {
            var directory = Path.GetDirectoryName(file);
            var fileName = Path.GetFileName(file);
            //var extension = Path.GetExtension(file);
            if (fileName.Contains(oldName))
            {
                var newFileName = fileName.Replace(oldName, newName);
                var newFilePath = Path.Combine(directory, newFileName);
                File.Move(file, newFilePath);
                Console.WriteLine($"Đã đổi tên: {file} -> {newFilePath}");
            }
        }
    }

    public static void Duplicate(string file)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException("File does not exist: " + file);

        string directory = Path.GetDirectoryName(file);
        string filename = Path.GetFileNameWithoutExtension(file);
        string extension = Path.GetExtension(file);

        string newName = $"{filename} - Copy{extension}";
        string newPath = Path.Combine(directory, newName);

        int index = 1;
        while (File.Exists(newPath))
        {
            newName = $"{filename} - Copy ({index}){extension}";
            newPath = Path.Combine(directory, newName);
            index++;
        }

        File.Copy(file, newPath);
    }

    public static void CopyFolderTree(string source)
    {
        var dirs = Directory.GetDirectories(source);
        var files = Directory.GetFiles(source);

        var result = new StringBuilder();
        result.Append(source);
        result.AppendLine("Directory");
        foreach (var dir in dirs)
        {
            result.AppendLine($"\t{Path.GetFileName(dir)}");
        }
        result.AppendLine("Files");
        foreach (var file in files)
        {
            result.AppendLine($"\t{Path.GetFileName(file)}");
        }
        result.ToString().CopyToClipboard();
    }

    public static void RemoveDSStores(string path)
    {
        var files = Directory.GetFiles(path, "*DS_Store*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            File.Delete(file);
            Console.WriteLine($"Removed: {file}");
        }
        files = Directory.GetFiles(path, "._*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            RemoveDSStore(file);
        }
    }

    static void RemoveDSStore(string file)
    {
        var fileName = Path.GetFileName(file);
        var fileParent = Path.GetDirectoryName(file);
        var check = Path.Combine(fileParent, fileName.Replace("._", ""));
        if (File.Exists(check) || Directory.Exists(check))
        {
            File.Delete(file);
            Console.WriteLine($"Removed: {file}");
        }
    }
}
