using ASI.Basecode.Services.ServiceModels.Catalog;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Borrower-facing, read-only view of reservable equipment (WBS 34.00).
    /// Administrator item CRUD belongs to a separate EquipmentItem service.
    /// </summary>
    public interface ICatalogService
    {
        CatalogIndexViewModel GetCatalog(CatalogFilterViewModel filter);
        CatalogItemDetailsViewModel RetrieveCatalogItem(long id);
    }
}
