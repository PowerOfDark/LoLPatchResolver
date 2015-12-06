using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static ManifestManager.Utils;

namespace ManifestManager
{

    #region Structures, thanks to https://github.com/xupwup/LoLPatcher
    public class Directory
    {
        public Directory[] subdirs;
        public ManifestFile[] files;
        public String name;
        public String path;
        public Directory(String name)
        {
            this.name = name;
            this.path = name.Length == 0 ? "" : name + "/";
        }
    }

    public class ManifestFile
    {
        public string release;
        public int releaseInt;
        public string name;
        public string path;
        public byte[] checksum;
        public int sizeCompressed;
        public int sizeUncompressed; // use this value instead of size when type is 6


        public override string ToString()
        {
            return path + name + " " + release + " type:" + fileType + " u2:" + sizeUncompressed + " u3:" + unknown3 + " u4:" + unknown4;
        }



        /**
         * 6 = uncompressed - archive
         * 22 = compressed - archive
         * 5 = managedfile
         * greater than 0 = compressed
         * 0 = normal file
         * 2 = compressed file
         * 4 = copy to sln?
         */
        public  int fileType;

        public  int unknown3;
        public  int unknown4;

        public ManifestFile() { }

        public ManifestFile(string release, int releaseInt, string name, byte[] checksum, int sizeCompressed,
                int fileType, int sizeUncompressed, int unknown3, int unknown4)
        {
            this.release = release;
            this.releaseInt = releaseInt;
            this.name = name;
            this.checksum = checksum;
            this.sizeCompressed = sizeCompressed;
            this.fileType = fileType;
            if (fileType != 0 && fileType != 2 && fileType != 5 && fileType != 6 && fileType != 22 && fileType != 4)
            {
                Console.WriteLine("Hmm... fileType = " + fileType + " (" + name + ")");
            }
            this.sizeUncompressed = sizeUncompressed;
            this.unknown3 = unknown3;
            this.unknown4 = unknown4;
        }
    }

    public class FileMetaData
    {

        public FileMetaData(int nameindex, int release, byte[] checksum, int fileType, int sizeUncompressed, int size, int unknownInt3, int unknownInt4)
        {
            this.nameindex = nameindex;
            this.release = release;
            this.checksum = checksum;
            this.fileType = fileType;
            this.sizeUncompressed = sizeUncompressed;
            this.sizeCompressed = size;
            this.unknownInt3 = unknownInt3;
            this.unknownInt4 = unknownInt4;
        }

        public int release;
        public byte[] checksum;
        /**
         * Type
         * 5 = managed file
         */
        public int nameindex;
        public int fileType;
        public int sizeCompressed; // size compressed. No idea what this means if a file is not supposed to be compressed
        public int sizeUncompressed; // the size of a file when its not compressed
        public int unknownInt3;
        public int unknownInt4;
    }

    public class DirectoryMetaData
    {
        public DirectoryMetaData(int nameindex, int subdirFirstIndex, int nsubdirs, int firstfileIndex, int fileCount)
        {
            this.nameindex = nameindex;
            this.subdirFirstIndex = subdirFirstIndex;
            this.nsubdirs = nsubdirs;
            this.firstfileIndex = firstfileIndex;
            this.fileCount = fileCount;
        }

        public int nameindex, subdirFirstIndex, nsubdirs, firstfileIndex, fileCount;
    }
    #endregion



    public class ReleaseManifest
    {
        public int MagicHeaderInt, FileType, ItemCount, Version, ReleaseVersion;
        public string ReleaseName;
        public string BranchName;
        public Directory[] Directories;
        public ManifestFile[] Files;
        public Dictionary<string, ManifestFile> FileDictionary;
    }

    public class ProjectItem
    {
        public string Version, Name;

        public ProjectItem(string Name, string Version)
        {
            this.Name = Name;
            this.Version = Version;
        }

