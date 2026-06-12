namespace ThoughtFlow;

public static class TextExportService
{
    public static string BuildPlainText(FlowTextFile file)
    {
        return string.Join(Environment.NewLine + Environment.NewLine, file.Messages.Select(message => message.Body));
    }
}
