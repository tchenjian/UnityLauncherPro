using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace UnityLauncherPro
{
    /// <summary>
    /// returns unity installations under given root folders
    /// </summary>
    public static class GetUnityInstallations
    {
        static Dictionary<string, string> platformNames = new Dictionary<string, string> { { "androidplayer", "Android" }, { "windowsstandalonesupport", "Win" }, { "linuxstandalonesupport", "Linux" }, { "LinuxStandalone", "Linux" }, { "OSXStandalone", "OSX" }, { "webglsupport", "WebGL" }, { "metrosupport", "UWP" }, { "iossupport", "iOS" } };

        // returns unity installations
        public static List<UnityInstallation> Scan()
        {
            // unityversion, exe_path
            List<UnityInstallation> results = new List<UnityInstallation>();

            // get list from settings
            var rootFolders = Properties.Settings.Default.rootFolders;

            // iterate all folders under root folders
            foreach (string rootFolder in rootFolders)
            {
                // if folder exists
                if (String.IsNullOrWhiteSpace(rootFolder) == true || Directory.Exists(rootFolder) == false) continue;

                // get all folders
                var directories = Directory.GetDirectories(rootFolder);
                // parse all folders under root, and search for unity editor files
                for (int i = 0, length = directories.Length; i < length; i++)
                {
                    var editorFolder = ResolveEditorFolder(directories[i]);
                    if (editorFolder == null) continue;

                    var unity = BuildUnityInstallation(editorFolder);
                    if (unity == null) continue;

                    AddIfNotDuplicate(results, unity);
                } // got folders
            } // all root folders

            // scan custom unity exe paths (added directly via "Add Editor")
            var customPaths = Properties.Settings.Default.customUnityExePaths;
            if (customPaths != null)
            {
                foreach (string exePath in customPaths)
                {
                    if (String.IsNullOrWhiteSpace(exePath) || File.Exists(exePath) == false) continue;
                    var editorFolder = Path.GetDirectoryName(exePath);
                    var unity = BuildUnityInstallation(editorFolder);
                    if (unity == null) continue;
                    AddIfNotDuplicate(results, unity);
                }
            }

            // sort by version
            results.Sort((s1, s2) => s2.VersionCode.CompareTo(s1.VersionCode));

            return results;
        } // scan()

        // resolve the editor folder (containing Unity.exe) under a candidate directory
        static string ResolveEditorFolder(string directory)
        {
            // standard install layout: <dir>/Editor/Unity.exe
            var editorFolder = Path.Combine(directory, "Editor");
            if (Directory.Exists(editorFolder) == false)
            {
                // source build layout: <dir>/build/WindowsEditor/x64/Release
                editorFolder = Path.Combine(directory, "build/WindowsEditor/x64/Release");
                if (Directory.Exists(editorFolder) == false)
                {
                    // fallback: recursively search for Unity.exe in subdirectories
                    editorFolder = FindUnityEditorFolder(directory);
                    if (editorFolder == null) return null;
                }
            }
            // verify Unity.exe is actually there
            if (File.Exists(Path.Combine(editorFolder, "Unity.exe")) == false) return null;
            return editorFolder;
        }

        // build a UnityInstallation object from an editor folder (containing Unity.exe)
        static UnityInstallation BuildUnityInstallation(string editorFolder)
        {
            var exePath = Path.Combine(editorFolder, "Unity.exe");
            if (File.Exists(exePath) == false) return null;

            // check if uninstaller is there, sure sign of unity
            var uninstallExe = Path.Combine(editorFolder, "Uninstall.exe");
            var haveUninstaller = File.Exists(uninstallExe);

            // get full version number from uninstaller (or try exe, if no uninstaller)
            var version = Tools.GetFileVersionData(haveUninstaller ? uninstallExe : exePath);

            // fallback for source builds where ProductName/FileDescription may be empty
            if (string.IsNullOrEmpty(version))
            {
                try
                {
                    var fvi = FileVersionInfo.GetVersionInfo(exePath);
                    // ProductVersion usually like "2021.3.12f1_12eddfd2dbe7", take part before '_' to get standard version
                    var pv = fvi.ProductVersion;
                    if (string.IsNullOrEmpty(pv) == false)
                    {
                        int idx = pv.IndexOf('_');
                        version = idx > 0 ? pv.Substring(0, idx) : pv;
                    }
                    // if productversion not usable, try fileversion
                    if (string.IsNullOrEmpty(version))
                    {
                        version = fvi.FileVersion;
                    }
                }
                catch { }
            }

            // if still empty, use folder name as last resort so it can still be listed
            if (string.IsNullOrEmpty(version))
            {
                version = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(editorFolder))) ?? "Unknown";
            }

            // we got new version to add
            var dataFolder = Path.Combine(editorFolder, "Data");
            DateTime? installDate = Tools.GetLastModifiedTime(dataFolder);
            UnityInstallation unity = new UnityInstallation();
            unity.Version = version;
            unity.VersionCode = Tools.VersionAsLong(version); // cached version code
            unity.Path = exePath;
            unity.Installed = installDate;
            unity.IsPreferred = (version == MainWindow.preferredVersion);
            unity.ProjectCount = GetProjectCountForUnityVersion(version);

            // TODO here need to check for vulnerabilities from CACHED data, BUT if its cached, then it might be old
            unity.InfoLabel = null;

            if (Tools.IsAlpha(version))
            {
                unity.ReleaseType = "Alpha";
            }
            else if (Tools.IsBeta(version))
            {
                unity.ReleaseType = "Beta";
            }
            else
                if (Tools.IsLTS(version))

            {
                unity.ReleaseType = "LTS";
            }
            else
            {
                unity.ReleaseType = ""; // cannot be null for UnitysFilter to work properly
            }

            // get platforms, NOTE if this is slow, do it later, or skip for commandline
            var platforms = GetPlatforms(editorFolder);
            // this is for editor tab, show list of all platforms in cell
            if (platforms != null) unity.PlatformsCombined = string.Join(", ", platforms);
            // this is for keeping array of platforms for platform combobox
            if (platforms != null) unity.Platforms = platforms;

            return unity;
        }

        static void AddIfNotDuplicate(List<UnityInstallation> results, UnityInstallation unity)
        {
            // add to list, if not there yet NOTE should notify that there are 2 same versions..? this might happen with preview builds..
            if (results.Contains(unity) == true)
            {
                Console.WriteLine("Warning: 2 same versions found for " + unity.Version);
                return;
            }
            // also skip if same exe path already added
            for (int i = 0; i < results.Count; i++)
            {
                if (string.Equals(results[i].Path, unity.Path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            results.Add(unity);
        }

        // recursively search for Unity.exe under the given directory (limited depth to avoid slow scans)
        static string FindUnityEditorFolder(string directory, int maxDepth = 4)
        {
            try
            {
                if (File.Exists(Path.Combine(directory, "Unity.exe"))) return directory;
                if (maxDepth <= 0) return null;
                foreach (var sub in Directory.EnumerateDirectories(directory))
                {
                    var result = FindUnityEditorFolder(sub, maxDepth - 1);
                    if (result != null) return result;
                }
            }
            catch (Exception) { }
            return null;
        }

        public static bool HasUnityInstallations(string path)
        {
            var directories = Directory.GetDirectories(path);

            // loop folders inside root
            for (int i = 0, length = directories.Length; i < length; i++)
            {
                var editorFolder = ResolveEditorFolder(directories[i]);
                if (editorFolder == null) continue;

                // have atleast 1 installation
                return true;
            }

            return false;
        }

        // scans unity installation folder for installed platforms
        // supports standard layout (Data/PlaybackEngines) and source build layout (platform folders in ancestor directory)
        static string[] GetPlatforms(string editorFolder)
        {
            // 1. try standard layout: <editorFolder>/Data/PlaybackEngines
            var dataFolder = Path.Combine(editorFolder, "Data");
            var platformFolder = Path.Combine(dataFolder, "PlaybackEngines");
            if (Directory.Exists(platformFolder))
            {
                return ScanPlatformFolder(platformFolder);
            }

            // 2. fallback: search ancestor for source build layout (platform folders like AndroidPlayer, iOSSupport next to WindowsEditor)
            var dir = editorFolder;
            string root = Path.GetPathRoot(dir);
            int maxUp = 6;
            while (dir != null && string.Equals(dir, root, StringComparison.OrdinalIgnoreCase) == false && maxUp-- > 0)
            {
                var parent = Path.GetDirectoryName(dir);
                if (parent == null) break;
                var platforms = ScanPlatformFoldersInDirectory(parent);
                if (platforms != null) return platforms;
                dir = parent;
            }

            return null;
        }

        // scan PlaybackEngines folder (standard layout)
        static string[] ScanPlatformFolder(string platformFolder)
        {
            var directories = new List<string>(Directory.GetDirectories(platformFolder));
            var count = directories.Count;
            for (int i = 0; i < count; i++)
            {
                var foldername = Path.GetFileName(directories[i]).ToLower();
                // check if have better name in dictionary
                if (platformNames.ContainsKey(foldername))
                {
                    directories[i] = platformNames[foldername];

                    // add also 64bit desktop versions for that platform, NOTE dont add android, ios or webgl
                    if (foldername.IndexOf("alone") > -1) directories.Add(platformNames[foldername] + "64");
                }
                else // use raw
                {
                    directories[i] = foldername;
                }
            }

            return directories.ToArray();
        }

        // scan a directory for platform folders by name (source build layout: AndroidPlayer, WindowsStandaloneSupport, iOSSupport, ...)
        static string[] ScanPlatformFoldersInDirectory(string dir)
        {
            if (Directory.Exists(dir) == false) return null;
            var result = new List<string>();
            try
            {
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    var fname = Path.GetFileName(sub).ToLower();
                    if (platformNames.ContainsKey(fname))
                    {
                        result.Add(platformNames[fname]);
                        // add also 64bit desktop versions for standalone platforms
                        if (fname.IndexOf("alone") > -1) result.Add(platformNames[fname] + "64");
                    }
                }
            }
            catch (Exception) { }
            return result.Count > 0 ? result.ToArray() : null;
        }

        static int GetProjectCountForUnityVersion(string version)
        {
            if (MainWindow.projectsSource == null) return 0;
            //Console.WriteLine("xxx "+(MainWindow.projectsSource==null));
            int count = 0;
            // count projects using this exact version
            for (int i = 0, len = MainWindow.projectsSource.Count; i < len; i++)
            {
                if (MainWindow.projectsSource[i].Version == version) count++;
            }
            return count;
        }

    } // class
} // namespace
