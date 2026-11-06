using ASI.Basecode.WebApp.Models;
using System.Linq;
using Xunit;

namespace ASI.Basecode.Tests.Pagination
{
    public class PaginationViewModelTests
    {
        [Theory]
        [InlineData(1, 3, 23, 1, 10)]
        [InlineData(2, 3, 23, 11, 20)]
        [InlineData(3, 3, 23, 21, 23)]
        [InlineData(99, 3, 23, 21, 23)]
        [InlineData(0, 0, 0, 0, 0)]
        public void Record_ranges_match_the_ten_record_limit(int page, int pages, int count, long first, long last)
        {
            var model = new PaginationViewModel { Page = page, TotalPages = pages, TotalCount = count };
            Assert.Equal(10, model.PageSize);
            Assert.Equal(first, model.FirstRecord);
            Assert.Equal(last, model.LastRecord);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(500000)]
        [InlineData(1000000)]
        public void Large_page_counts_render_a_bounded_window_with_first_last_and_current(int page)
        {
            var model = new PaginationViewModel { Page = page, TotalPages = 1000000 };
            var visible = model.VisiblePages.ToArray();
            Assert.InRange(visible.Length, 1, 5);
            Assert.Contains(1, visible);
            Assert.Contains(1000000, visible);
            Assert.Contains(page, visible);
            Assert.Equal(visible.OrderBy(value => value).Distinct(), visible);
        }

        [Fact]
        public void Links_preserve_filters_and_the_correct_page_parameter_without_mutating_original_routes()
        {
            var model = new PaginationViewModel
            {
                Page = 2, TotalPages = 3, PageParameter = "equipmentPage",
                Routes = { ["tab"] = "equipment", ["equipmentSearch"] = "Camera & mic + stand", ["equipmentCategoryId"] = "42" }
            };
            var previous = model.RoutesFor(1);
            var next = model.RoutesFor(3);
            Assert.Equal("equipment", next["tab"]);
            Assert.Equal("Camera & mic + stand", next["equipmentSearch"]);
            Assert.Equal("42", next["equipmentCategoryId"]);
            Assert.Equal("1", previous["equipmentPage"]);
            Assert.Equal("3", next["equipmentPage"]);
            Assert.False(model.Routes.ContainsKey("equipmentPage"));
            Assert.False(next.ContainsKey("page"));
        }
    }
}
