using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

internal class SupportDLLProject
{
    public static void Run(string path)
    {
        var hFiles = Directory.GetFiles(path, "*.hpp", SearchOption.AllDirectories).ToList();
        hFiles.AddRange(Directory.GetFiles(path, "*.h", SearchOption.AllDirectories));
        var cppFiles = Directory.GetFiles(path, "*.cpp", SearchOption.AllDirectories).ToList();
        cppFiles.AddRange(Directory.GetFiles(path, "*.c", SearchOption.AllDirectories));

        // Lấy tất cả file .vcxproj trong thư mục hiện tại
        var vcxprojFiles = Directory.GetFiles(path, "*.vcxproj", SearchOption.TopDirectoryOnly);

        foreach (var vcxproj in vcxprojFiles)
        {
            UpdateVcxproj(vcxproj, hFiles, cppFiles, path);
        }
    }

    private static void UpdateVcxproj(string vcxprojPath, List<string> hFiles, List<string> cppFiles, string projectDir)
    {
        string filterPath = vcxprojPath + ".filters";
        XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

        // Load file project
        XDocument projDoc;
        try { projDoc = XDocument.Load(vcxprojPath); } catch { return; }

        var existingIncludes = projDoc.Descendants(ns + "ItemGroup").Elements(ns + "ClInclude").ToList();
        var existingCompiles = projDoc.Descendants(ns + "ItemGroup").Elements(ns + "ClCompile").ToList();

        // Load file filter
        XDocument filterDoc = null;
        if (File.Exists(filterPath))
        {
            try { filterDoc = XDocument.Load(filterPath); } catch { }
        }
        if (filterDoc == null)
        {
            filterDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(ns + "Project", new XAttribute("ToolsVersion", "4.0"))
            );
        }

        var existingFilterIncludes = filterDoc.Descendants(ns + "ItemGroup").Elements(ns + "ClInclude").ToList();
        var existingFilterCompiles = filterDoc.Descendants(ns + "ItemGroup").Elements(ns + "ClCompile").ToList();
        var existingFilters = filterDoc.Descendants(ns + "ItemGroup").Elements(ns + "Filter").ToList();

