namespace TfsMigrationTool
{
    using System;
    using System.Xml;

    using Microsoft.TeamFoundation.Server;

    // http://blogs.microsoft.co.il/shair/2009/01/30/tfs-api-part-9-get-areaiteration-programmatically/
    // http://blogs.microsoft.co.il/shair/2009/01/30/tfs-api-part-10-add-areaiteration-programmatically/
    public class StructureHelper
    {
        private static void ShowTree()
        {
            ICommonStructureService service = ServiceFactory.Create<ICommonStructureService>();

            //Gets Area/Iteration base Project
            ProjectInfo projectInfo = service.GetProjectFromName("DeloitteConnect");
            NodeInfo[] nodes = service.ListStructures(projectInfo.Uri);

            //GetNodes can use with:
            var areaNodeInfo = nodes[0];
            var iterationNodeInfo = nodes[1];

            XmlElement AreaTree = service.GetNodesXml(new[] { areaNodeInfo.Uri }, true);
            XmlElement IterationsTree = service.GetNodesXml(new[] { iterationNodeInfo.Uri }, true);



            XmlNode AreaNodes = AreaTree.ChildNodes[0];
            XmlNode IterationsNodes = IterationsTree.ChildNodes[0];

            PrintStructure(AreaNodes, StructureType.Area);
            PrintStructure(IterationsNodes, StructureType.Iteration);
        }

        enum StructureType
        {
            Iteration = 0,
            Area = 1
        }

        private static void PrintStructure(XmlNode tree, StructureType type)
        {
            //Check if Area/Iteration has Childerns
            if (tree.FirstChild != null)
            {
                int myNodeCount = tree.FirstChild.ChildNodes.Count;
                for (int i = 0; i < myNodeCount; i++)
                {
                    XmlNode Node = tree.ChildNodes[0].ChildNodes[i];
                    if (type == StructureType.Area)
                        Console.WriteLine("Area: {0}", Node.Attributes["Name"].Value);

                    else
                        Console.WriteLine("Iteration: {0}", Node.Attributes["Name"].Value);
                }
            }
        }S 
    }
}