using System.Collections.Generic;
using System.IO;
using System.Net;

using Xunit;

namespace Sibs.IPNetworks.Tests;

public class Leaf
{
    public required IPAddress ip;
}

public class IPv4
{
    public class RootNode
    {
        [Fact]
        public void LookupInEmptyTrie()
        {
            var t = new IPBinaryTrie<Leaf>();
            Leaf? l0 = t.Lookup(IPAddress.Parse("0.0.0.0"));
            Assert.Null(l0);

            Leaf? l4 = t.Lookup(IPAddress.Parse("4.0.0.0"));
            Assert.Null(l4);

            Leaf? l252 = t.Lookup(IPAddress.Parse("252.0.0.0"));
            Assert.Null(l252);
        }

        [Fact]
        public void SplitRootNodeInHalf()
        {
            // Follow networks split root node in half:
            // net/prefix  min addr    max addr
            // 0.0.0.0/1   0.0.0.0     127.255.255.255
            // 128.0.0.0/1 128.0.0.0   255.255.255.255

            var t = new IPBinaryTrie<Leaf>();

            // Add first half
            var leaf0 = IPAddress.Parse("1.1.1.0");
            t.AddOrUpdate(IPNetwork.Parse("0.0.0.0/1"), new Leaf() { ip = leaf0 });

            Leaf? l0 = t.Lookup(IPAddress.Parse("0.0.0.0"));
            Assert.NotNull(l0);
            Assert.Equal(leaf0, l0.ip);

            Leaf? l1 = t.Lookup(IPAddress.Parse("127.255.255.255"));
            Assert.NotNull(l1);
            Assert.Equal(leaf0, l1.ip);

            Leaf? l2 = t.Lookup(IPAddress.Parse("128.0.0.0"));
            Assert.Null(l2);

            Leaf? l3 = t.Lookup(IPAddress.Parse("255.255.255.255"));
            Assert.Null(l3);

            // Add second half
            var leaf1 = IPAddress.Parse("1.1.1.1");
            t.AddOrUpdate(IPNetwork.Parse("128.0.0.0/1"), new Leaf() { ip = leaf1 });

            l0 = t.Lookup(IPAddress.Parse("0.0.0.0"));
            Assert.NotNull(l0);
            Assert.Equal(leaf0, l0.ip);

            l1 = t.Lookup(IPAddress.Parse("127.255.255.255"));
            Assert.NotNull(l1);
            Assert.Equal(leaf0, l1.ip);

            l2 = t.Lookup(IPAddress.Parse("128.0.0.0"));
            Assert.NotNull(l2);
            Assert.Equal(leaf1, l2.ip);

            l3 = t.Lookup(IPAddress.Parse("255.255.255.255"));
            Assert.NotNull(l3);
            Assert.Equal(leaf1, l3.ip);
        }

        [Fact]
        public void RemoveNetworkFromRootNode()
        {
            // Remove first level node (0.0.0.0/1 network) in trie
            //   trieRoot -> 0.0.0.0/1
            var t = new IPBinaryTrie<Leaf>();
            var a0 = "0.0.0.0";
            Leaf? l0 = t.Lookup(a0);
            Assert.Null(l0);

            t.AddOrUpdate(IPNetwork.Parse("0.0.0.0/1"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });

            Leaf? l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            t.Remove(IPNetwork.Parse("0.0.0.0/1"));

            l1 = t.Lookup(a0);
            Assert.Null(l1);

            // trie has only root node
            Assert.Equal(1, t.CountNodes().IPv4NodeCount);
        }

        [Fact]
        public void RemoveNetworkFromRootNode_Transit()
        {
            // Remove transit node (0.0.0.0/1 network) in trie
            //   trieRoot -> 0.0.0.0/1 -> 0.0.0.0/2
            var t = new IPBinaryTrie<Leaf>();
            var a0 = "0.0.0.0";
            Leaf? l0 = t.Lookup(a0);
            Assert.Null(l0);

            t.AddOrUpdate(IPNetwork.Parse("0.0.0.0/1"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });
            t.AddOrUpdate(IPNetwork.Parse("0.0.0.0/2"), new Leaf() { ip = IPAddress.Parse("1.1.1.2") });

            Leaf? l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.2"), l1.ip);
            Assert.Equal(3, t.CountNodes().IPv4NodeCount);

            t.Remove(IPNetwork.Parse("0.0.0.0/1"));
            t.Remove(IPNetwork.Parse("0.0.0.0/1")); // Can remove a network that is not in the trie.

            l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.2"), l1.ip);
            Assert.Equal(3, t.CountNodes().IPv4NodeCount);

            t.Remove(IPNetwork.Parse("0.0.0.0/2"));

            Assert.Equal(1, t.CountNodes().IPv4NodeCount);
        }

