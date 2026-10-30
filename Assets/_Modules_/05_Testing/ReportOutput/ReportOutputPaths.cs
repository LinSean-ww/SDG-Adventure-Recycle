using System;
using System.IO;
using UnityEngine;

namespace SDGAdventure.Testing
{
    // Shared by Editor diagnostics and PlayMode tests. Never writes into a Unity project.
    public static class ReportOutputPaths
    {
        public const string EnvironmentVariable = "SDG_ADVENTURE_TEST_OUTPUT_ROOT";

        public static string Root
        {
            get
            {
                string projectRoot = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, ".."));
                string configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
                string root;
                if (string.IsNullOrWhiteSpace(configured))
                {
                    root = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                        projectRoot, "..", System.IO.Path.GetFileName(projectRoot) + "_TestOutputs"));
                }
                else
                {
                    if (!System.IO.Path.IsPathRooted(configured))
                        throw new InvalidOperationException(EnvironmentVariable + " must be an absolute path.");
                    root = System.IO.Path.GetFullPath(configured.Trim());
                }
                if (SameOrChild(root, projectRoot) || SameOrChild(projectRoot, root))
                    throw new InvalidOperationException("Test output root must be outside the Unity project: " + root);
                for (DirectoryInfo current = new DirectoryInfo(root); current != null; current = current.Parent)
                {
                    if (Directory.Exists(System.IO.Path.Combine(current.FullName, "Assets")) &&
                        Directory.Exists(System.IO.Path.Combine(current.FullName, "Packages")) &&
                        Directory.Exists(System.IO.Path.Combine(current.FullName, "ProjectSettings")))
                        throw new InvalidOperationException("Test output root cannot be inside a Unity project or backup: " + root);
                }
                Directory.CreateDirectory(root);
                return root;
            }
        }

        public static string Resolve(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || System.IO.Path.IsPathRooted(relativePath))
                throw new ArgumentException("Expected a relative output path.", nameof(relativePath));
            string normalized = relativePath.Replace('\\', '/');
            foreach (string segment in normalized.Split('/'))
                if (segment == "." || segment == "..")
                    throw new ArgumentException("Output path cannot traverse directories.", nameof(relativePath));
            string root = Root;
            string result = System.IO.Path.GetFullPath(System.IO.Path.Combine(root,
                normalized.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            if (!SameOrChild(result, root) || string.Equals(result, root, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output path must stay below the output root.", nameof(relativePath));
            string parent = System.IO.Path.GetDirectoryName(result);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            return result;
        }

        private static bool SameOrChild(string path, string ancestor)
        {
            string a = ancestor.TrimEnd(System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar);
            return string.Equals(path, a, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(a + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
