using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using TfsMigrationTool.Utils;

namespace TfsMigrationTool.Migrators
{
    // http://blogs.msdn.com/b/densto/archive/2010/03/04/the-test-management-api-part-2-creating-modifying-test-plans.aspx
    public class TestManagementMigrator
    {
        private readonly ITestManagementService _service;
        private readonly ITestManagementTeamProject _deloitteConnectProject;
        private readonly ITestManagementTeamProject _connectProject;

        public static readonly Dictionary<int, IdAndName> TargetConfigs = new Dictionary<int, IdAndName>
        {
            {953, new IdAndName(953, "Google Chrome")},
            {954, new IdAndName(954, "Mozilla Firefox")},
            {956, new IdAndName(956, "Internet Explorer 11.0")},
            {955, new IdAndName(955, "Internet Explorer 9.0")},
            {957, new IdAndName(957, "Safari")},
            {922, new IdAndName(922, "Windows 7")}
        };

        private static readonly Dictionary<int, IdAndName> ConfigMap = new Dictionary<int, IdAndName>
        {
            {651, TargetConfigs[953]},  // Chrome
            {653, TargetConfigs[954]},  // FF
            {650, TargetConfigs[956]},  // IE 11
            {649, TargetConfigs[955]},  // IE 9
            {652, TargetConfigs[957]},  // Safari
            {541, TargetConfigs[922]}   // Windows
        };

        public TestManagementMigrator(string sourceProjectName, string targetProjectName)
        {
            _service = ServiceFactory.Create<ITestManagementService>();
            _deloitteConnectProject = _service.GetTeamProject(sourceProjectName);
            _connectProject = _service.GetTeamProject(targetProjectName);
        }

        public void CopyTestPlan(int sourceTestPlanId, int targetTestPlanId)
        {
            ITestPlan sourcePlan = _deloitteConnectProject.TestPlans.Find(sourceTestPlanId);
            ITestPlan targetPlan = _connectProject.TestPlans.Find(targetTestPlanId);

            // Clean test plan
            DeleteTestSuitesFrom(targetPlan);

            // suiteMap will contain TestSuiteId in Source Project and corresponding mirrowedTestSuite in target project
            var suiteMap = new Dictionary<int, IStaticTestSuite>
            {
                {sourcePlan.RootSuite.Id, targetPlan.RootSuite}
            };
            // recursivelly iterate through all entries
            foreach (ITestSuiteEntry entry in sourcePlan.RootSuite.Entries)
            {
                CopyEntry(entry, suiteMap);
            }

            targetPlan.Save();
        }

        private void CopyEntry(ITestSuiteEntry sourceEntry, Dictionary<int, IStaticTestSuite> map, int nodeLevel = 0)
        {
            var copiedParrentTestSuite = map[sourceEntry.ParentTestSuite.Id];
            var indent = string.Join("", Enumerable.Repeat('\t', nodeLevel));

            if (sourceEntry.EntryType == TestSuiteEntryType.StaticTestSuite)
            {
                var copiedTestSuite = _connectProject.TestSuites.CreateStatic();

                // copy all useful infomation
                copiedTestSuite.Title = sourceEntry.Title;
                if (sourceEntry.Configurations != null && sourceEntry.Configurations.Count > 0)
                {
                    var mappedConfings = sourceEntry.Configurations.Select(c => ConfigMap[c.Id]);
                    copiedTestSuite.SetDefaultConfigurations(mappedConfings);
                }

                // add new test suite to an appropriate parent Test Suite                
                copiedParrentTestSuite.Entries.Add(copiedTestSuite);

                // update map
                map.Add(sourceEntry.Id, copiedTestSuite);

                // go through children
                var sourceTestSuite = (IStaticTestSuite)sourceEntry.TestSuite;


                Console.WriteLine("{0}- SUITE: \"{1}\" ({2} items)", indent, copiedTestSuite.Title, sourceTestSuite.Entries.Count);

                foreach (var entry in sourceTestSuite.Entries)
                {
                    CopyEntry(entry, map, nodeLevel + 1);
                }
            }
            else if (sourceEntry.EntryType == TestSuiteEntryType.TestCase)
            {
                var links = sourceEntry.TestCase.WorkItem.Links;
                var link = links.OfType<RelatedLink>().Single(l => l.Comment == "History Ref");
                var targetTestCaseId = link.RelatedWorkItemId;

                var targetTestCase = _connectProject.TestCases.Find(targetTestCaseId);
                copiedParrentTestSuite.Entries.Add(targetTestCase);

                Console.WriteLine("{0}- {1}", indent, targetTestCase.Title);
            }
        }

        private void DeleteTestSuitesFrom(ITestPlan testPlan)
        {
            Console.WriteLine("Cleaning up '{0}'", testPlan.RootSuite.Title);
            Console.WriteLine("Completed");
            Console.WriteLine();

            var rootSuite = testPlan.RootSuite;

            var testSuites = rootSuite.Entries.Select(e => e.TestSuite).OfType<IStaticTestSuite>().ToList();

            foreach (var testSuit in testSuites)
            {
                rootSuite.Entries.Remove(testSuit);
            }

            testPlan.Save();
        }

        public IList<ITestConfiguration> GetConfigurations(string projectName)
        {
            var query = BuildConfigurtionQueryText(projectName);
            ITestConfigurationCollection collection = _service.GetTeamProject(projectName).TestConfigurations.Query(query);

            return collection;
        }

        private string BuildConfigurtionQueryText(string projectName)
        {
            var sb = new StringBuilder("SELECT * FROM TestConfiguration");

            sb.AppendFormat(" WHERE [System.TeamProject] = '{0}'", projectName);

            return sb.ToString();
        }
    }
}