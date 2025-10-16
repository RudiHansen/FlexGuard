namespace FlexGuard.Core.Models
{
    public sealed class FlexTestRow
    {
        public int Id { get; init; }
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
