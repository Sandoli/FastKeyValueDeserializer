using System.Text;

namespace FastKeyValueDeserializer;

public static class ParallelLazyDeserializer
{
    public static async Task<LazyDict> DecodeFile(string filePath)
    {
        var builder = new StringBuilder();

        using var reader = new StreamReader(filePath);

        while (await reader.ReadLineAsync() is { } line)
        {
            var commentSep = "--".AsSpan();
            var span = line.AsSpan().Trim();

            if (span.IsEmpty || span.StartsWith(commentSep))
                continue;

            var commentIdx = span.IndexOf(commentSep);
            if (commentIdx >= 0)
                span = span[..commentIdx].Trim();

            if (span.IsEmpty)
                continue;

            var endsWithSemicolon = span.EndsWith(';');

            if (endsWithSemicolon)
                span = span[..^1].TrimEnd();

            builder.Append(span);

            builder.Append(endsWithSemicolon ? '\n' : ' ');
        }

        var buffer = builder.ToString().AsMemory();
        return new LazyDict(await DecodeBufferParallel(buffer));
    }

    public static async Task<Dictionary<string, object>> DecodeBufferParallel(ReadOnlyMemory<char> buffer,
        int maxThreads = 0)
    {
        if (maxThreads <= 0)
            maxThreads = Environment.ProcessorCount;

        var span = buffer.Span;
        var length = span.Length;

        // 1. Scanner début de lignes
        List<int> lineStarts = [0];
        for (var i = 0; i < length;)
            if (span[i] == '\r' || span[i] == '\n')
            {
                var next = i + 1;
                if (next < length && span[i] == '\r' && span[next] == '\n') next++;
                if (next < length) lineStarts.Add(next);
                i = next;
            }
            else
            {
                i++;
            }

        if (lineStarts.Count <= 1 || maxThreads == 1)
            return LazyDeserializer.DecodeBuffer(buffer); // Pas assez de lignes ou mono-thread demandé

        // 2. Découper en segments équilibrés
        var chunks = SplitChunks(lineStarts, length, maxThreads);

        // 3. Lancer les tâches
        var tasks = new Task<Dictionary<string, object>>[chunks.Count];
        for (var i = 0; i < chunks.Count; i++)
        {
            var (start, end) = chunks[i];
            var slice = buffer.Slice(start, end - start);
            tasks[i] = Task.Run(() => LazyDeserializer.DecodeBuffer(slice)); // Réutilise ton DecodeBuffer
        }

        await Task.WhenAll(tasks);

        // 4. Fusionner les dictionnaires
        var finalResult = new Dictionary<string, object>();
        foreach (var task in tasks) MergeDictionaries(finalResult, task.Result);

        return finalResult;
    }

    private static List<(int start, int end)> SplitChunks(List<int> lineStarts, int totalLength, int parts)
    {
        List<(int start, int end)> chunks = new(parts);

        var linesPerPart = Math.Max(1, lineStarts.Count / parts);
        var current = 0;

        for (var i = 0; i < parts && current < lineStarts.Count; i++)
        {
            var start = lineStarts[current];
            var end = current + linesPerPart < lineStarts.Count ? lineStarts[current + linesPerPart] : totalLength;
            chunks.Add((start, end));
            current += linesPerPart;
        }

        // Fixer la fin du dernier chunk
        if (chunks.Count > 0)
        {
            var last = chunks[^1];
            chunks[^1] = (last.start, totalLength);
        }

        return chunks;
    }

    private static void MergeDictionaries(Dictionary<string, object> target, Dictionary<string, object> source)
    {
        foreach (var kvp in source)
            if (!target.TryGetValue(kvp.Key, out var existing))
                target[kvp.Key] = kvp.Value;
            else if (existing is Dictionary<string, object> dictExisting &&
                     kvp.Value is Dictionary<string, object> dictNew)
                MergeDictionaries(dictExisting, dictNew);
            else
                target[kvp.Key] = kvp.Value; // Overwrite
    }
}