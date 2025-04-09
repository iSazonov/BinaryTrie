using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Sibs.IPNetworks;

/// <summary>
/// Represent a data structure designed to search for information associated with IP networks.
/// </summary>
/// <typeparam name="TLeaf"> specifies the type of information associated with IP networks.</typeparam>
/// <remarks>
/// Examples of information associated with IP networks are an ip route, session, geographic or post address information, etc.
/// </remarks>
public sealed class IPBinaryTrie<TLeaf>
{
    private class Node<TNodeLeaf>
    {
        public Node<TNodeLeaf>? Branch0;
        public Node<TNodeLeaf>? Branch1;
        public bool IsLeaf;
        public TNodeLeaf? NodeLeaf;
    }

    const int IPv4AddressBytes = 4;
    const int IPv6AddressBytes = 16;

    // The type was introduced in .Net 9.0 but perf tests should work in .Net 8.0.
    ////private readonly System.Threading.Lock _lock = new();
    private readonly object _lock = new();

    private Node<TLeaf> _rootIPv4;
    private Node<TLeaf> _rootIPv6;
    private TLeaf? _defaultResult;

    /// <summary>
    /// Initializes a new instance of the <see cref="IPBinaryTrie{TLeaf}"/>.
    /// </summary>
    public IPBinaryTrie() : this(defaultResult: default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IPBinaryTrie{TLeaf}"/>.
    /// </summary>
    /// <param name="defaultResult">The default value returned if IP address <see cref="IPAddress"/> is not found.</param>
    public IPBinaryTrie(TLeaf? defaultResult)
    {
        _rootIPv4 = new Node<TLeaf>();
        _rootIPv6 = new Node<TLeaf>();
        _defaultResult = defaultResult;
    }

    /// <summary>
    /// Search and return an information <see typeparamref="TLeaf"/> associated with a network if <paramref name="address"/> is in the network.
    /// </summary>
    /// <param name="address">A string that contains an IP address in dotted-quad notation for IPv4 and in colon-hexadecimal notation for IPv6.</param>
    /// <returns><see typeparamref="TLeaf"/> if found, otherwise null.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="address"/> is null.</exception>
    /// <exception cref="FormatException">If <paramref name="address"/> is not a valid IP address.</exception>
    public TLeaf? Lookup(string address)
    {
        IPAddress ip = IPAddress.Parse(address);

        return Lookup(ip);
    }

    /// <summary>
    /// Search and return an information <see typeparamref="TLeaf"/> associated with a network if <paramref name="address"/> is in the network.
    /// </summary>
    /// <param name="address">An IP address <see cref="IPAddress"/>.</param>
    /// <returns><see typeparamref="TLeaf"/> if found, otherwise null.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="address"/> is null.</exception>
    public TLeaf? Lookup(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        Span<byte> addressBuffer = stackalloc byte[IPv6AddressBytes];
        address.TryWriteBytes(addressBuffer, out int bytesWritten);
        addressBuffer = addressBuffer.Slice(0, bytesWritten);

        Node<TLeaf> trieRoot = address.AddressFamily == AddressFamily.InterNetwork ? _rootIPv4 : _rootIPv6;

        TLeaf? result = LookupCore(addressBuffer, trieRoot, _defaultResult);
        return result;
    }

    /// <summary>
    /// Search and return an information <see typeparamref="TLeaf"/> associated with a network if <paramref name="addressBuffer"/> is in the network.
    /// </summary>
    /// <param name="addressBuffer">An IP address in network (BigEndian) order.</param>
    /// <returns><see typeparamref="TLeaf"/> if found, otherwise null.</returns>
    /// <exception cref="ArgumentException">If <paramref name="addressBuffer"/> does not have a length of 4 for IPv4 or 16 for IPv6.</exception>
    public TLeaf? Lookup(ReadOnlySpan<byte> addressBuffer)
    {
        IPBinaryTrie<TLeaf>.ThrowIfNotIP(addressBuffer);

        return LookupCore(addressBuffer, addressBuffer.Length == IPv4AddressBytes ? _rootIPv4 : _rootIPv6, _defaultResult);
    }

    private static TLeaf? LookupCore(ReadOnlySpan<byte> addressBuffer, Node<TLeaf> root, TLeaf? defaultResult)
    {
        Node<TLeaf> currentNode = root;
        TLeaf? result = defaultResult;
        int currentBit = 0;
        for (int i = 0; i < addressBuffer.Length; i++)
        {
            for (int j = 7; j >= 0; j--)
            {
                currentBit++;
                int bit = (addressBuffer[i] >> j) & 1;
                Node<TLeaf>? branch = bit == 0 ? currentNode.Branch0 : currentNode.Branch1;
                if (branch is null)
                {
                    return result;
                }

                currentNode = branch;
                if (currentNode.IsLeaf)
                {
                    result = currentNode.NodeLeaf;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Add or update the associated information <paramref name="leaf"/> for the <paramref name="network"/>.
    /// </summary>
    /// <param name="network">A network <see cref="IPNetwork"/> to add or update.</param>
    /// <param name="leaf">An associated information with the <paramref name="network"/>.</param>
    public void AddOrUpdate(IPNetwork network, TLeaf leaf)
    {
        Span<byte> addressBuffer = stackalloc byte[IPv6AddressBytes];
        network.BaseAddress.TryWriteBytes(addressBuffer, out int bytesWritten);
        addressBuffer = addressBuffer.Slice(0, bytesWritten);

        lock (_lock)
        {
            Node<TLeaf> trieRoot = network.BaseAddress.AddressFamily == AddressFamily.InterNetwork ? _rootIPv4 : _rootIPv6;
            AddOrUpdateCore(addressBuffer, network.PrefixLength, trieRoot, leaf);
        }
    }

    private static void AddOrUpdateCore(ReadOnlySpan<byte> addressBuffer, int prefixLength, Node<TLeaf> root, TLeaf leaf)
    {
        Node<TLeaf> currentNode = root;
        int currentBit = 0;
        for (int i = 0; i < addressBuffer.Length; i++)
        {
            for (int j = 7; j >= 0; j--)
            {
                int bit = (addressBuffer[i] >> j) & 1;
                ref Node<TLeaf>? branch = ref (bit == 0 ? ref currentNode.Branch0 : ref currentNode.Branch1);
                branch ??= new Node<TLeaf>();
                currentNode = branch;

                if (++currentBit == prefixLength)
                {
                    currentNode.NodeLeaf = leaf;
                    currentNode.IsLeaf = true;
                    return;
                }
            }
        }

        Debug.Assert(false, "Never should be here.", nameof(currentNode));
    }

    /// <summary>
    /// Remove the <paramref name="network"/> with associated information.
    /// </summary>
    /// <param name="network">A network <see cref="IPNetwork"/> to remove.</param>
    public void Remove(IPNetwork network)
    {
        Span<byte> addressBuffer = stackalloc byte[IPv6AddressBytes];
        network.BaseAddress.TryWriteBytes(addressBuffer, out int bytesWritten);
        addressBuffer = addressBuffer.Slice(0, bytesWritten);

        lock (_lock)
        {
            Node<TLeaf> trieRoot = network.BaseAddress.AddressFamily == AddressFamily.InterNetwork ? _rootIPv4 : _rootIPv6;
            RemoveCore(addressBuffer, network.PrefixLength, trieRoot);
        }
    }

    private static void RemoveCore(ReadOnlySpan<byte> addressBuffer, int prefixLength, Node<TLeaf> root)
    {
        Node<TLeaf> currentNode = root;
        const int IPv6AddressBits = 128;
        Stack<Node<TLeaf>> stack = new Stack<Node<TLeaf>>(IPv6AddressBits);
        int currentBit = 0;
        for (int i = 0; i < addressBuffer.Length; i++)
        {
            for (int j = 7; j >= 0; j--)
            {
                int bit = (addressBuffer[i] >> j) & 1;
                Node<TLeaf>? nextNode = bit == 0 ? currentNode.Branch0 : currentNode.Branch1;
                if (nextNode is null)
                {
                    // Network is not found.
                    return;
                }

                stack.Push(currentNode);
                currentNode = nextNode;

                if (++currentBit == prefixLength)
                {
#pragma warning disable S907 // "goto" statement should not be used
                    goto LastNetworkNode;
#pragma warning restore S907 // "goto" statement should not be used
                }
            }
        }

        // Network has not been found.
        return;

    LastNetworkNode:

        // Last network node has been found.
        // If currentNode.IsLeaf is TRUE the network is in the trie and we has found it.
        // But we don't need explicitly check the fact - we can just mark the node as not leaf.
        currentNode.IsLeaf = false;
        currentNode.NodeLeaf = default;

        foreach (Node<TLeaf> previousNode in stack)
        {
            if (currentNode.IsLeaf || currentNode.Branch0 is not null || currentNode.Branch1 is not null)
            {
                // It is a leaf or transit node for another network.
                stack.Clear();
                return;
            }

            // It is not a leaf or transit node so we can remove it.
            if (previousNode.Branch0 == currentNode)
            {
                previousNode.Branch0 = null;
            }
            else
            {
                previousNode.Branch1 = null;
            }

            currentNode = previousNode;
        }
    }

    private static void ThrowIfNotIP(ReadOnlySpan<byte> addressBuffer)
    {
        if (addressBuffer.Length != IPv4AddressBytes && addressBuffer.Length != IPv6AddressBytes)
        {
            throw new ArgumentException("IPv4 or IPv6 is expected.", nameof(addressBuffer));
        }
    }

    /// <summary>
    /// Counts nodes in the BinaryTrie.
    /// </summary>
    /// <returns>Numbers of IPv4 and IPv6 nodes.</returns>
    internal (int IPv4NodeCount, int IPv6NodeCount) CountNodes()
    {
        static int CountNodesCore(Node<TLeaf>? node)
        {
            if (node is null)
            {
                return 0;
            }

            int count = 1;
            count += CountNodesCore(node.Branch0);
            count += CountNodesCore(node.Branch1);

            return count;
        }

        int ipv4count = CountNodesCore(_rootIPv4);
        int ipv6count = CountNodesCore(_rootIPv6);

        return (ipv4count, ipv6count);
    }

    /// <summary>
    /// Counts non-leaf nodes in the BinaryTrie.
    /// </summary>
    /// <returns>Numbers of IPv4 and IPv6 non-leaf nodes.</returns>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal (int IPv4NodeCount, int IPv6NodeCount) CountNonLeafNodes()
    {
        static int CountNodesCore(Node<TLeaf>? node)
        {
            if (node is null)
            {
                return 0;
            }

            int count = node.IsLeaf ? 0 : 1;
            count += CountNodesCore(node.Branch0);
            count += CountNodesCore(node.Branch1);

            return count;
        }

        int ipv4count = CountNodesCore(_rootIPv4);
        int ipv6count = CountNodesCore(_rootIPv6);

        return (ipv4count, ipv6count);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal static Type GetNodeType()
    {
        return typeof(Node<TLeaf>);
    }
}
