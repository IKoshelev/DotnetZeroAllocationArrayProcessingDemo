## Optimzing array data processing


```mermaid
flowchart TD
    A[LINQ] --> B[loops]
    B --> |exact equals/copy/parsing| C[Span + stackallock]
    B --> |Computation needed| D[Vectors + SIMD]
    C --> |Make code maintaineable| E[ref struct]
```

![String-palindrome-results](./images/String-palindrome-results.png)

![String-parse-results](./images/String-parse-results.png)

![ByteArrayCompareResults](./images/ByteArrayCompareResults.png)

![JsonProcessing](./images/JsonProcessing.png)