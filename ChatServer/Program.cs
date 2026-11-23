using ChatShared;

namespace ChatServer
{
    internal static class Program
    {
        private const string AppName = "ChatServer";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            InstallGlobalErrorHandlers();
            Application.Run(new ServerForm());
        }

        /// <summary>Beklenmeyen hatalar uygulamayı sessizce kapatmasın: loglanır ve kullanıcıya bildirilir.</summary>
        private static void InstallGlobalErrorHandlers()
        {
            // UI thread'indeki yakalanmamış hatalar: uygulama çalışmaya devam eder.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ReportUnexpectedError(e.Exception, willExit: false);

            // Diğer thread'lerdeki yakalanmamış hatalar: .NET süreci sonlandırır, en azından nedeni kaydedilir.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                ReportUnexpectedError(e.ExceptionObject as Exception ?? new InvalidOperationException(e.ExceptionObject?.ToString()), willExit: e.IsTerminating);

            // Beklenmeyen Task hataları: sessizce kaybolmasın diye yalnızca loglanır.
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                CrashLog.Write(AppName, e.Exception);
                e.SetObserved();
            };
        }

        private static void ReportUnexpectedError(Exception exception, bool willExit)
        {
            string? logPath = CrashLog.Write(AppName, exception);
            string where = logPath is null ? "Hata ayrıntıları kaydedilemedi." : $"Ayrıntılar şu dosyaya kaydedildi:\n{logPath}";
            string consequence = willExit ? "Uygulama kapanacak." : "Uygulama çalışmaya devam edecek.";

            MessageBox.Show(
                $"Beklenmeyen bir hata oluştu. {consequence}\n\n{exception.Message}\n\n{where}",
                "ChatApp Sunucu - Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
