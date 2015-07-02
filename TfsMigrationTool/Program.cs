namespace TfsMigrationTool
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using Microsoft.TeamFoundation.WorkItemTracking.Client;

    // https://msdn.microsoft.com/en-us/library/bb130347(v=vs.110).aspx
    // https://msdn.microsoft.com/en-us/library/ms194971.aspx
    // https://msdn.microsoft.com/en-us/library/dd997576.aspx
    internal class Program
    {

        private static WorkItemStore store;

        public static void Main()
        {
            store = ServiceFactory.Create<WorkItemStore>();

            DeleteAllWorkItems(project: "Connect");

            CopyIteration("DeloitteConnect\\Release 2\\Sprint 3");
            
            Console.WriteLine();
            Console.WriteLine("Freedom!!!");
            Console.ReadLine();
        }        

        private static void CopyIteration(string iterationPath)
        {
            var items = GetWorkItems("DeloitteConnect", iterationPath: iterationPath);

            Console.WriteLine();
            Console.WriteLine("---Copying {0} items from iteration {1}", items.Count, iterationPath);

            foreach (WorkItem item in items)
            {
                CopyItem(item);
            }
        }

        private static readonly Dictionary<int, int> IdMap = new Dictionary<int, int>();

        private static void CopyItem(int itemId)
        {
            var item = GetWorkItemById(itemId);
            CopyItem(item);
        }

        private static int cnt = 1;
        private static void CopyItem(WorkItem item)
        {
            if (IdMap.ContainsKey(item.Id))
                return;

            var type = GetWorkItemType(item.Type.Name);
            var copiedItem = item.Copy(type);

            // Adjust parent tag (if needed)
            var links = copiedItem.WorkItemLinks.Cast<WorkItemLink>();
            var parentLink = links.FirstOrDefault(l => l.LinkTypeEnd.Name == "Parent");

            if (parentLink != null)
            {
                var parentItemId = parentLink.TargetId;

                int copiedParentId;
                if (!IdMap.TryGetValue(parentItemId, out copiedParentId))
                {
                    CopyItem(parentItemId);
                    copiedParentId = IdMap[parentItemId];                    
                }
                
                //Remove original one
                copiedItem.WorkItemLinks.Remove(parentLink);

                // Add updated one
                var linkType = store.WorkItemLinkTypes[CoreLinkTypeReferenceNames.Hierarchy];
                copiedItem.WorkItemLinks.Add(new WorkItemLink(linkType.ReverseEnd, copiedParentId));
            }

            // Copy Tags
            copiedItem.Tags = item.Tags;
            
            // Set proper iteration
            copiedItem.IterationId = 17749;

            if (item.AttachedFileCount > 0)
            {
                foreach (Attachment scrAtt in item.Attachments)
                {
                    var copiedAtt = AttachmentHelper.Copy(scrAtt);
                    copiedItem.Attachments.Add(copiedAtt);
                }
            }

            ValidateAndSave(copiedItem);

            // Set proper state
            if (copiedItem.State != item.State)
            {
                copiedItem.State = item.State;
                //copiedItem.Reason = item.Reason;

                ValidateAndSave(copiedItem);
            }

            // Update Id Map
            IdMap.Add(item.Id, copiedItem.Id);

            Console.WriteLine("{0}) #{1} -> #{2}: {3}", cnt++, item.Id, copiedItem.Id, item.Title);
        }

        private static void ValidateAndSave(WorkItem copiedItem)
        {
            var errors = copiedItem.Validate();

            if (errors.Count > 0)
            {
                Console.Write("Item #{0} can't be saved. The following fields are incorrect: ");
                var isFirstFieldWritten = false;
                foreach (Field field in errors)
                {
                    if (isFirstFieldWritten)
                    {
                        Console.Write(", ");
                    }

                    Console.Write(field);
                    isFirstFieldWritten = true;
                }
            }

            copiedItem.Save();
        }

        private static WorkItemType GetWorkItemType(string typeName)
        {
            var targetProject = store.Projects["Connect"];
            var type = targetProject.WorkItemTypes[typeName];
            return type;
        }

        private static void DeleteAllWorkItems(string project)
        {
            WorkItemCollection workItems = GetWorkItems(project, areaPath: "Connect");

            if (workItems.Count > 0)
            {
                Console.WriteLine();
                var header = string.Format("-----Deleting {0} items------", workItems.Count);
                Console.WriteLine(header);
                Console.WriteLine("Started...");

                try
                {
                    Console.WriteLine("Deleting....");

                    IEnumerable<int> workItemIds = from WorkItem wi in workItems
                                                   select wi.Id;

                    IEnumerable<WorkItemOperationError> itemOperationErrors = store.DestroyWorkItems(workItemIds);
                    
                    foreach (var error in itemOperationErrors)
                    {
                        Console.WriteLine(error.ToString());
                    }

                    Console.WriteLine("Completed");
                    Console.WriteLine(string.Join("", Enumerable.Repeat("-", header.Length)));
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

        private static WorkItemCollection GetWorkItems(string projectName, string areaPath = null, string iterationPath = null)
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

            WorkItemCollection wis = store.Query(builder.ToString());

            return wis;
        }

        private static WorkItem GetWorkItemById(int id)
        {
            var builder = new StringBuilder();
            builder.AppendFormat(@"SELECT [System.Id] " +
                                 "FROM WorkItems " +
                                 "WHERE [System.Id] = '{0}'", id);

            WorkItemCollection wis = store.Query(builder.ToString());
            return wis[0];
        }
    }
}
