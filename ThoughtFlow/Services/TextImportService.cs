using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ThoughtFlow;

public enum TextImportSplitMode
{
    Words,
    Paragraphs
}

public sealed record TextImportOptions(TextImportSplitMode Mode, int WordsPerMessage);

public static partial class TextImportService
{
    private sealed record TextUnit(string Text);
    private sealed record ImportChunk(string Text, int ParagraphIndex);

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".docx"
    };

    private static readonly XNamespace WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static bool IsSupportedImportFile(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    public static string ReadText(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".txt" => File.ReadAllText(path),
            ".docx" => ReadDocxText(path),
            _ => throw new NotSupportedException("Only .txt and .docx imports are supported right now.")
        };
    }

    public static IReadOnlyList<string> SplitIntoMessages(string text, TextImportOptions options)
    {
        var normalized = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        return options.Mode == TextImportSplitMode.Paragraphs
            ? SplitByParagraphs(normalized)
            : SplitByWords(normalized, options.WordsPerMessage);
    }

    private static string ReadDocxText(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var documentEntry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidDataException("This .docx file does not contain a readable Word document body.");

        using var stream = documentEntry.Open();
        var document = XDocument.Load(stream);
        var paragraphs = document
            .Descendants(WordNamespace + "p")
            .Select(ReadParagraph)
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph));

        return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
    }

    private static string ReadParagraph(XElement paragraph)
    {
        var parts = paragraph
            .Descendants()
            .Where(element =>
                element.Name == WordNamespace + "t" ||
                element.Name == WordNamespace + "tab" ||
                element.Name == WordNamespace + "br" ||
                element.Name == WordNamespace + "cr")
            .Select(element =>
            {
                if (element.Name == WordNamespace + "t")
                {
                    return element.Value;
                }

                return element.Name == WordNamespace + "tab" ? "\t" : "\n";
            });

        return string.Concat(parts).Trim();
    }

    private static IReadOnlyList<string> SplitByWords(string text, int wordsPerMessage)
    {
        var targetWordCount = Math.Clamp(wordsPerMessage, 1, 700);
        var chunks = new List<ImportChunk>();
        var paragraphs = SplitTextIntoParagraphs(text);
        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Count; paragraphIndex++)
        {
            chunks.AddRange(SplitParagraphByWords(paragraphs[paragraphIndex], targetWordCount)
                .Select(chunk => new ImportChunk(chunk, paragraphIndex)));
        }

        return MergeFittingChunks(chunks, targetWordCount);
    }

    private static IReadOnlyList<string> SplitParagraphByWords(string paragraph, int targetWordCount)
    {
        var units = BuildSentenceUnits(paragraph);
        if (units.Count == 0)
        {
            return [];
        }

        if (units.Count == 1 && CountWords(units[0].Text) > targetWordCount && !HasSentenceEnding(units[0].Text))
        {
            return SplitByExactWordCount(units[0].Text, targetWordCount);
        }

        var chunks = new List<string>();
        var currentUnits = new List<TextUnit>();
        var currentWords = 0;

        foreach (var unit in units)
        {
            if (currentUnits.Count > 0 && currentWords >= targetWordCount)
            {
                chunks.Add(JoinUnits(currentUnits));
                currentUnits.Clear();
                currentWords = 0;
            }

            currentUnits.Add(unit);
            currentWords += CountWords(unit.Text);

            if (currentWords >= targetWordCount)
            {
                chunks.Add(JoinUnits(currentUnits));
                currentUnits.Clear();
                currentWords = 0;
            }
        }

        if (currentUnits.Count > 0)
        {
            chunks.Add(JoinUnits(currentUnits));
        }

        return chunks;
    }

    private static IReadOnlyList<TextUnit> BuildSentenceUnits(string paragraph)
    {
        var cleanParagraph = InlineWhitespaceRegex().Replace(paragraph, " ").Trim();
        var units = new List<TextUnit>();
        if (string.IsNullOrWhiteSpace(cleanParagraph))
        {
            return units;
        }

        var sentences = SplitParagraphIntoSentences(cleanParagraph);
        if (sentences.Count == 0)
        {
            units.Add(new TextUnit(cleanParagraph));
            return units;
        }

        foreach (var sentence in sentences)
        {
            units.Add(new TextUnit(sentence));
        }

        return units;
    }

    private static IReadOnlyList<string> SplitParagraphIntoSentences(string paragraph)
    {
        var matches = SentenceRegex()
            .Matches(paragraph)
            .Select(match => match.Value.Trim())
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .ToList();

        return matches.Count == 0 ? [paragraph] : matches;
    }

    private static string JoinUnits(IEnumerable<TextUnit> units)
    {
        return string.Join(' ', units.Select(unit => unit.Text)).Trim();
    }

    private static IReadOnlyList<string> MergeFittingChunks(IReadOnlyList<ImportChunk> chunks, int targetWordCount)
    {
        var merged = new List<string>();
        var current = new List<ImportChunk>();
        var currentWords = 0;

        foreach (var chunk in chunks)
        {
            var chunkWords = CountWords(chunk.Text);
            if (chunkWords == 0)
            {
                continue;
            }

            if (current.Count > 0 && currentWords + chunkWords > targetWordCount)
            {
                merged.Add(JoinChunks(current));
                current.Clear();
                currentWords = 0;
            }

            current.Add(chunk);
            currentWords += chunkWords;
        }

        if (current.Count > 0)
        {
            merged.Add(JoinChunks(current));
        }

        return merged;
    }

    private static string JoinChunks(IReadOnlyList<ImportChunk> chunks)
    {
        var parts = new List<string>();
        for (var i = 0; i < chunks.Count; i++)
        {
            if (i > 0)
            {
                parts.Add(chunks[i - 1].ParagraphIndex == chunks[i].ParagraphIndex
                    ? " "
                    : Environment.NewLine + Environment.NewLine);
            }

            parts.Add(chunks[i].Text);
        }

        return string.Concat(parts).Trim();
    }

    private static IReadOnlyList<string> SplitByExactWordCount(string text, int wordsPerMessage)
    {
        var words = WordRegex().Matches(text).Select(match => match.Value).ToArray();
        if (words.Length == 0)
        {
            return [];
        }

        var chunks = new List<string>();
        for (var i = 0; i < words.Length; i += wordsPerMessage)
        {
            chunks.Add(string.Join(' ', words.Skip(i).Take(wordsPerMessage)));
        }

        return chunks;
    }

    private static int CountWords(string text)
    {
        return WordRegex().Matches(text).Count;
    }

    private static bool HasSentenceEnding(string text)
    {
        return text.Any(character => character is '.' or '!' or '?' or '…');
    }

    private static IReadOnlyList<string> SplitByParagraphs(string text)
    {
        var paragraphs = SplitTextIntoParagraphs(text).ToList();
        return paragraphs;
    }

    private static IReadOnlyList<string> SplitTextIntoParagraphs(string text)
    {
        var paragraphs = BlankLineRegex()
            .Split(text.Trim())
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        if (paragraphs.Count > 1 || !text.Contains('\n'))
        {
            return paragraphs;
        }

        return SingleLineRegex()
            .Split(text.Trim())
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();
    }

    private static string NormalizeText(string text)
    {
        return text.ReplaceLineEndings("\n").Trim();
    }

    [GeneratedRegex(@"\S+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"[^\s].*?(?:[.!?…]+[""'\u2019\u201D\)\]\u00BB]*)(?=\s|$)|[^\s].+?(?=$)")]
    private static partial Regex SentenceRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex InlineWhitespaceRegex();

    [GeneratedRegex(@"(?:[ \t]*\n){2,}")]
    private static partial Regex BlankLineRegex();

    [GeneratedRegex(@"[ \t]*\n[ \t]*")]
    private static partial Regex SingleLineRegex();
}