        [Fact]
        public void RemoveNetworkFromRootNode_Transit2()
        {
            // Remove transit node (0.0.0.0/1 network) in trie
            //   trieRoot -> 0.0.0.0/1 -> 0.0.0.0/2
            //            -> 128.0.0.0/1
            var t = new IPBinaryTrie<Leaf>();
            var a0 = "0.0.0.0";
            Leaf? l0 = t.Lookup(a0);
            Assert.Null(l0);

            t.AddOrUpdate(IPNetwork.Parse("0.0.0.0/1"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });
            t.AddOrUpdate(IPNetwork.Parse("0.0.0.0/2"), new Leaf() { ip = IPAddress.Parse("1.1.1.2") });
            t.AddOrUpdate(IPNetwork.Parse("128.0.0.0/1"), new Leaf() { ip = IPAddress.Parse("1.1.1.3") });

            Leaf? l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.2"), l1.ip);

            Assert.Equal(4, t.CountNodes().IPv4NodeCount);

            t.Remove(IPNetwork.Parse("0.0.0.0/1"));

            l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.2"), l1.ip);

            l1 = t.Lookup("128.0.0.1");
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.3"), l1.ip);

            Assert.Equal(4, t.CountNodes().IPv4NodeCount);

            t.Remove(IPNetwork.Parse("0.0.0.0/2"));

            l1 = t.Lookup("128.0.0.1");
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.3"), l1.ip);

            Assert.Equal(2, t.CountNodes().IPv4NodeCount);
        }
    }

    public class AddOperations
    {
        [Fact]
        public void Add_Network_1_0_0_0_24()
        {
            // Check case 'netmask mod CHUNK_SIZE = 0', 24 mod 6 = 0
            var t = new IPBinaryTrie<Leaf>();
            var leaf0 = IPAddress.Parse("195.66.225.86");
            t.AddOrUpdate(IPNetwork.Parse("1.0.0.0/24"), new Leaf() { ip = leaf0 });

            // First address in the network.
            Leaf? l0 = t.Lookup("1.0.0.0");
            Assert.NotNull(l0);
            Assert.Equal(leaf0, l0.ip);

            Leaf? l0_1 = t.Lookup("1.0.0.1");
            Assert.NotNull(l0_1);
            Assert.Equal(leaf0, l0_1.ip);

            // Last address in the network.
            Leaf? l0_255 = t.Lookup("1.0.0.255");
            Assert.NotNull(l0_255);
            Assert.Equal(leaf0, l0_255.ip);

            // Next address.
            Leaf? l1_0 = t.Lookup("1.0.1.0");
            Assert.Null(l1_0);

            Assert.Equal(25, t.CountNodes().IPv4NodeCount);
        }

        [Fact]
        public void Add_Network_28_0_18_0_24()
        {
            var t = new IPBinaryTrie<Leaf>();

            var a0 = "28.0.18.3";
            Leaf? l0 = t.Lookup(a0);
            Assert.Null(l0);

            // Check neighboring networks do not affect each other.
            t.AddOrUpdate(IPNetwork.Parse("28.0.19.0/24"), new Leaf() { ip = IPAddress.Parse("1.1.1.19") });
            t.AddOrUpdate(IPNetwork.Parse("28.0.18.0/24"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });
            t.AddOrUpdate(IPNetwork.Parse("28.0.17.0/24"), new Leaf() { ip = IPAddress.Parse("1.1.1.17") });

            var a19_0 = "28.0.19.0";
            Leaf? l19_0 = t.Lookup(a19_0);
            Assert.NotNull(l19_0);
            Assert.Equal(IPAddress.Parse("1.1.1.19"), l19_0.ip);

            var a18_255 = "28.0.18.255";
            Leaf? l18_255 = t.Lookup(a18_255);
            Assert.NotNull(l18_255);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l18_255.ip);

