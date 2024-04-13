using System.Diagnostics;
using System.Drawing;
using System.Text;
using WinterTerrainMapper;
using LicariousPDXLibrary;
using System.Globalization;

namespace WinterTerrainMaper
{
    internal class Program
    {
        private static void Main(string[] args) {

            //starting at the root of the progject move up unitll we find the _Input folder
            string localDir = Directory.GetCurrentDirectory();
            while (!Directory.Exists(localDir + @"\_Input")) {
                localDir = Directory.GetParent(localDir).FullName;
                //stop looking after we reach the root of the drive
                if (localDir.Length <= 4) {
                    Console.WriteLine("Could not find _Input folder");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }
            }

            bool avWinterValues = false; //true - Averages all the winter values for each province, false - uses the value covering the most shared pixels
            string name = "V 1.8";
            
            //send a eror message if the .net version is not 7.0
            if (Environment.Version.Major < 7) {
                Console.WriteLine("This program requires .NET 7.0 or higher to run.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }


            Stopwatch stopwatch = new();
            stopwatch.Start();
            
            GetConfig();
            
            Dictionary<Color, Province> provDict = LicariousPDXLib.ParseDefinitions(localDir + @"\_Input\map_data\");
            LicariousPDXLib.ParseDefaultMap(provDict, localDir + @"\_Input\map_data\");
            //dictonary for storing winterName and float
            Dictionary<string, float> winterValues = new();
            ParseWinter(provDict, winterValues);
            LicariousPDXLib.ParseProvMap(provDict, localDir + @"\_Input\");
            
            if (File.Exists(localDir + @"\_Input\winter.png")) {
                Dictionary<Color, Province> winterProvDict = ParseWinterMap();
                MatchCoords2(provDict, winterProvDict);
                WriteWinterValues(provDict, winterValues);
            }
            DrawWinterMap(provDict);

            //print done and wait
            Console.WriteLine("Done! " + stopwatch.Elapsed + "s");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            void ParseWinter(Dictionary<Color, Province> provDict, Dictionary<string, float> winterValues) {
                //convert provDict to dictionary with id as key
                Dictionary<int, Province> provDict2 = new();

                foreach (Province prov in provDict.Values) {
                    provDict2.Add(prov.id, prov);
                }

                //read all files in common\province_terrain\ with "properties" or "winter" in the name
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

                        //if line starts with @ split the line on = and store the name and float in winterValues
                        if (cl.StartsWith("@")) {
                            string[] parts = cl.Split('=');
                            
                            string name = parts[0].Replace("@", "").Trim();
                            float value = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                            //if name is already in winterValues update the value
                            if (winterValues.ContainsKey(name)) {
                                winterValues[name] = value;
                            }
                            else {
                                winterValues.Add(name, value);
                            }

                            Console.WriteLine("\t"+parts[0].Replace("@", "").Trim() + " = " + float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture));
                        }

                        //if cl contains a "=" and starts with a number get the province
                        if (cl.Contains('=') && int.TryParse(cl.Split("=")[0].Trim(), out int id)) {
                            //find prov with id
                            if (provDict2.TryGetValue(id, out Province prov)) {
                                tmpProv = prov;
                            }
                        }

                        if (cl.Contains("winter_severity_bias") && tmpProv != null) {
                            //if line contains "@" 
                            if (cl.Contains('@')) {
                                //get string after @ and match it to the value in winterValues and set the value of the prov
                                if (winterValues.TryGetValue(cl.Split('@')[1].Trim(), out float value) &&
                                float.TryParse(value.ToString(CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedValue)) { 
                                    tmpProv.winter = parsedValue;

                                }
                                else {
                                    tmpProv.winterAtNotFound = cl.Split('@')[1].Split("}")[0].Trim();
                                }
                            }
                            else {
                                //get all strings after = and check if they are a float, if so set the value of the prov
                                string[] parts = cl.Split('=')[^1].Split();
                                float val = 0f;
                                foreach (string part in parts) {
                                    if (winterValues.TryGetValue(part.Trim(), out float value) &&
                                    float.TryParse(value.ToString(CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedValue)) {
                                        val = parsedValue;

                                    }
                                }
                                if (val > 1) val = 1;
                                else if (val < 0) val = 0;

                                tmpProv.winter = val;
                            }
                        }
                        //set tmpProv to null when line contains "}"
                        if (cl.Contains('}')) tmpProv = null;
                    }
                }

                
                //for every prov in provDict that has a value in winterAtNotFound, try to find a match in winterValues
                foreach (Province prov in provDict.Values) {
                    if (prov.winterAtNotFound != "") {
                        //find a match in winterValues
                        foreach (string key in winterValues.Keys) {
                            if (key.Contains(prov.winterAtNotFound)) {
                                prov.winter = winterValues[key];
                                //Console.WriteLine("\t" + prov.winterAtNotFound + " = " + prov.winter);
                                prov.winterAtNotFound = "";
                                break;
                            }
                        }
                    }
                }
            }

            void DrawWinterMap(Dictionary<Color, Province> provDict) {
                Bitmap bmp = new(localDir + @"\_Input\map_data\provinces.png");
                //create a new bitmap of the same size
                Bitmap bmpWinter = new(bmp.Width, bmp.Height);

                //for each prov in provDict
                foreach (Province prov in provDict.Values) {
                    //loop through all coords
                    foreach ((int x, int y) in prov.coords) {
                        //set the pixel to the winter value
                        bmpWinter.SetPixel(x, y, Color.FromArgb(255, (int)(prov.winter * 200) + 25, (int)(prov.winter * 200) + 25, (int)(prov.winter * 200) + 25));
                    }
                }

                //if the output folder doesn't exist create it
                if (!Directory.Exists(localDir + @"\_Output\" + name+"\\")) {
                    try {
                        Directory.CreateDirectory(localDir + @"\_Output\" + name + "\\");
                    }
                    catch {
                        Console.WriteLine("Error " + localDir + @"\_Output\" + name + "\\" + "could not be created");
                        //wait for user input to close
                        Console.ReadLine();
                        Environment.Exit(0);
                    }
                }

                //save the bitmap
                bmpWinter.Save(localDir + @"\_Output\"+name+@"\winter.png");
            }

            Dictionary<Color, Province> ParseWinterMap() {
                Console.WriteLine("Parsing winter map...");
                
                Dictionary<Color, Province> winterProvDict = new();

                //load witer.png
                Bitmap bmp = new(localDir + @"\_Input\winter.png");

                //check that winter.png and provinces.png are the same size
                Bitmap provbmp = new(localDir + @"\_Input\map_data\provinces.png");
                if (bmp.Width != provbmp.Width || bmp.Height != provbmp.Height) {
                    Console.WriteLine("Error: winter.png and provinces.png are not the same size\npress any key to exit");
                    //wait for user input
                    Console.ReadLine();
                    //exit program
                    Environment.Exit(0);
                }


                //loop through all pixels
                for (int x = 0; x < bmp.Width; x++) {
                    for (int y = 0; y < bmp.Height; y++) {
                        //get the color of the pixel
                        Color color = bmp.GetPixel(x, y);
                        //if color is in provDict add the coord to the prov
                        if (winterProvDict.TryGetValue(color, out Province value)) {
                            value.coords.Add(new(x, y));
                        }
                        else {
                            //create new prov
                            Province prov = new();
                            prov.coords.Add(new(x, y));
                            prov.color = color;

                            float winter = (color.R - 25) / 200f;
                            //constrain value between 0 and 1
                            if (winter > 1) winter = 1;
                            else if (winter < 0) winter = 0;
                            prov.winter = winter;
                            
                            winterProvDict.Add(color, prov);
                        }
                    }

                    //print progress every 20%
                    if (x % (bmp.Width / 5) == 0) {
                        Console.WriteLine("\t"+(x / (bmp.Width / 5) * 20) + "%");
                    }
                }

                //if there are multiple provs with the same winter value merge the coords and remove the duplicate provs
                foreach (Province prov in winterProvDict.Values) {
                    if (winterProvDict.Values.Where(p => p.winter == prov.winter).Count() > 1) {
                        foreach (Province prov2 in winterProvDict.Values.Where(p => p.winter == prov.winter)) {
                            if (prov != prov2) {
                                Console.WriteLine("duplicate winter value fond for " + prov.color + " and " + prov2.color + " merging...");
                                prov.coords.UnionWith(prov2.coords);
                                winterProvDict.Remove(prov2.color);
                            }
                        }
                    }
                }



                return winterProvDict;
            }

            void MatchCoords(Dictionary<Color, Province> provDict, Dictionary<Color, Province> winterDict) {
                Console.WriteLine("matching provs to Winter...");
                int count = provDict.Count;
                int i = 0;

                //loop through all provs in provDict
                //paralell for each
                Parallel.ForEach(provDict.Values, prov => {
                    if (prov.coords.Count == 0) {
                        //Console.WriteLine("prov " + prov.id + " has no coords");
                        return;
                    }

                    //find all winterProv in winterDict whose coords overlap with prov.coords and create a new ProvWinterMatch with prov and winterProv and add it to prov.wmList
                    prov.wmList.AddRange(winterDict.Values.Where(winterProv => winterProv.coords.Overlaps(prov.coords)).Select(winterProv => new ProvWinterMatch(prov, winterProv)));

                    
                    if (avWinterValues) {
                        //do a weighted average of the winter values of the provs in wmList
                        float sum = 0;
                        foreach (ProvWinterMatch match in prov.wmList) {
                            sum += match.sharedPixels * match.winterValue;
                        }
                        prov.winter = sum / prov.coords.Count;
                    }
                    else {
                        prov.winter = prov.wmList.OrderByDescending(match => match.sharedPixels).First().winterValue;
                    }
                    i += 1;
                    //print progress every 500 provs
                    if (i % 500 == 0) {
                        Console.WriteLine("\t" + Math.Round((i / (float)count * 100), 0)+"%");
                    }
                });

            }

            void MatchCoords2(Dictionary<Color, Province> provDict, Dictionary<Color, Province> winterDict) {
                Console.WriteLine("matching provs to Winter...");
                int count = provDict.Count;
                int i = 0;

                //loop through all provs in provDict
                //paralell for each
                Parallel.ForEach(provDict.Values, prov => {
                    if (prov.coords.Count == 0) {
                        //Console.WriteLine("prov " + prov.id + " has no coords");
                        return;
                    }

                    foreach (Province winterProv in winterDict.Values) {
                        //if prov.winterValues does not contain winterProv.winter continue
                        if (!prov.winterValues.Contains(winterProv.winter)) {
                            continue;
                        }

                        //find all winterProv in winterDict whose coords overlap with prov.coords and create a new ProvWinterMatch with prov and winterProv and add it to prov.wmList
                        if (winterProv.coords.Overlaps(prov.coords)) {
                            prov.wmList.Add(new ProvWinterMatch(prov, winterProv));
                        }
                    }

                    /*
                    //find all winterProv in winterDict whose coords overlap with prov.coords and create a new ProvWinterMatch with prov and winterProv and add it to prov.wmList
                    prov.wmList.AddRange(winterDict.Values.Where(winterProv 
                        => winterProv.coords.Overlaps(prov.coords)).Select(winterProv 
                        => new ProvWinterMatch(prov, winterProv)));
                    */

                    if (avWinterValues) {
                        //do a weighted average of the winter values of the provs in wmList
                        float sum = 0;
                        foreach (ProvWinterMatch match in prov.wmList) {
                            sum += match.sharedPixels * match.winterValue;
                        }
                        prov.winter = sum / prov.coords.Count;
                    }
                    else {
                        prov.winter = prov.wmList.OrderByDescending(match => match.sharedPixels).First().winterValue;
                    }
                    i += 1;
                    //print progress every 500 provs
                    if (i % 500 == 0) {
                        Console.WriteLine("\t" + Math.Round((i / (float)count * 100), 0) + "%");
                    }
                });

            }

            void WriteWinterValues(Dictionary<Color, Province> provDict, Dictionary<string, float> winterValues) {
                Console.WriteLine("Writing winter values...");

                List<string> waterType = new() { "sea_zones", "river_provinces", "lakes", "impassable_seas" };

                //if the output folder doesn't exist create it
                if (!Directory.Exists(localDir + @"\_Output\" + name + "\\")) {
                    Directory.CreateDirectory(localDir + @"\_Output\" + name + "\\");
                }

                //create a new file with utf-8-bom encoding
                using StreamWriter sw = new(localDir + @"\_Output\"+name+@"\01_province_properties.txt", false, Encoding.UTF8);
                

                //write @string = float from winterValues to sw
                foreach (KeyValuePair<string, float> pair in winterValues) {
                    sw.WriteLine("@" + pair.Key + " = " + pair.Value);
                }

                sw.WriteLine();

                //for each prov in provDict
                foreach (Province prov in provDict.Values) {
                    //override water to 0.0
                    if (waterType.Contains(prov.type)) prov.winter = 0.0f;

                    //if wValue is a value in winterValues write name 
                    if (winterValues.ContainsValue(prov.winter)) {
                        sw.WriteLine(prov.id + " ={ winter_severity_bias = @" + winterValues.Keys.ElementAt(winterValues.Values.ToList().IndexOf(prov.winter)) + " } #" + prov.name);
                    }
                    else {
                        sw.WriteLine(prov.id + " ={ winter_severity_bias = " + (float)Math.Round(prov.winter, 3) + " } #" + prov.name);
                    }
                }

                sw.Close();
            }
            
            void GetConfig() {
                //read settings.cfg and set variables
                try {
                    string[] lines = File.ReadAllLines(localDir + @"\_Input\settings.cfg");

                    foreach (string line in lines) {
                        string cl = LicariousPDXLib.CleanLine(line);

                        if (cl == "") continue;

                        if (cl.Contains('=')) {
                            string[] parts = cl.Split('=');

                            if (parts[0].Contains("name")) {
                                name = parts[1].Replace("\"", "").Trim();
                            }
                            else if (parts[0].Contains("averageWinterValues")) {
                                avWinterValues = bool.Parse(parts[1].Trim());
                            }
                        }
                    }
                }
                catch {
                    Console.WriteLine("Could not read settings.cfg file using default settings");
                }

                //print values
                Console.WriteLine("name: " + name);
                Console.WriteLine("averageWinterValues: " + avWinterValues);

            }
        }
    }
}