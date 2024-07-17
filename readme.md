## Optimzing array data processing


```mermaid
flowchart TD
    A[LINQ] --> B[loops]
    B --> |exact equals/copy/parsing| C[Span + stackallock]
    B --> |Computation needed| D[Vectors + SIMD]
    C --> |Make code maintaineable| E[ref struct]
```
## Check string palindrome

![String-palindrome-results](./images/String-palindrome-results.png)

## Parse string of ints and sum them

![String-parse-results](./images/String-parse-results.png)

## Compare sensors output (two arrays of int16) and find count of cases where difference >= 10000

![ByteArrayCompareResults](./images/ByteArrayCompareResults.png)

## Parse and process JSON stream without disserializing 

![JsonProcessing](./images/JsonProcessing.png)
