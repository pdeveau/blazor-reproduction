using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Blazor.Http;
using Microsoft.AspNetCore.Blazor.Services;
using Microsoft.AspNetCore.Components;
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

            services.AddSingleton<TokenWebAssemblyMessageHandler>();
            services.AddSingleton(provider =>
            {
                var uriHelper = provider.GetRequiredService<IUriHelper>();

                Console.WriteLine(uriHelper.GetBaseUri());

                return new HttpClient(provider.GetService<TokenWebAssemblyMessageHandler>())
                    {BaseAddress = new Uri(WebAssemblyUriHelper.Instance.GetBaseUri()) };
            });
            services.AddSingleton<IHttpClientFactory>(provider => new HttpClientFactory(provider));

            services.AddHttpClient<ISomeClient, SomeClient>()
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
        private readonly IUriHelper _uriHelper;

        public SomeClient(HttpClient client, IUriHelper uriHelper)
        {
            _client = client;
            _uriHelper = uriHelper; //not sure why I'd need urihelper here, injected client should have a baseuri already.
        }

        public async Task<string> GetDataAsync()
        {
            var response = await _client.GetAsync($"{_uriHelper.GetBaseUri()}/counter");
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

    public class HttpClientFactory : IHttpClientFactory
    {
        private readonly IServiceProvider _provider;

        public HttpClientFactory(IServiceProvider provider)
        {
            _provider = provider;
        }

        public HttpClient CreateClient(string name)
        {
            return _provider.GetService<HttpClient>();
        }
    }
}