        public ReleaseManifest GetReleaseManifest()
        {
            byte[] data;
            using (WebClient wc = new WebClient())
            {
                data = wc.DownloadData($"{Program.API_BASE}projects/{this.Name}/releases/{this.Version}/releasemanifest");
            }

            var m = new ReleaseManifest();

            using (MemoryStream ms = new MemoryStream(data))
            {
                m.BranchName = this.Name;

                m.MagicHeaderInt = GetInt(ms);
                m.FileType = GetInt(ms);
                m.ItemCount = GetInt(ms);
                m.ReleaseVersion = GetInt(ms);
                m.ReleaseName = GetReleaseName(m.ReleaseVersion);

                DirectoryMetaData[] directoryMetaDatas = new DirectoryMetaData[GetInt(ms)];

                for (int i = 0; i < directoryMetaDatas.Length; i++)
                {
                    directoryMetaDatas[i] = new DirectoryMetaData(GetInt(ms),
                            GetInt(ms),
                            GetInt(ms),
                            GetInt(ms),
                            GetInt(ms));
                }

                FileMetaData[] fileMetaDatas = new FileMetaData[GetInt(ms)];
                for (int i = 0; i < fileMetaDatas.Length; i++)
                {
                    fileMetaDatas[i] = new FileMetaData(GetInt(ms),
                            GetInt(ms),
                            GetBytes(ms, 16),
                            GetInt(ms),
                            GetInt(ms),
                            GetInt(ms),
                            GetInt(ms),
                            GetInt(ms));
                }
                String[] strs = new String[GetInt(ms)];
                int datasize = GetInt(ms); // ignored

                int c;
                int idx = 0;
                StringBuilder sb = new StringBuilder();
                while ((c = ms.ReadByte()) != -1)
                {
                    if (c == '\0')
                    {
                        strs[idx] = sb.ToString();
                        sb = new StringBuilder();
                        idx++;
                        continue;
                    }
                    sb.Append((char)c);
                }
                //assert(component.equals(strs[strs.length - 1]));

                // creating proper objects
                m.Directories = new Directory[directoryMetaDatas.Length];
                for (int i = 0; i < directoryMetaDatas.Length; i++)
                {
                    m.Directories[i] = new Directory(strs[directoryMetaDatas[i].nameindex]);
                }
                // linking subdirectories
                for (int i = 0; i < directoryMetaDatas.Length; i++)
                {
                    int start = directoryMetaDatas[i].subdirFirstIndex;
                    if (start == i) start++;
                    m.Directories[i].subdirs = new Directory[directoryMetaDatas[i].nsubdirs];

                    for (int j = 0; j < directoryMetaDatas[i].nsubdirs; j++)
                    {
                        m.Directories[i].subdirs[j] = m.Directories[start + j];
                        m.Directories[start + j].path = m.Directories[i].path + m.Directories[start + j].path;
                    }
                }
                m.Files = new ManifestFile[fileMetaDatas.Length];
                for (int i = 0; i < fileMetaDatas.Length; i++)
                {
                    FileMetaData meta = fileMetaDatas[i];
                    m.Files[i] = new ManifestFile(GetReleaseName(meta.release),
                            meta.release,
                            strs[meta.nameindex],
                            meta.checksum, meta.sizeCompressed,
                            meta.fileType, meta.sizeUncompressed,
                            meta.unknownInt3, meta.unknownInt4);
                }
                // linking files to directories
                for (int i = 0; i < directoryMetaDatas.Length; i++)
                {
                    int start = directoryMetaDatas[i].firstfileIndex;
                    m.Directories[i].files = new ManifestFile[directoryMetaDatas[i].fileCount];

                    for (int j = 0; j < directoryMetaDatas[i].fileCount; j++)
                    {
                        m.Directories[i].files[j] = m.Files[start + j];
                        m.Files[start + j].path = m.Directories[i].path;
                    }
                }
                m.FileDictionary = new Dictionary<string, ManifestFile>();
                foreach (ManifestFile fi in m.Files)
                {
                    m.FileDictionary[fi.path + fi.name] =  fi;
                }
            }

        

            return m;

            

        }
    }

    public class SolutionManifest
    {
        public string Branch, SolutionVersion;
        public List<ProjectItem> Projects;

        public SolutionManifest(string Branch, string Version)
        {
            string data;
            using (WebClient wc = new WebClient())
            {
                data = wc.DownloadString($"{Program.API_BASE}solutions/{Branch}/releases/{Version}/solutionmanifest");
            }

            var lines = data.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            this.Branch = lines[2];
            this.SolutionVersion = lines[3];

            Projects = new List<ProjectItem>();
            Projects.Add(new ProjectItem(lines[5], lines[6])); // Parse only lol_game_client 
            //TODO: Parse all projects

        }

    }
}
