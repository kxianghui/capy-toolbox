using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PdfWordTool
{
    sealed class ConvertException : Exception
    {
        public ConvertException(string message) : base(message) { }
    }

    static class Engine
    {
        const string EngineResourceName = "PdfWordTool.pdf2docx_engine.exe";

        static bool _engineChecked;
        static string _enginePath;

        public static string BaseDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        // 单文件发布：内置引擎在首次转换时释放到本地缓存目录
        public static string EnginePath
        {
            get
            {
                if (!_engineChecked)
                {
                    _engineChecked = true;
                    _enginePath = ResolveEngine();
                }
                return _enginePath;
            }
        }

        public static bool IsPdfFile(string path)
        {
            return string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        // outputDir 为空时输出到源文件所在目录。
        public static string Convert(string input, string outputDir, out string output)
        {
            if (!File.Exists(input))
                throw new ConvertException("文件不存在：" + input);
            if (!IsPdfFile(input))
                throw new ConvertException("仅支持 PDF 文件：" + Path.GetExtension(input));
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                throw new ConvertException("输出目录不存在：" + outputDir);

            output = MakeOutputPath(input, outputDir);
            ConvertPdfToWord(input, output);
            return output;
        }

        static string MakeOutputPath(string input, string outputDir)
        {
            string dir = string.IsNullOrEmpty(outputDir)
                ? Path.GetDirectoryName(Path.GetFullPath(input))
                : outputDir;
            string name = Path.GetFileNameWithoutExtension(input);
            string candidate = Path.Combine(dir, name + ".docx");
            if (!File.Exists(candidate)) return candidate;

            for (int i = 1; ; i++)
            {
                candidate = Path.Combine(dir, string.Format("{0} ({1}).docx", name, i));
                if (!File.Exists(candidate)) return candidate;
            }
        }

        static void ConvertPdfToWord(string input, string output)
        {
            string engine = EnginePath;
            string engineError = null;

            if (engine != null)
            {
                try
                {
                    RunProcess(engine, Quote(input) + " " + Quote(output), 600);
                    VerifyOutput(output);
                    return;
                }
                catch (ConvertException ex)
                {
                    engineError = ex.Message;
                    try { if (File.Exists(output)) File.Delete(output); } catch { }
                }
            }

            if (TryWordCom(input, output))
            {
                VerifyOutput(output);
                return;
            }

            if (engineError != null)
                throw new ConvertException("PDF→Word：" + engineError);

            throw new ConvertException("内置转换引擎不可用，请重新下载完整的程序文件。");
        }

        static string ResolveEngine()
        {
            // 开发调试时允许把引擎放在程序目录或 engine 子目录
            string[] devCandidates = new string[]
            {
                Path.Combine(BaseDir, "pdf2docx_engine.exe"),
                Path.Combine(BaseDir, "engine", "pdf2docx_engine.exe")
            };
            foreach (string candidate in devCandidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream res = asm.GetManifestResourceStream(EngineResourceName))
                {
                    if (res == null) return null;
                    return ExtractEngine(res);
                }
            }
            catch
            {
                return null;
            }
        }

        static string ExtractEngine(Stream res)
        {
            string dir;
            try
            {
                dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PdfWordTool");
                Directory.CreateDirectory(dir);
            }
            catch
            {
                dir = Path.Combine(Path.GetTempPath(), "PdfWordTool");
                Directory.CreateDirectory(dir);
            }

            string target = Path.Combine(dir, "pdf2docx_engine.exe");
            if (IsValidEngineFile(target, res.Length)) return target;

            string tmp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = res.Read(buffer, 0, buffer.Length)) > 0)
                    fs.Write(buffer, 0, read);
            }

            try
            {
                if (File.Exists(target)) File.Delete(target);
            }
            catch { }

            try
            {
                File.Move(tmp, target);
            }
            catch
            {
                try { File.Delete(tmp); } catch { }
                if (!IsValidEngineFile(target, res.Length)) throw;
            }
            return target;
        }

        static bool IsValidEngineFile(string path, long expectedLength)
        {
            try
            {
                if (!File.Exists(path)) return false;
                FileInfo info = new FileInfo(path);
                if (info.Length != expectedLength) return false;

                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int first = fs.ReadByte();
                    int second = fs.ReadByte();
                    return first == 'M' && second == 'Z';
                }
            }
            catch
            {
                return false;
            }
        }

        static bool TryWordCom(string input, string output)
        {
            if (!WordComLooksLikeRealWord()) return false;

            object appObj = null;
            try
            {
                Type wordType = Type.GetTypeFromProgID("Word.Application");
                if (wordType == null) return false;

                appObj = Activator.CreateInstance(wordType);
                dynamic app = appObj;
                app.Visible = false;
                app.DisplayAlerts = 0;

                dynamic doc = app.Documents.Open(input, false, true);
                if (doc == null) return false;
                doc.SaveAs2(output, 16);
                try { doc.Close(0); } catch { }
                return File.Exists(output) && new FileInfo(output).Length > 0;
            }
            catch
            {
                return File.Exists(output) && new FileInfo(output).Length > 0;
            }
            finally
            {
                if (appObj != null)
                {
                    try { ((dynamic)appObj).Quit(); } catch { }
                    try { Marshal.ReleaseComObject(appObj); } catch { }
                }
            }
        }

        // 只在 Word.Application 确实指向 winword.exe 时才尝试 COM，
        // 避免在只装了 WPS 的机器上触发其伪装 ProgID。
        static bool WordComLooksLikeRealWord()
        {
            try
            {
                using (RegistryKey k = Registry.ClassesRoot.OpenSubKey(@"Word.Application\CLSID"))
                {
                    if (k == null) return false;
                    string clsid = k.GetValue(null) as string;
                    if (string.IsNullOrEmpty(clsid)) return false;

                    using (RegistryKey s = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + clsid + @"\LocalServer32"))
                    {
                        if (s == null) return false;
                        string exe = ExtractExe(s.GetValue(null) as string);
                        return exe != null
                            && string.Equals(Path.GetFileName(exe), "winword.exe", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch { return false; }
        }

        static void VerifyOutput(string output)
        {
            if (!File.Exists(output))
                throw new ConvertException("转换结束但未找到输出文件。");
            if (new FileInfo(output).Length == 0)
            {
                try { File.Delete(output); } catch { }
                throw new ConvertException("转换输出为空，文件可能已损坏或无有效内容。");
            }
        }

        static void RunProcess(string exe, string arguments, int timeoutSeconds)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, arguments);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WorkingDirectory = Path.GetDirectoryName(exe);

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.Start();
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();

                if (!p.WaitForExit(timeoutSeconds * 1000))
                {
                    try { p.Kill(); } catch { }
                    throw new ConvertException(string.Format("转换超时（超过 {0} 秒）。", timeoutSeconds));
                }

                if (p.ExitCode != 0)
                    throw new ConvertException(MapExitCode(p.ExitCode, stdout, stderr));
            }
        }

        static string MapExitCode(int code, string stdout, string stderr)
        {
            string detail = (stderr + " " + stdout).Trim();
            if (detail.Length > 300) detail = detail.Substring(0, 300);

            switch (code)
            {
                case 2:
                    return "引擎调用参数错误。";
                case 3:
                    return "找不到输入文件。";
            }

            return detail.Length > 0
                ? string.Format("转换失败（退出码 {0}）。{1}", code, detail)
                : string.Format("转换失败（退出码 {0}）。", code);
        }

        static string Quote(string s)
        {
            return "\"" + s.Replace("\"", "\\\"") + "\"";
        }

        static string ExtractExe(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine)) return null;
            string cmd = commandLine.Trim();
            if (cmd.StartsWith("\""))
            {
                int end = cmd.IndexOf('"', 1);
                if (end > 1) return cmd.Substring(1, end - 1);
            }
            int space = cmd.IndexOf(' ');
            return space > 0 ? cmd.Substring(0, space) : cmd;
        }
    }

    static class CliRunner
    {
        public static int Run(string[] files)
        {
            int ok = 0;
            int fail = 0;
            string log = Path.Combine(Path.GetTempPath(), "pdf2word_cli.log");
            StreamWriter writer = null;
            try { writer = new StreamWriter(log, false); } catch { }

            foreach (string file in files)
            {
                string line;
                try
                {
                    string input = Path.GetFullPath(file);
                    string output;
                    Engine.Convert(input, null, out output);
                    ok++;
                    line = "OK\t" + input + "\t" + output;
                }
                catch (Exception ex)
                {
                    fail++;
                    line = "FAIL\t" + file + "\t" + ex.Message;
                }
                Console.WriteLine(line);
                if (writer != null) { writer.WriteLine(line); writer.Flush(); }
            }

            string summary = string.Format("DONE ok={0} fail={1}", ok, fail);
            Console.WriteLine(summary);
            if (writer != null) { writer.WriteLine(summary); writer.Close(); }
            return fail == 0 ? 0 : 1;
        }
    }
}
