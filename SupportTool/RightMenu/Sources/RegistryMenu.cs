using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;


internal class RegistryMenu
{
    public static void Run(string[] args)
    {
        string executablePath = @"D:\Project\CSharp\RightMenu\RightMenu\RightMenu\bin\Debug\net8.0\RightMenu.exe";
        string parentMenuKey = @"Directory\shell\KhaiPV";
        string childMenuKey = @"Directory\shell\KhaiPV\shell\XoaDSStore";

        try
        {
            // Xóa menu cha và menu con cũ nếu tồn tại
            Registry.ClassesRoot.DeleteSubKeyTree(parentMenuKey, false);

            Console.WriteLine("Đã xóa menu chuột phải cũ.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi xóa registry: " + ex.Message);
        }

        try
        {
            // Menu cha
            using (RegistryKey parentKey = Registry.ClassesRoot.CreateSubKey(parentMenuKey))
            {
                parentKey.SetValue("MUIVerb", "KhaiPV Tools");

                using (RegistryKey commandKey = parentKey.CreateSubKey("command"))
                {
                    commandKey.SetValue("", $"\"{executablePath}\" \"%1\" ADD");
                }
            }

            // Menu con
            using (RegistryKey childKey = Registry.ClassesRoot.CreateSubKey(childMenuKey))
            {
                childKey.SetValue("MUIVerb", "Xóa DS_Store");

                using (RegistryKey commandKey = childKey.CreateSubKey("command"))
                {
                    commandKey.SetValue("", $"\"{executablePath}\" REMOVE_DS \"%1\"");
                }
            }

            Console.WriteLine("Đã tạo menu cha và menu con thành công.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi tạo menu: " + ex.Message);
        }
    }

    public static void RemoveRegistryKey(string keyPath)
    {
        try
        {
            // Xóa menu cha và menu con cũ nếu tồn tại
            Registry.ClassesRoot.DeleteSubKeyTree(keyPath, false);

            Console.WriteLine("Đã xóa menu chuột phải cũ.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi xóa registry: " + ex.Message);
        }
    }

}

