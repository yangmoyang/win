using System;
using System.IO;
using System.Text;
using System.Threading;

public class AdvancedFileLinesMonitor : IDisposable
{
    private FileSystemWatcher _fileWatcher;
    private string _filePath;
    private System.Threading.Timer _debounceTimer;
    private readonly int _debounceInterval;

    public event Action<string[], string> FileLinesRead;
    public event Action<string> FileDeleted;
    public event Action<Exception> FileError;

    // 构造函数 - 没有返回类型！
    public AdvancedFileLinesMonitor(string filePath, int debounceInterval = 500)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        _filePath = filePath;
        _debounceInterval = debounceInterval;
        _debounceTimer = new System.Threading.Timer(DebounceTimerElapsed, null,
            Timeout.Infinite, Timeout.Infinite);

        InitializeFileWatcher();
    }

    private void InitializeFileWatcher()
    {
        string directory = Path.GetDirectoryName(_filePath);
        string fileName = Path.GetFileName(_filePath);

        if (string.IsNullOrEmpty(directory))
            throw new ArgumentException("无效的文件路径", nameof(_filePath));

        _fileWatcher = new FileSystemWatcher
        {
            Path = directory,
            Filter = fileName,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Created += OnFileChanged;
        _fileWatcher.Deleted += OnFileDeleted;
        _fileWatcher.Error += OnFileError;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // 防抖处理
        _debounceTimer.Change(_debounceInterval, Timeout.Infinite);
    }

    private void DebounceTimerElapsed(object state)
    {
        ReadFileWithRetry(_filePath, 3, 100);
    }

    private void ReadFileWithRetry(string filePath, int maxRetries, int retryDelay)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    FileDeleted?.Invoke(filePath);
                    return;
                }

                // 使用 ReadAllLines 读取所有行
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                FileLinesRead?.Invoke(lines, filePath);
                return;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                Thread.Sleep(retryDelay);
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    FileError?.Invoke(ex);
                }
            }
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        FileDeleted?.Invoke(e.FullPath);
    }

    private void OnFileError(object sender, ErrorEventArgs e)
    {
        FileError?.Invoke(e.GetException());
    }

    public void Start()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = true;
        }
    }

    public void Stop()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
        }
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
        _debounceTimer?.Dispose();
    }
}