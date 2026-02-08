using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using WinterTerrainMapper;

namespace LicariousPDXLibrary
{
    internal class Province : IDrawable
    {
        public int ID { get; set; } = -1;
        public string Name { get; set; } = string.Empty;
        public Color Color { get; set; } = Color.FromArgb(0, 0, 0, 0);
        public HashSet<(int x, int y)> Coords { get; set; } = new();
        public string Type { get; set; } = string.Empty;
        public float Winter { get; set; } = 0.0f;
        public string WinterAtNotFound { get; set; } = string.Empty;
        public List<ProvWinterMatch> WinterMatches { get; set; } = new();
        public List<(int x, int y, int h, int w)> MaximumRectangles { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Province(Color color, int id, string name) {
            Color = color;
            ID = id;
            Name = name;
        }

        public Province() { }

        public void GetCenter(bool floodFill = false) {
            throw new NotImplementedException();
        }
    }

    internal class LicariousPDXLib
    {
        public static readonly HashSet<string> WaterTypes = new() { "sea_zones", "lakes", "river_provinces", "impassable_seas" };
        public static readonly HashSet<string> WastelandTypes = new() { "wasteland", "impassable_terrain", "impassable_mountains", "uninhabitable", "impassable_seas" };

        public static string CleanLine(string line) => line.Split('#')[0].Replace("{", " { ").Replace("}", " } ").Replace("=", " = ").Replace("  ", " ").Trim();

        public static Dictionary<Color, Province> ParseDefinitions(string path) {
            Console.WriteLine("Parsing definitions...");
            var provDict = new Dictionary<Color, Province>();

            foreach (var line in File.ReadLines(Path.Combine(path, "definition.csv"))) {
                var l1 = CleanLine(line);
                if (l1.Length == 0) continue;

                var parts = l1.Split(';');
                if (int.TryParse(parts[0], out int id) && id != 0) {
                    var color = Color.FromArgb(int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
                    if (!provDict.ContainsKey(color)) {
                        provDict[color] = new Province(color, id, parts[4]);
                    }
                }
            }
            return provDict;
        }

        public static void ParseDefaultMap(Dictionary<Color, Province> provDict, string path) {
            Console.WriteLine("Parsing default.map...");
            foreach (var line in File.ReadLines(Path.Combine(path, "default.map"))) {
                var cl = CleanLine(line);
                if (cl.Length > 0) {
                    GetRangeList(cl, provDict);
                }
            }

            //print the number types of provinces
            Dictionary<string, int> typeCount = new();
            foreach (var prov in provDict.Values) {
                if (typeCount.ContainsKey(prov.Type)) {
                    typeCount[prov.Type]++;
                }
                else {
                    typeCount[prov.Type] = 1;
                }
            }
            foreach (var kvp in typeCount) {
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
            }
        }

        public static void GetRangeList(string line, Dictionary<Color, Province> provDict) {
            var type = line.Split('=')[0].Trim().ToLower();

            void UpdateProvinceType(Province prov) {
                if ((WaterTypes.Contains(prov.Type) && WastelandTypes.Contains(type)) || (WastelandTypes.Contains(prov.Type) && WaterTypes.Contains(type))) {
                    prov.Type = "impassable_seas";
                }
                else {
                    prov.Type = type;
                }
            }

            var parts = System.Text.RegularExpressions.Regex.Matches(line, @"\d+")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value)
                .ToList();

            if (line.ToUpper().Contains("RANGE") && parts.Count >= 2 && int.TryParse(parts.First(), out int first) && int.TryParse(parts.Last(), out int last)) {
                for (int i = first; i <= last; i++) {
                    if (provDict.Values.FirstOrDefault(prov => prov.ID == i) is Province prov) {
                        UpdateProvinceType(prov);
                    }
                }
            }
            else if (line.ToUpper().Contains("LIST")) {
                foreach (var part in parts) {
                    if (int.TryParse(part, out int num) && provDict.Values.FirstOrDefault(prov => prov.ID == num) is Province prov) {
                        UpdateProvinceType(prov);
                    }
                }
            }
        }

        public static void ParseProvMap(Dictionary<Color, Province> provinces, string path) {
            if (!File.Exists(path)) {
                Console.WriteLine($"File not found: {path}");
                return;
            }

            using Bitmap image = new(path);

            Console.WriteLine("Parsing Map");

            // Lock the bitmap's bits
            Rectangle rect = new(0, 0, image.Width, image.Height);
            BitmapData bmpData = image.LockBits(rect, ImageLockMode.ReadOnly, image.PixelFormat);

            int width = image.Width;
            int height = image.Height;
            int stride = bmpData.Stride;
            int pixelSize = Image.GetPixelFormatSize(image.PixelFormat) / 8;

            try {
                // Get the address of the first line
                IntPtr ptr = bmpData.Scan0;

                unsafe {
                    byte* rgbValues = (byte*)ptr;

                    // Process the pixel data
                    for (int y = 0; y < height; y++) {
                        byte* row = rgbValues + (y * stride);
                        for (int x = 0; x < width; x++) {
                            byte* pixel = row + (x * pixelSize);
                            Color c = Color.FromArgb(
                                255,      // A
                                pixel[2], // R
                                pixel[1], // G
                                pixel[0]  // B
                            );

                            if (!provinces.TryGetValue(c, out Province? value)) {
                                value = new Province { Color = c };
                                provinces[c] = value;
                            }
                            value.Coords.Add((x, y));
                        }
                        if (y % (height / 5) == 0) {
                            Console.WriteLine($"\t{y * 100 / height}%");
                        }
                    }
                }
            }
            finally {
                // Unlock the bits
                image.UnlockBits(bmpData);
            }
        }
    }
}

