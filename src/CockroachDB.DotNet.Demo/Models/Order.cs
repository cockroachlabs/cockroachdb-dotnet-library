using CockroachDB.DotNet.Abstractions.Attributes;

namespace CockroachDB.DotNet.Demo.Models;

[RegionalByRow(DefaultRegion = "us-east1")]
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Product { get; set; } = "";

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [CrdbRegion]
    public string Region { get; set; } = "us-east1";
}
