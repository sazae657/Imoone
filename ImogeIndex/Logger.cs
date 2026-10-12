using ImogeIndex.Common;
using System.Text;

namespace ImogeIndex;

/// <summary>
/// ﾛｸﾞﾚﾍﾞﾙ
/// </summary>
public enum LogLevel {
    Error,
    Warn,
    Info,
    Debug
}

/// <summary>
/// ﾛｸﾞ
/// </summary>
public class Logger {
    /// <summary>
    /// ﾃﾞﾌｫﾙﾄ世代数
    /// </summary>
    public const int DefaultGenerationCount = 5;

    static Logger() 
    {
        dictator = new();
    }

    private static Logger dictator;
    private FileInfo? logFilePath = null;
    private readonly object syncRoot = new object();
    private StreamWriter? stream = null;

    public static Logger Dictator => dictator;

    private Logger() {
    }

    /// <summary>
    /// ﾛｸﾞを開いて既存のﾛｸﾞを世代ﾛーﾃーｼｮﾝする
    /// </summary>
    /// <param name="root">ﾛｸﾞの出力先</param>
    /// <param name="generationCount">保持する世代数</param>
    public void Open(DirectoryInfo root, int generationCount = DefaultGenerationCount)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (generationCount < 1) {
            throw new ArgumentOutOfRangeException(nameof(generationCount), Properties.Resources.GL_E_FewGeneration);
        }

        lock (syncRoot) {
            CloseStream();
            logFilePath = root.CombineFile("log.log");
            if (logFilePath.Directory != null && !logFilePath.Directory.Exists) {
                logFilePath.Directory.Create();
            }
            Logrotated(logFilePath, generationCount);
            CreateLogfile(logFilePath);
        }
    }

    /// <summary>
    /// とじこ
    /// </summary>
    public void Close()
    {
        lock (syncRoot) {
            CloseStream();
        }
    }

    /// <summary>
    /// とじこーむ
    /// </summary>
    private void CloseStream() {
        stream?.Close();
        stream?.Dispose();
        stream = null;
    }


    /// <summary>
    /// 数字が大きい世代から順にﾛｸﾞを移動する
    /// </summary>
    /// <param name="logFile"></param>
    /// <param name="geme">世代数</param>
    private static void Logrotated(FileInfo logFile, int geme) {
        if (logFile.Directory == null) {
            return;
        }
        if (!logFile.Directory.Exists) {
            logFile.Directory.Create();
        }

        // 世代数を超えたのをﾊﾟーｼﾞ
        foreach (var oldLog in logFile.Directory.EnumerateFiles($"{logFile.Name}.*")) {
            if (oldLog.Name.Length <= logFile.Name.Length + 1 ||
                !oldLog.Name.StartsWith($"{logFile.Name}.", StringComparison.Ordinal)) {
                continue;
            }
            var suffix = oldLog.Name[(logFile.Name.Length + 1)..];
            if (int.TryParse(suffix, out var generation) && generation >= geme) {
                oldLog.Delete();
            }
        }

        for (int gen = geme - 1; gen >= 1; gen--) {
            var destination = GetGenerationPath(logFile, gen);
            var source = gen == 1
                ? logFile.FullName
                : GetGenerationPath(logFile, gen - 1);

            if (File.Exists(source)) {
                File.Move(source, destination, true);
            }
            else if (File.Exists(destination)) {
                File.Delete(destination);
            }
        }
    }


    /// <summary>
    /// 世代付きﾌｧｲﾙ
    /// </summary>
    /// <param name="logFile"></param>
    /// <param name="generation">世代数</param>
    /// <returns></returns>
    private static string GetGenerationPath(FileInfo logFile, int generation) {
        return $"{logFile.FullName}.{generation}";
    }

    /// <summary>
    /// Errorﾚﾍﾞﾙのﾛｸﾞを出す
    /// </summary>
    /// <param name="msg"></param>
    public void Error(string msg) {
        if ((int)LogLevel.Error <= Profile.Active.LogLevel) {
            Writen(LogLevel.Error, msg);
        }
    }

    /// <summary>
    /// Errorﾚﾍﾞﾙ(ｽﾀｯｸﾄﾚーｽ付き)ﾛｸﾞ出す
    /// </summary>
    /// <param name="ex"></param>
    public void Error(Exception ex, string msg) {
        if ((int)LogLevel.Error <= Profile.Active.LogLevel) {
            Writen(LogLevel.Error, msg + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace);
        }
    }

    /// <summary>
    /// Warnﾚﾍﾞﾙのﾛｸﾞを出す
    /// </summary>
    /// <param name="msg"></param>
    public void Warn(string msg) {
        if ((int)LogLevel.Warn <= Profile.Active.LogLevel) {
            Writen(LogLevel.Warn, msg);
        }
    }

    /// <summary>
    /// Infoﾚﾍﾞﾙのﾛｸﾞを出す
    /// </summary>
    /// <param name="msg"></param>
    public void Info(string msg) {
        if ((int)LogLevel.Info <= Profile.Active.LogLevel) {
            Writen(LogLevel.Info, msg);
        }
    }

    /// <summary>
    /// Debugﾚﾍﾞﾙのﾛｸﾞを出す
    /// </summary>
    /// <param name="msg"></param>
    public void Debug(string msg) {
        if ((int)LogLevel.Debug <= Profile.Active.LogLevel) {
            Writen(LogLevel.Debug, msg);
        }
    }

    /// <summary>
    /// ﾚﾍﾞﾙ指定でﾛｸﾞを出す
    /// </summary>
    /// <param name="level"></param>
    /// <param name="msg"></param>
    public void Log(LogLevel level, string msg) {
        Writen(level, msg);
    }

    /// <summary>
    /// ﾛｸﾞ出力
    /// </summary>
    /// <param name="level"></param>
    /// <param name="msg"></param>
    private void Writen(LogLevel level, string msg) {
        int tid = Thread.CurrentThread.ManagedThreadId;
        var fullMsg = $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}][{tid}][{level}] {msg}";
        lock (syncRoot) {
            stream?.WriteLine(fullMsg);
#if DEBUG
            System.Diagnostics.Debug.WriteLine(fullMsg);
#endif
        }
    }

    /// <summary>
    /// ﾛｸﾞﾌｧｲﾙ作成
    /// </summary>
    /// <param name="logFile"></param>
    private void CreateLogfile(FileInfo logFile) {
        if (logFile == null || logFile.Directory == null) {
            return;
        }
        if (!logFile.Directory.Exists) {
            logFile.Directory.Create();
        }
        if (File.Exists(logFile.FullName)) {
            File.Delete(logFile.FullName);
        }

        stream = new StreamWriter(logFile.FullName, true, Encoding.UTF8)
        {
            AutoFlush = true
        };
    }
}