            var a18_0 = "28.0.18.0";
            Leaf? l18_0 = t.Lookup(a18_0);
            Assert.NotNull(l18_0);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l18_0.ip);

            var a17_255 = "28.0.17.255";
            Leaf? l17_255 = t.Lookup(a17_255);
            Assert.NotNull(l17_255);
            Assert.Equal(IPAddress.Parse("1.1.1.17"), l17_255.ip);

            // Test leaf node update for 28.0.18.0
            t.AddOrUpdate(IPNetwork.Parse("28.0.18.0/24"), new Leaf() { ip = IPAddress.Parse("1.1.1.2") });
            Leaf? l18_update = t.Lookup(a18_0);
            Assert.NotNull(l18_update);
            Assert.Equal(IPAddress.Parse("1.1.1.2"), l18_update.ip);

            l19_0 = t.Lookup(a19_0);
            Assert.NotNull(l19_0);
            Assert.Equal(IPAddress.Parse("1.1.1.19"), l19_0.ip);

            l17_255 = t.Lookup(a17_255);
            Assert.NotNull(l17_255);
            Assert.Equal(IPAddress.Parse("1.1.1.17"), l17_255.ip);

            // Test leaf node update for 28.0.19.0
            t.AddOrUpdate(IPNetwork.Parse("28.0.19.0/24"), new Leaf() { ip = IPAddress.Parse("1.1.1.191") });
            l18_update = t.Lookup(a18_0);
            Assert.NotNull(l18_update);
            Assert.Equal(IPAddress.Parse("1.1.1.2"), l18_update.ip);

            l19_0 = t.Lookup(a19_0);
            Assert.NotNull(l19_0);
            Assert.Equal(IPAddress.Parse("1.1.1.191"), l19_0.ip);

            l17_255 = t.Lookup(a17_255);
            Assert.NotNull(l17_255);
            Assert.Equal(IPAddress.Parse("1.1.1.17"), l17_255.ip);

            // Test leaf node update for 28.0.17.0
            t.AddOrUpdate(IPNetwork.Parse("28.0.17.0/24"), new Leaf() { ip = IPAddress.Parse("1.1.1.171") });
            l18_update = t.Lookup(a18_0);
            Assert.NotNull(l18_update);
            Assert.Equal(IPAddress.Parse("1.1.1.2"), l18_update.ip);

            l19_0 = t.Lookup(a19_0);
            Assert.NotNull(l19_0);
            Assert.Equal(IPAddress.Parse("1.1.1.191"), l19_0.ip);

