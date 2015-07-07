using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace TfsMigrationTool.Utils
{
    // http://blogs.microsoft.co.il/shair/2009/01/30/tfs-api-part-9-get-areaiteration-programmatically/
    // http://blogs.microsoft.co.il/shair/2009/01/30/tfs-api-part-10-add-areaiteration-programmatically/
    public class StructureHelper
    {
        private static readonly ICommonStructureService4 CommonStructureService = ServiceFactory.Create<ICommonStructureService4>();

        private readonly Dictionary<int, int> _iterationMap;

        public StructureHelper(string sourceProjectName, string targetProjectName)
        {
            _iterationMap = BuildIterationMap(sourceProjectName, targetProjectName);            
        }
        
        public int MapIterationId(int iterationId)
        {
            return _iterationMap[iterationId];
        }

        private Dictionary<int, int> BuildIterationMap(string sourceProjectName, string targetProjectName)
        {
            // Sync iterations
            Console.WriteLine("----Iteration Sync started----");
            int sourceCommonNodeId;
            var sourceStructure = GetIterationStructure(sourceProjectName, out sourceCommonNodeId);
            foreach (var iteration in sourceStructure.Children)
            {
                SyncIteration(iteration, targetProjectName);
            }
            Console.WriteLine("----Iteration Sync Completed----");
            Console.WriteLine();
            int tartgeCommonNodeId;
            var targetStructure = GetIterationStructure(targetProjectName, out tartgeCommonNodeId);

            // Build map
            var map = new Dictionary<int, int>
            {
                // add root level mapping (common structure node between areas and nodes)
                {sourceCommonNodeId, tartgeCommonNodeId}
            };

            // add all iterations
            AddMappingInfo(map, sourceStructure, targetStructure);
            
            return map;
        }

        private void SyncIteration(Iteration srcIteration, string targetProjectName)
        {
            Console.Write("syncing: '{0}'", srcIteration.Path);
            CreateOrUpdateIteration(targetProjectName, srcIteration.GetRelativePath(), srcIteration.Start, srcIteration.Finish);
            if (Console.CursorLeft > 0) Console.WriteLine();


            foreach (var child in srcIteration.Children)
            {
                SyncIteration(child, targetProjectName);
            }
        }

        private void AddMappingInfo(Dictionary<int, int> map, Iteration srcIteration, Iteration allTargetIterations)
        {
            var srcId = srcIteration.Id;
            var srcRelativePath = srcIteration.GetRelativePath();
            var targetId = allTargetIterations.FindByRelativePath(srcRelativePath).Id;

            map.Add(srcId, targetId);

            foreach (var child in srcIteration.Children)
            {
                AddMappingInfo(map, child, allTargetIterations);
            }
        }

        #region Project strutures (XML trees)
        private XmlNode GetXmlStructure(string projectName, StructureType type)
        {
            ProjectInfo projectInfo = CommonStructureService.GetProjectFromName(projectName);
            NodeInfo[] structures = CommonStructureService.ListStructures(projectInfo.Uri);

            var structureType = type == StructureType.Area
                ? "ProjectModelHierarchy"
                : "ProjectLifecycle";
            var nodeInfo = structures.Single(n => n.StructureType == structureType);

            XmlElement tree = CommonStructureService.GetNodesXml(new[] { nodeInfo.Uri }, true);

            return tree.ChildNodes[0];
        }
        #endregion

        #region Iteration Dates (Start & Finish)
        private Dictionary<string, ScheduleInfo> GetIterationDates(string projectName)
        {
            var result = new Dictionary<string, ScheduleInfo>();

            var iterationStructure = GetXmlStructure(projectName, StructureType.Iteration);

            ParseIterationDates(iterationStructure, result);

            return result;
        }

        private void ParseIterationDates(XmlNode node, Dictionary<string, ScheduleInfo> result)
        {
            if (node == null)
                return;

            string url = node.Attributes["NodeID"].Value;

            result[url] = new ScheduleInfo
            {
                StartDate = ParseDateTimeAttribute(node, "StartDate"),
                FinishDate = ParseDateTimeAttribute(node, "FinishDate")
            };

            // Visit children (sub-iterations).
            if (node.FirstChild != null)
            {
                // The first child node is the <Children> tag, which we'll skip.
                var subIterations = node.ChildNodes[0].ChildNodes;

                foreach (XmlElement subIteration in subIterations)
                {
                    ParseIterationDates(subIteration, result);
                }
            }
        }

        private DateTime? ParseDateTimeAttribute(XmlNode node, string attributeName)
        {
            string stringValue = node.Attributes[attributeName] != null ? node.Attributes[attributeName].Value : null;

            DateTime datetimeValue;
            return !string.IsNullOrEmpty(stringValue) && DateTime.TryParse(stringValue, out datetimeValue)
                ? (DateTime?)datetimeValue
                : null;
        }
        #endregion

        public Iteration GetIterationStructure(string projectName, out int commonNodeId)
        {
            var dates = GetIterationDates(projectName);

            var ss = ServiceFactory.Create<WorkItemStore>();
            var project = ss.Projects[projectName];

            var rootNode = GetRoot(project.IterationRootNodes[0]);

            var iteration = Iteration.ParseStructure(rootNode, dates, null);
            commonNodeId = rootNode.ParentNode.Id;

            return iteration;
        }

        private Node GetRoot(Node node)
        {
            Node root = node;
            Node parent = node.ParentNode;
            while (parent != null && node.IsAreaNode == parent.IsAreaNode && node.IsIterationNode == parent.IsIterationNode)
            {
                root = parent;
                parent = parent.ParentNode;
            }

            return root;
        }

        private NodeInfo CreateOrUpdateIteration(string projectName, string relativePath, DateTime? start = null, DateTime? finish = null)
        {
            bool isNewCreated = false;
            bool isDateUpdated = false;

            var rootNodePath = "\\" + projectName + "\\" + StructureType.Iteration;
            var absolutePath = rootNodePath + relativePath;

            NodeInfo iterationNode = TryGetNode(absolutePath);

            // Create new node
            if (iterationNode == null)
            {
                int backSlashIndex = relativePath.LastIndexOf("\\");
                string nodeName = relativePath.Substring(backSlashIndex + 1);

                string parentRelativePath = (backSlashIndex == 0
                    ? string.Empty
                    : relativePath.Substring(0, backSlashIndex));
                var parentNode = CreateOrUpdateIteration(projectName, parentRelativePath);

                string newPathUri = CommonStructureService.CreateNode(nodeName, parentNode.Uri);
                iterationNode = CommonStructureService.GetNode(newPathUri);
                isNewCreated = true;
            }

            // Update node
            if (iterationNode.StartDate != start || iterationNode.FinishDate != finish)
            {
                CommonStructureService.SetIterationDates(iterationNode.Uri, start, finish);
                isDateUpdated = true;
            }

            if (isNewCreated)
            {
                Console.Write(" - created");
            }
            else if (isDateUpdated)
            {
                Console.Write(" - updated");
            }

            return iterationNode;
        }

        private NodeInfo TryGetNode(string path)
        {
            try
            {
                var existingNode = CommonStructureService.GetNodeFromPath(path);
                return existingNode;
            }
            catch (CommonStructureSubsystemException ex)
            {
                if (ex.Message.Contains("The following node does not exist"))
                {
                    return null;
                }

                throw;
            }
        }
    }

    internal enum StructureType
    {
        Iteration = 0,
        Area = 1
    }

    internal class ScheduleInfo
    {
        public DateTime? StartDate { get; set; }
        public DateTime? FinishDate { get; set; }
    }

    public class Iteration
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string Path { get; private set; }
        public Uri Url { get; private set; }

        public DateTime? Start { get; private set; }
        public DateTime? Finish { get; private set; }

        public Iteration Parent { get; private set; }
        public IList<Iteration> Children { get; private set; }

        private Iteration()
        {
            Children = new List<Iteration>();
        }

        internal static Iteration ParseStructure(Node node, Dictionary<string, ScheduleInfo> dates, Iteration parent)
        {
            var iteration = new Iteration
            {
                Id = node.Id,
                Name = parent == null ? node.Path : node.Name,
                Path = node.Path,
                Url = node.Uri,

                Start = dates[node.Uri.AbsoluteUri].StartDate,
                Finish = dates[node.Uri.AbsoluteUri].FinishDate,

                Parent = parent
            };

            if (node.HasChildNodes)
            {
                foreach (Node childNode in node.ChildNodes)
                {
                    var child = ParseStructure(childNode, dates, iteration);
                    iteration.Children.Add(child);
                }
            }

            return iteration;
        }

        public Iteration GetRoot()
        {
            if (Parent == null)
                return this;

            var temp = Parent;
            while (temp.Parent != null)
            {
                temp = temp.Parent;
            }

            return temp;
        }

        /// <summary>
        /// Returns path without project name
        /// </summary>
        /// <returns></returns>
        public string GetRelativePath()
        {
            var projectName = GetRoot().Name;
            return Path.Replace(projectName, "");
        }

        /// <summary>
        /// Return the same path in an another project
        /// </summary>
        /// <param name="mirrowProjectName">Another project name</param>
        /// <returns></returns>
        public string MirrowPath(string mirrowProjectName)
        {
            var projectName = GetRoot().Name;

            return Path.Replace(projectName, mirrowProjectName + "\\" + StructureType.Iteration);
        }

        public Iteration FindByRelativePath(string relativePath)
        {
            var rootPath = GetRoot().Path;
            var absolutePath = rootPath + relativePath;
            return FindByAbsolutePath(absolutePath);
        }

        private Iteration FindByAbsolutePath(string absolutePath)
        {
            if (Path == absolutePath)
                return this;

            return Children
                .Select(child => child.FindByAbsolutePath(absolutePath))
                .FirstOrDefault(found => found != null);
        }
    }
}