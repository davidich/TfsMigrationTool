using System.IO;
using System.Runtime.InteropServices;
using Microsoft.TeamFoundation.TestManagement.Client;
using TfsMigrationTool.Migrators;

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
        public static void Main()
        {
            Console.WindowWidth = Console.LargestWindowWidth;
            Console.WindowHeight = Console.LargestWindowHeight;
            Console.SetWindowPosition(0, 0);

            CopyWorkItems();

            CopyTestManagementItems();

            Console.WriteLine("COMPLETED!!!");
            Console.WriteLine();
            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }

        private static void CopyWorkItems()
        {
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("----------------WORK ITEMS --------------");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine();

            var migrator = new WorkItemMigrator(Config.SourceProject, Config.TargetProject);

            migrator.MigrateIterationPath("DeloitteConnect");

            //migrator.MigrateIterationPath("DeloitteConnect\\Release 2");
            //migrator.MigrateIterationPath("DeloitteConnect\\Release 2.1");
            //migrator.MigrateIterationPath("DeloitteConnect\\Release 3");
            //migrator.MigrateIterationPath("DeloitteConnect\\Release 4");
            //migrator.MigrateIterationPath("DeloitteConnect\\Release 5");

            Console.WriteLine();            
        }

        private static void CopyTestManagementItems()
        {
            Console.WriteLine("------------------------------------------");
            Console.WriteLine("-----------TEST MANAGEMENT ITEMS----------");
            Console.WriteLine("------------------------------------------");
            Console.WriteLine();

            var migrator = new TestManagementMigrator(Config.SourceProject, Config.TargetProject);

            migrator.CopyTestPlan(1162, 1301);
            migrator.CopyTestPlan(895, 1299);
            migrator.CopyTestPlan(966, 1300);
        }
    }
}
