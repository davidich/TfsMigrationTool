using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace TfsMigrationTool.Utils
{
    public static class WorkItemHelper
    {
        private static readonly Dictionary<int, WorkItem> Cache = new Dictionary<int, WorkItem>();
        private static readonly WorkItemStore Store = ServiceFactory.Create<WorkItemStore>();

        public static WorkItem Get(int workItemId)
        {
            WorkItem item;
            if (!Cache.TryGetValue(workItemId, out item))
            {
                try
                {
                    item = Store.GetWorkItem(workItemId);
                    Cache[workItemId] = item;
                }
                catch
                {
                    return null;
                }
            }

            return item;
        }

        public static WorkItemCollection GetList(string projectName, string areaPath = null, string iterationPath = null, bool bypassCache = false)
        {
            var builder = new StringBuilder();
            builder.AppendFormat(@"SELECT [System.Id] " +
                                 "FROM WorkItems " +
                                 "WHERE [System.TeamProject] = '{0}'", projectName);

            if (!string.IsNullOrWhiteSpace(areaPath))
                builder.AppendFormat(" AND  [System.AreaPath] UNDER '{0}'", areaPath);

            if (!string.IsNullOrWhiteSpace(iterationPath))
                builder.AppendFormat(" AND  [System.IterationPath] UNDER '{0}'", iterationPath);

            builder.Append(" ORDER BY [System.Id]");

            WorkItemCollection wis = Store.Query(builder.ToString());

            if (!bypassCache)
            {
                foreach (WorkItem wi in wis)
                {
                    Cache[wi.Id] = wi;
                }
            }

            return wis;
        }

        public static WorkItem Copy(WorkItem srcItem, string targetProjectName)
        {
            var srcTypeName = srcItem.Type.Name;

            var targetProject = Store.Projects[targetProjectName];
            var targetType = targetProject.WorkItemTypes[srcTypeName];

            var copiedItem = srcItem.Copy(targetType, WorkItemCopyFlags.CopyFiles);

            copiedItem.WorkItemLinks[0].Comment = "History Ref";

            return copiedItem;
        }

        public static void DeleteItems(string project, string iterationPath)
        {
            WorkItemCollection workItems = GetList(project, iterationPath: iterationPath, bypassCache: true);

            if (workItems.Count > 0)
            {
                var header = string.Format("-----Deleting {0} items------", workItems.Count);
                Console.WriteLine(header);
                Console.WriteLine("Started...");

                try
                {
                    Console.WriteLine("Deleting....");

                    IEnumerable<int> workItemIds = from WorkItem wi in workItems
                                                   select wi.Id;

                    IEnumerable<WorkItemOperationError> itemOperationErrors = Store.DestroyWorkItems(workItemIds);

                    foreach (var error in itemOperationErrors)
                    {
                        Console.WriteLine(error.ToString());
                    }

                    Console.WriteLine("Completed");
                    Console.WriteLine(string.Join("", Enumerable.Repeat("-", header.Length)));
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Delete failed:");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine();
                    Console.WriteLine("Press ENTER to exit");
                    Console.ReadLine();
                }
            }
        }

        public static bool Exists(int workItemId)
        {
            return Get(workItemId) != null;
        }

        public static void AddMappedLink(this WorkItem item, WorkItemLink link, Dictionary<int, int> idMap)
        {
            var mappedTargetId = idMap[link.TargetId];
            var mappedLink = new WorkItemLink(link.LinkTypeEnd, mappedTargetId);
            item.WorkItemLinks.Add(mappedLink);
        }
    }
}