using Npgsql;
using Polly;
using Polly.Retry;

namespace IdentityService.Extentions
{
    public static class Resilience
    {
        public static RetryPolicy GetExecRetry()
        {
            var retryPolicy = Policy
            .Handle<NpgsqlException>()
            .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(10));
            return retryPolicy;
        }
    }
}
