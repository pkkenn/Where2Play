namespace Where2Play.Contracts.V1
{
    /// <summary>
    /// Standard paginated response envelope.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    public class PaginatedResult<T>
    {
        /// <summary>Current page number (1-based).</summary>
        public int Page { get; set; }
        /// <summary>Requested page size.</summary>
        public int Size { get; set; }
        /// <summary>Total items across all pages.</summary>
        public int Total { get; set; }
        /// <summary>Items in the current page.</summary>
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    }
}
