namespace ASI.Basecode.WebApp.Services.EquipmentImages
{
    public sealed class EquipmentImageSaveResult
    {
        private EquipmentImageSaveResult(string imageUrl, string error)
        {
            ImageUrl = imageUrl;
            Error = error;
        }

        public bool Succeeded => ImageUrl != null;
        public string ImageUrl { get; }
        public string Error { get; }

        public static EquipmentImageSaveResult Success(string imageUrl) =>
            new(imageUrl, null);

        public static EquipmentImageSaveResult Failure(string error) =>
            new(null, error);
    }
}
