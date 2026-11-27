using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PdfWordTool
{
    static class Program
    {
        private const string InstanceMutexName = "Local\\PdfWordTool.SingleInstance";
        private const string ActivateEventName = "Local\\PdfWordTool.Activate";

        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int processId);

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int processId);

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                AttachConsole(-1);
                Environment.ExitCode = CliRunner.Run(args);
                Environment.Exit(Environment.ExitCode);
            }

            using (Mutex instanceMutex = new Mutex(false, InstanceMutexName))
            {
                bool isFirstInstance;
                try
                {
                    isFirstInstance = instanceMutex.WaitOne(0, false);
                }
                catch (AbandonedMutexException)
                {
                    // 上一个实例异常退出，接管它的单实例锁。
                    isFirstInstance = true;
                }

                if (!isFirstInstance)
                {
                    ActivateExistingInstance();
                    return;
                }

                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    Form owner = Form.ActiveForm;
                    AppDialog.ShowError(owner, "程序发生未处理的错误：\r\n" + e.Exception.Message, "错误");
                };
                RunGui();
            }
        }

        private static void RunGui()
        {
            // 供后续实例请求激活；AutoReset 在主线程取走信号后自动复位。
            using (EventWaitHandle activateEvent = new EventWaitHandle(
                false, EventResetMode.AutoReset, ActivateEventName))
            {
                MainForm form = new MainForm();
                Thread listener = new Thread(delegate() { ListenForActivation(activateEvent, form); });
                listener.IsBackground = true;
                listener.Start();
                Application.Run(form);
            }
        }

        private static void ListenForActivation(EventWaitHandle activateEvent, MainForm form)
        {
            while (true)
            {
                try
                {
                    activateEvent.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                try
                {
                    // 窗口句柄在 Application.Run 后才创建，先等它就绪再投递激活请求。
                    while (!form.IsHandleCreated)
                    {
                        if (form.IsDisposed) return;
                        Thread.Sleep(20);
                    }
                    form.BeginInvoke((MethodInvoker)form.ActivateFromSecondInstance);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (InvalidOperationException)
                {
                    return;
                }
            }
        }

        private static void ActivateExistingInstance()
        {
            // 本进程刚被用户启动，处于前台，可把前台权限移交给已运行的实例。
            AllowSetForegroundWindow(-1); // ASFW_ANY
            try
            {
                using (EventWaitHandle activateEvent = EventWaitHandle.OpenExisting(ActivateEventName))
                    activateEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // 已运行实例尚未建立事件，直接退出即可。
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
