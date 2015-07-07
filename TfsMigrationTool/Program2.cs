using System.IO;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Microsoft.TeamFoundation.TestManagement.Client;
using TfsMigrationTool.Migrators;
using TfsMigrationTool.Utils;

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
    internal class Program2
    {
        private static readonly ITestManagementService _service = ServiceFactory.Create<ITestManagementService>();
        private static readonly ITestManagementTeamProject _connectProject = _service.GetTeamProject(Config.TargetProject);

        public static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;

            Console.WindowWidth = Console.LargestWindowWidth;
            Console.WindowHeight = Console.LargestWindowHeight;
            Console.SetWindowPosition(0, 0);

            TfsAuthorizer.Authenticate();

            int srcId = 0;
            while (srcId == 0)
            {
                Console.Clear();
                Console.WriteLine("Enter source test suite id:");

                var input = Console.ReadLine();
                int.TryParse(input, out srcId);
            }

            Console.WriteLine("OK");
            Console.WriteLine();

            PopulateSprint(srcId);

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("COMPLETED!!!");
            Console.WriteLine();
            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }

        private static void PopulateSprint(int sourceTestSuiteId)
        {
            var sourceTestSuite = (IStaticTestSuite)_connectProject.TestSuites.Find(sourceTestSuiteId);

            IList<IdAndName> configs = sourceTestSuite.TestSuiteEntry.Configurations;
            var missingConfigs = TestManagementMigrator.TargetConfigs.Values
                .Except(configs)
                .Where(conf => conf.Id != 922 && conf.Id != 957);

            foreach (var missingConfig in missingConfigs)
            {
                ITestSuiteBase testSuiteCopy = sourceTestSuite.Parent.SubSuites.SingleOrDefault(s => s.Title == missingConfig.Name);

                Console.WriteLine();
                Console.WriteLine("- SUITE: \"{0}\"", missingConfig.Name);

                if (testSuiteCopy == null)
                {
                    testSuiteCopy = _connectProject.TestSuites.CreateStatic();

                    testSuiteCopy.Title = missingConfig.Name;
                    testSuiteCopy.SetDefaultConfigurations(new List<IdAndName> { missingConfig });
                    sourceTestSuite.Parent.Entries.Add(testSuiteCopy);
                }

                foreach (var entry in sourceTestSuite.Entries)
                {
                    ShallowCopy(entry, (IStaticTestSuite)testSuiteCopy, 1);
                }
            }
        }

        private static void ShallowCopy(ITestSuiteEntry sourceEntry, IStaticTestSuite targetParenTestSuite, int nodeLevel = 0)
        {
            var indent = string.Join("", Enumerable.Repeat('\t', nodeLevel));

            if (sourceEntry.EntryType == TestSuiteEntryType.StaticTestSuite)
            {
                IStaticTestSuite copiedTestSuite = (IStaticTestSuite)targetParenTestSuite.SubSuites.FirstOrDefault(s => s.Title == sourceEntry.Title);

                if (copiedTestSuite != null)
                {
                    WriteSkipped("{0}- SUITE: \"{1}\"", indent, sourceEntry.Title);
                }
                else
                {
                    copiedTestSuite = _connectProject.TestSuites.CreateStatic();

                    // copy all useful infomation
                    copiedTestSuite.Title = sourceEntry.Title;

                    // add new test suite to an appropriate parent Test Suite                
                    targetParenTestSuite.Entries.Add(copiedTestSuite);

                    Console.WriteLine("{0}- SUITE: \"{1}\"", indent, copiedTestSuite.Title);
                }

                // go through children
                var sourceTestSuite = (IStaticTestSuite)sourceEntry.TestSuite;
                foreach (var entry in sourceTestSuite.Entries)
                {
                    ShallowCopy(entry, copiedTestSuite, nodeLevel + 1);
                }
            }
            else if (sourceEntry.EntryType == TestSuiteEntryType.TestCase)
            {
                var targetTestCase = sourceEntry.TestCase;

                if (targetParenTestSuite.TestCases.Any(s => s.Title == sourceEntry.Title))
                {
                    WriteSkipped("{0}- {1}", indent, targetTestCase.Title);
                }
                else
                {
                    targetParenTestSuite.Entries.Add(targetTestCase);
                    Console.WriteLine("{0}- {1}", indent, targetTestCase.Title);
                }
            }
        }

        [StringFormatMethod("text")]
        private static void WriteSkipped(string text, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(text + " - SKIPPED", args);
            Console.ForegroundColor = ConsoleColor.White;
        }        
    }

    public static class Ext
    {
        public static ITestSuiteEntry FindChildWithTitle(this ITestSuiteBase entry, string title)
        {
            return entry.Parent.Entries.FirstOrDefault(e => e.Title == title);
        }
    }
}
