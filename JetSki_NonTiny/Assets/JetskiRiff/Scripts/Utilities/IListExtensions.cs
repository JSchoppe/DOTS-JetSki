using System.Collections.Generic;
using UnityEngine;

public static class IListExtensions
{
    public static void Shuffle<T>(this IList<T> collection)
    {
        int size = collection.Count;
        for (int i = 0; i < size - 1; i++)
            collection.Swap(i, Random.Range(i, size));
    }

    public static void Swap<T>(this IList<T> collection, int indexA, int indexB)
    {
        T holdingValue = collection[indexA];
        collection[indexA] = collection[indexB];
        collection[indexB] = holdingValue;
    }
}
