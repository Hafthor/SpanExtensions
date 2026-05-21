using System.Diagnostics;

namespace com.hafthor.SpanExtensions;

public static class SpanExtensions {
    /// <summary>
    /// Partitions a span. Useful if you want the smallest/largest k elements of a list without sorting the whole list.
    /// This is a generalization of the partition step of quickselect.
    /// </summary>
    /// <param name="array">source array</param>
    /// <param name="k">desired split point</param>
    /// <param name="comparer">optional comparer to use instead of the default for the type</param>
    /// <typeparam name="T">element type</typeparam>
    /// <returns>
    /// A tuple such that .startingAt is less than or equal to k and .startingAt + .ties is greater than or equal to k.
    /// All elements before .startingAt will be less than the .ties elements starting at .startingAt. Remaining elements
    /// will be greater than those. No order within the less than or greater than lists is guaranteed. May return .ties
    /// equal to 0 on boundary conditions, such as k equaling 0 or array.Length.
    /// </returns>
    /// <remarks>
    /// Because elements might compare equally at k, we may return results such that if you split the array at k, some
    /// elements that compare equally will be on either side of the split.
    /// </remarks>
    public static (int ties, int startingAt) Partition<T>(this Span<T> array, int k, Comparer<T> comparer = null) {
        comparer ??= Comparer<T>.Default;
        int add = 0;
        while (k > 0 && k < array.Length) {
            var (low, high) = Partition(array, comparer);
            if (k < low)
                array = array[..low];
            else if (k > high) {
                array = array[high..];
                k -= high;
                add += high;
            } else
                return (high - low, low + add);
        }

        return (0, add + k);
    }

    // Bentley-McIlroy three-way partitioning (fat partition)
    // Returns (low, high) where array[..low] < pivot, array[low..high] == pivot, array[high..] > pivot
    private static (int low, int high) Partition<T>(Span<T> array, Comparer<T> comparer) {
        // [=pivot][<pivot][>pivot][=pivot]
        // 0......|.......||.......|......^
        //    peLeft  left/right  peRight
        // as an optimization, peLeft will be one less than the start of the less-than list
        int n = array.Length, peLeft = 0, left = 1, right = n - 1, peRight = n;
        for (T pivot = array[0];; (array[left], array[right]) = (array[right--], array[left++])) {
            for (int cmp; left <= right && (cmp = comparer.Compare(array[left], pivot)) <= 0; left++)
                if (cmp == 0 && left != ++peLeft)
                    (array[peLeft], array[left]) = (array[left], array[peLeft]);
            for (int cmp; left <= right && (cmp = comparer.Compare(array[right], pivot)) >= 0; right--)
                if (cmp == 0 && right != --peRight)
                    (array[right], array[peRight]) = (array[peRight], array[right]);
            if (left > right) break;
        }

        peLeft++; // adjust for optimization above

        // Swap equals from the ends to the middle
        // we use Max to avoid double swap on overlaps
        int eqStart = left - peLeft;
        int eqIdx = Math.Max(peLeft, eqStart); // will become gtStart
        for (int i = 0; eqIdx < left;)
            (array[i], array[eqIdx]) = (array[eqIdx++], array[i++]);
        for (int i = Math.Max(peRight, n - peRight - left); i < n;)
            (array[i], array[eqIdx]) = (array[eqIdx++], array[i++]);

        return (eqStart, eqIdx);
    }
}

[TestClass]
public class TestPartition {
    [TestMethod]
    public void BasicTest() => Test();
    
    private static void Test(Random r = null, int count = 1000, int size = 10, int range = 10, bool quiet = true) {
        r ??= new(0);
        for (int i = 0; i < count; i++) {
            int[] org = Enumerable.Range(0, size).Select(_ => r.Next(range)).ToArray();
            for (int k = 1; k < size; k++) {
                int[] array = org.ToArray(); // copy
                var (ties, startingAt) = array.AsSpan().Partition(k);
                if (!quiet)
                    Console.WriteLine($"i={i}, k={k} -> org=[" + string.Join(",", org) + "] -> " +
                                      $"ties={ties},startingAt={startingAt}, array=[" + string.Join(",", array) + "]");
                var lt = array[..startingAt];
                var eq = array[startingAt..(startingAt + ties)];
                var gt = array[(startingAt + ties)..];
                var aMax = lt.Length == 0 ? int.MinValue : lt.Max();
                var cMin = gt.Length == 0 ? int.MaxValue : gt.Min();
                if (aMax >= cMin || eq.Any(v => v != eq[0]) || k < lt.Length || k > lt.Length + eq.Length) {
                    Assert.Fail($"i={i}, k={k} -> org=[" + string.Join(",", org) + "] -> " +
                               $"ties={ties},startingAt={startingAt}, array=[" + string.Join(",", array) + "]");
                }
            }
        }

        var same = Enumerable.Repeat(0, size).ToArray();
        var (tiesSame, startingAtSame) = same.AsSpan().Partition(size >> 1);
        Assert.IsTrue(tiesSame == size && startingAtSame == 0);
    }
}
