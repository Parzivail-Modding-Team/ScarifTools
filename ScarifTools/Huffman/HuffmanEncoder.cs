using System.Collections.Generic;
using System.IO;
using Sewer56.BitStream;
using Sewer56.BitStream.ByteStreams;

namespace ScarifTools.Huffman;

public record HuffmanNode<T>(T Datum, int Frequency, HuffmanNode<T> Left, HuffmanNode<T> Right);

public class HuffmanEncoder
{
    private static void BuildTree<T>(HuffmanNode<T> root, string traversal, Dictionary<T, string> map)
    {
        if (root == null)
            return;

        if (root.Left == null && root.Right == null)
            map[root.Datum] = traversal;

        BuildTree<T>(root.Left, traversal + "0", map);
        BuildTree<T>(root.Right, traversal + "1", map);
    }

    public static Dictionary<T, string> Build<T>(T[] data)
    {
        var histogram = new Dictionary<T, int>();
        foreach (var t in data)
        {
            if (!histogram.ContainsKey(t))
                histogram[t] = 0;
            histogram[t]++;
        }

        var pq = new PriorityQueue<HuffmanNode<T>, int>();

        foreach (var (datum, freq) in histogram)
            pq.Enqueue(new HuffmanNode<T>(datum, freq, null, null), freq);

        while (pq.Count != 1)
        {
            var left = pq.Dequeue();
            var right = pq.Dequeue();

            var sum = left.Frequency + right.Frequency;
            pq.Enqueue(new HuffmanNode<T>(default, sum, left, right), sum);
        }

        var root = pq.Dequeue();

        var map = new Dictionary<T, string>();
        BuildTree(root, "", map);

        return map;
    }

    public static void Encode<T>(Dictionary<T, string> map, T[] data, Stream dest)
    {
        var stream = new BitStream<StreamByteStream>(new StreamByteStream(dest));

        foreach (var datum in data)
        {
            var encoding = map[datum];
            foreach (var c in encoding)
                stream.WriteBit((byte)(c == '0' ? 0 : 1));
        }
    }
}