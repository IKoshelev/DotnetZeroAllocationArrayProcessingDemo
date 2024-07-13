using BenchmarkDotNet.Attributes;

namespace DotnetZeroAllocationArrayProcessingDemo.Benchmarks;


[MemoryDiagnoser]
public class StringSequenceParseBenchmark
{
    public string subject;
    public long checksum;

    [IterationSetup]
    public void GlobalSetup()
    {
        var rnd = new Random();
        var ints = Enumerable.Range(1, 1000000).Select(x => (long)rnd.Next()).ToArray();
        checksum = ints.Sum();
        subject = string.Join(';', ints);
    }

    [Benchmark]
    public bool StringSplit()
    {
        var total = subject.Split(';').Select(x => (long)int.Parse(x)).Sum();
        return total == checksum;
    }

    [Benchmark]
    public bool Loop()
    {
        var offset = 0;
        var total = 0l;
        while (offset < subject.Length)
        {
            string nextInt;
            if (subject.IndexOf(';', offset)
                is var index and not -1)
            {
                var len = index - offset;
                nextInt = subject.Substring(offset, len);
                offset += len + 1;
            }
            else
            {
                nextInt = subject.Substring(offset);
                offset = subject.Length;
            }
            var paresd = int.Parse(nextInt);
            total += paresd;
        };

        return total == checksum;
    }

    [Benchmark]
    public bool RefStructWhile()
    {
        var enumerator = new SpanCharSplitEnumerator(subject, ';');
        var total = 0l;

        while (enumerator.MoveNext())
        {
            total += int.Parse(enumerator._current);
        };

        return total == checksum;
    }

    //[Benchmark]
    //public bool RefStructWhileBatch()
    //{
    //    var enumerator = new SpanCharSplitEnumerator(subject, ';');
    //    var total = 0l;

    //    while (enumerator.MoveNext())
    //    {
    //        int i1 = Int32.Parse(enumerator.Current);
    //        int i2 = enumerator.MoveNext() ? Int32.Parse(enumerator.Current) : 0;
    //        int i3 = enumerator.MoveNext() ? Int32.Parse(enumerator.Current) : 0;
    //        int i4 = enumerator.MoveNext() ? Int32.Parse(enumerator.Current) : 0;
    //        int i5 = enumerator.MoveNext() ? Int32.Parse(enumerator.Current) : 0;
    //        int i6 = enumerator.MoveNext() ? Int32.Parse(enumerator.Current) : 0;
    //        int i7 = enumerator.MoveNext() ? Int32.Parse(enumerator.Current) : 0;
    //        int i8 = enumerator.MoveNext() ? Int32.Parse(enumerator.Current) : 0;
    //        total += i1 + i2 + i3 + i4
    //               + i5 + i6 + i7 + i8;
    //    };

    //    return total == checksum;
    //}

    [Benchmark]
    public bool RefStructForeach()
    {
        var total = 0l;

        foreach (var entry in new SpanCharSplitEnumerator(subject, ';'))
        {
            total += int.Parse(entry);
        };

        return total == checksum;
    }
}

ref struct SpanCharSplitEnumerator(
    ReadOnlySpan<char> source,
    in char separator)
{
    ReadOnlySpan<char> Remaining = source;
    public readonly ReadOnlySpan<char> Current => _current;
    public ReadOnlySpan<char> _current = default;
    private bool Finished = false;

    readonly char Separator = separator;

    public SpanCharSplitEnumerator GetEnumerator() => this;

    public bool MoveNext()
    {
        if (Finished)
        {
            return false;
        }

        if (Remaining.IsEmpty)
        {
            Finished = true;
            _current = default;
            return false;
        }

        if (Remaining.IndexOf(Separator)
            is var index and not -1)
        {
            _current = Remaining.Slice(0, index);
            Remaining = Remaining.Slice(index + 1);

            return true;
        }

        _current = Remaining;
        Remaining = default;
        return true;
    }
}