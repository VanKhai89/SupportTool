using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NaturalSort.Extension;

internal class MenuHandle
{

    public static void Run(string[] args)
    {
        //var paths = @"D:\Project\Unity\Pikachu\PawLink\IOS\AnimalTiles\Unity-iPhone\Images.xcassets\AppIcon.appiconset";
        //var files = Directory.GetFiles(paths, "*.png");
        //Console.WriteLine(string.Join("\n", files.Select(x => Path.GetFileName(x))));
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(string.Join("\n", args));
        if (args.Length >= 2)
        {
            switch (args[0])
            {
                case "REMOVE_DS":
                    DirectoryAndFile.RemoveDSStores(args[1]);
                    break;
                case "REMOVE_FILE_INFO":
                    DirectoryAndFile.RemoveFileInfos(args[1]);
                    break;
                case "REMOVE_UNITY_META":
                    DirectoryAndFile.RemoveMetas(args[1]);
                    break;
                case "CREATE_ICON":
                    ImageUtils.MakeIcon(args[1]);
                    break;
                case "EXPORT_COCOS2DX_IMAGE":
                    ExportCocos2dxImage.Run(args[1]);
                    break;
                case "CHANGE_NAME":
                    DirectoryAndFile.ChangeName(args[1]);
                    break;
                case "DUPLICATE":
                    DirectoryAndFile.Duplicate(args[1]);
                    return;
                case "FREE_TEXTURE_PACKER":
                    FreeTexturePacker.Run(args[1]);
                    break;
                case "COPY_FOLDER_TREE":
                    DirectoryAndFile.CopyFolderTree(args[1]);
                    break;
                case "INCLUDE_CPP":
                    SupportDLLProject.Run(args[1]);
                    break;
                case "RESIZE_IMAGE":
                    ImageUtils.Resize(args[1]);
                    break;
                case "COPY_BASE64":
                    ImageUtils.CopyBase64(args[1]);
                    break;
                default:
                    Console.WriteLine("Invalid command.");
                    break;
            }
        }
        //Console.WriteLine("Press any key to exit");
        //Console.ReadKey();
    }

    void BuildReg()
    {

    }
}

