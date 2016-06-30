using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManifestManager
{
    public class Program
    {
        public static string Realm = "live";
        public static string Region = "NA";
        public static string API_BASE { get { return $"http://l3cdn.riotgames.com/releases/{Program.Realm}/"; } }

        static void Main(string[] args)
        {
            Dictionary<string, string> mapsToUpdate = new Dictionary<string, string>()
            { { "EUNE", "live" }}; //TODO keep more regions updated

            foreach(string key in mapsToUpdate.Keys)
            {
                UpdateFile(mapsToUpdate[key].ToLower(), key.ToUpper());
            }

            Console.ReadKey();
        }

        public static void UpdateFile(string realm, string region, bool forceAll = false)
        {
            string _realm = Realm, _region = Region;
            Realm = realm;
            Region = region;
            string fileName = $"map_{Realm}_{Region}.txt";
            List<string> versions = Utils.GetSolutionVersions("lol_game_client_sln"), toDownload = new List<string>(versions), i;
            Dictionary<string, string> output = new Dictionary<string, string>();
            if(!forceAll && File.Exists(fileName))
            {
                using (StreamReader sr = new StreamReader(fileName))
                {
                    string str;
                    string[] split;
                    while((str = sr.ReadLine()) != null)
                    {
                        split = str.Split(new string[] { " -> " }, StringSplitOptions.RemoveEmptyEntries);
                        toDownload.Remove(split[0]);
                        output.Add(split[0], split[1]);
                    }
                }
            }

            Console.WriteLine($"Updating {toDownload.Count} items for {region}@{realm}");

            

            while (toDownload.Count > 0)
            {
                int all = toDownload.Count, ok = 0;
                i = new List<string>(toDownload);
                toDownload.Clear();
                Parallel.ForEach(i, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, version => 
                {
                    try
                    {
                        Console.Title = $"Left {all - ok}";
                        SolutionManifest m = new SolutionManifest("lol_game_client_sln", version);
                        ReleaseManifest rm = m.Projects[0].GetReleaseManifest();
                        string v = Utils.GetClientVersion(rm) + $" ({m.Projects[0].Version})";
                        GC.Collect();
                        lock (output)
                        {
                            output.Add(version, v);
                        }
                        Interlocked.Increment(ref ok);
                        Log(string.Format("{0} -> {1}", version, v));
                    }
                    catch
                    {
                        Console.WriteLine("Retrying {0}..", version);
                        lock (toDownload) { toDownload.Add(version); }
                    }
                });
            }

            using (StreamWriter sw = new StreamWriter(fileName, false))
            {
                foreach (string v in versions)
                {
                    sw.WriteLine($"{v} -> {output[v]}");
                }
            }

            File.Copy(fileName, $@"..\..\..\..\output\{fileName}", true);

            Realm = _realm;
            Region = _region;
        }

        //Single use function
        static void Fix()
        {
            foreach(FileInfo f in (new DirectoryInfo(Environment.CurrentDirectory).EnumerateFiles("map_*")))
            {
                Dictionary<string, string> output = new Dictionary<string, string>();
                string[] split = f.Name.Replace(".txt", "").Split('_');
                Realm = split[1];
                Region = split[2];
                Console.WriteLine($"Fixing {Region}@{Realm}");
                using (StreamReader sr = new StreamReader(f.Name))
                {
                    string str;
                    while ((str = sr.ReadLine()) != null)
                    {
                        split = str.Split(new string[] { " -> " }, StringSplitOptions.RemoveEmptyEntries);
                        output.Add(split[0], split[1]);
                    }
                }
                var keys = output.Keys.ToArray();
                foreach (string v in keys)
                {
                    var p = new ManifestManager.SolutionManifest("lol_game_client_sln", v).Projects[0];
                    output[v] += $" ({p.Version})";
                }

                using (StreamWriter sw = new StreamWriter(f.Name, false))
                {
                    foreach (string v in keys)
                    {
                        sw.WriteLine($"{v} -> {output[v]}");
                    }
                }

            }

            
        }

        public static void Log(string str)
        {
            Console.WriteLine(str);
            using (StreamWriter sw = new StreamWriter("log.txt", true))
            {
                sw.WriteLine(str);
            }
        }



    }
}
