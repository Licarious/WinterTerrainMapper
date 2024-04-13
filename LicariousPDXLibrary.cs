using System.Drawing;
using WinterTerrainMapper;

namespace LicariousPDXLibrary
{
    internal class Province
    {
        public int id = -1;
        public string name = "";
        public Color color = Color.FromArgb(0, 0, 0, 0);
        public HashSet<(int x, int y)> coords = new();
        public string type = "";

        public float winter = 0.0f;
        public string winterAtNotFound = "";
        
        public HashSet<float> winterValues = new();

        public List<ProvWinterMatch> wmList = new();

        public Dictionary<Color, Province> winterDict = new();

        public Province(Color color, int id, string name) {
            this.color = color;
            this.id = id;
            this.name = name;
        }
        public Province() { }
    }

    internal class LicariousPDXLib
    {
        public static string CleanLine(string line) => line.Split('#')[0].Replace("{", " { ").Replace("}", " } ").Replace("=", " = ").Replace("  ", " ").Trim();

        public static Dictionary<Color, Province> ParseDefinitions(string path) {
            Console.WriteLine("Parsing definitions...");
            Dictionary<Color, Province> provDict = new();

            //string[] lines = File.ReadAllLines(localDir + @"\_Input\FillColors.txt");
            string[] lines = File.ReadAllLines(path + @"definition.csv");
            foreach (string line in lines) {
                string l1 = CleanLine(line);
                if (l1.Length == 0) continue;

                //split the line on ; the first part is the id and the next 3 are the rgb values
                string[] parts = l1.Split(';');
                //try parse the id
                if (int.TryParse(parts[0], out int id)) {
                    if (id == 0) continue; //games do not use id 0
                    int r = int.Parse(parts[1]);
                    int g = int.Parse(parts[2]);
                    int b = int.Parse(parts[3]);
                    string name = parts[4];

                    //if key does not exist, add it
                    if (!provDict.ContainsKey(Color.FromArgb(r, g, b))) {
                        provDict.Add(Color.FromArgb(r, g, b), new Province(Color.FromArgb(r, g, b), id, name));
                    }
                }
            }
            return provDict;
        }
        
        public static void ParseDefaultMap(Dictionary<Color, Province> provDict, string path) {
            Console.WriteLine("Parsing default.map...");
            //read all string in default map in the Input folder
            string[] lines = File.ReadAllLines(path + @"default.map");

            //loop through all lines
            foreach (string line in lines) {
                string cl = CleanLine(line);
                if (cl.Length == 0) continue;

                //if cl contains RANGE or LIST
                GetRangeList(cl, provDict);
            }
        }

        public static void GetRangeList(string line, Dictionary<Color, Province> provDict) {
            string type = line.Split("=")[0].Trim().ToLower();

            //lists of water and wasteland types
            List<string> watterTypes = new List<string>() { "sea_zones", "lakes", "river_provinces" };
            List<string> wastelandTypes = new List<string>() { "wasteland", "impassable_terrain", "uninhabitable"};

            //if line contains RANGE
            if (line.ToUpper().Contains("RANGE")) {
                //split the line on { and }
                string[] parts = line.Split('{', '}')[1].Split();
                //get the first and last number in parts
                int first = -1;
                int last = -1;
                foreach (string part in parts) {
                    //try parse int
                    if (int.TryParse(part, out int num)) {
                        if (first == -1) first = num;
                        else last = num;
                    }
                }
                //loop through all numbers between first and last
                for (int i = first; i <= last; i++) {
                    //find prov with id i
                    foreach (Province prov in provDict.Values) {
                        if (prov.id == i) {
                            //if prov.type is in watterTypes and type is in wastelandTypes or vice versa then set prov.type to "impassable_sea"
                            if (watterTypes.Contains(prov.type) && wastelandTypes.Contains(type) || wastelandTypes.Contains(prov.type) && watterTypes.Contains(type)) {
                                prov.type = "impassable_sea";
                            }
                            else {
                                //set type of prov
                                prov.type = type;
                            }
                            //print prov id and type
                            //Console.WriteLine(prov.id + " " + prov.type);
                        }
                    }
                }

            }
            else if (line.ToUpper().Contains("LIST")) {
                //split the line on { and }
                string[] parts = line.Split('{', '}')[1].Split();
                //loop through all parts
                foreach (string part in parts) {
                    //try parse int
                    if (int.TryParse(part, out int num)) {
                        //find prov with id num

                        foreach (Province prov in provDict.Values) {
                            if (prov.id == num) {
                                if ((prov.type == "sea_zones" && (type == "wasteland" || type == "impassable_terrain"))
                                || (prov.type == "wasteland" || prov.type == "impassable_terrain") && type == "sea_zones") {
                                    prov.type = "impassable_sea";
                                }
                                else {
                                    //set type of prov
                                    prov.type = type;
                                }
                            }
                        }
                    }
                }
            }

        }

        public static void ParseProvMap(Dictionary<Color, Province> provDict, string path) {
            Console.WriteLine("Parsing provinces map...");
            //load the provinces.bmp image
            Bitmap bmp = new(path + @"map_data\provinces.png");
            Bitmap? winter = null;
            if (File.Exists(path + @"\winter.png")) {
                winter = new(path + @"\winter.png");
            }
            //loop through all pixels
            for (int x = 0; x < bmp.Width; x++) {
                for (int y = 0; y < bmp.Height; y++) {
                    //get the color of the pixel
                    Color color = bmp.GetPixel(x, y);
                    //if color is in provDict add the coord to the prov
                    if (provDict.TryGetValue(color, out Province value)) {
                        value.coords.Add(new(x, y));
                        if (winter != null) {
                            Color winterColor = winter.GetPixel(x, y);
                            //convert to int using the Red - 25 / 200 and add it to value if it is not already in winterValues
                            float winterValue = ((float)winterColor.R -25) / 200;
                            if (!value.winterValues.Contains(winterValue)) {
                                value.winterValues.Add(winterValue);
                                //Console.WriteLine(value.id +" - "+winterValue);
                            }
                        }
                    }
                }

                //print progress every 20%
                if (x % (bmp.Width / 5) == 0) {
                    Console.WriteLine("\t" + (x / (bmp.Width / 5) * 20) + "%");
                }
            }
        }

    }

}

