using System;
using System.IO;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace TfsMigrationTool.Utils
{
    public class Logger
    {
        private readonly string _logFolderPath;
        private int _copyFailureCnt;
        private int _brokenLinkCnt;
        private readonly string _debugLogPath;
        private readonly string _copyFailureLogPath;
        private readonly string _brokenLinksLogPath;

        public Logger(string logFolderPath)
        {
            _logFolderPath = logFolderPath;

            _debugLogPath = Path.Combine(logFolderPath, "Debug.log");
            _copyFailureLogPath = Path.Combine(logFolderPath, "FailedItems.log");
            _brokenLinksLogPath = Path.Combine(logFolderPath, "BrokenLinks.log");

            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }
        }

        public void ClearLogs()
        {
            foreach (var file in Directory.GetFiles(_logFolderPath))
            {
                File.Delete(file);
            }
        }

        public void Warning(string text)
        {
            using (var writer = File.AppendText(_debugLogPath))
            {
                writer.WriteLine("{0:T} [WARNING]: {1}", DateTime.Now, text);
                writer.WriteLine();
            }
        }

        public void LogPartialCopy(WorkItem srcItem, int targetItemId = 0, params PartialCopyInfo[] infos)
        {
            using (var writer = File.AppendText(_debugLogPath))
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

        public void LogCopyFailure(WorkItem item, Exception exception)
        {
            using (var writer = File.AppendText(_copyFailureLogPath))
            {
                writer.WriteLine("{0:T}: {1} - '{2}'", DateTime.Now, item.Id, item.Title);
                writer.WriteLine(exception);
                writer.WriteLine();
            }

            _copyFailureCnt++;
        }

        public void LogBrokenLink(int sourceId, WorkItemLink link)
        {
            using (var writer = File.AppendText(_brokenLinksLogPath))
            {
                writer.WriteLine("{0:T}: SourceId: {1}, TargetId: '{2}', type: {3}", DateTime.Now, sourceId, link.TargetId, link.LinkTypeEnd.Name);
            }

            _brokenLinkCnt++;
        }

        public void ReportError()
        {
            if (_brokenLinkCnt > 0)
            {
                Console.WriteLine("---------------------------");
                Console.WriteLine("{0} BROKEN LINK(S) OCCURED. Details", _brokenLinkCnt);
                Console.WriteLine();
                using (var reader = File.OpenText(_brokenLinksLogPath))
                {
                    string s;
                    while ((s = reader.ReadLine()) != null)
                    {
                        Console.WriteLine(s);
                    }
                }
                Console.WriteLine("");
            }

            if (_copyFailureCnt > 0)
            {
                Console.WriteLine("---------------------------");
                Console.WriteLine("{0} ITEMS FAILED.", _copyFailureCnt);
                Console.WriteLine();
                using (var reader = File.OpenText(_copyFailureLogPath))
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