namespace ASI.Basecode.Services.Common
{
    /// <summary>
    /// Base type for all service operation outcomes. Carries the universal
    /// succeeded/message pair; concrete results extend it with domain-specific
    /// state flags (stale, missing, denied, conflict, ...).
    /// </summary>
    public abstract class ServiceResult
    {
        public bool Succeeded { get; protected set; }
        public string Message { get; protected set; } = string.Empty;
    }
}
