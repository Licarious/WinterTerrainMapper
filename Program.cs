using LicariousPDXLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinterTerrainMapper
{
    internal class Program
    {
        private static void Main(string[] args) {
            if (System.Environment.Version.Major < 8) {
                Console.WriteLine("This program requires .NET 8.0 or higher. Please install it and try again.");
                Console.ReadKey();
                return;
            }
            Stopwatch stopwatch = Stopwatch.StartNew();

            string localDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\"));
            Console.WriteLine(localDir);

            bool avWinterValues = false; //true - Averages all the Winter values for each province, false - uses the value covering the most shared pixels
            string name = "V 1.8";

            GetConfig();

            Dictionary<Color, Province> provDict = LicariousPDXLib.ParseDefinitions(localDir + @"\_Input\map_data\");
            LicariousPDXLib.ParseDefaultMap(provDict, localDir + @"\_Input\map_data\");
            //dictionary for storing winterName and float
            Dictionary<string, float> winterValues = new();
            ParseWinter(provDict, winterValues);
            LicariousPDXLib.ParseProvMap(provDict, Path.Combine(localDir, "_Input", "map_data", "provinces.png"));

            if (File.Exists(localDir + @"\_Input\winter.png")) {
                Dictionary<Color, Province> winterProvDict = ParseWinterMap();
                MatchCoords(provDict, winterProvDict);
                WriteWinterValues(provDict, winterValues);
            }
            DrawWinterMap(provDict);

            //print done and wait
            Console.WriteLine("Done! " + stopwatch.Elapsed + "s");
            Console.WriteLine("Press any key to exit...");
            //Console.ReadKey();

            void ParseWinter(Dictionary<Color, Province> provDict, Dictionary<string, float> winterValues) {
                //convert provDict to dictionary with ID as key
                Dictionary<int, Province> provDict2 = new();

                foreach (Province prov in provDict.Values) {
                    provDict2.Add(prov.ID, prov);
                }

                //read all files in common\province_terrain\ with "properties" or "Winter" in the Name
                string[] files = Directory.GetFiles(localDir + @"\_Input\common\province_terrain\", "*properties*").Concat(Directory.GetFiles(localDir + @"\_Input\common\province_terrain\", "*winter*")).ToArray();

                //loop through all files
                foreach (string file in files) {
                    //read all lines in file
                    string[] lines = File.ReadAllLines(file);

                    Province? tmpProv = null;
                    //loop through all lines
                    foreach (string line in lines) {
                        string cl = LicariousPDXLib.CleanLine(line);
                        if (cl.Length == 0) continue;

                        //if line starts with @ split the line on = and store the Name and float in WinterValues
                        if (cl.StartsWith("@")) {
                            string[] parts = cl.Split('=');

                            string name = parts[0].Replace("@", "").Trim();
                            float value = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                            //if Name is already in WinterValues update the value
                            if (winterValues.ContainsKey(name)) {
                                winterValues[name] = value;
                            }
                            else {
                                winterValues.Add(name, value);
                            }

                            Console.WriteLine("\t" + parts[0].Replace("@", "").Trim() + " = " + float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture));
                        }

                        //if cl contains a "=" and starts with a number get the province
                        if (cl.Contains('=') && int.TryParse(cl.Split("=")[0].Trim(), out int id)) {
                            //find prov with ID
                            if (provDict2.TryGetValue(id, out var prov)) {
                                tmpProv = prov;
                            }
                        }

                        if (cl.Contains("winter_severity_bias") && tmpProv != null) {
                            //if line contains "@" 
                            if (cl.Contains('@')) {
                                //get string after @ and match it to the value in WinterValues and set the value of the prov


                                if (winterValues.TryGetValue(cl.Split('@')[1].Trim(), out float value) &&
                                float.TryParse(value.ToString(CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedValue)) {
                                    tmpProv.Winter = parsedValue;

                                }
                                else {
                                    tmpProv.WinterAtNotFound = cl.Split('@')[1].Split("}")[0].Trim();
                                }
                            }
                            else {
                                //get all strings after = and check if they are a float, if so set the value of the prov
                                string[] parts = cl.Split('=')[^1].Split();
                                float val = 0f;
                                foreach (string part in parts) {
                                    if (float.TryParse(part.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out float value)) {
                                        val = value;
                                    }
                                }
                                if (val > 1) val = 1;
                                else if (val < 0) val = 0;

                                tmpProv.Winter = val;
                            }
                        }
                        //set tmpProv to null when line contains "}"
                        if (cl.Contains('}')) tmpProv = null;
                    }
                }


                //for every prov in provDict that has a value in WinterAtNotFound, try to find a match in WinterValues
                foreach (Province prov in provDict.Values) {
                    if (prov.WinterAtNotFound != "") {
                        //find a match in WinterValues
                        foreach (string key in winterValues.Keys) {
                            if (key.Contains(prov.WinterAtNotFound)) {
                                prov.Winter = winterValues[key];
                                //Console.WriteLine("\t" + prov.WinterAtNotFound + " = " + prov.Winter);
                                prov.WinterAtNotFound = "";
                                break;
                            }
                        }
                    }
                }
            }

            void DrawWinterMap(Dictionary<Color, Province> provDict) {
                Bitmap bmp = new(localDir + @"\_Input\map_data\provinces.png");
                Drawer.MapSize = (bmp.Width, bmp.Height);

                var winterProvinces = provDict.Values.GroupBy(prov =>
                    LicariousPDXLib.WaterTypes.Any(waterType => string.Equals(prov.Type, waterType, StringComparison.OrdinalIgnoreCase))
                    ? Color.FromArgb(255, 25, 25, 255)
                    : Color.FromArgb(255, (int)(prov.Winter * 200) + 25, (int)(prov.Winter * 200) + 25, (int)(prov.Winter * 200) + 25)
                ).ToDictionary(g => g.Key, g => g.ToList());

                var bitmaps = winterProvinces.Select(pair => {
                    var drawableProvinces = pair.Value.Cast<IDrawable>().ToList();
                    return Drawer.DrawMap(drawableProvinces, pair.Key);
                }).ToList();

                string outputDir = Path.Combine(localDir, @"_Output\", name);
                if (!Directory.Exists(outputDir)) {
                    try {
                        Directory.CreateDirectory(outputDir);
                    }
                    catch {
                        Console.WriteLine("Error: " + outputDir + " could not be created");
                        Console.ReadLine();
                        System.Environment.Exit(0);
                    }
                }

                Drawer.MergeImages(bitmaps).Save(Path.Combine(outputDir, "winter.png"));
            }

            Dictionary<Color, Province> ParseWinterMap() {
                Console.WriteLine("Parsing winter map...");

                var winterProvDict = new Dictionary<Color, Province>();

                // Load winter.png
                using var bmp = new Bitmap(localDir + @"\_Input\winter.png");

                // Check that winter.png and provinces.png are the same size
                using var provinceBitmap = new Bitmap(localDir + @"\_Input\map_data\provinces.png");
                if (bmp.Width != provinceBitmap.Width || bmp.Height != provinceBitmap.Height) {
                    Console.WriteLine("Error: winter.png and provinces.png are not the same size\npress any key to exit");
                    Console.ReadLine();
                    System.Environment.Exit(1);
                }

                LicariousPDXLib.ParseProvMap(winterProvDict, Path.Combine(localDir, "_Input", "winter.png"));
                foreach (var prov in winterProvDict.Values) {
                    prov.Winter = Math.Clamp((prov.Color.R - 25) / 200f, 0, 1);
                }

                // Merge provinces with the same winter value
                var groupedProvinces = winterProvDict.Values.GroupBy(p => p.Winter).Where(g => g.Count() > 1);
                foreach (var group in groupedProvinces) {
                    var mergedProv = group.First();
                    foreach (var prov in group.Skip(1)) {
                        Console.WriteLine($"Duplicate winter value found for {mergedProv.Color} and {prov.Color}, merging...");
                        mergedProv.Coords.UnionWith(prov.Coords);
                        winterProvDict.Remove(prov.Color);
                    }
                }

                return winterProvDict;
            }

            void MatchCoords(Dictionary<Color, Province> provDict, Dictionary<Color, Province> winterDict) {
                Console.WriteLine("Matching provinces to winter...");

                int count = provDict.Count;
                int processedCount = 0;
                int maxThreads = (int)Math.Ceiling(System.Environment.ProcessorCount * 0.8f);
                ParallelOptions options = new() { MaxDegreeOfParallelism = maxThreads };

                Console.WriteLine("Running on " + maxThreads + " threads");

                Parallel.ForEach(provDict.Values, options, prov => {
                    if (prov.Coords.Count == 0) return;

                    foreach (var winterProv in winterDict.Values) {
                        if (winterProv.Coords.Overlaps(prov.Coords)) {
                            prov.WinterMatches.Add(new ProvWinterMatch(prov, winterProv));
                        }
                    }

                    if (avWinterValues) {
                        prov.Winter = prov.WinterMatches.Sum(match => match.SharedPixels * match.WinterValue) / prov.Coords.Count;
                    }
                    else {
                        prov.Winter = prov.WinterMatches.OrderByDescending(match => match.SharedPixels).First().WinterValue;
                    }

                    prov.WinterMatches.Clear();

                    int currentCount = Interlocked.Increment(ref processedCount);
                    if (currentCount % 500 == 0) {
                        Console.WriteLine($"\t{Math.Round(currentCount / (float)count * 100, 0)}%");
                        GC.Collect();
                    }
                });
            }

            void WriteWinterValues(Dictionary<Color, Province> provDict, Dictionary<string, float> winterValues) {
                Console.WriteLine("Writing winter values...");

                List<string> waterType = new() { "sea_zones", "river_provinces", "lakes", "impassable_seas" };
                string outputDir = Path.Combine(localDir, "_Output", name);

                // Ensure the output directory exists
                Directory.CreateDirectory(outputDir);

                // Create a new file with UTF-8 BOM encoding
                using StreamWriter sw = new(Path.Combine(outputDir, "01_province_properties.txt"), false, Encoding.UTF8);

                // Write @string = float from WinterValues to sw
                foreach (var pair in winterValues) {
                    sw.WriteLine($"@{pair.Key} = {pair.Value.ToString(CultureInfo.InvariantCulture)}");
                }

                sw.WriteLine();

                // For each province in provDict
                foreach (var prov in provDict.Values) {
                    // Override water to 0.0
                    if (waterType.Contains(prov.Type)) prov.Winter = 0.0f;

                    // Write winter severity bias
                    string winterValue = winterValues.ContainsValue(prov.Winter)
                        ? $"@{winterValues.First(kv => kv.Value == prov.Winter).Key}"
                        : Math.Round(prov.Winter, 3).ToString(CultureInfo.InvariantCulture);

                    sw.WriteLine($"{prov.ID} ={{ winter_severity_bias = {winterValue} }} #{prov.Name}");
                }
            }

            void GetConfig() {
                try {
                    foreach (string line in File.ReadAllLines(localDir + @"\_Input\settings.cfg")) {
                        string cl = LicariousPDXLib.CleanLine(line);
                        if (string.IsNullOrEmpty(cl) || !cl.Contains('=')) continue;

                        string[] parts = cl.Split('=');
                        string key = parts[0].Trim();
                        string value = parts[1].Replace("\"", "").Trim();

                        if (key.Contains("name")) {
                            name = value;
                        }
                        else if (key.Contains("averageWinterValues")) {
                            avWinterValues = bool.Parse(value);
                        }
                    }
                }
                catch {
                    Console.WriteLine("Could not read settings.cfg file using default settings");
                }

                // Print values
                Console.WriteLine($"name: {name}");
                Console.WriteLine($"averageWinterValues: {avWinterValues}");
            }
        }
    }
}