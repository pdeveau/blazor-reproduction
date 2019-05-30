using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Blazor.Http;
using Microsoft.AspNetCore.Components.Builder;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Blazor.Server.Client
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            var policy = Policy.Handle<Exception>()
                .WaitAndRetryAsync(4, currentRetry => TimeSpan.FromMilliseconds(currentRetry * 100));

            services.AddSingleton(provider => new HttpClient(provider.GetService<TokenWebAssemblyMessageHandler>()));

            services.AddHttpClient<ISomeClient, SomeClient>(httpClient =>
                {
                    httpClient.BaseAddress = new Uri("https://nope.org");
                })
                .AddPolicyHandler(policy.AsAsyncPolicy<HttpResponseMessage>());
        }

        public void Configure(IComponentsApplicationBuilder app)
        {
            app.AddComponent<App>("app");
        }
    }


    public class SomeClient : ISomeClient
    {
        private readonly HttpClient _client;

        public SomeClient(HttpClient client)
        {
            _client = client;
        }

        public async Task<string> GetDataAsync()
        {
            var response = await _client.GetAsync("/");
            return await response.Content.ReadAsStringAsync();
        }
    }

    public interface ISomeClient
    {
        Task<string> GetDataAsync();
    }

    public class TokenWebAssemblyMessageHandler : WebAssemblyHttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var uri = request.RequestUri?.ToString() ?? string.Empty;

            if (uri.StartsWith("https://nope.org", StringComparison.InvariantCultureIgnoreCase))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "N/A");
            }

            var response = await base.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return response;
        }
    }
}
