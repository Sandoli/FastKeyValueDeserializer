using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace FastKeyValueDeserializer;

public static class Utils
{
    private static readonly Func<ReadOnlySpan<char>, int> CountCommasImpl;
    private static readonly Func<ReadOnlySpan<char>, char, char, int> IndexOfTwoCharsImpl;

    static Utils()
    {
        if (Avx2.IsSupported)
        {
            CountCommasImpl = CountCommasAvx2;
            IndexOfTwoCharsImpl = IndexOfTwoCharsAvx2;
        }
        else if (Sse2.IsSupported)
        {
            CountCommasImpl = CountCommasSse2;
            IndexOfTwoCharsImpl = IndexOfTwoCharsSse2;
        }
        else if (AdvSimd.IsSupported)
        {
            CountCommasImpl = CountCommasAdvSimd;
            IndexOfTwoCharsImpl = IndexOfTwoCharsAdvSimd;
        }
        else if (Vector.IsHardwareAccelerated)
        {
            CountCommasImpl = CountCommasVector;
            IndexOfTwoCharsImpl = IndexOfTwoCharsVector;
        }
        else
        {
            CountCommasImpl = CountCommasScalar;
            IndexOfTwoCharsImpl = IndexOfTwoChars;
        }
    }

    internal static object ParseValue(ReadOnlySpan<char> value)
    {
        if (value.StartsWith('(') && value.EndsWith(')'))
        {
            var inner = value[1..^1];
            var commaCount = CountCommas(inner);
            Span<Range> chunks = stackalloc Range[commaCount + 1];
            var count = inner.Split(chunks, ',');
            var list = new List<object>(count);
            for (var i = 0; i < count; i++) list.Add(ParseValue(inner[chunks[i]].Trim()));
            return list;
        }

