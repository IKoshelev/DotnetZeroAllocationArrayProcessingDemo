using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;

namespace DotnetZeroAllocationArrayProcessingDemo.Benchmarks;


[MemoryDiagnoser]
public class StringPalindromeBenchmark
{
    public string subject;

    [IterationSetup]
    public void GlobalSetup()
    {
        var rnd = RandomNumberGenerator.GetString(
            choices: "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789",
            length: 1 * 1024 * 1024 // 2MB
        );
        SetSubject(rnd);
    }

    public void SetSubject(string partReflectedOnBothSides, string middlePart = "")
    {
        subject = partReflectedOnBothSides + middlePart + new string(partReflectedOnBothSides.Reverse().ToArray());
    }


    [Benchmark]
    public bool ReverseIter()
    {
        var len = subject.Length / 2;
        var charsForward = subject.GetEnumerator();
        var charsBackwards = subject.Reverse().GetEnumerator();
        while (len >= 0)
        {
            charsForward.MoveNext();
            charsBackwards.MoveNext();
            if (charsForward.Current != charsBackwards.Current)
            {
                return false;
            }
            len--;
        }
        return true;
    }

    [Benchmark]
    public bool ReverseSequenceEqualFull()
    {
        return subject.SequenceEqual(
            subject.Reverse());
    }

    [Benchmark]
    public bool ReverseSequenceEqualHalf()
    {
        var len = subject.Length / 2;
        return subject.Take(len).SequenceEqual(
            subject.Reverse().Take(len));
    }

    [Benchmark]
    public bool Loop()
    {
        return TestInLoop(subject);
    }

    private static bool TestInLoop(string subject)
    {
        var len = subject.Length / 2;
        for (int offset = 0; offset < len; offset++)
        {
            if (subject[offset] != subject[subject.Length - offset - 1])
            {
                return false;
            }
        }
        return true;
    }

    [Benchmark]
    public bool VectorEquals()
    {
        var subjectLength = subject.Length;
        var len = subject.Length / 16 / 2;
        var strBytes = MemoryMarshal.Cast<char, ushort>(subject);
        for (int offset = 0; offset < len; offset++)
        {
            var finalOffset = offset * 16;
            var backFinalOffset = subjectLength - 16 - finalOffset;
            var v1 = Vector256.Create(strBytes.Slice(finalOffset, 16));
            var v2 = Vector256.Create(
                strBytes[backFinalOffset + 15],
                strBytes[backFinalOffset + 14],
                strBytes[backFinalOffset + 13],
                strBytes[backFinalOffset + 12],
                strBytes[backFinalOffset + 11],
                strBytes[backFinalOffset + 10],
                strBytes[backFinalOffset + 9],
                strBytes[backFinalOffset + 8],
                strBytes[backFinalOffset + 7],
                strBytes[backFinalOffset + 6],
                strBytes[backFinalOffset + 5],
                strBytes[backFinalOffset + 4],
                strBytes[backFinalOffset + 3],
                strBytes[backFinalOffset + 2],
                strBytes[backFinalOffset + 1],
                strBytes[backFinalOffset + 0]
            );

            if (v1 != v2)
            {
                return false;
            }
        }
        return true;
    }

    [Benchmark]
    public bool VectorEqualsWithShuffleLoad()
    {
        return TestWithVectors(subject);
    }

    [Benchmark]
    public bool StackallockReverse()
    {
        const int ARR_SIZE = 64;
        if (subject.Length < ARR_SIZE)
        {
            return TestInLoop(subject);
        }

        var subjectLength = subject.Length;
        var cycles = subject.Length / (ARR_SIZE * 2) + (subject.Length % (ARR_SIZE * 2) > 0 ? 1 : 0);
        //Console.WriteLine($"String.length:{subject.Length}, cycles: {cycles}");
        var strBytes = MemoryMarshal.Cast<char, char>(subject);
        Span<char> right = stackalloc char[ARR_SIZE];
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            var offsetFromStart = cycle * ARR_SIZE;
            var offsetFromBack = subjectLength - ARR_SIZE - offsetFromStart;
            var left = strBytes.Slice(offsetFromStart, ARR_SIZE);
            strBytes.Slice(offsetFromBack, ARR_SIZE).CopyTo(right);
            right.Reverse();

            if (!left.SequenceEqual(right))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TestWithVectors(string subject)
    {
        if (subject.Length < 32)
        {
            // If we expect many strings less than 32 chars - we can also optimize those cases with vectors
            return TestInLoop(subject);
        }

        var subjectLength = subject.Length;
        var cycles = subject.Length / (16 * 2) + (subject.Length % (16 * 2) > 0 ? 1 : 0); // In string where length is not divisible by 32 - there will be 1 cycle of overlap
        //Console.WriteLine($"String.length:{subject.Length}, cycles: {cycles}");
        var strBytes = MemoryMarshal.Cast<char, ushort>(subject);
        var shuffleMask = Vector256.Create((byte)14, 15, 12, 13, 10, 11, 8, 9, 6, 7, 4, 5, 2, 3, 0, 1, 14, 15, 12, 13, 10, 11, 8, 9, 6, 7, 4, 5, 2, 3, 0, 1);
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            var offsetFromStart = cycle * 16;
            var offsetFromBack = subjectLength - 16 - offsetFromStart;
            var v1 = Vector256.Create(strBytes.Slice(offsetFromStart, 16));
            var v2 = Vector256.Create(strBytes.Slice(offsetFromBack, 16));

            //reverse ushort order [0,1,2..14,15] => [15,14..2,1,0]
            v2 = System.Runtime.Intrinsics.X86.Avx2.Shuffle(v2.AsByte(), shuffleMask).AsUInt16();
            v2 = System.Runtime.Intrinsics.X86.Avx2.Permute2x128(v2, v2, 0b00000001);

            if (v1 != v2)
            {
                return false;
            }
        }
        return true;
    }

    [Benchmark]
    public bool SimdEquals()
    {
        var subjectLength = subject.Length;
        var len = subject.Length / 16 / 2;
        var strBytes = MemoryMarshal.Cast<char, ushort>(subject);
        for (int offset = 0; offset < len; offset++)
        {
            var finalOffset = offset * 16;
            var backFinalOffset = subjectLength - 16 - finalOffset;
            var v1 = Vector256.Create(strBytes.Slice(finalOffset, 16));
            var v2 = Vector256.Create(
                strBytes[backFinalOffset + 15],
                strBytes[backFinalOffset + 14],
                strBytes[backFinalOffset + 13],
                strBytes[backFinalOffset + 12],
                strBytes[backFinalOffset + 11],
                strBytes[backFinalOffset + 10],
                strBytes[backFinalOffset + 9],
                strBytes[backFinalOffset + 8],
                strBytes[backFinalOffset + 7],
                strBytes[backFinalOffset + 6],
                strBytes[backFinalOffset + 5],
                strBytes[backFinalOffset + 4],
                strBytes[backFinalOffset + 3],
                strBytes[backFinalOffset + 2],
                strBytes[backFinalOffset + 1],
                strBytes[backFinalOffset + 0]
            );

            var res = System.Runtime.Intrinsics.X86.Avx2.CompareEqual(v1, v2);

            if (res != Vector256<ushort>.AllBitsSet)
            {
                return false;
            }
        }
        return true;
    }
}
