using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace MediaSplitter
{
    class Program
    {
        public static List<string> FileTypes { get; set; }

        public static List<string> PicTypes { get; set; }
        public static List<string> VideoTypes { get; set; }


        static List<string> problemFiles = new List<string>();
        static List<string> duplicateFiles = new List<string>();
        static List<string> sameNameFiles = new List<string>();

        static void Main(string[] args)
        {
            var folderPath = ConfigurationManager.AppSettings["sourcePath"];

            FileTypes = new List<string> { "*.*"/*,  */};
            VideoTypes = ConfigurationManager.AppSettings["videoTypes"].Split(',', ';', ' ').Where(f => !string.IsNullOrEmpty(f)).Select(f => f.Trim()).ToList();
            PicTypes = ConfigurationManager.AppSettings["picTypes"].Split(',', ';', ' ').Where(f => !string.IsNullOrEmpty(f)).Select(f => f.Trim()).ToList();

            var files = new List<string>();
            foreach (var ft in FileTypes)
            {
                files.AddRange(Directory.GetFiles(folderPath, ft, SearchOption.AllDirectories));
            }

            var drive = ConfigurationManager.AppSettings["drive"];

            var destPic = ConfigurationManager.AppSettings["destPic"];
            var destVid = ConfigurationManager.AppSettings["destVid"];
            var destOther = ConfigurationManager.AppSettings["destOther"];

            var sourcePics = files.Where(f => PicTypes.Any(p => f.EndsWith(p)));
            var sourceVideos = files.Where(f => VideoTypes.Any(p => f.EndsWith(p)));
            var otherFiles = files.Where(f => !(PicTypes.Any(p => f.EndsWith(p)) || VideoTypes.Any(p => f.EndsWith(p))));


            var s1 = DateTime.Now;

            Console.WriteLine("Pictures copy started...");
            CopyFiles(drive, destPic, sourcePics);
            var s2 = DateTime.Now;
            Console.WriteLine(string.Format("Pictures copied: {0} sec", (s2 - s1).TotalSeconds.ToString()));

            Console.WriteLine("Videos copy started...");
            CopyFiles(drive, destVid, sourceVideos);
            var s3 = DateTime.Now;
            Console.WriteLine(string.Format("Videos copied: {0} sec", (s3 - s2).TotalSeconds.ToString()));

            Console.WriteLine("Other files copy started...");
            CopyFiles(drive, destOther, otherFiles);
            var s4 = DateTime.Now;
            Console.WriteLine(string.Format("Other files copied: {0} sec", (s4 - s3).TotalSeconds.ToString()));

            LogProblems();

            Console.ReadKey();
        }

        private static void LogProblems()
        {
            if (duplicateFiles.Any())
                File.WriteAllLines(@".\_duplicateFiles.txt", duplicateFiles);
            if (sameNameFiles.Any())
                File.WriteAllLines(@".\_sameNameFiles.txt", sameNameFiles);
            if (problemFiles.Any())
                File.WriteAllLines(@".\_problemFiles.txt", problemFiles);
        }

        private static void CopyFiles(string drive, string destFolder, IEnumerable<string> source)
        {

            foreach (var p in source)
            {
                var filePath = p;
                try
                {
                    var minDate = GetFileDate(p);

                    var year = minDate.Year;
                    var month = minDate.Month;

                    var destYear = Path.Combine(drive,destFolder, year.ToString());
                    if (!Directory.Exists(destYear))
                    {
                        Directory.CreateDirectory(destYear);
                    }
                    var destMonth = Path.Combine(destYear,month.ToString().PadLeft(2,'0'));
                    if (!Directory.Exists(destMonth))
                    {
                        Directory.CreateDirectory(destMonth);
                    }
                    var fileName = Path.GetFileName(p);
                    filePath = Path.Combine(destMonth,fileName);
                
                    if (File.Exists(filePath))
                    {
                        if (ContentEquals(p, filePath))
                        {
                            duplicateFiles.Add(string.Format("{0} - {1}", p, filePath));
                        }
                        else
                        {
                            sameNameFiles.Add(string.Format("{0} - {1}", p, filePath));
                        }
                    }
                    else
                    {
                        File.Copy(p, filePath);
                    }
                }
                catch (Exception)
                {
                    problemFiles.Add(string.Format("{0} - {1}", p, filePath));
                }
            }
        }

        private static DateTime GetFileDate(string p)
        {
            DateTime? fileNameDate = GetFileDateByName(p);
            if (!fileNameDate.HasValue)
            {
                var access = File.GetLastAccessTime(p);
                var write = File.GetLastWriteTime(p);
                var create = File.GetCreationTime(p);
                var min = Math.Min(access.ToFileTime(), write.ToFileTime());
                min = Math.Min(min, create.ToFileTime());
                fileNameDate = DateTime.FromFileTime(min);
            }
            return fileNameDate.Value;
        }

        private static DateTime? GetFileDateByName(string p)
        {
            var fileName = Path.GetFileNameWithoutExtension(p);
            if (fileName.Length >= 8)
            {
                try
                {
                    var year = int.Parse(fileName.Substring(0,4));
                    var month = int.Parse(fileName.Substring(4, 2));
                    var day = int.Parse(fileName.Substring(6, 2));
                    return new DateTime(year,month,day);
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return null;
        }

        private static bool ContentEquals(string p, string filePath)
        {
            var h1 = ComputeHash(p);
            var h2 = ComputeHash(filePath);
            return h1.SequenceEqual(h2);
        }

        private static byte[] ComputeHash(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(File.ReadAllBytes(filePath));
            }
        }

        private static List<string> FileFinder(string folderPath)
        {
            var l = new List<string>();
            try
            {
                foreach (string dir in Directory.EnumerateDirectories(folderPath))
                {
                    l.AddRange(FileFinder(dir));
                }
            }
            catch { }
            foreach (var f in FileTypes)
            {
                foreach (string file in Directory.EnumerateFiles(folderPath, f))
                {
                    l.Add(file);
                }
            }
            return l;
        }
    }
}
