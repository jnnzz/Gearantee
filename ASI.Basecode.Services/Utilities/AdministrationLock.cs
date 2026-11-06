namespace ASI.Basecode.Services.Utilities
{
    /// <summary>
    /// The SQL Server application-lock resource that serializes administrative writes.
    /// Role and permission changes (<c>UserAdministrationService</c>), account edits and
    /// borrower-profile edits all take this same lock (returns take it in Shared mode), so an authorization check cannot be
    /// overtaken by a concurrent permission revocation between the check and the commit.
    /// </summary>
    public static class AdministrationLock
    {
        public const string ResourceName = "Gearantee.UserAdministration";
    }
}
