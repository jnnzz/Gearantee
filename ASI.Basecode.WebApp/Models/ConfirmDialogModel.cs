namespace ASI.Basecode.WebApp.Models
{
    /// <summary>
    /// Content for Views/Shared/Components/_ConfirmDialog. A form opens it by setting
    /// data-confirm-dialog="{Id}" and including wwwroot/js/confirm-dialog.js.
    /// </summary>
    public class ConfirmDialogModel
    {
        public string Id { get; set; } = "confirm-dialog";
        public string Title { get; set; } = "Are you sure?";
        public string Message { get; set; } = string.Empty;

        /// <summary>Optional extra line, such as the record being changed.</summary>
        public string Detail { get; set; }

        public string ConfirmText { get; set; } = "Confirm";
        public string CancelText { get; set; } = "Cancel";
        public bool IsDestructive { get; set; }
    }
}
