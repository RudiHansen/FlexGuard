using NUlid;

namespace FlexGuard.Core.Models
{
    public sealed class FlexTestRow
    {
        public string Id { get; init; } = Ulid.NewUlid().ToString(); // 26-tegns ULID
        public required string TestNavn { get; init; }
        public decimal Pris { get; init; }
        public TestType Type { get; init; } = TestType.None;

    }
    public enum TestType
    {
        None,
        Normal,
        Medium,
        High
    }
}