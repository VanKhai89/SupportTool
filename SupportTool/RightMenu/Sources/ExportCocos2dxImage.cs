using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

internal class ExportCocos2dxImage
{
    class SpriteFrame
    {
        public string name;
        public Rectangle frame;
        public bool rotated;
        public Size sourceSize;
    }


    public static void Run(string file)
    {
        string plistPath = file;
        string imagePath = file.Replace(".plist", ".png");
        string outputDir = Path.Combine(Path.GetDirectoryName(file), "output");

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var frames = ReadPlistFrames(plistPath);
        using var sourceImage = Image.Load<Rgba32>(imagePath);

        foreach (var f in frames)
        {
            Rectangle rect = f.frame;

            // Swap width & height if rotated
            if (f.rotated)
                rect = new Rectangle(rect.X, rect.Y, rect.Height, rect.Width);

            var cropped = sourceImage.Clone(ctx => ctx.Crop(rect));

            if (f.rotated)
            {
                // Rotate -90 degrees (counter-clockwise) to restore
                cropped.Mutate(ctx => ctx.Rotate(RotateMode.Rotate270));
            }

            string outputPath = Path.Combine(outputDir, Path.GetFileName(f.name));
            cropped.Save(outputPath);
            Console.WriteLine($"Saved: {outputPath}");
        }

        Console.WriteLine("Done!");
    }

    static List<SpriteFrame> ReadPlistFrames(string plistPath)
    {
        var doc = new XmlDocument();
        doc.Load(plistPath);

        var framesList = new List<SpriteFrame>();
        var dict = doc.SelectSingleNode("//plist/dict/dict");

        foreach (XmlNode keyNode in dict.ChildNodes)
        {
            if (keyNode.Name != "key") continue;

            string name = keyNode.InnerText;
            var valueNode = keyNode.NextSibling;
            var frameData = new SpriteFrame { name = name };

            foreach (XmlNode child in valueNode.ChildNodes)
            {
                if (child.Name != "key") continue;

                string key = child.InnerText;
                var data = child.NextSibling;

                switch (key)
                {
                    case "frame":
                        frameData.frame = ParseRect(data.InnerText);
                        break;
                    case "rotated":
                        frameData.rotated = data.Name == "true";
                        break;
                }
            }

            framesList.Add(frameData);
        }

        return framesList;
    }

    static Rectangle ParseRect(string s)
    {
        // Example: {{4,4},{102,142}}
        s = s.Replace("{", "").Replace("}", "");
        var parts = s.Split(',');

        return new Rectangle(
            int.Parse(parts[0]),
            int.Parse(parts[1]),
            int.Parse(parts[2]),
            int.Parse(parts[3])
        );
    }
}

