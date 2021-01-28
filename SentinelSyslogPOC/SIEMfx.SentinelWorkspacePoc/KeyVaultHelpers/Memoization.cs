// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc.KeyVaultHelpers
{
    using System;
    using System.Collections.Concurrent;

    public static class MemoizationExtensions
    {
        public static Func<T?, TResult> MemoizeNullable<T, TResult>(Func<T?, TResult> func) where T : struct
        {
            var cache = new ConcurrentDictionary<T?, TResult>();

            return x =>
            {
                TResult result;
                if (x.HasValue && cache.TryGetValue(x, out result))
                {
                    return result;
                }

                result = func(x);
                if (x.HasValue)
                {
                    cache.TryAdd(x, result);
                }

                return result;
            };
        }

        public static Func<T, TResult> Memoize<T, TResult>(Func<T, TResult> func) where T : class
        {
            var cache = new ConcurrentDictionary<T, TResult>();

            object syncLockObject = new object();

            return x =>
            {
                TResult result;
                if (x != null && cache.TryGetValue(x, out result))
                {
                    return result;
                }

                lock (syncLockObject)
                {
                    if (x != null && cache.TryGetValue(x, out result))
                    {
                        return result;
                    }

                    result = func(x);
                    if (x != null)
                    {
                        cache.TryAdd(x, result);
                    }
                }

                return result;
            };
        }

        public static Func<T, TParam, TResult> Memoize<T, TParam, TResult>(Func<T, TParam, TResult> func)
            where T : class
        {
            var cache = new ConcurrentDictionary<Tuple<T, TParam>, TResult>();

            return (x, param) =>
            {
                var key = Tuple.Create(x, param);
                TResult result;
                if (cache.TryGetValue(key, out result))
                {
                    return result;
                }

                result = func(x, param);
                cache.TryAdd(key, result);

                return result;
            };
        }

        public static Func<T, TResult> MemoizeValueType<T, TResult>(Func<T, TResult> func) where T : struct
        {
            var cache = new ConcurrentDictionary<T, TResult>();

            return x =>
            {
                TResult result;
                if (cache.TryGetValue(x, out result))
                {
                    return result;
                }

                result = func(x);
                cache.TryAdd(x, result);

                return result;
            };
        }

        private static readonly EnumComparer EnumComparer = new EnumComparer();

        public static Func<Enum, string> Memoize(Func<Enum, string> func)
        {
            var cache = new ConcurrentDictionary<Enum, string>(EnumComparer);

            return x =>
            {
                string result;
                if (cache.TryGetValue(x, out result))
                {
                    return result;
                }

                result = func(x);
                if (x != null)
                {
                    cache.TryAdd(x, result);
                }

                return result;
            };
        }
    }
}