namespace Leaderboard.Api.Extensions
{
    public static class ReaderWriterLockSlimExtensions
    {
        public static void WithReadLock(this ReaderWriterLockSlim rwLock, Action action)
        {
            rwLock.EnterReadLock();
            try
            {
                action();
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public static T WithReadLock<T>(this ReaderWriterLockSlim rwLock, Func<T> func)
        {
            rwLock.EnterReadLock();
            try
            {
                return func();
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public static void WithUpgradeableReadLock(this ReaderWriterLockSlim rwLock, Action action)
        {
            rwLock.EnterUpgradeableReadLock();
            try
            {
                action();
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }
        }

        public static T WithUpgradeableReadLock<T>(this ReaderWriterLockSlim rwLock, Func<T> func)
        {
            rwLock.EnterUpgradeableReadLock();
            try
            {
                return func();
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }
        }

        public static void WithWriteLock(this ReaderWriterLockSlim rwLock, Action action)
        {
            rwLock.EnterWriteLock();
            try
            {
                action();
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public static T WithWriteLock<T>(this ReaderWriterLockSlim rwLock, Func<T> func)
        {
            rwLock.EnterWriteLock();
            try
            {
                return func();
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
    }
}