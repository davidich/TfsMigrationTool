using System;
using System.IO;
using System.Text;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace TfsMigrationTool
{
    public static class Logger
    {
        private static int copyFailureCnt = 0;
        private static int brokenLinkCnt = 0;
        private static readonly string LogPath = Path.Combine(Environment.CurrentDirectory, "Logs");
        private static readonly string DebugLogPath = Path.Combine(LogPath, "Debug.log");
        private static readonly string CopyFailureLogPath = Path.Combine(LogPath, "FailedItems.log");
        private static readonly string BrokenLinksLogPath = Path.Combine(LogPath, "BrokenLinks.log");

        static Logger()
        {
            if (!Directory.Exists(LogPath))
            {
                Directory.CreateDirectory(LogPath);
            }
        }

        public static void ClearLogs()
        {
            foreach (var file in Directory.GetFiles(LogPath))
            {
                File.Delete(file);
            }
        }

        public static void Warning(string text)
        {
            using (var writer = File.AppendText(DebugLogPath))
            {
                writer.WriteLine("{0:T} [WARNING]: {1}", DateTime.Now, text);
                writer.WriteLine();
            }
        }

        public static void LogPartialCopy(WorkItem srcItem, int targetItemId = 0, params PartialCopyInfo[] infos)
        {
            using (var writer = File.AppendText(DebugLogPath))
            {
                writer.WriteLine("{0:T} [PARTIAL COPY]: #{1} - '{2}'",
                    DateTime.Now,
                    targetItemId == 0 ? srcItem.Id : targetItemId,
                    srcItem.Title);

                foreach (var info in infos)
                {
                    writer.WriteLine("\t{0} = '{1}' (but expected to be '{2}')", info.FieldName, info.Value, info.ExpectedValue);
                }

                writer.WriteLine();
            }
        }



        public static void LogCopyFailure(WorkItem item, Exception exception)
        {
            using (var writer = File.AppendText(CopyFailureLogPath))
            {
                writer.WriteLine("{0:T}: {1} - '{2}'", DateTime.Now, item.Id, item.Title);
                writer.WriteLine(exception);
                writer.WriteLine();
            }

            copyFailureCnt++;
        }

        public static void LogBrokenLink(int sourceId, WorkItemLink link)
        {
            using (var writer = File.AppendText(BrokenLinksLogPath))
            {
                writer.WriteLine("{0:T}: SourceId: {1}, TargetId: '{2}', type: {3}", DateTime.Now, sourceId, link.TargetId, link.LinkTypeEnd.Name);
            }

            brokenLinkCnt++;
        }

        public static void ReportError()
        {
            if (brokenLinkCnt > 0)
            {
                Console.WriteLine("---------------------------");
                Console.WriteLine("{0} BROKEN LINK(S) OCCURED. Details", brokenLinkCnt);
                Console.WriteLine();
                using (var reader = File.OpenText(BrokenLinksLogPath))
                {
                    string s;
                    while ((s = reader.ReadLine()) != null)
                    {
                        Console.WriteLine(s);
                    }
                }
                Console.WriteLine("");
            }

            if (copyFailureCnt > 0)
            {
                Console.WriteLine("---------------------------");
                Console.WriteLine("{0} ITEMS FAILED.", copyFailureCnt);
                Console.WriteLine();
                using (var reader = File.OpenText(CopyFailureLogPath))
                {
                    string s;
                    while ((s = reader.ReadLine()) != null)
                    {
                        Console.WriteLine(s);
                    }
                }
                Console.WriteLine("");
            }
        }
    }

    public class PartialCopyInfo
    {
        public string FieldName { get; set; }
        public string Value { get; set; }
        public string ExpectedValue { get; set; }
    }
}