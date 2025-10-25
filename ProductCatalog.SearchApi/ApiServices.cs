using Elastic.Clients.Elasticsearch;

namespace ProductCatalog.SearchApi;
public class ApiServices(ElasticsearchClient client, CancellationToken cancellationToken)
{
    public ElasticsearchClient Client => client;
    public CancellationToken CancellationToken => cancellationToken;
}