        if (int.TryParse(value, out var intVal)) return intVal;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblVal)) return dblVal;
        if (value.StartsWith('"') && value.EndsWith('"')) return value[1..^1].ToString();

        return value.ToString();
    }

    private static int CountCommasAdvSimd(ReadOnlySpan<char> span)
    {
        var count = 0;
        var i = 0;

        if (span.Length >= Vector128<ushort>.Count)
        {
            var commaVec = Vector128.Create((ushort)',');
            var oneVec = Vector128.Create((ushort)1);
            var accumulator = Vector128<ushort>.Zero;

            while (i <= span.Length - Vector128<ushort>.Count)
            {
                var slice = span.Slice(i, Vector128<ushort>.Count);
                var inputVec = MemoryMarshal.Read<Vector128<ushort>>(MemoryMarshal.AsBytes(slice));
                var equals = AdvSimd.CompareEqual(inputVec, commaVec);
                var mask = AdvSimd.And(equals, oneVec);
                accumulator = AdvSimd.Add(accumulator, mask);

                i += Vector128<ushort>.Count;
            }

            var accArray = new ushort[Vector256<ushort>.Count];
            MemoryMarshal.Write(MemoryMarshal.AsBytes(accArray.AsSpan()), in accumulator);
            count += accArray.Sum(x => x);
        }

        for (; i < span.Length; i++)
            if (span[i] == ',')
                count++;

        return count;
    }

    private static int CountCommasAvx2(ReadOnlySpan<char> span)
    {
        var count = 0;
        var i = 0;

        if (span.Length >= Vector256<ushort>.Count)
        {
            var commaVec = Vector256.Create((ushort)',');
            var oneVec = Vector256.Create((ushort)1);
            var accumulator = Vector256<ushort>.Zero;

            while (i <= span.Length - Vector256<ushort>.Count)
            {
                var slice = span.Slice(i, Vector256<ushort>.Count);
                var inputVec = MemoryMarshal.Read<Vector256<ushort>>(MemoryMarshal.AsBytes(slice));
                var equals = Avx2.CompareEqual(inputVec, commaVec);
                var mask = Avx2.And(equals, oneVec);
                accumulator = Avx2.Add(accumulator, mask);

                i += Vector256<ushort>.Count;
            }

            var accArray = new ushort[Vector256<ushort>.Count];
            MemoryMarshal.Write(MemoryMarshal.AsBytes(accArray.AsSpan()), in accumulator);
            count += accArray.Sum(x => x);
        }

        for (; i < span.Length; i++)
            if (span[i] == ',')
                count++;

        return count;
    }

    private static int CountCommasSse2(ReadOnlySpan<char> span)
    {
        var count = 0;
        var i = 0;

        if (span.Length >= Vector128<ushort>.Count)
        {
            var commaVec = Vector128.Create((ushort)',');
            var oneVec = Vector128.Create((ushort)1);
            var accumulator = Vector128<ushort>.Zero;

            while (i <= span.Length - Vector128<ushort>.Count)
            {
                var slice = span.Slice(i, Vector128<ushort>.Count);
                var inputVec = MemoryMarshal.Read<Vector128<ushort>>(MemoryMarshal.AsBytes(slice));
                var equals = Sse2.CompareEqual(inputVec, commaVec);
                var mask = Sse2.And(equals, oneVec);
                accumulator = Sse2.Add(accumulator, mask);

                i += Vector128<ushort>.Count;
            }

            var accArray = new ushort[Vector128<ushort>.Count];
            MemoryMarshal.Write(MemoryMarshal.AsBytes(accArray.AsSpan()), in accumulator);
            count += accArray.Sum(x => x);
        }

        for (; i < span.Length; i++)
            if (span[i] == ',')
                count++;

        return count;
    }

    private static int CountCommasVector(ReadOnlySpan<char> span)
    {
        var count = 0;
        var i = 0;

        if (span.Length >= Vector<ushort>.Count)
        {
            var commaVec = new Vector<ushort>(',');
            var oneVec = new Vector<ushort>(1);
            var accumulator = Vector<ushort>.Zero;

            while (i <= span.Length - Vector<ushort>.Count)
            {
                var slice = span.Slice(i, Vector<ushort>.Count);
                var vec = new Vector<ushort>(MemoryMarshal.Cast<char, ushort>(slice));
                var equals = Vector.Equals(vec, commaVec);
                var mask = Vector.BitwiseAnd(equals, oneVec);
                accumulator = Vector.Add(accumulator, mask);

                i += Vector<ushort>.Count;
            }

            count += Vector.Dot(accumulator, oneVec);
        }

        for (; i < span.Length; i++)
            if (span[i] == ',')
                count++;

        return count;
    }

    private static int CountCommasScalar(ReadOnlySpan<char> span)
    {
        var count = 0;
        var i = 0;

        for (; i < span.Length; i++)
            if (span[i] == ',')
                count++;

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountCommas(ReadOnlySpan<char> span)
    {
        return CountCommasImpl(span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> FastTrim(this ReadOnlySpan<char> span, out int start, out int end)
    {
        start = 0;
        end = span.Length - 1;

        while (start <= end && char.IsWhiteSpace(span[start])) start++;
        while (end >= start && char.IsWhiteSpace(span[end])) end--;

        return span.Slice(start, end - start + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> FastTrim(this ReadOnlySpan<char> span)
    {
        return FastTrim(span, out _, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfSeparator(this ReadOnlySpan<char> span, ReadOnlySpan<char> separator)
    {
        switch (separator.Length)
        {
            case 2 when separator[0] == ':' && separator[1] == '=':
                return IndexOfTwoCharsImpl(span, ':', '=');
            case 2 when separator[0] == '-' && separator[1] == '-':
                return IndexOfTwoCharsImpl(span, '-', '-');
            default:
                return span.IndexOf(separator);
        }
    }

    private static int IndexOfTwoCharsAvx2(ReadOnlySpan<char> span, char first, char second)
    {
        var length = span.Length;
        var i = 0;

        if (length >= Vector256<ushort>.Count)
        {
            var firstVec = Vector256.Create((ushort)first);
            var secondVec = Vector256.Create((ushort)second);

            var vectorSize = Vector256<ushort>.Count;

            while (i <= length - vectorSize)
            {
                var slice = span.Slice(i, vectorSize);
                var inputVec = MemoryMarshal.Read<Vector256<ushort>>(MemoryMarshal.AsBytes(slice));

                var shifted = Avx2.AlignRight(inputVec, Vector256<ushort>.Zero, 2); // Shift right by 1 char (2 bytes)
                var eqFirst = Avx2.CompareEqual(shifted, firstVec);
                var eqSecond = Avx2.CompareEqual(inputVec, secondVec);

                var match = Avx2.And(eqFirst, eqSecond);

                var bitmask = (uint)Avx2.MoveMask(match.AsByte());
                if (bitmask != 0)
                {
                    var bitPos = BitOperations.TrailingZeroCount(bitmask);
                    var matchIndex = bitPos / 2; // Each char is 2 bytes
                    return i + matchIndex - 1;
                }

                i += vectorSize - 1;
            }
        }

        // Scalar fallback
        for (; i < length - 1; i++)
            if (span[i] == first && span[i + 1] == second)
                return i;
        return -1;
    }

    private static int IndexOfTwoCharsSse2(ReadOnlySpan<char> span, char first, char second)
    {
        var length = span.Length;
        var i = 0;

        if (length >= Vector128<ushort>.Count)
        {
            var firstVec = Vector128.Create((ushort)first);
            var secondVec = Vector128.Create((ushort)second);

            var vectorSize = Vector128<ushort>.Count;

            while (i <= length - vectorSize)
            {
                var slice = span.Slice(i, vectorSize);
                var inputVec = MemoryMarshal.Read<Vector128<ushort>>(MemoryMarshal.AsBytes(slice));

                var shifted = Sse2.ShiftRightLogical128BitLane(inputVec, 2); // shift by 2 bytes
                var eqFirst = Sse2.CompareEqual(shifted, firstVec);
                var eqSecond = Sse2.CompareEqual(inputVec, secondVec);

                var match = Sse2.And(eqFirst, eqSecond);

                var bitmask = (uint)Sse2.MoveMask(match.AsByte());
                if (bitmask != 0)
                {
                    var bitPos = BitOperations.TrailingZeroCount(bitmask);
                    var matchIndex = bitPos / 2;
                    return i + matchIndex - 1;
                }

                i += vectorSize - 1;
            }
        }

        // Scalar fallback
        for (; i < length - 1; i++)
            if (span[i] == first && span[i + 1] == second)
                return i;
        return -1;
    }

    private static int IndexOfTwoCharsAdvSimd(ReadOnlySpan<char> span, char first, char second)
    {
        var length = span.Length;
        var i = 0;

        if (AdvSimd.IsSupported && length >= Vector128<ushort>.Count)
        {
            var firstVec = Vector128.Create((ushort)first);
            var secondVec = Vector128.Create((ushort)second);

            var vectorSize = Vector128<ushort>.Count;

            while (i <= length - vectorSize)
            {
                var slice = span.Slice(i, vectorSize);
                var inputVec = MemoryMarshal.Read<Vector128<ushort>>(MemoryMarshal.AsBytes(slice));

                var shifted = VectorExtensions.ShiftRight(inputVec); // Shift 16 bits = 1 char
                var eqFirst = AdvSimd.CompareEqual(shifted, firstVec);
                var eqSecond = AdvSimd.CompareEqual(inputVec, secondVec);

                var match = AdvSimd.Or(eqFirst, eqSecond);

                var mask = AdvSimd.Arm64.MaxAcross(match);
                ulong bitmask = mask.ToScalar();

                if (bitmask != 0)
                    for (var j = 0; j < vectorSize; j++)
                        if (span[i + j] == first && i + j + 1 < span.Length && span[i + j + 1] == second)
                            return i + j;

                i += vectorSize - 1;
            }
        }

        // Scalar fallback
        for (; i < length - 1; i++)
            if (span[i] == first && span[i + 1] == second)
                return i;
        return -1;
    }

    private static int IndexOfTwoCharsVector(ReadOnlySpan<char> span, char first, char second)
    {
        var length = span.Length;
        var i = 0;

        if (length >= Vector<ushort>.Count)
        {
            var firstVec = new Vector<ushort>(first);
            var secondVec = new Vector<ushort>(second);
            var vectorSize = Vector<ushort>.Count;

            while (i <= length - vectorSize)
            {
                var slice = span.Slice(i, vectorSize);
                var vec = new Vector<ushort>(MemoryMarshal.Cast<char, ushort>(slice));

                var shifted = VectorExtensions.ShiftRight(vec);
                var eqFirst = Vector.Equals(shifted, firstVec);
                var eqSecond = Vector.Equals(vec, secondVec);

                var match = Vector.BitwiseOr(eqFirst, eqSecond);

                if (Vector.GreaterThanAny(match, Vector<ushort>.Zero))
                    for (var j = 0; j < vectorSize; j++)
                        if (eqFirst[j] != 0 && eqSecond[j] != 0)
                            return i + j - 1;

                i += vectorSize - 1;
            }
        }

        for (; i < length - 1; i++)
            if (span[i] == first && span[i + 1] == second)
                return i;
        return -1;
    }

    private static int IndexOfTwoChars(ReadOnlySpan<char> span, char first, char second)
    {
        var length = span.Length;
        var i = 0;

        // Scalar fallback
        for (; i < length - 1; i++)
            if (span[i] == first && span[i + 1] == second)
                return i;
        return -1;
    }

    internal static class VectorExtensions
    {
        public static Vector<ushort> ShiftRight(Vector<ushort> vector)
        {
            var shifted = Vector<ushort>.Zero;

            for (var i = 1; i < Vector<ushort>.Count; i++) shifted = shifted.WithElement(i, vector[i - 1]);

            return shifted;
        }

        public static Vector128<ushort> ShiftRight(Vector128<ushort> vector)
        {
            var shifted = Vector128<ushort>.Zero;

            for (var i = 1; i < Vector128<ushort>.Count; i++) shifted = shifted.WithElement(i, vector[i - 1]);

            return shifted;
        }
    }
}