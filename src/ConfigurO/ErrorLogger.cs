using System;
using System.IO;
using System.Text;

namespace ConfigurO
{
    internal static class Logger
    {
        internal static string ErrorLogFile = Path.Combine(CoreHelper.CoreFolder, "ConfigurO.log");

        static StringBuilder _silentReportLog;

        private static void LogErrorSilent(string functionName, string errorMessage, string errorStackTrace)
        {
            if (_silentReportLog == null) return;
            _silentReportLog.AppendLine(string.Format("[ERROR] [{0}] in function [{1}]", DateTime.Now.ToString(), functionName));
            _silentReportLog.AppendLine();
            _silentReportLog.AppendLine(errorMessage);
            _silentReportLog.AppendLine();
            _silentReportLog.AppendLine(errorStackTrace);
            _silentReportLog.AppendLine();
            _silentReportLog.AppendLine();
        }

        /// <summary>
        /// Appends to the silent-run report. Only meaningful during a silent
        /// configuration run, which is the only thing that builds the report.
        ///
        /// The null check is not decoration. This is a logger: if it throws, it
        /// takes down whatever was trying to record something, and it does so
        /// at exactly the moment there is a problem worth recording. It threw
        /// on every interactive launch after a caller outside SilentOps started
        /// using it, and killed the app before it drew a window.
        /// </summary>
        internal static void LogInfoSilent(string message)
        {
            if (_silentReportLog == null) return;
            _silentReportLog.AppendLine($"[OK] {message}");
            _silentReportLog.AppendLine();
        }

        /// <summary>
        /// Records something noteworthy that is not a failure, in whichever
        /// place this run is writing to. Safe from any thread at any point in
        /// startup, including before settings exist.
        /// </summary>
        internal static void LogInfo(string message)
        {
            if (Program.SILENT_MODE) { LogInfoSilent(message); return; }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ErrorLogFile));
                File.AppendAllText(ErrorLogFile, string.Format("[INFO] [{0}] {1}{2}",
                    DateTime.Now.ToString(), message, Environment.NewLine));
            }
            catch { }
        }

        internal static void InitializeSilentReport()
        {
            _silentReportLog = new StringBuilder();

            _silentReportLog.AppendLine(Utilities.GetWindowsDetails());
            _silentReportLog.AppendLine(string.Format("ConfigurO {0} - .NET Framework {1} - Experimental build: {2}", Program.GetCurrentVersionTostring(), Utilities.GetNETFramework(), Program.EXPERIMENTAL_BUILD));
            _silentReportLog.AppendLine($"{DateTime.Now.ToLongDateString()} - {DateTime.Now.ToLongTimeString()}");

            _silentReportLog.AppendLine();
            _silentReportLog.AppendLine();
        }

        internal static void GenerateSilentReport()
        {
            try
            {
                File.WriteAllText($"ConfigurO.SilentReport.{DateTime.Now.ToString("yyyyMMddTHHmm")}.log", _silentReportLog.ToString());
            }
            catch { }
        }

        internal static void LogError(string functionName, string errorMessage, string errorStackTrace)
        {
            if (Program.SILENT_MODE)
            {
                LogErrorSilent(functionName, errorMessage, errorStackTrace);
                return;
            }

            try
            {
                if (!File.Exists(ErrorLogFile) || (File.Exists(ErrorLogFile) && File.ReadAllText(ErrorLogFile).Trim() == string.Empty))
                {
                    File.AppendAllText(ErrorLogFile, Utilities.GetWindowsDetails());
                    File.AppendAllText(ErrorLogFile, Environment.NewLine);
                    File.AppendAllText(ErrorLogFile, string.Format("ConfigurO {0} - .NET Framework {1} - Experimental build: {2}", Program.GetCurrentVersionTostring(), Utilities.GetNETFramework(), Program.EXPERIMENTAL_BUILD));
                    File.AppendAllText(ErrorLogFile, Environment.NewLine);
                    File.AppendAllText(ErrorLogFile, Environment.NewLine);
                    File.AppendAllText(ErrorLogFile, Environment.NewLine);
                }

                File.AppendAllText(ErrorLogFile, string.Format("[ERROR] [{0}] in function [{1}]", DateTime.Now.ToString(), functionName));
                File.AppendAllText(ErrorLogFile, Environment.NewLine);
                File.AppendAllText(ErrorLogFile, errorMessage);
                File.AppendAllText(ErrorLogFile, Environment.NewLine);
                File.AppendAllText(ErrorLogFile, Environment.NewLine);
                File.AppendAllText(ErrorLogFile, errorStackTrace);

                // seperator
                File.AppendAllText(ErrorLogFile, Environment.NewLine);
                File.AppendAllText(ErrorLogFile, Environment.NewLine);
                File.AppendAllText(ErrorLogFile, Environment.NewLine);
            }
            catch { }
            //finally
            //{
            //    if (!Options.CurrentOptions.DisableConfigurOTelemetry)
            //    {
            //        TelemetryHelper.GenerateTelemetryData(functionName, errorMessage, errorStackTrace);
            //    }
            //}
        }
    }
}
