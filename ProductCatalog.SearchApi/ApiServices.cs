using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ProductCatalog.SearchApi;
public class ApiServices(ElasticsearchClient client, IHostEnvironment environment, ILogger<ApiServices> logger, CancellationToken cancellationToken)
{
    public ElasticsearchClient Client => client;
    public IHostEnvironment Environment => environment;
    public ILogger<ApiServices> Logger => logger;
    public CancellationToken CancellationToken => cancellationToken;
}
