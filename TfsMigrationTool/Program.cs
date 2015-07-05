using System.IO;
using System.Runtime.InteropServices;

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
        private static readonly Dictionary<int, int> CopiedItemsMap = new Dictionary<int, int>();

        public static void Main()
        {
            Console.WindowWidth = Console.LargestWindowWidth;
            Console.WindowHeight = Console.LargestWindowHeight;
            Console.SetWindowPosition(0, 0);

            WorkItemHelper.DeleteAll(project: "Connect");

            Logger.ClearLogs();
            StructureHelper.Init("DeloitteConnect", "Connect");

            CopyIteration("DeloitteConnect");

            //CopyIteration("DeloitteConnect\\Release 2");
            //CopyIteration("DeloitteConnect\\Release 2.1");
            //CopyIteration("DeloitteConnect\\Release 3");
            //CopyIteration("DeloitteConnect\\Release 4");
            //CopyIteration("DeloitteConnect\\Release 5");

            Console.WriteLine();
            Console.WriteLine("COMPLETED!!!");
            Console.WriteLine();
            Logger.ReportError();
            Console.ReadLine();
        }

        private static void CopyIteration(string iterationPath)
        {
            var items = WorkItemHelper.GetList("DeloitteConnect", iterationPath: iterationPath);

            Console.WriteLine();
            Console.WriteLine("---Copying {0} items from iteration {1}", items.Count, iterationPath);

            foreach (WorkItem item in items)
            {
                CopyItem(item);
            }
        }

        private static void CopyItem(int itemId)
        {
            var item = WorkItemHelper.Get(itemId);
            CopyItem(item);
        }

        private static void CopyItem(WorkItem item)
        {
            if (CopiedItemsMap.ContainsKey(item.Id))
                return;

            try
            {
                var copiedItem = WorkItemHelper.Copy(item, "Connect");

                // Remap links (to new copies in a target project)
                foreach (WorkItemLink link in item.WorkItemLinks)
                {
                    var linkedItem = WorkItemHelper.Get(link.TargetId);

                    // We might have broken links, which refence to a deleted work items
                    if (linkedItem == null)
                    {
                        Logger.Warning("Broken link is skipped: " + link.SourceId + " -> " + link.TargetId);
                        continue;
                    }

                    // Create Related link  only after target item is already copied
                    // Otherwise cross-reference issue will occur
                    if (link.LinkTypeEnd.Name == "Related")
                    {
                        if (CopiedItemsMap.ContainsKey(link.TargetId))
                        {
                            copiedItem.AddMappedLink(link, CopiedItemsMap);
                        }
                    }
                    // If that's not "Related" link, then we don'r care about cross-refs,
                    // But we need to create only Backward links (i.e.: link to parent)
                    // As Forward links (i.e.: link to child) will created once item on the other side of this link will be copied
                    else if (!link.LinkTypeEnd.IsForwardLink)
                    {
                        if (!CopiedItemsMap.ContainsKey(link.TargetId))
                        {
                            CopyItem(link.TargetId);
                        }

                        copiedItem.AddMappedLink(link, CopiedItemsMap);
                    }
                }

                // Set proper iteration
                copiedItem.IterationId = StructureHelper.MapIterationId(item.IterationId, item.Project.Name,
                    copiedItem.Project.Name);

                ValidateAndSave(item, copiedItem);

                // Set proper state
                if (copiedItem.State != item.State)
                {
                    copiedItem.State = item.State;
                    if (copiedItem.Reason != item.Reason)
                    {
                        Logger.LogPartialCopy(item, copiedItem.Id, new PartialCopyInfo
                        {
                            FieldName = "Reason", 
                            Value = copiedItem.Reason, 
                            ExpectedValue = item.Reason
                        });
                    }

                    ValidateAndSave(item, copiedItem);
                }

                // Update Id Map
                CopiedItemsMap.Add(item.Id, copiedItem.Id);

                StatusReporter.ReportCopySucces(item, copiedItem);
            }
            catch (Exception ex)
            {
                StatusReporter.ReportCopyFailure(item, ex);
            }
        }

        private static void ValidateAndSave(WorkItem srcItem, WorkItem copiedItem)
        {
            var errors = copiedItem.Validate();

            if (errors.Count > 0)
            {
                var partialCopyInfos = new List<PartialCopyInfo>();
                foreach (Field field in errors)
                {
                    partialCopyInfos.Add(new PartialCopyInfo
                    {
                        FieldName = field.Name,
                        Value = field.OriginalValue.ToString(),
                        ExpectedValue = field.Value.ToString()
                    });

                    copiedItem[field.Name] = field.OriginalValue;
                }

                Logger.LogPartialCopy(srcItem, copiedItem.Id, partialCopyInfos.ToArray());
            }

            copiedItem.Save();
        }
    }
}
