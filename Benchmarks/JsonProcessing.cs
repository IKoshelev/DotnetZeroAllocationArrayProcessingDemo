using BenchmarkDotNet.Attributes;
using System.Text.Json;
using JsonNondeserializingProcessor;

namespace DotnetZeroAllocationArrayProcessingDemo.Benchmarks;

[MemoryDiagnoser]
public class JsonProcessing
{
    public byte[] jsonUtf8;
    public long checksum;

    [IterationSetup]
    public void GlobalSetup()
    {
        var subj = Generator.Generate(100000);
        jsonUtf8 = JsonSerializer.SerializeToUtf8Bytes(subj);
        checksum = subj.Telemetry.Where(x => x.Model == "TX").SelectMany(x => x.Telemetry).Select(x => (long)x.Value).Sum();
    }

    [Benchmark]
    public bool DeserializeAndLINQ()
    {
        var reader = new Utf8JsonReader(jsonUtf8);
        var subj = JsonSerializer.Deserialize<JsonDeserializationSubject>(ref reader);
        var res = subj.Telemetry.Where(x => x.Model == "TX").SelectMany(x => x.Telemetry).Select(x => (long)x.Value).Sum();
        return res == checksum;
    }

    [Benchmark]
    public bool StreamProcessor()
    {

        var p = new NondeserializingJsonProcessor<long>(
            startingAggregatedState: 0, 
            ruleDefintions: [
                new (
                    ["{", "Telemetry", "[", "{"],
                    "Model"u8.ToArray(),
                    JsonTokenType.String,
                    delegate (
                        in Utf8JsonReader reader,
                        in ReadOnlySpan<byte> propName,
                        ref Span<PathSegment> path,
                        ref long runningResult)
                    {
                        if (false == reader.ValueSpan.SequenceEqual("TX"u8))
                        {
                            // object is Telemetry, but model is not relevant, replace StartObject with None 
                            path[2].TokenType = JsonTokenType.None;
                        }
                    }
                ),

                new (
                    ["{", "Telemetry", "[", "{", "Telemetry", "[", "{"],
                    "Value"u8.ToArray(),
                    JsonTokenType.Number,
                    delegate (
                        in Utf8JsonReader reader,
                        in ReadOnlySpan<byte> propName,
                        ref Span<PathSegment> path,
                        ref long runningResult)
                    {
                        runningResult += reader.GetInt32();
                    }
                ),

                //new(
                //    ["{", "Telemetry", "[", "{", "Telemetry", "[", "{"],
                //    null,
                //    null,
                //    delegate (
                //        in Utf8JsonReader reader,
                //        in ReadOnlySpan<byte> propName,
                //        ref Span<PathSegment> path,
                //        ref long runningResult)
                //    {
                //        Console.WriteLine(System.Text.Encoding.UTF8.GetString(propName));
                //    }
                //)

        ]);

        var res = p.Process(jsonUtf8);
        return res == checksum;
    }
}