        // Helpers để parse dictionary an toàn (tránh key trùng lặp nếu file xml bị lỗi)
        Dictionary<string, XElement> ToSafeDictionary(IEnumerable<XElement> elements)
        {
            var dict = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in elements)
            {
                var key = e.Attribute("Include")?.Value;
                if (!string.IsNullOrEmpty(key) && !dict.ContainsKey(key))
                    dict[key] = e;
            }
            return dict;
        }

        var projIncludeMap = ToSafeDictionary(existingIncludes);
        var projCompileMap = ToSafeDictionary(existingCompiles);
        var filterIncludeMap = ToSafeDictionary(existingFilterIncludes);
        var filterCompileMap = ToSafeDictionary(existingFilterCompiles);
        var filterDefMap = ToSafeDictionary(existingFilters);

        // Chuẩn hoá đường dẫn Project Directory
        string projectDirNormalized = projectDir;
        if (!projectDirNormalized.EndsWith("\\") && !projectDirNormalized.EndsWith("/"))
        {
            projectDirNormalized += "\\";
        }

        string GetRelativePath(string fullPath)
        {
            if (fullPath.StartsWith(projectDirNormalized, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(projectDirNormalized.Length);
            }
            return Path.GetFileName(fullPath);
        }

        HashSet<string> currentIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> currentCompiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> neededFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Bỏ qua các thư mục build / gen code
        string[] ignores = { "\\obj\\", "\\bin\\", "\\.vs\\", "\\x64\\", "\\x86\\", "\\debug\\", "\\release\\" };

        Action<string, HashSet<string>> processFile = (file, set) =>
        {
            if (ignores.Any(ig => file.IndexOf(ig, StringComparison.OrdinalIgnoreCase) >= 0)) return;
            
            string relPath = GetRelativePath(file);
            set.Add(relPath);
            
            string dir = Path.GetDirectoryName(relPath);
            while (!string.IsNullOrEmpty(dir))
            {
                neededFilters.Add(dir);
                dir = Path.GetDirectoryName(dir);
            }
        };

        foreach (var f in hFiles) processFile(f, currentIncludes);
        foreach (var f in cppFiles) processFile(f, currentCompiles);

        // 1. Cập nhật Filter definitions (khai báo các folder Filter)
        XElement filterDefGroup = existingFilters.FirstOrDefault()?.Parent;
        if (filterDefGroup == null)
        {
            filterDefGroup = new XElement(ns + "ItemGroup");
            filterDoc.Root.Add(filterDefGroup);
        }

        foreach (var filter in neededFilters)
        {
            if (!filterDefMap.ContainsKey(filter))
            {
                var fElement = new XElement(ns + "Filter", new XAttribute("Include", filter),
                    new XElement(ns + "UniqueIdentifier", "{" + Guid.NewGuid().ToString() + "}"));
                filterDefGroup.Add(fElement);
                filterDefMap[filter] = fElement;
            }
        }

        // Xoá các Filter không còn tồn tại
        foreach (var kvp in filterDefMap.ToList())
        {
            if (!neededFilters.Contains(kvp.Key)) kvp.Value.Remove();
        }

        // Helper: Lấy ItemGroup hoặc tạo mới
        XElement GetOrCreateItemGroup(XDocument doc, List<XElement> existingElements)
        {
            var group = existingElements.FirstOrDefault()?.Parent;
            if (group == null || group.Parent == null)
            {
                group = new XElement(ns + "ItemGroup");
                doc.Root.Add(group);
            }
            return group;
        }

        var projIncludeGroup = GetOrCreateItemGroup(projDoc, existingIncludes);
        var projCompileGroup = GetOrCreateItemGroup(projDoc, existingCompiles);
        var filterIncludeGroup = GetOrCreateItemGroup(filterDoc, existingFilterIncludes);
        var filterCompileGroup = GetOrCreateItemGroup(filterDoc, existingFilterCompiles);

        void SyncElements(
            HashSet<string> currentFiles, 
            Dictionary<string, XElement> projMap, XElement projGroup, string elementName,
            Dictionary<string, XElement> filterMap, XElement filterGroup)
        {
            foreach (var file in currentFiles)
            {
                // Thêm vào project .vcxproj nếu chưa có
                if (!projMap.ContainsKey(file))
                {
                    projGroup.Add(new XElement(ns + elementName, new XAttribute("Include", file)));
                }

                // Thêm/Cập nhật vào .filters
                string filterName = Path.GetDirectoryName(file) ?? "";
                if (!filterMap.ContainsKey(file))
                {
                    var fe = new XElement(ns + elementName, new XAttribute("Include", file));
                    if (!string.IsNullOrEmpty(filterName))
                        fe.Add(new XElement(ns + "Filter", filterName));
                    filterGroup.Add(fe);
                }
                else
                {
                    var fe = filterMap[file];
                    var fChild = fe.Element(ns + "Filter");
                    if (string.IsNullOrEmpty(filterName))
                    {
                        fChild?.Remove();
                    }
                    else
                    {
                        if (fChild == null) fe.Add(new XElement(ns + "Filter", filterName));
                        else fChild.Value = filterName;
                    }
                }
            }

            // Xoá các file đã bị xoá
            foreach (var kvp in projMap.ToList())
            {
                if (!currentFiles.Contains(kvp.Key)) kvp.Value.Remove();
            }
            foreach (var kvp in filterMap.ToList())
            {
                if (!currentFiles.Contains(kvp.Key)) kvp.Value.Remove();
            }
        }

        // Đồng bộ ClInclude (*.h, *.hpp) và ClCompile (*.cpp, *.c)
        SyncElements(currentIncludes, projIncludeMap, projIncludeGroup, "ClInclude", filterIncludeMap, filterIncludeGroup);
        SyncElements(currentCompiles, projCompileMap, projCompileGroup, "ClCompile", filterCompileMap, filterCompileGroup);

        // Dọn dẹp các ItemGroup rỗng
        projDoc.Descendants(ns + "ItemGroup").Where(g => !g.HasElements).Remove();
        filterDoc.Descendants(ns + "ItemGroup").Where(g => !g.HasElements).Remove();

        // Lưu lại file
        projDoc.Save(vcxprojPath);
        filterDoc.Save(filterPath);
    }
}
