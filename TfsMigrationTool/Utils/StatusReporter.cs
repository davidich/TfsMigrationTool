using System;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace TfsMigrationTool.Utils
{
    public class StatusReporter
    {
        private static int _lineNumber = 1;

        public static void ReportCopySucces(WorkItem item, WorkItem copiedItem)
        {
            Console.WriteLine("{0:0000}) #{1} -> #{2}: {3}", _lineNumber++, item.Id, copiedItem.Id, item.Title);
        }

        public static void ReportCopyFailure(WorkItem item, Exception ex)
        {
            

            Console.WriteLine();
            Console.WriteLine("COPY FAILURE (see logs for details)");
            Console.WriteLine("{0:0000}) #{1}: {2}", _lineNumber++, item.Id, item.Title);
            Console.WriteLine();
        }
    }
}