            l17_255 = t.Lookup(a17_255);
            Assert.NotNull(l17_255);
            Assert.Equal(IPAddress.Parse("1.1.1.171"), l17_255.ip);
        }

        [Fact]
        public void Add_Network_28_0_18_3_32()
        {
            // Check network of one ip address.
            var t = new IPBinaryTrie<Leaf>();

            Leaf? l0 = t.Lookup("28.0.18.3");
            Assert.Null(l0);

            t.AddOrUpdate(IPNetwork.Parse("28.0.18.3/32"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });

            Leaf? l1 = t.Lookup("28.0.18.3");
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            l1 = t.Lookup("28.0.18.2");
            Assert.Null(l1);

            l1 = t.Lookup("28.0.18.4");
            Assert.Null(l1);
        }

        [Fact]
        public void Add_Network_255_255_255_255_32()
        {
            // Check max network of one ip address.
            var t = new IPBinaryTrie<Leaf>();

            Leaf? l0 = t.Lookup("255.255.255.255");
            Assert.Null(l0);

            t.AddOrUpdate(IPNetwork.Parse("255.255.255.255/32"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });

            Leaf? l1 = t.Lookup("255.255.255.255");
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            Leaf? l2 = t.Lookup("255.255.255.254");
            Assert.Null(l2);
        }

        [Fact]
        public void Add_Network_0_0_0_0_32()
        {
            // Check min network of one ip address.
            var t = new IPBinaryTrie<Leaf>();
            Leaf? l0 = t.Lookup("0.0.0.0");
            Assert.Null(l0);

            t.AddOrUpdate(IPNetwork.Parse("0.0.0.0/32"), new Leaf() { ip = IPAddress.Parse("1.1.1.0") });

            l0 = t.Lookup("0.0.0.0");
            Assert.NotNull(l0);
            Assert.Equal(IPAddress.Parse("1.1.1.0"), l0.ip);

            t.AddOrUpdate(IPNetwork.Parse("0.0.0.1/32"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });

            l0 = t.Lookup("0.0.0.0");
            Assert.NotNull(l0);
            Assert.Equal(IPAddress.Parse("1.1.1.0"), l0.ip);

            Leaf? l1 = t.Lookup("0.0.0.1");
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            l1 = t.Lookup("0.0.0.2");
            Assert.Null(l1);
        }

        [Fact]
        public void Add_Network_1_1_0_0_16()
        {
            var t = new IPBinaryTrie<Leaf>();
            var a0 = "1.1.2.0";
            Leaf? l0 = t.Lookup(a0);
            Assert.Null(l0);

            t.AddOrUpdate(IPNetwork.Parse("1.1.0.0/16"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });

            Leaf? l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            t.AddOrUpdate(IPNetwork.Parse("1.1.1.0/24"), new Leaf() { ip = IPAddress.Parse("1.1.1.2") });

            Leaf? l2 = t.Lookup("1.1.0.255");
            Assert.NotNull(l2);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l2.ip);

            Leaf? l3 = t.Lookup("1.1.1.0");
            Assert.NotNull(l3);
            Assert.Equal(IPAddress.Parse("1.1.1.2"), l3.ip);

            l3 = t.Lookup("1.1.1.255");
            Assert.NotNull(l3);
            Assert.Equal(IPAddress.Parse("1.1.1.2"), l3.ip);

            l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);
        }

        [Fact]
        public void Add_Network_1_0_0_0_14()
        {
            var t = new IPBinaryTrie<Leaf>();
            var a0 = "1.0.0.0";
            Leaf? l0 = t.Lookup(a0);
            Assert.Null(l0);

            t.AddOrUpdate(IPNetwork.Parse("1.0.0.0/14"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });

            Leaf? l1 = t.Lookup("1.0.0.0");
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            Leaf? l2 = t.Lookup("1.3.255.255");
            Assert.NotNull(l2);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l2.ip);

            l2 = t.Lookup("1.4.0.0");
            Assert.Null(l2);
        }
    }

    public class RemoveOperation
    {
        [Fact]
        public void Remove_Network_0_0_0_0_32_Last()
        {
            // Remove last level node in trie.
            var t = new IPBinaryTrie<Leaf>();
            var a0 = "0.0.0.0";
            Leaf? l0 = t.Lookup(a0);
            Assert.Null(l0);

            t.AddOrUpdate(IPNetwork.Parse("0.0.0.0/32"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });

            Leaf? l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            t.Remove(IPNetwork.Parse("0.0.0.0/32"));

            l1 = t.Lookup(a0);
            Assert.Null(l1);
            Assert.Equal(1, t.CountNodes().IPv4NodeCount);

            // Can "remove" an undiscovered network.
            t.Remove(IPNetwork.Parse("0.0.0.0/32"));

            l1 = t.Lookup(a0);
            Assert.Null(l1);
            Assert.Equal(1, t.CountNodes().IPv4NodeCount);
        }

        [Fact]
        public void Remove_Undiscovered_Network()
        {
            // Remove last level node in trie.
            var t = new IPBinaryTrie<Leaf>();
            var a0 = "0.0.0.0";
            Leaf? l0 = t.Lookup(a0);
            Assert.Null(l0);

            t.Remove(IPNetwork.Parse("0.0.0.0/32"));

            t.AddOrUpdate(IPNetwork.Parse("0.0.0.1/32"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });

            t.Remove(IPNetwork.Parse("0.0.0.0/32"));

            Assert.Equal(33, t.CountNodes().IPv4NodeCount);
        }

        [Fact]
        public void Remove_Network_0_0_0_0_32_Last_Transit()
        {
            // Remove last level node in trie
            //   trieRoot -> 0.0.0.0/31 -> 0.0.0.0/32
            var t = new IPBinaryTrie<Leaf>();
            var a0 = "0.0.0.0";
            Leaf? l0 = t.Lookup(a0);
            Assert.Null(l0);

            t.AddOrUpdate(IPNetwork.Parse("0.0.0.0/32"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });
            t.AddOrUpdate(IPNetwork.Parse("0.0.0.0/31"), new Leaf() { ip = IPAddress.Parse("1.1.1.2") });

            Leaf? l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            t.Remove(IPNetwork.Parse("0.0.0.0/32"));

            l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.2"), l1.ip);
            Assert.Equal(32, t.CountNodes().IPv4NodeCount);
        }

        [Fact]
        public void Remove_Network_1_1_0_0_16()
        {
            var t = new IPBinaryTrie<Leaf>();
            var a0 = "1.1.2.10";
            Leaf? l0 = t.Lookup(a0);
            Assert.Null(l0);

            t.AddOrUpdate(IPNetwork.Parse("1.1.0.0/16"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });

            Leaf? l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            t.AddOrUpdate(IPNetwork.Parse("1.1.1.0/24"), new Leaf() { ip = IPAddress.Parse("1.1.1.2") });

            Leaf? l2 = t.Lookup("1.1.1.0");
            Assert.NotNull(l2);
            Assert.Equal(IPAddress.Parse("1.1.1.2"), l2.ip);

            l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            t.Remove(IPNetwork.Parse("1.1.1.0/24"));

            l1 = t.Lookup(a0);
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            // Test Lookup(ReadOnlySpan<byte> addressBuffer)
            l1 = t.Lookup(IPAddress.Parse(a0).GetAddressBytes());
            Assert.NotNull(l1);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l1.ip);

            l2 = t.Lookup("1.1.1.0");
            Assert.NotNull(l2);
            Assert.Equal(IPAddress.Parse("1.1.1.1"), l2.ip);

            Assert.Equal(17, t.CountNodes().IPv4NodeCount);
        }
    }
}


