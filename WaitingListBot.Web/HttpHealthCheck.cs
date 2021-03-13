using Microsoft.Extensions.Diagnostics.HealthChecks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WaitingListBot.Web
{
    public class HttpHealthCheck : IHealthCheck
    {
        string requestUrl;

        public HttpHealthCheck(string requestUrl)
        {
            this.requestUrl = requestUrl;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            CancellationTokenSource source = new CancellationTokenSource(500);
            try
            {
                HttpClient client = new HttpClient();

                var result = await client.GetAsync(requestUrl, CancellationTokenSource.CreateLinkedTokenSource(source.Token, cancellationToken).Token);

                if (result.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, "Error");
                }

                return new HealthCheckResult(HealthStatus.Healthy);
            }
            catch (OperationCanceledException)
            {
                if (source.IsCancellationRequested)
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, "Not reachable");
                }

                throw;
            }
        }
    }
}
