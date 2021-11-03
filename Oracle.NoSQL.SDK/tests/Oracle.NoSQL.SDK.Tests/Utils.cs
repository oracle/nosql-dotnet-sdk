/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Force.DeepCloner;
    using DeepEqual;
    using DeepEqual.Syntax;
    using System.Security.Cryptography;

    internal sealed class TempFileCache : IDisposable
    {
        private HashSet<string> files = new HashSet<string>();

        internal string GetTempFile()
        {
            var fileName = Path.GetTempFileName();
            var info = new FileInfo(fileName)
            {
                Attributes = FileAttributes.Temporary
            };
            files.Add(fileName);
            return fileName;
        }

        internal string GetTempFile(string data)
        {
            var fileName = GetTempFile();
            File.WriteAllText(fileName, data);
            return fileName;
        }

        internal string GetTempFile(string[] lines)
        {
            var fileName = GetTempFile();
            File.WriteAllLines(fileName, lines);
            return fileName;
        }

        internal void DeleteTempFile(string fileName)
        {
            File.Delete(fileName);
            files.Remove(fileName);
        }

        public void Dispose()
        {
            if (files != null)
            {
                foreach (var fileName in files)
                {
                    File.Delete(fileName);
                }

                files = null;
            }
        }

    }

    internal static class Utils
    {
        // Source should be an anonymous object that has some of the same
        // properties as target.
        internal static T AssignProperties<T>(T target, object source)
        {
            foreach (var sourceProperty in source.GetType().GetProperties())
            {
                var targetProperty = typeof(T).GetProperty(
                    sourceProperty.Name,
                    BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.Public);
                Debug.Assert(targetProperty != null);
                targetProperty.SetValue(target,
                    sourceProperty.GetValue(source));
            }
            return target;
        }

        internal static T CombineProperties<T>(T target, object source)
            where T : new()
        {
            T result = new T();
            if (target != null)
            {
                AssignProperties(result, target);
            }

            return AssignProperties(result, source);
        }

        internal static T DeepCopy<T>(T val)
        {
            return val.DeepClone();
        }

        internal static T CopyAndSetProperties<T>(T value, object properties)
        {
            Assert.IsNotNull(value);
            value = DeepCopy(value);
            return AssignProperties(value, properties);
        }
        
        private class CustomComparison<T> : IComparison
        {
            private Func<T,T,bool> comparator;

            internal CustomComparison(Func<T,T,bool> comparator)
            {
                this.comparator = comparator;
            }

            public bool CanCompare(Type type1, Type type2) =>
                typeof(T).IsAssignableFrom(type1) &&
                typeof(T).IsAssignableFrom(type2);

            public (ComparisonResult result, IComparisonContext context)
                Compare(IComparisonContext context, object value1, object value2)
            {
                var result = Object.ReferenceEquals(value1, value2) ||
                    (value1 != null && value2 != null &&
                    comparator((T)value1, (T)value2));
                return (result
                    ? ComparisonResult.Pass : ComparisonResult.Fail,
                    context);
            }
        }

        // Workaround for a bug in DeepEqual where it throws on RSA type.
        // Other types may be added for custom comparison if needed.
        private static readonly IComparison customCompare1 =
            new CustomComparison<RSA>((value1, value2) =>
            value1.ToXmlString(true) == value2.ToXmlString(true));

        private static CompareSyntax<T,T> CreateCompareSyntax<T>(T expected,
            T actual, bool comparePrivate)
        {
            var compareSyntax = expected.WithDeepEqual(actual)
                .WithCustomComparison(customCompare1);

            if (comparePrivate)
            {
                compareSyntax = compareSyntax.ExposeInternalsOf<T>();
            }
            return compareSyntax;
        }

        internal static void AssertDeepEqual<T>(T expected, T actual,
            bool comparePrivate = false)
        {
            if (expected == null)
            {
                Assert.IsNull(actual);
                return;
            }

            try
            {
                CreateCompareSyntax(expected, actual, comparePrivate)
                    .Assert();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Values are not deep equal: {ex}");
            }
        }
        internal static void AssertNotDeepEqual<T>(T expected, T actual,
            bool comparePrivate = false)
        {
            if (expected == null)
            {
                Assert.IsNotNull(actual);
                return;
            }

            Assert.IsFalse(CreateCompareSyntax(expected, actual,
                comparePrivate).Compare(),
                "Values are not supposed to be equal");
        }

        internal static void AssertThrowsDerived<T>(Action action)
            where T : Exception
        {
            try
            {
                action();
                Assert.Fail(
                    $"Expected exception {nameof(T)} but no exception was " +
                    "thrown");
            }
            catch (AssertFailedException)
            {
                throw;
            }
            catch(Exception ex)
            {
                Assert.IsTrue(ex is T,
                    $"Expected exception {nameof(T)}, but exception " +
                    $"{ex.GetType().FullName} was thrown");
            }
        }

        internal static async Task AssertThrowsDerivedAsync<T>(
            Func<Task> func) where T : Exception
        {
            try
            {
                await func();
                Assert.Fail(
                    $"Expected exception {nameof(T)} but no exception was " +
                    "thrown");
            }
            catch (AssertFailedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is T,
                    $"Expected exception {nameof(T)}, but exception " +
                    $"{ex.GetType().FullName} was thrown");
            }
        }

        internal static MapValue SetFieldValueInMap(MapValue map,
            string fieldName, FieldValue newValue)
        {
            map = DeepCopy(map);
            map[fieldName] = newValue;
            return map;
        }

        internal static MapValue RemoveFieldFromMap(MapValue map, string fieldName)
        {
            map = DeepCopy(map);
            map.Remove(fieldName);
            return map;
        }

        internal static MapValue SetFieldValuesInMap(MapValue map,
            IEnumerable<string> fieldNames, MapValue newValues)
        {
            map = DeepCopy(map);
            foreach(var fieldName in fieldNames)
            {
                map[fieldName] =
                    newValues != null && newValues.ContainsKey(fieldName)
                        ? newValues[fieldName]
                        : FieldValue.Null;
            }
            return map;
        }

        internal static MapValue SetFieldValuesInMapAsOne(MapValue map,
            IEnumerable<string> fieldNames, FieldValue newValue)
        {
            map = DeepCopy(map);
            foreach (var fieldName in fieldNames)
            {
                map[fieldName] = newValue;
            }
            return map;
        }

        internal static RecordValue ProjectRecord(RecordValue record,
            IEnumerable<string> fieldNames)
        {
            var result = new RecordValue();
            foreach (var fieldName in fieldNames)
            {
                result[fieldName] = record[fieldName];
            }

            return result;
        }

        internal static string RepeatString(string value, int count) =>
            string.Concat(Enumerable.Repeat(value, count));
    }
}