public class IPv6
{
    [Fact]
    public void Add_Network()
    {
        var t = new IPBinaryTrie<Leaf>();
        //t.AddOrUpdate(IPNetwork.Parse("fe80::0/64"), new Leaf() { ip = IPAddress.Parse("fe80::69d:7ec2:cc4b:87ea") });
        var leaf = IPAddress.Parse("2001:7f8:4::1a0b:1");
        t.AddOrUpdate(IPNetwork.Parse("2600:2004::/32"), new Leaf() { ip = leaf });

        Leaf? l0 = t.Lookup("2600:2004:4::1a0b:1");
        Assert.NotNull(l0);
        Assert.Equal(leaf, l0.ip);

        Leaf? l1 = t.Lookup("2600:2004:0000:0000:0000:0000:0000:0000");
        Assert.NotNull(l1);
        Assert.Equal(leaf, l1.ip);

        // Test Lookup(ReadOnlySpan<byte> addressBuffer)
        Leaf? l1b = t.Lookup(IPAddress.Parse("2600:2004:0000:0000:0000:0000:0000:0000").GetAddressBytes());
        Assert.NotNull(l1b);
        Assert.Equal(leaf, l1b.ip);

        Leaf? l255 = t.Lookup("2600:2004:ffff:ffff:ffff:ffff:ffff:ffff");
        Assert.NotNull(l255);
        Assert.Equal(leaf, l255.ip);
    }

    [Fact]
    public void Remove_Undiscovered_Network()
    {
        var t = new IPBinaryTrie<Leaf>();
        var a0 = "0.0.0.0";
        Leaf? l0 = t.Lookup(a0);
        Assert.Null(l0);

        var network = IPNetwork.Parse("2a00:86c0:1009::/48");
        t.Remove(network);

        t.AddOrUpdate(IPNetwork.Parse("2607:f750:5000::/40"), new Leaf() { ip = IPAddress.Parse("1.1.1.1") });

        t.Remove(network);

        Assert.Equal(1, t.CountNodes().IPv4NodeCount);
    }
}
