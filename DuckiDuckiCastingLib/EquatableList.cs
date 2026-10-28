using System;
using System.Collections.Generic;

namespace DuckiDuckiCastingLib
{
    internal class EquatableList<T> : List<T>, IEquatable<EquatableList<T>>
    {
        public EquatableList() { }

        public EquatableList(IEnumerable<T> items) : base(items) { }

        public bool Equals(EquatableList<T>? other)
        {
            // If the other list is null or a different size, they're not equal
            if (other is null || Count != other.Count)
            {
                return false;
            }

            // Compare each pair of elements for equality
            for (int i = 0; i < Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
                {
                    return false;
                }
            }

            // If we got this far, the lists are equal
            return true;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as EquatableList<T>);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (T item in this)
                {
                    hash = hash * 31 + (item?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }

        public static bool operator ==(EquatableList<T>? list1, EquatableList<T>? list2)
        {
            return ReferenceEquals(list1, list2)
                || list1 is not null && list2 is not null && list1.Equals(list2);
        }

        public static bool operator !=(EquatableList<T>? list1, EquatableList<T>? list2)
        {
            return !(list1 == list2);
        }
    }
}
