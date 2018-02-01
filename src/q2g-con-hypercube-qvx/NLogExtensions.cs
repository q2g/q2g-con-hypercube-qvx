namespace q2gconhypercubeqvx
{
    #region Usings
    using System;
    using System.Threading.Tasks;
    #endregion

    public static class TaskNLogExtensions
    {
        public static Task<T> DefaultIfFaulted<T>(this Task<T> @this)
        {
            return @this.ContinueWith(t => t.IsCanceled || t.IsFaulted ? default(T) : t.Result);
        }

        public static Task<T> DefaultAndLogIfFaulted<T>(this Task<T> @this, NLog.Logger logger)
        {
            return @this.ContinueWith(t =>
            {
                if (@this.IsFaulted)
                {
                    foreach (var ex in @this.Exception.InnerExceptions)
                        logger.Error(ex);
                }

                return t.IsCanceled || t.IsFaulted ? default(T) : t.Result;
            });
        }

        public static Task LogIfFaulted(this Task @this, NLog.Logger logger)
        {
            return @this.ContinueWith(t =>
            {
                if (@this.IsFaulted)
                {
                    foreach (var ex in @this.Exception.InnerExceptions)
                        logger.Error(ex);
                }
            });
        }
    }
}
