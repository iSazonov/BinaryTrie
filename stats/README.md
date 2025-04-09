# Statistics

## Run statistics

```cmd
dotnet run
```

### Statistics output

```text
Node full size: 48
Node size: 32
Node layout:
 Type layout for 'Node`1'
Size: 32 bytes. Paddings: 7 bytes (%21 of empty space)
|=====================================|
| Object Header (8 bytes)             |
|-------------------------------------|
| Method Table Ptr (8 bytes)          |
|=====================================|
|   0-7: Node`1 Branch0 (8 bytes)     |
|-------------------------------------|
|  8-15: Node`1 Branch1 (8 bytes)     |
|-------------------------------------|
| 16-23: IPAddress NodeLeaf (8 bytes) |
|-------------------------------------|
|    24: Boolean IsLeaf (1 byte)      |
|-------------------------------------|
| 25-31: padding (7 bytes)            |
|=====================================|


Load IPv4: time(ms)=284: networks=518231: nodes=1211746: nodes/ms=4266: nodes/network=2
Load IPv6: time(ms)=43: networks=20440: nodes=128316: nodes/ms=2984: nodes/network=6
Consumed memory by nodes for IPv4 (MB): 55
Consumed memory by nodes for IPv6 (MB): 5
Non-leaf IPv4 nodes: 693515 (57%)
Non-leaf IPv6 nodes: 107876 (84%)
```
