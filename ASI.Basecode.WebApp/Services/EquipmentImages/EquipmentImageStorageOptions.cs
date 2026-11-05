namespace ASI.Basecode.WebApp.Services.EquipmentImages
{
    public sealed class EquipmentImageStorageOptions
    {
        public const string SectionName = "EquipmentImages";

        public string StoragePath { get; set; } = "App_Data/EquipmentImages";
        public int StagingLifetimeHours { get; set; } = 24;
    }
}
