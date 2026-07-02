using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Newtonsoft.Json;

internal class FreeTexturePacker
{
    const int padding = 0;
    const int maxAtlasSize = 2048;

    class InputImage
    {
        public string Path;
        public int Width;
        public int Height;
        public Image<Rgba32> Img;
    }

    class PlacedImage
    {
        public InputImage Source;
        public int X, Y;
    }

    public static void Run(string directory)
    {
        var pathRoot = Path.GetDirectoryName(directory);
        var folderName = Path.GetFileName(directory);
        string outputAtlas = Path.Combine(pathRoot, $"{folderName}.png");
        string outputMeta = Path.Combine(pathRoot, $"{folderName}.json");

        Console.WriteLine($"Processing folder: {directory}");
        Console.WriteLine($"Output Atlas: {outputAtlas}");
        Console.WriteLine($"Output Meta: {outputMeta}");

        if (!Directory.Exists(directory))
        {
            Console.WriteLine($"Folder not found: {directory}");
            return;
        }

        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f =>
            {
                var ext = Path.GetExtension(f).ToLower();
                return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp";
            })
            .ToArray();

        if (files.Length == 0)
        {
            Console.WriteLine("No images found in folder.");
            return;
        }

        var inputs = new List<InputImage>();
        foreach (var f in files)
        {
            var img = Image.Load<Rgba32>(f);
            inputs.Add(new InputImage { Path = f, Width = img.Width, Height = img.Height, Img = img });
        }

        inputs = inputs.OrderByDescending(i => i.Height).ThenByDescending(i => i.Width).ToList();

        int maxW = inputs.Max(i => i.Width + padding);
        int sumW = inputs.Sum(i => i.Width + padding);

        int minWidth = maxW;
        int maxWidth = Math.Max(minWidth, Math.Min(sumW, maxAtlasSize));
        int trials = 2000;
        int step = Math.Max(1, (maxWidth - minWidth) / trials);
        if (step == 0) step = 1;

        float bestScore = float.MaxValue;
        int bestWidth = minWidth;
        int bestHeight = 0;
        List<PlacedImage> bestPlacement = null;

        Func<int, (int height, List<PlacedImage> placed)> PackWithWidth = (width) =>
        {
            var placed = new List<PlacedImage>();
            int curX = 0, curY = 0, rowH = 0;

            foreach (var it in inputs)
            {
                int w = it.Width + padding;
                int h = it.Height + padding;

                if (w > width)
                    return (int.MaxValue, null);

                if (curX + w <= width)
                {
                    placed.Add(new PlacedImage { Source = it, X = curX, Y = curY });
                    curX += w;
                    rowH = Math.Max(rowH, h);
                }
                else
                {
                    curY += rowH;
                    curX = 0;
                    rowH = h;
                    placed.Add(new PlacedImage { Source = it, X = curX, Y = curY });
                    curX += w;
                }
            }

            int totalHeight = curY + rowH;
            return (totalHeight, placed);
        };

        // ==== TRY WIDTHS WITH SQUARE PRIORITY SCORE ====
        for (int w = minWidth; w <= maxWidth; w += step)
        {
            var (h, placed) = PackWithWidth(w);
            if (h == int.MaxValue) continue;
            if (h > maxAtlasSize) continue;

            long area = (long)w * h;
            float ratio = (float)Math.Max(w, h) / Math.Min(w, h);
            float penalty = 1f + ((ratio - 1f) * 0.2f);  // ưu tiên vuông
            float score = area * penalty;

            if (score < bestScore)
            {
                bestScore = score;
                bestWidth = w;
                bestHeight = h;
                bestPlacement = placed;
            }
        }

        // Try exact max width
        {
            var (h, placed) = PackWithWidth(maxWidth);
            if (h != int.MaxValue && h <= maxAtlasSize)
            {
                long area = (long)maxWidth * h;
                float ratio = (float)Math.Max(maxWidth, h) / Math.Min(maxWidth, h);
                float penalty = 1f + ((ratio - 1f) * 0.2f);
                float score = area * penalty;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestWidth = maxWidth;
                    bestHeight = h;
                    bestPlacement = placed;
                }
            }
        }

        // Fallback
        if (bestPlacement == null)
        {
            int w = sumW;
            int x = 0, y = 0, maxh = 0;
            var placed = new List<PlacedImage>();

            foreach (var it in inputs)
            {
                placed.Add(new PlacedImage { Source = it, X = x, Y = y });
                x += it.Width + padding;
                maxh = Math.Max(maxh, it.Height + padding);
            }

            bestPlacement = placed;
            bestWidth = w;
            bestHeight = maxh;
        }

        // Tight bounding box
        int usedW = 0, usedH = 0;
        foreach (var p in bestPlacement)
        {
            usedW = Math.Max(usedW, p.X + p.Source.Width);
            usedH = Math.Max(usedH, p.Y + p.Source.Height);
        }

        // Build atlas
        using (var atlas = new Image<Rgba32>(Configuration.Default, usedW, usedH, new Rgba32(0, 0, 0, 0)))
        {
            foreach (var p in bestPlacement)
                atlas.Mutate(ctx => ctx.DrawImage(p.Source.Img, new Point(p.X, p.Y), 1f));

            atlas.Save(outputAtlas);
        }

        // Metadata
        var meta = new Dictionary<string, object>();
        meta["width"] = usedW;
        meta["height"] = usedH;

        var frames = new Dictionary<string, object>();
        foreach (var p in bestPlacement)
        {
            frames[Path.GetRelativePath(directory, p.Source.Path).Replace("\\", "/")] = new
            {
                x = p.X,
                y = p.Y,
                w = p.Source.Width,
                h = p.Source.Height
            };
        }

        meta["frames"] = frames;

        File.WriteAllText(outputMeta, JsonConvert.SerializeObject(meta, Formatting.Indented));

        Console.WriteLine($"Done. Atlas: {outputAtlas} ({usedW}x{usedH}), metadata: {outputMeta}");
    }
}
