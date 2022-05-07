using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NuGet;

namespace NugetCheck
{
    class Program
    {
        private const string TempPackageFolder = "temp";

        private static void TraverseDirectory(string originalHash, DirectoryInfo directoryInfo)
        {
            var subdirectories = directoryInfo.EnumerateDirectories();

            foreach (var subdirectory in subdirectories)
            {
                TraverseDirectory(originalHash, subdirectory);
            }

            var files = directoryInfo.EnumerateFiles();

            foreach (var file in files)
            {
                HandleFile(originalHash, file);
            }
        }

        private static void HandleFile(string originalHash, FileInfo file)
        {
            if (!(file.Extension == ".dll" || file.Extension == ".exe" || file.Extension == ".lib"))
                return;

            var hash = Utils.ChecksumFile(file.FullName);
            ConsoleHelper.PrintDebug($"{file.Name} {hash}");

            if (hash != originalHash)
                return;

            ConsoleHelper.PrintSuccess("Match found");
            Console.ReadKey();
            Environment.Exit(0);
        }

        private static void PerformCheck(string originalFile, string searchName, string version)
        {
            ConsoleHelper.PrintInfo($"Searching NuGet for {searchName}");
            
            ConsoleHelper.PrintDebug("Repository init");
            var repo = PackageRepositoryFactory.Default.CreateRepository("https://packages.nuget.org/api/v2");

            if (Directory.Exists(TempPackageFolder))
            {
                ConsoleHelper.PrintDebug("Cleaning temp folder");
                Directory.Delete(TempPackageFolder, true);
            }

            ConsoleHelper.PrintDebug("Package manager init");
            var packageManager = new PackageManager(repo, TempPackageFolder);

            ConsoleHelper.PrintDebug("Trying to install package");
            packageManager.InstallPackage(searchName, SemanticVersion.Parse(version), true, true);

            /*ConsoleHelper.PrintDebug("Searching for packages");
            var packages = repo.FindPackagesById(searchName).ToList();
            if (packages.Count == 0)
            {
                ConsoleHelper.PrintError("Zero packages found with given a name");
                return;
            }
            ConsoleHelper.PrintDebug($"Found {packages.Count} packages");*/

            ConsoleHelper.PrintInfo("Scanning files");
            var originalHash = Utils.ChecksumFile(originalFile);
            TraverseDirectory(originalHash, new DirectoryInfo(TempPackageFolder));

            ConsoleHelper.PrintError("Match not found");
        }
        
        static void Main(string[] args)
        {
            ConsoleHelper.Init();

            if (args.Length != 1)
            {
                ConsoleHelper.PrintError("Invalid arguments");
                Console.ReadKey();
                return;
            }

            var targetFile = args[0];
            if (!File.Exists(targetFile))
            {
                ConsoleHelper.PrintError("Invalid path");
                Console.ReadKey();
                return;
            }

            ConsoleHelper.PrintDebug($"Loading assembly {new FileInfo(targetFile).Name}");
            var data = File.ReadAllBytes(targetFile);

            try
            {
                var assembly = Assembly.Load(data);
                ConsoleHelper.PrintDebug(assembly.FullName);

                var name = assembly.GetName().Name;
                var version = assembly.GetName().Version.ToString();
                PerformCheck(targetFile, name, version);
            }
            catch (Exception error)
            {
                ConsoleHelper.PrintError(error.Message);
                Console.ReadKey();
                return;
            }

            Console.ReadKey();
        }
    }
}
