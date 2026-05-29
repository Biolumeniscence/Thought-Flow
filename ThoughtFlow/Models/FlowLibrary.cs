using System.Collections.ObjectModel;

namespace ThoughtFlow;

public sealed class FlowLibrary
{
    public FlowAppSettings Settings { get; set; } = new();
    public ObservableCollection<FlowChannel> Channels { get; set; } = [];

    public void Normalize()
    {
        Settings ??= new FlowAppSettings();
        Settings.Normalize();
        Channels ??= [];
        foreach (var channel in Channels)
        {
            channel.Normalize();
        }
    }

    public static FlowLibrary CreateStarter()
    {
        return new FlowLibrary
        {
            Channels =
            [
                new FlowChannel
                {
                    Name = "writing-room",
                    Files =
                    [
                        new FlowTextFile
                        {
                            Name = "main",
                            Messages =
                            [
                                new FlowMessage
                                {
                                    Body = "A workspace holds files. A file is written as a stream of message chunks."
                                },
                                new FlowMessage
                                {
                                    Body = "The stream reads like one continuous text, but each message can still be opened and edited on the right."
                                }
                            ]
                        },
                        new FlowTextFile
                        {
                            Name = "ideas",
                            Messages =
                            [
                                new FlowMessage
                                {
                                    Body = "Possible next ideas: rename files, export one file to Markdown, drag messages between files, and a clean focus mode."
                                }
                            ]
                        }
                    ]
                },
                new FlowChannel
                {
                    Name = "scenes",
                    Files =
                    [
                        new FlowTextFile
                        {
                            Name = "dialogue draft",
                            Messages =
                            [
                                new FlowMessage
                                {
                                    Body = "A scene can start as chat-like fragments, then become prose when the pieces finally know where they belong."
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }
}

