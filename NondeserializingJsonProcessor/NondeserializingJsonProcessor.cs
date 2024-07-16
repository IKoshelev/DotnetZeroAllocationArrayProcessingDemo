
using Object.Extensions.ScopeFunction;
using System.Text;
using System.Text.Json;

namespace JsonNondeserializingProcessor;

public struct PathSegment
{
    public JsonTokenType TokenType;
    public byte EncodedPropName;

    public PathSegment(
        byte propName,
        JsonTokenType tokenType)
    {
        TokenType = tokenType;
        EncodedPropName = propName;
    }

    public PathSegment(
        JsonTokenType tokenType)
    {
        TokenType = tokenType;
        EncodedPropName = 1;
    }
}

public delegate void JsonProcessingAction<T>(
         in Utf8JsonReader reader,
         in ReadOnlySpan<byte> propName,
         ref Span<PathSegment> path,
         ref T runningResult);

public record struct JsonProcessingRuleDefinition<T>(
        string[] path,
        ReadOnlyMemory<byte>? propName,
        JsonTokenType? tokenType,
        JsonProcessingAction<T> action);

internal record struct JsonProcessingRulePrepared<T>(
    int depth,
    PathSegment[] path,
    byte? propName,
    JsonTokenType? tokenType,
    JsonProcessingAction<T> action);

public class Utf8StringEncoder
{
    public const byte None = 0;
    public const byte Any = 1;
    public List<byte[]> FromByte = new(256);

    public void Record(ReadOnlyMemory<byte> str)
    {
        if (Encode(str.Span) > 1)
        {
            return;
        }

        if (FromByte.Count == 253)
        {
            throw new Exception("Can't work with more than 254 strings.");
        }

        FromByte.Add(str.ToArray());
    }

    public byte Encode(ReadOnlySpan<byte> str)
    {
        // 0 and 1 are reserved
        for (int i = 0;i < FromByte.Count; i++)
        {
            if (FromByte[i].AsSpan().SequenceEqual(str))
            {
                return (byte)(i + 2);
            }
        }
        return 1;
    }

    public static byte[] NoneStr = "None"u8.ToArray();
    public static byte[] AnyStr = "*"u8.ToArray();

    public ReadOnlyMemory<byte> Decode(byte value)
    {
        if (value == 0)
        {
            return NoneStr;
        } 
        else if (value == 1)
        {
            return AnyStr;
        }

        return FromByte[value - 2];
    }
}

public class NondeserializingJsonProcessor<T>
{
    private readonly JsonProcessingRulePrepared<T>[] JsonActions;

    private static bool KeyEquals(
        JsonProcessingRulePrepared<T> key,
        in Utf8JsonReader reader,
        in byte propName,
        in ReadOnlySpan<PathSegment> path)
    {
        return reader.CurrentDepth == key.depth
                && path.Slice(0, key.depth).SequenceEqual(key.path)
                && (key.propName.HasValue == false
                    || key.propName == propName)
                && (key.tokenType.HasValue == false 
                   || reader.TokenType == key.tokenType);
    }

    T AggregatedState = default;

    public readonly Utf8StringEncoder PropNameEncode = new();

    public NondeserializingJsonProcessor(
        T startingAggregatedState,
        JsonProcessingRuleDefinition<T>[] ruleDefintions)
    {
        AggregatedState = startingAggregatedState;

        RecordAllStringsOfInterest(ruleDefintions);

        JsonActions = PrepareRuleDefinitions(ruleDefintions);
    }

    private void RecordAllStringsOfInterest(
        JsonProcessingRuleDefinition<T>[] ruleDefintions)
    {
        foreach (var action in ruleDefintions)
        {
            if (action.propName.HasValue)
            {
                PropNameEncode.Record(action.propName.Value);
            }

            foreach (var pathSegment in action.path)
            {
                if (pathSegment is "{" or "[")
                {
                    continue;
                }
                PropNameEncode.Record(Encoding.UTF8.GetBytes(pathSegment));
            }
        }
    }

