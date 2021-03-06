﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ManifestManager
{
    public static class Utils
    {

        static readonly int[] Empty = new int[0];

        public static int[] Locate(this byte[] self, byte[] candidate)
        {
            if (IsEmptyLocate(self, candidate))
                return Empty;

            var list = new List<int>();

            for (int i = 0; i < self.Length; i++)
            {
                if (!IsMatch(self, i, candidate))
                    continue;

                list.Add(i);
            }

            return list.Count == 0 ? Empty : list.ToArray();
        }

        static bool IsMatch(byte[] array, int position, byte[] candidate)
        {
            if (candidate.Length > (array.Length - position))
                return false;

            for (int i = 0; i < candidate.Length; i++)
                if (array[position + i] != candidate[i])
                    return false;

            return true;
        }

        static bool IsEmptyLocate(byte[] array, byte[] candidate)
        {
            return array == null
                || candidate == null
                || array.Length == 0
                || candidate.Length == 0
                || candidate.Length > array.Length;
        }

        public static string TrimSpaces(string str)
        {
            RegexOptions options = RegexOptions.None;
            Regex regex = new Regex(@"[ ]{2,}", options);
            return regex.Replace(str, @" ");
        }

        public static bool IsByteADigit(byte b)
        {
            return b >= 0x30 && b <= 0x39;
        }

        public static bool IsByteALowercase(byte b)
        {
            return b >= 0x61 && b <= 0x7A;
        }

        public static bool IsByteAnUppercase(byte b)
        {
            return b >= 0x41 && b <= 0x5A;
        }


        public static byte[] PacketByTable(string table)
        {
            string[] entries = table.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            byte[] buffer = new byte[entries.Count()];
            int index = 0;
            foreach (string hexa in entries)
            {
                buffer[index++] = (byte)int.Parse(hexa, System.Globalization.NumberStyles.HexNumber);
            }
            return buffer;
        }

        public static int ContainsSequence(byte[] data, ref int i, Predicate<byte> p)
        {
            try
            {
                int count = 0;
                while (p.Invoke(data[i--]))
                {
                    ++count;
                }
                ++i;
                return count;
            }
            catch { return 0; }
        }


        public static string GetClientReleaseDescriptionLegacy(string filepath)
        {
            byte[] data;
            int len;
            using (FileStream f = new FileStream(filepath, FileMode.Open))
            {
                data = new byte[(f.Length - f.Length / 2)];
                f.Seek(f.Length / 2, SeekOrigin.Begin);
                len = f.Read(data, 0, (int)(f.Length - f.Length / 2));
                //read from the half to the end
            }

            //data = PacketByTable("31 2e 30 2e 30 2e 31 34 30 00 00 00 31 39 3a 31 36 3a 35 38 00 00 00 00 4d 61 79 20 33 30 20 32 30 31 32 00 56 65 72 73 69 6f 6e 20");
            //len = data.Length;

            int temp, temp2;

            for (int i = len - 1; i >= 0; i--)
            {
                try
                {
                    if (data[i] == 0x56)//V
                    {


                        temp = i - 1;
                        if (data[temp] != 0)
                            continue;
                        --temp;

                        //year
                        temp2 = ContainsSequence(data, ref temp, p => IsByteADigit(p));
                        if (temp2 != 4)
                            continue;

                        if (data[temp] != 0x20)
                            continue;
                        --temp;
                        //day
                        temp2 = ContainsSequence(data, ref temp, p => IsByteADigit(p));
                        if (temp2 == 0 || temp2 > 2)
                            continue;

                        if (data[temp] != 0x20)
                            continue;
                        --temp;
                        if (temp2 == 1)
                            --temp;//additional space  for one digit
                        //Month

                        temp2 = ContainsSequence(data, ref temp, p => IsByteALowercase(p));
                        if (temp2 != 2)
                            continue;
                        temp2 = ContainsSequence(data, ref temp, p => IsByteAnUppercase(p));
                        if (temp2 != 1)
                            continue;

                        //ENOUGH IS ENOUGH

                        ContainsSequence(data, ref temp, p => p == 0);//space
                        temp -= 8;//hour
                        ContainsSequence(data, ref temp, p => p == 0);//space
                        for (int j = 0; j < 3; j++)
                        {
                            ContainsSequence(data, ref temp, p => IsByteADigit(p));//don't give a fuck about the output
                            --temp;//dot

                        }

                        int startIndex = temp;
                        string str = TrimSpaces(Encoding.ASCII.GetString(data, startIndex, i - startIndex).Replace("\0", " "));
                        data = null;
                        //Console.WriteLine((double)startIndex / (double)len);
                        return str;


                    }
                }
                catch
                {
                    Console.WriteLine("fuck this");
                }
            }
            return FileVersionInfo.GetVersionInfo(filepath).ProductVersion; //fallback
        }

        public static string GetClientReleaseDescription(string filepath)
        {
            byte[] data;
            int len;
            using (FileStream f = new FileStream(filepath, FileMode.Open))
            {
                data = new byte[(f.Length - f.Length / 2)];
                f.Seek(f.Length / 2, SeekOrigin.Begin);
                len = f.Read(data, 0, (int)(f.Length - f.Length / 2));
                //read from the half to the end
            }

            //data = PacketByTable("20 e5 fc 00 41 75 67 20 32 30 20 32 30 31 35 00 30 36 3a 30 39 3a 32 33 00 00 00 00 35 2e 31 37 2e 30 2e 31 39 31 00 00");
            //len = data.Length;

            int temp, temp2;
            byte[] releases = Encoding.UTF8.GetBytes("Releases/");
            var releasesSearch = data.Locate(releases);
            if (releasesSearch.Count() == 0)
                return GetClientReleaseDescriptionLegacy(filepath);
            int pos = Math.Max(0, releasesSearch.First() - 512);
            string txt = Encoding.UTF8.GetString(data, pos, 768);

            string date = "", hour = "", patchlong = "";
            Regex r;
            Match m;
            try
            {
                r = new Regex("(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec).*?(\\d{1,2}).*?(\\d{4})", RegexOptions.Singleline);
                m = r.Match(txt);
                date = m.Groups[1].Value + " " + m.Groups[2].Value + " " + m.Groups[3].Value;
            }
            catch { }
            try
            {
                r = new Regex("(\\d{2}:\\d{2}:\\d{2})", RegexOptions.Singleline);
                m = r.Match(txt);
                hour = m.Groups[1].Value;
            }
            catch { }
            //try
            //{
            //    r = new Regex("(\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,5})", RegexOptions.Singleline);
            //    m = r.Match(txt);
            //    patchlong = m.Groups[1].Value;
            //}
            //catch {  }
            patchlong = FileVersionInfo.GetVersionInfo(filepath).ProductVersion;


            if (patchlong.Length < 3)
                return GetClientReleaseDescriptionLegacy(filepath);



            return $"{date} {hour} {patchlong}";
        }


        public static List<string> GetSolutionVersions(string solutionName)
        {
            using (WebClient wc = new WebClient())
            {
                string str = wc.DownloadString($"{Program.API_BASE}solutions/{solutionName}/releases/releaselisting_{Program.Region.ToUpper()}");
                return str.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        public static List<string> GetProjectVersions(string projectName)
        {
            using (WebClient wc = new WebClient())
            {
                string str = wc.DownloadString($"{Program.API_BASE}projects/{projectName}/releases/releaselisting_{Program.Region.ToUpper()}");
                return str.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }



        /// <summary>
        /// Get the detailed game client version from manifest
        /// </summary>
        /// <param name="m"></param>
        /// <returns>Patch number (for example 5.23.0.239)</returns>
        public static string GetClientVersion(ReleaseManifest m)
        {
            var t = m.Files.Where(p => p.name.EndsWith(".exe"));
            var t2 = t.Select(p => p.name);

            string tmpName = Path.GetTempFileName();
            string fileVersion = t.ElementAt(1).release;



            if (t2.Contains("League of Legends.exe"))
            {

                try
                {
                    byte[] data;
                    using (WebClient wc = new WebClient())
                    {
                        data = wc.DownloadData($"{Program.API_BASE}/projects/{m.BranchName}/releases/{fileVersion}/files/League of Legends.exe.compressed");

                    }

                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        DecompressFile(ms, tmpName);
                    }

                    string ret = GetClientReleaseDescription(tmpName);
                    File.Delete(tmpName);
                    return ret;
                }
                catch { }
            }


            throw new Exception();
        }

        /// <summary>
        /// Decompress a single compressed file
        /// </summary>
        /// <param name="inFile"></param>
        /// <param name="outFile">Target output path</param>

        public static void DecompressFile(Stream inFile, string outFile)
        {
            byte[] data = new byte[inFile.Length];
            inFile.Read(data, 0, (int)inFile.Length);
            using (FileStream fs = new FileStream(outFile, FileMode.Create))
            {
                byte[] output = Ionic.Zlib.ZlibStream.UncompressBuffer(data);
                fs.Write(output, 0, output.Length);
            }
            data = null;
        }


        public static int GetInt(Stream input)
        {
            byte[] bytes = GetBytes(input, 4);
            int ret = 0;
            for (int i = 3; i >= 0; i--)
            {
                ret = ret << 8 | (bytes[i] & 0xFF);
            }
            return ret;
        }


        public static byte[] GetBytes(Stream input, int count)
        {
            byte[] bytes = new byte[count];
            int tmp;
            for (int i = 0; i < count; i++)
            {
                tmp = input.ReadByte();
                if (tmp == -1)
                    throw new OverflowException();
                bytes[i] = (byte)tmp;
            }

            return bytes;
        }

        public static string GetReleaseName(int rel)
        {
            string s = "" + (rel & 255);
            rel = rel >> 8;
            s = (rel & 255) + "." + s;
            rel = rel >> 8;
            s = (rel & 255) + "." + s;
            rel = rel >> 8;
            s = (rel & 255) + "." + s;
            return s;
        }

    }
}
