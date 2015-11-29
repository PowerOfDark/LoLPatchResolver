using System;
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
    public class Utils
    {

        public static Dictionary<string, string> Cached = new Dictionary<string, string>();

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
            try {
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
                        for(int j = 0; j < 3; j++)
                        {
                            ContainsSequence(data, ref temp, p => IsByteADigit(p));//don't give a fuck about the output
                            --temp;//dot

                        }

                        int startIndex = temp;
                        //Console.WriteLine((double)startIndex / (double)len);
                        return TrimSpaces(Encoding.ASCII.GetString(data, startIndex, i - startIndex).Replace("\0", " "));


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
                len = f.Read(data, 0, (int)(f.Length - f.Length/2));
                //read from the half to the end
            }

            //data = PacketByTable("20 e5 fc 00 41 75 67 20 32 30 20 32 30 31 35 00 30 36 3a 30 39 3a 32 33 00 00 00 00 35 2e 31 37 2e 30 2e 31 39 31 00 00");
            //len = data.Length;

            int temp, temp2;


            for(int i = len - 1;i>=0;i--)
            {
                if(data[i] == 0)
                {

                    temp = i - 1;
                    bool error = false;
                    for (int j = 0; j < 3; j++)
                    {
                        temp2 = ContainsSequence(data, ref temp, p => IsByteADigit(p));
                        if (temp2 == 0 || temp2 > 10 || data[temp--] != 0x2e)
                        {
                            error = true;
                            break;
                        }
                    }

                    if (error)
                        continue;


                    temp2 = ContainsSequence(data, ref temp, p => IsByteADigit(p));
                    if (temp == 0 || temp2 > 10)
                        continue;

                    temp2 = ContainsSequence(data, ref temp, p => p == 0);
                    if (temp2 == 0 || temp2 > 10)
                        continue;

                    //hour
                    //hh:mm:ss
                    for (int j = 0; j < 2; j++)
                    {
                        temp2 = ContainsSequence(data, ref temp, p => IsByteADigit(p));
                        if (temp2 != 2 || data[temp--] != 0x3a)
                        {
                            error = true;
                            break;
                        }
                    }

                    if (error)
                        continue;

                    if(ContainsSequence(data, ref temp, p=>IsByteADigit(p)) == 2)//final hour, literally
                    {
                        int startIndex = temp - 11;
                        //Console.WriteLine((double)startIndex / (double)len);
                        return TrimSpaces(Encoding.ASCII.GetString(data, startIndex, i - startIndex).Replace("\0", " "));
                    }


                }
            }
            return GetClientReleaseDescriptionLegacy(filepath); //fallback
        }


        public static List<string> GetSolutionVersions(string solutionName)
        {
            using (WebClient wc = new WebClient())
            {
                string str = wc.DownloadString($"{Program.API_BASE}solutions/{solutionName}/releases/releaselisting_{Program.Region.ToUpper()}");
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
                if (Cached.ContainsKey(fileVersion))
                    return Cached[fileVersion];

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
                    Cached.Add(fileVersion, ret);
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
            for(int i = 0; i < count; i++)
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
