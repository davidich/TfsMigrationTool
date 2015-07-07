using System;
using System.Collections.Generic;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using TfsMigrationTool.Utils;

namespace TfsMigrationTool.Migrators
{
    public class WorkItemMigrator
    {
        private readonly string _sourceProjectName;
        private readonly string _targetProjectName;
        private readonly StructureHelper _structureHelper;
        private readonly Dictionary<int, int> _migratedItemsMap;
        private readonly Logger _logger;

        public WorkItemMigrator(string sourceProjectName, string targetProjectName)
        {
            _sourceProjectName = sourceProjectName;
            _targetProjectName = targetProjectName;
            _migratedItemsMap = new Dictionary<int, int>();

            _structureHelper = new StructureHelper(sourceProjectName, targetProjectName);
            _logger = new Logger(Config.LogFolder);
            _logger.ClearLogs();
        }

        public void MigrateIterationPath(string iterationPath)
        {
            WorkItemHelper.DeleteItems(project: _targetProjectName, iterationPath: iterationPath);

            var items = WorkItemHelper.GetList(_sourceProjectName, iterationPath: iterationPath);

            Console.WriteLine();
            Console.WriteLine("---Copying {0} items from iteration {1}", items.Count, iterationPath);

            foreach (WorkItem item in items)
            {
                MigrateItem(item);
            }

            _logger.ReportError();
        }

        private void MigrateItem(int itemId)
        {
            var item = WorkItemHelper.Get(itemId);
            MigrateItem(item);
        }

        private void MigrateItem(WorkItem item)
        {
            if (_migratedItemsMap.ContainsKey(item.Id))
                return;

            try
            {
                var copiedItem = WorkItemHelper.Copy(item, _targetProjectName);

                // Remap links (to new copies in a target project)
                foreach (WorkItemLink link in item.WorkItemLinks)
                {
                    if (!WorkItemHelper.Exists(link.TargetId))
                    {
                        _logger.Warning("Broken link is skipped: " + link.SourceId + " -> " + link.TargetId);
                        continue;
                    }                                       

                    // Create Related link  only after target item is already copied
                    // Otherwise cross-reference issue will occur
                    if (link.LinkTypeEnd.Name == "Related")
                    {
                        if (_migratedItemsMap.ContainsKey(link.TargetId))
                        {
                            copiedItem.AddMappedLink(link, _migratedItemsMap);
                        }
                    }
                    // If that's not "Related" link, then we don'r care about cross-refs,
                    // But we need to create only Backward links (i.e.: link to parent)
                    // As Forward links (i.e.: link to child) will created once item on the other side of this link will be copied
                    else if (!link.LinkTypeEnd.IsForwardLink)
                    {
                        if (!_migratedItemsMap.ContainsKey(link.TargetId))
                        {
                            MigrateItem(link.TargetId);
                        }

                        copiedItem.AddMappedLink(link, _migratedItemsMap);
                    }
                }

                // Set proper iteration
                copiedItem.IterationId = _structureHelper.MapIterationId(item.IterationId);

                ValidateAndSave(item, copiedItem);

                // Set proper state
                if (copiedItem.State != item.State)
                {
                    copiedItem.State = item.State;
                    if (copiedItem.Reason != item.Reason)
                    {
                        _logger.LogPartialCopy(item, copiedItem.Id, new PartialCopyInfo
                        {
                            FieldName = "Reason",
                            Value = copiedItem.Reason,
                            ExpectedValue = item.Reason
                        });
                    }

                    ValidateAndSave(item, copiedItem);
                }

                // Update Id Map
                _migratedItemsMap.Add(item.Id, copiedItem.Id);

                StatusReporter.ReportCopySucces(item, copiedItem);
            }
            catch (Exception ex)
            {
                _logger.LogCopyFailure(item, ex);

                StatusReporter.ReportCopyFailure(item, ex);
            }
        }

        private void ValidateAndSave(WorkItem srcItem, WorkItem copiedItem)
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

                _logger.LogPartialCopy(srcItem, copiedItem.Id, partialCopyInfos.ToArray());
            }

            copiedItem.Save();
        }
    }
}