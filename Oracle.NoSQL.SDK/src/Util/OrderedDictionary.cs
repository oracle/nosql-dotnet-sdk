/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;

    // Unfortunately the framework does not provide generic OrderedDictionary.
    // Here we implement our own.  The implementation is similar to the
    // implementation of non-generic
    // System.Collections.Specialized.OrderedDictionary

    internal class OrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> dictionary;
        private readonly List<TKey> keyList;

        public TValue this[TKey key]
        {
            get => dictionary[key];

            set
            {
                if (!dictionary.ContainsKey(key))
                {
                    keyList.Add(key);
                }

                dictionary[key] = value;
            }
        }

        public ICollection<TKey> Keys => keyList;

        public ICollection<TValue> Values
        {
            // This should be slightly more efficient than using Linq
            // Select with lambda function.
            get
            {
                var result = new List<TValue>(keyList.Count);
                foreach (var key in keyList)
                {
                    result.Add(dictionary[key]);
                }

                return result;
            }
        }

        public int Count => keyList.Count;

        public bool IsReadOnly => false;

        public OrderedDictionary()
        {
            dictionary = new Dictionary<TKey, TValue>();
            keyList = new List<TKey>();
        }

        public OrderedDictionary(int capacity)
        {
            dictionary = new Dictionary<TKey, TValue>(capacity);
            keyList = new List<TKey>(capacity);
        }

        public void Add(TKey key, TValue value)
        {
            dictionary.Add(key, value);
            // Previous line should throw if the key already exists
            Debug.Assert(!keyList.Contains(key));
            keyList.Add(key);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Insert(int index, TKey key, TValue value)
        {
            dictionary.Add(key, value);
            // Previous line should throw if the key already exists
            Debug.Assert(!keyList.Contains(key));
            keyList.Insert(index, key);
        }

        public void Clear()
        {
            keyList.Clear();
            dictionary.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ContainsKey(item.Key);
        }

        public bool ContainsKey(TKey key)
        {
            return dictionary.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach (var pair in this)
            {
                array[arrayIndex++] = pair;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var key in keyList)
                yield return new KeyValuePair<TKey, TValue>(
                    key, dictionary[key]);
        }

        public bool Remove(TKey key)
        {
            var result = dictionary.Remove(key);
            if (result)
            {
                keyList.Remove(key);
            }

            return result;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        public void RemoveAt(int index)
        {
            dictionary.Remove(keyList[index]);
            keyList.RemoveAt(index);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return dictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public TKey GetKeyAtIndex(int index)
        {
            return keyList[index];
        }

        public TValue GetValueAtIndex(int index)
        {
            return dictionary[GetKeyAtIndex(index)];
        }
    }
}
