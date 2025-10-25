using EventBus.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductCatalog.Events;
public class VariantCreatedEvent: IntegrationEvent
{
    public Guid VariantId { get; set; }
    public VariantInfo Variant { get; set; } = default!;
}
