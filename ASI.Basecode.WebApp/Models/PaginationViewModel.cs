using ASI.Basecode.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ASI.Basecode.WebApp.Models
{
    public class PaginationViewModel
    {
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
        public string Label { get; set; } = "Results pages";
        public string PageParameter { get; set; } = "page";
        public Dictionary<string, string> Routes { get; } = new();
        public int PageSize => PaginationDefaults.PageSize;
        public int PageCount => Math.Max(1, TotalPages);
        public int CurrentPage => Math.Clamp(Page, 1, PageCount);
        public long FirstRecord => TotalCount == 0 ? 0 : (long)(CurrentPage - 1) * PageSize + 1;
        public long LastRecord => Math.Min((long)CurrentPage * PageSize, TotalCount);

        public IEnumerable<int> VisiblePages
        {
            get
            {
                var start = Math.Max(1, CurrentPage - 1);
                var end = Math.Min(PageCount, start + 2);
                start = Math.Max(1, end - 2);
                return Enumerable.Range(start, end - start + 1)
                    .Append(1).Append(PageCount).Distinct().OrderBy(page => page);
            }
        }

        public Dictionary<string, string> RoutesFor(int page)
        {
            var routes = new Dictionary<string, string>(Routes)
            {
                [PageParameter] = Math.Clamp(page, 1, PageCount).ToString(CultureInfo.InvariantCulture)
            };
            return routes;
        }
    }
}