    private JsonProcessingRulePrepared<T>[] PrepareRuleDefinitions(
            JsonProcessingRuleDefinition<T>[] ruleDefintions)
    {
        return ruleDefintions.Select(a =>
        {
            var path = new List<PathSegment>();
            var enumerator = a.path.AsEnumerable<string>().GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current is "{")
                {
                    path.Add(new PathSegment(JsonTokenType.StartObject));
                }
                else if (enumerator.Current is "[")
                {
                    path.Add(new PathSegment(JsonTokenType.StartArray));
                }
                else
                {
                    var propName = Encoding.UTF8.GetBytes(enumerator.Current);
                    var encodedName = PropNameEncode.Encode(propName);
                    enumerator.MoveNext();
                    JsonTokenType tokenType = enumerator.Current is "{"
                        ? JsonTokenType.StartObject
                        : JsonTokenType.StartArray;
                    path.Add(new PathSegment(encodedName, tokenType));
                }
            }

            return new JsonProcessingRulePrepared<T>(
                path.Count,
                path.ToArray(),
                a.propName?.Map( x => (byte?)PropNameEncode.Encode(a.propName.Value.Span)),
                a.tokenType,
                a.action);
        })
        .ToArray();
    }

    public T Process(in ReadOnlySpan<byte> jsonUtf8)
    {
        Span<PathSegment> currentPathSegmentsStack = stackalloc PathSegment[16];
        int nextPathSegmentIndex = 0;

        static void pushPathSegmentsStack(
            ref Span<PathSegment> pathSegmentsStack, 
            ref int nextPathSegmentIndex,
            PathSegment s)
        {
            //Console.WriteLine($"Push {nextSegment}");
            pathSegmentsStack[nextPathSegmentIndex] = s;
            nextPathSegmentIndex++;
        }

        static void popPathSegmentsStack(
            ref Span<PathSegment> pathSegmentsStack,
            ref int nextPathSegmentIndex)
        {
            nextPathSegmentIndex--;
            //Console.WriteLine($"Pop {nextSegment}");
            pathSegmentsStack[nextPathSegmentIndex] = default;
        }

        var options = new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };
        var reader = new Utf8JsonReader(jsonUtf8, options);

        while (reader.Read())
        {
            //Console.WriteLine($"{reader.TokenType}: {reader.CurrentDepth}, {Encoding.UTF8.GetString(reader.ValueSpan)}");

            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject
                     or JsonTokenType.StartArray:
                    {
                        pushPathSegmentsStack(
                                ref currentPathSegmentsStack,
                                ref nextPathSegmentIndex,
                                new PathSegment(Utf8StringEncoder.Any, reader.TokenType));
                        break;
                    }

                case JsonTokenType.EndObject
                     or JsonTokenType.EndArray:
                    {
                        popPathSegmentsStack(
                                ref currentPathSegmentsStack,
                                ref nextPathSegmentIndex);
                        break;
                    }

                case JsonTokenType.PropertyName:
                    {
                        var propNameRaw = reader.ValueSpan;
                        var propName = PropNameEncode.Encode(propNameRaw);
                        reader.Read(); // todo check bool?

                        //if (propName == PropName.Model)
                        //{
                        //    Console.WriteLine(Encoding.UTF8.GetString(reader.ValueSpan));
                        //}

                        if (reader.TokenType is JsonTokenType.StartObject
                                                or JsonTokenType.StartArray)
                        {
                            pushPathSegmentsStack(
                                ref currentPathSegmentsStack,
                                ref nextPathSegmentIndex,
                                new PathSegment(propName, reader.TokenType));
                        }
                        else
                        {
                            foreach (var action in JsonActions)
                            {
                                if (KeyEquals(
                                        action, 
                                        ref reader,
                                        propName,
                                        currentPathSegmentsStack))
                                {
                                    action.action(
                                        reader, 
                                        propNameRaw,  
                                        ref currentPathSegmentsStack, 
                                        ref AggregatedState);
                                }
                            }

                        }
                    break;
                }
            }
        }

        return AggregatedState;
    }
}