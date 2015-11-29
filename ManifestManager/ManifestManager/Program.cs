﻿using System;
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
        public static string Realm = "pbe";
        public static string Region = "PBE";
        public static string API_BASE { get { return $"http://l3cdn.riotgames.com/releases/{Program.Realm}/"; } }

        static void Main(string[] args)
        {
            //string test = Utils.GetClientVersion(new SolutionManifest("lol_game_client_sln", "0.0.0.254").Projects[0].GetReleaseManifest());

            List<string> versions = Utils.GetSolutionVersions("lol_game_client_sln");
            List<string> toDownload = new List<string>(versions);
            List<string> i;
            Dictionary<string, string> output = new Dictionary<string, string>();

            int all = versions.Count;
            int ok = 0;
            //toDownload.Reverse();
            //foreach (string version in toDownload)
            //{
            //    try
            //    {
            //        Console.Title = $"Left {all - ok}";
            //        SolutionManifest m = new SolutionManifest("lol_game_client_sln", version);
            //        ReleaseManifest rm = m.Projects[0].GetReleaseManifest();
            //        string v = Utils.GetClientVersion(rm);
            //        lock (output)
            //        {
            //            output.Add(version, v);
            //        }
            //        Interlocked.Increment(ref ok);
            //        Log(string.Format("{0} -> {1}", version, v));
            //    }
            //    catch
            //    {
            //        Console.WriteLine("Retrying {0}..", version);
            //        lock (toDownload) { toDownload.Add(version); }
            //    }
            //}

            while (toDownload.Count > 0)
            {
                i = new List<string>(toDownload);
                toDownload.Clear();
                Parallel.ForEach(i, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, version => {
                    try
                    {
                        Console.Title = $"Left {all - ok}";
                        SolutionManifest m = new SolutionManifest("lol_game_client_sln", version);
                        ReleaseManifest rm = m.Projects[0].GetReleaseManifest();
                        string v = Utils.GetClientVersion(rm);
                        lock(output)
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

            Log("_output_");

            foreach(string v in versions)
            {
                Log($"{v} -> {output[v]}");
            }

            //foreach(string version in Utils.GetSolutionVersions("lol_game_client_sln"))
            //{

            //}
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