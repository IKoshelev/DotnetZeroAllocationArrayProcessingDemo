using DotnetZeroAllocationArrayProcessingDemo.Benchmarks;

namespace DotnetZeroAllocationArrayProcessingDemo;


public class Program
{
    public static void Main(string[] args)
    {
#if DEBUG
#else
        //dotnet run -c Release
        //var summary1 = BenchmarkDotNet.Running.BenchmarkRunner.Run<StringPalindromeBenchmark>();
        var summary2 = BenchmarkDotNet.Running.BenchmarkRunner.Run<StringSequenceParseBenchmark>();
        var summary3 = BenchmarkDotNet.Running.BenchmarkRunner.Run<ByteArrayCompareWithThresholdBenchmark>();
#endif

#if DEBUG
        //DoManualStringPalindromeTest();
        //DoManualSimdTrhesholdTest();
#endif
    }

    private static void DoManualSimdTrhesholdTest()
    {
        var t = new ByteArrayCompareWithThresholdBenchmark();
        t.GlobalSetup();

        t.ScalarLoop();
    }

    public static void DoManualStringPalindromeTest()
    {
        Console.WriteLine($"System.Numerics.Vector<UInt16>.IsSupported:{System.Numerics.Vector<UInt16>.IsSupported}");
        //This code uses Vector256, becuase that's the max supported by my CPU, thats 16 UInt16s.
        //If you get 32 here - your CPU supports Vector512 and you can update the code to be even faster
        Console.WriteLine($"Max supported UInt16 Vector is {System.Numerics.Vector<UInt16>.Count}");
        Console.WriteLine($"System.Runtime.Intrinsics.X86.Avx2.IsSupported:{System.Runtime.Intrinsics.X86.Avx2.IsSupported}");

        var t = new StringPalindromeBenchmark();

        t.GlobalSetup();
        var r2 = t.StackallockReverse();

        // palindromes
        t.SetSubject("abcdefghijklmno");
        var res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("abcdefghijklmnop");
        res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("abcdefghijklmnopq");
        res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("abcdefghijklmnopabcdefghijklmno");
        res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("abcdefghijklmnopabcdefghijklmnop");
        res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("abcdefghijklmnopabcdefghijklmnopq");
        res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("Les Misérables  ");
        res = t.VectorEqualsWithShuffleLoad();

        // non-palindromes

        t.SetSubject("abcdefghijklmno", "xyy");
        res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("abcdefghijklmnop", "xyy");
        res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("abcdefghijklmnopq", "xyy");
        res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("abcdefghijklmnopabcdefghijklmn", "xyy");
        res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("abcdefghijklmnopabcdefghijklmno", "xyy");
        res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("abcdefghijklmnopabcdefghijklmnop", "xyy");
        res = t.VectorEqualsWithShuffleLoad();

        t.SetSubject("Les Misérables  ", "xyy");
        res = t.VectorEqualsWithShuffleLoad();
    }
}