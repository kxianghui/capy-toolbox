using System;
using System.IO;

namespace PdfWordTool
{
    // 用户偏好：保存在 %APPDATA%\Capy\settings.ini，读写失败时静默回退默认值。
    static class AppSettings
    {
        public enum CloseAction
        {
            Ask,
            Minimize,
            Exit
        }

        private const string FileName = "settings.ini";
        private static bool _loaded;
        private static CloseAction _closeBehavior = CloseAction.Ask;

        public static CloseAction CloseBehavior
        {
            get
            {
                EnsureLoaded();
                return _closeBehavior;
            }
            set
            {
                EnsureLoaded();
                if (_closeBehavior == value) return;
                _closeBehavior = value;
                Save();
            }
        }

        private static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Capy", FileName);
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    if (string.Equals(key, "CloseBehavior", StringComparison.OrdinalIgnoreCase))
                        _closeBehavior = ParseAction(value);
                }
            }
            catch
            {
                _closeBehavior = CloseAction.Ask;
            }
        }

        private static CloseAction ParseAction(string value)
        {
            if (string.Equals(value, "minimize", StringComparison.OrdinalIgnoreCase))
                return CloseAction.Minimize;
            if (string.Equals(value, "exit", StringComparison.OrdinalIgnoreCase))
                return CloseAction.Exit;
            return CloseAction.Ask;
        }

        private static string ToToken(CloseAction action)
        {
            if (action == CloseAction.Minimize) return "minimize";
            if (action == CloseAction.Exit) return "exit";
            return "ask";
        }

        private static void Save()
        {
            try
            {
                string path = FilePath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, "CloseBehavior=" + ToToken(_closeBehavior) + Environment.NewLine);
            }
            catch
            {
                // 偏好保存失败不影响主流程。
            }
        }
    }
}
