using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;

namespace DotnetZeroAllocationArrayProcessingDemo.Benchmarks;

[MemoryDiagnoser]
public class ByteArrayCompareWithThresholdBenchmark
{
    public Int16[] subjectA;
    public Int16[] subjectB;
    public UInt32 checksum;

    [IterationSetup]
    public void GlobalSetup()
    {
        var rnd = RandomNumberGenerator.GetBytes(2 * 1024 * 1024);
        subjectA = MemoryMarshal.Cast<Byte, Int16>(rnd).ToArray();
        rnd = RandomNumberGenerator.GetBytes(2 * 1024 * 1024);
        subjectB = MemoryMarshal.Cast<Byte, Int16>(rnd).ToArray();

        checksum = ScalarLoopTest(subjectA,subjectB);
    }

    [Benchmark]
    public bool ScalarLoop()
    {
        var res = ScalarLoopTest(subjectA, subjectB);
        return res == checksum;
    }

    public static UInt32 ScalarLoopTest(Int16[] arrA, Int16[] arrB)
    {
        UInt32 result = 0;

        for (int index = 0; index < arrA.Length; index++)
        {
            var a = arrA[index];
            var b = arrB[index];
            if (b > a)
            {
                (b, a) = (a, b);
            }
            result += ((a - b) >= 10000) ? (UInt32)1 : 0;
        }

        return result;
    }

    [Benchmark]
    public bool SimdLoop()
    {
        var res = SimdLoopTest(subjectA, subjectB);
        return res == checksum;
    }

    public static Int32 SimdLoopTest(ReadOnlySpan<Int16> arrA, ReadOnlySpan<Int16> arrB)
    {
        Int16 ths = 9999; // threshold
        ReadOnlySpan<Int16> threshold = stackalloc Int16[]
        {
            ths, ths, ths, ths, ths, ths, ths, ths,
            ths, ths, ths, ths, ths, ths, ths, ths
        };
        var thresholdVector = Vector256.Create(threshold);

        var result = 0;
        var i = 0;
        for (i = 0; i <= arrA.Length - 16; i += 16)
        {
            var v256a = Vector256.Create(arrA.Slice(i, 16));
            var v256b = Vector256.Create(arrB.Slice(i, 16));

            var subtracted = Avx2.SubtractSaturate(Avx2.Max(v256a, v256b), Avx2.Min(v256a, v256b));

            var thresholdPassed = Avx2.CompareGreaterThan(subtracted, thresholdVector);
            var sum = 0 - thresholdPassed[0] 
                        - thresholdPassed[1] 
                        - thresholdPassed[2] 
                        - thresholdPassed[3] 
                        - thresholdPassed[4]
                        - thresholdPassed[5]
                        - thresholdPassed[6]
                        - thresholdPassed[7]
                        - thresholdPassed[8]
                        - thresholdPassed[9]
                        - thresholdPassed[10]
                        - thresholdPassed[11]
                        - thresholdPassed[12]
                        - thresholdPassed[13]
                        - thresholdPassed[14]
                        - thresholdPassed[15];

            result += sum;

            //    var rtemp = Intrinsics.Arm.AdvSimd.AddPairwiseWidening(normalized);
            //    result = Intrinsics.Arm.AdvSimd.AddPairwiseWideningAndAdd(result, rtemp);

        }

        return result;

        //return -1 * (int)(Intrinsics.Arm.AdvSimd.Extract(result, 0)
        //                + Intrinsics.Arm.AdvSimd.Extract(result, 1)
        //                + Intrinsics.Arm.AdvSimd.Extract(result, 2)
        //                + Intrinsics.Arm.AdvSimd.Extract(result, 3));
    }
}
