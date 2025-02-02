namespace SC2Client;

public class ConsoleLogSink : ILogSink {
    public void Log(string message) {
        Console.WriteLine(message);
    }
}
