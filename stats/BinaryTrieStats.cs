using System.Net;
using ObjectLayoutInspector;


namespace Sibs.IPNetworks.Stats;

class Program
{
    static void Main(string[] args)
    {
        static List<(IPNetwork, IPAddress)> ParseFile(string fileName)
        {
            IPNetwork network;
            IPAddress route;

            string[] lines = File.ReadAllLines(fileName);
            List<(IPNetwork, IPAddress)> data = new List<(IPNetwork, IPAddress)>(lines.Length);
            foreach (string line in lines)
            {
                string[] ss = line.Split(' ');
                network = IPNetwork.Parse(ss[0]);
                route = IPAddress.Parse(ss[1]);
                data.Add((network, route));
            }

            return data;
        }

        static int AddNetworks(List<(IPNetwork, IPAddress)> lines, IPBinaryTrie<IPAddress> trie)
        {
            int count = 0;
            foreach ((IPNetwork network, IPAddress route) line in lines)
            {
                trie.AddOrUpdate(line.network, line.route);
            }

            return count;
        }

        var trie = new IPBinaryTrie<IPAddress>();
        TypeLayout typeLayout = TypeLayout.GetLayout(IPBinaryTrie<IPAddress>.GetNodeType());
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        List<(IPNetwork, IPAddress)> lines =ParseFile(@"..\tests\data\linx-rib.20141217.0000-p46.txt");
        int ipv4Count = lines.Count;
        sw.Start();
        AddNetworks(lines, trie);
        var ipv4timeElapsed = sw.ElapsedMilliseconds;

        lines = ParseFile(@"..\tests\data\linx-rib-ipv6.20141225.0000.p69.txt");
        int ipv6Count = lines.Count;
        sw.Restart();
        AddNetworks(lines, trie);
        var ipv6timeElapsed = sw.ElapsedMilliseconds;
        (int, int) result = trie.CountNodes();
        (int, int) NonLeafResult = trie.CountNonLeafNodes();

        Console.WriteLine($"Node full size: {typeLayout.FullSize}");
        Console.WriteLine($"Node size: {typeLayout.Size}");
        Console.WriteLine($"Node layout:\n {typeLayout.ToString()}");

        Console.WriteLine($"Load IPv4: time(ms)={ipv4timeElapsed}: networks={ipv4Count}: nodes={result.Item1}: nodes/ms={result.Item1 / ipv4timeElapsed}: nodes/network={result.Item1 / ipv4Count}");
        Console.WriteLine($"Load IPv6: time(ms)={ipv6timeElapsed}: networks={ipv6Count}: nodes={result.Item2}: nodes/ms={result.Item2 / ipv6timeElapsed}: nodes/network={result.Item2 / ipv6Count}");

        Console.WriteLine($"Consumed memory by nodes for IPv4 (MB): {result.Item1 * typeLayout.FullSize / 1024 / 1024}");
        Console.WriteLine($"Consumed memory by nodes for IPv6 (MB): {result.Item2 * typeLayout.FullSize / 1024 / 1024}");

        Console.WriteLine($"Non-leaf IPv4 nodes: {NonLeafResult.Item1} ({NonLeafResult.Item1 * 100 / result.Item1}%)");
        Console.WriteLine($"Non-leaf IPv6 nodes: {NonLeafResult.Item2} ({NonLeafResult.Item2 * 100 / result.Item2}%)");
    }
}
