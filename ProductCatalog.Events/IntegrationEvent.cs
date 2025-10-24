using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductCatalog.Events;
public class IntegrationEvent
{
    public Guid Id { get; private set; }
    public DateTime CreationDateUtc { get; private set; }
    public IntegrationEvent()
    {
        Id = Guid.CreateVersion7();
        CreationDateUtc = DateTime.UtcNow;
    }
}
