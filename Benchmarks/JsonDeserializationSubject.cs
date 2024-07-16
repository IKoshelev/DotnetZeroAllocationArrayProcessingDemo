namespace DotnetZeroAllocationArrayProcessingDemo.Benchmarks;

public class JsonDeserializationSubject
{
    public DateTime Timestamp { get; set; }
    public TelemetryPerTruck[] Telemetry { get; set; }
    public string Comment { get; set; }
}

public class TelemetryPerTruck
{
    public string Model { get; set; }

    public TelemetryItem[] Telemetry { get; set; }
}

public class TelemetryItem
{ 
    public DateTime Timestamp { get; set; }
    public string Comment { get; set; }
    public int Value { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public static class Generator
{
    public static Random rng = new Random();

    public static long count = 1;

    public static JsonDeserializationSubject Generate(int amountToGenerate)
    {
        return new JsonDeserializationSubject()
        {
            Timestamp = DateTime.Now,
            Telemetry =
                Enumerable
                    .Range(0, amountToGenerate)
                    .Select(_ => GenerateRandomTelemtry())
                    .ToArray(),
            Comment = $"Random global comment {count++}"
        };
    }

    private static TelemetryPerTruck GenerateRandomTelemtry()
    {
        return new TelemetryPerTruck()
        {
            Model = GetModel(),
            Telemetry = Enumerable
                .Range(0, 10)
                .Select(_ => GenerateRandomTelemtryItem())
                .ToArray()
        };
    }

    private static TelemetryItem GenerateRandomTelemtryItem()
    {
        return new TelemetryItem()
        {
            Timestamp = DateTime.Now.AddMilliseconds(-rng.Next()),
            Value = rng.Next(),
            Latitude = rng.NextDouble(),
            Longitude = rng.NextDouble(),
            Comment = $"Random TelemetryItem comment {count++}"
        };
    }

    public static string GetModel() => (rng.Next() % 3) switch
    {
        0 => "TX",
        1 => "GX",
        2 => "HX",
        _ => "PX"
    };
    
}