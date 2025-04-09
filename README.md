# BinaryTrie IP Lookup (multi-thread)

C# binary trie implementation is designed to search for an information (`TLeaf`) associated with IP network. Notice, it is not for routing but for applications, with balanced lookup/add/remove operations performance in mind. The implementation is thread-safe, `Lookup()` is lock-free (many threads can look up concurrently), `AddOrUpdate()` and `Remove()` use a lock (only one thread can modify the `IPBinaryTrie<TLeaf>` in the same time).

## Usage

See sources in `tests` folder.

## Build

```cmd
cd .\src
dotnet build
```

## Build package

```cmd
cd .\src
dotnet pack
```

## Run xUnit tests

```cmd
cd .\tests
dotnet test
```

## Run performance tests

```cmd
cd .\perf
dotnet run -c Release -f net8.0 --  -r net8.0 net9.0 --iterationCount 32 -f *
```

## Run statistics

```cmd
cd .\stats
dotnet run
```
