namespace SC2Client;

public class FileLogSink : ILogSink {
    private readonly StreamWriter _fileStream;

    public FileLogSink(string logFilePath) {
        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

        // For performance reasons, the file stream is kept open
        _fileStream = new StreamWriter(logFilePath, append: true);

        // For safety reasons, the file stream is set to auto flush
        _fileStream.AutoFlush = true;
    }

    public void Log(string message) {
        _fileStream.WriteLine(message);
    }
}
