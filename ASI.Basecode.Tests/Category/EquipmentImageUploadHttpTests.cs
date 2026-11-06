using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Category
{
    /// <summary>
    /// End-to-end coverage for the equipment image upload path: the controller validates the
    /// file, uploads through IImageStorageService, persists the stable object reference, and
    /// cleans up orphaned or replaced objects. Storage is the offline FakeImageStorageService.
    /// </summary>
    public class EquipmentImageUploadHttpTests
    {
        // A real, decodable 1x1 PNG so server-side decode/dimension validation passes.
        private static readonly byte[] PngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

        [Fact]
        public async Task HttpCreateEquipment_UploadsImage_AndPersistsPublicUrl()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var categoryId = await SeedCategoryAsync(site, "IMG-OK");
            var token = await site.Token("/Categories/CreateEquipment");

            using var form = CreateForm(token, categoryId, "IMG-OK-ITEM", PngBytes);
            var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var saved = await db.EquipmentItems.SingleAsync(item => item.ItemName == "Uploaded item");
            Assert.Equal(
                "https://fake.supabase.co/storage/v1/object/public/equipment-images/items/upload-001.png",
                saved.ImageUrl);
            Assert.Single(site.ImageStorage.Uploads);
            Assert.Equal("image/png", site.ImageStorage.Uploads[0].ContentType);
            Assert.Empty(site.ImageStorage.DeletedUrls);
        }

        [Fact]
        public async Task HttpCreateEquipment_RejectsInvalidImage_BeforeUpload()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var categoryId = await SeedCategoryAsync(site, "IMG-BAD");
            var token = await site.Token("/Categories/CreateEquipment");

            using var form = CreateForm(
                token,
                categoryId,
                "IMG-BAD-ITEM",
                Encoding.ASCII.GetBytes("This is not an image."),
                "photo.png",
                "image/png");
            var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("not a valid", html, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(site.ImageStorage.Uploads);
            Assert.Empty(site.ImageStorage.DeletedUrls);

            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.False(await db.EquipmentItems.AnyAsync(item => item.ItemName == "Uploaded item"));
        }

        [Fact]
        public async Task HttpCreateEquipment_OversizeImage_RejectedWithFriendlyMessage()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var categoryId = await SeedCategoryAsync(site, "IMG-BIG");
            var token = await site.Token("/Categories/CreateEquipment");

            // Larger than the test host's default Supabase:MaxFileBytes (10 MB) so the
            // validation message, not a framework exceptions page, is what the user sees.
            var oversize = new byte[11 * 1024 * 1024];
            PngBytes.CopyTo(oversize, 0);

            using var form = CreateForm(
                token, categoryId, "IMG-BIG-ITEM", oversize);
            var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("or smaller", html, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(site.ImageStorage.Uploads);

            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.False(await db.EquipmentItems.AnyAsync(item => item.ItemName == "Uploaded item"));
        }

        [Fact]
        public async Task HttpCreateEquipment_Exceeding2MB_RejectedBeforeUpload()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var categoryId = await SeedCategoryAsync(site, "IMG-2MB");
            var token = await site.Token("/Categories/CreateEquipment");

            // 2.5 MB image exceeds the 2 MB ceiling
            var oversize = new byte[(int)(2.5 * 1024 * 1024)];
            PngBytes.CopyTo(oversize, 0);

            using var form = CreateForm(
                token, categoryId, "IMG-2MB-ITEM", oversize);
            var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Image size exceeds the maximum allowed size of 2 MB", html, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(site.ImageStorage.Uploads);

            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.False(await db.EquipmentItems.AnyAsync(item => item.ItemName == "Uploaded item"));
        }

        [Fact]
        public async Task HttpCreateEquipment_WhenCategoryIsInactive_DeletesUnpublishedImage()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var categoryId = await SeedCategoryAsync(site, "IMG-DUP");
            await SeedItemAsync(site, categoryId, "IMG-DUP-ITEM", null);
            await using (var scope = site.Environment.Provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = await db.EquipmentCategories.FindAsync(categoryId);
                category.IsActive = false;
                await db.SaveChangesAsync();
            }
            var token = await site.Token("/Categories/CreateEquipment");

            using var form = CreateForm(token, categoryId, "IMG-DUP-ITEM", PngBytes);
            var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Single(site.ImageStorage.Uploads);
            Assert.Single(site.ImageStorage.DeletedUrls);
            Assert.Equal(
                "https://fake.supabase.co/storage/v1/object/public/equipment-images/items/upload-001.png",
                site.ImageStorage.DeletedUrls[0]);
        }

        [Fact]
        public async Task HttpEditEquipment_WithNewImage_ReplacesUrlAndKeepsPublishedObject()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var categoryId = await SeedCategoryAsync(site, "IMG-EDIT");
            var oldUrl = "https://old.example.test/projector.png";
            var equipmentId = await SeedItemAsync(site, categoryId, "IMG-EDIT-ITEM", oldUrl);
            var token = await site.Token($"/Categories/EditEquipment?id={equipmentId}");

            using var form = EditForm(token, equipmentId, categoryId, "IMG-EDIT-ITEM", PngBytes);
            var response = await site.Client.PostAsync("/Categories/EditEquipment", form);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var saved = await db.EquipmentItems.SingleAsync(item => item.EquipmentId == equipmentId);
            Assert.Equal(
                "https://fake.supabase.co/storage/v1/object/public/equipment-images/items/upload-001.png",
                saved.ImageUrl);
            Assert.Single(site.ImageStorage.Uploads);
            Assert.Empty(site.ImageStorage.DeletedUrls);
        }

        [Fact]
        public async Task HttpEditEquipment_WithoutFile_KeepsImageAndDoesNotTouchStorage()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var categoryId = await SeedCategoryAsync(site, "IMG-KEEP");
            var oldUrl = "https://old.example.test/keep.png";
            var equipmentId = await SeedItemAsync(site, categoryId, "IMG-KEEP-ITEM", oldUrl);
            var token = await site.Token($"/Categories/EditEquipment?id={equipmentId}");

            using var form = EditForm(token, equipmentId, categoryId, "IMG-KEEP-ITEM");
            var response = await site.Client.PostAsync("/Categories/EditEquipment", form);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var saved = await db.EquipmentItems.SingleAsync(item => item.EquipmentId == equipmentId);
            Assert.Equal(oldUrl, saved.ImageUrl);
            Assert.Empty(site.ImageStorage.Uploads);
            Assert.Empty(site.ImageStorage.DeletedUrls);
        }

        [Fact]
        public async Task HttpEditEquipment_RemoveImage_ClearsUrlAndKeepsPublishedObject()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var categoryId = await SeedCategoryAsync(site, "IMG-REMOVE");
            var oldUrl = "https://old.example.test/remove.png";
            var equipmentId = await SeedItemAsync(site, categoryId, "IMG-REMOVE-ITEM", oldUrl);
            var token = await site.Token($"/Categories/EditEquipment?id={equipmentId}");

            using var form = EditForm(
                token, equipmentId, categoryId, "IMG-REMOVE-ITEM", removeImage: true);
            var response = await site.Client.PostAsync("/Categories/EditEquipment", form);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var saved = await db.EquipmentItems.SingleAsync(item => item.EquipmentId == equipmentId);
            Assert.Null(saved.ImageUrl);
            Assert.Empty(site.ImageStorage.Uploads);
            Assert.Empty(site.ImageStorage.DeletedUrls);
        }

        private static async Task<long> SeedCategoryAsync(CategoryHttpSite site, string code)
        {
            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var category = new EquipmentCategory
            {
                CategoryCode = code,
                CategoryName = code + " category",
                IsActive = true
            };
            db.EquipmentCategories.Add(category);
            await db.SaveChangesAsync();
            return category.CategoryId;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Shared_images_survive_another_items_replacement_or_removal(bool removeImage)
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var categoryId = await SeedCategoryAsync(site, "IMG-SHARED");
            const string reference = "https://test-project.supabase.co/storage/v1/object/equipment-images/items/shared.png";
            var first = await SeedItemAsync(site, categoryId, "SHARED-A", reference);
            await SeedItemAsync(site, categoryId, "SHARED-B", reference);
            var token = await site.Token($"/Categories/EditEquipment?id={first}");
            using var form = EditForm(token, first, categoryId, "SHARED-A",
                imageBytes: removeImage ? null : PngBytes, removeImage: removeImage);

            Assert.Equal(HttpStatusCode.Redirect,
                (await site.Client.PostAsync("/Categories/EditEquipment", form)).StatusCode);
            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.Equal(reference, (await db.EquipmentItems.SingleAsync(item => item.ItemCode == "SHARED-B")).ImageUrl);
            Assert.False(site.ImageStorage.WasDeleted(reference));
        }

        private static async Task<long> SeedItemAsync(
            CategoryHttpSite site,
            long categoryId,
            string itemCode,
            string imageUrl)
        {
            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var item = new EquipmentItem
            {
                CategoryId = categoryId,
                ItemCode = itemCode,
                ItemName = itemCode + " item",
                Location = "Test store",
                ConditionStatus = DomainValues.ReturnConditions.Good,
                ItemStatus = DomainValues.EquipmentStatuses.Available,
                ImageUrl = imageUrl
            };
            db.EquipmentItems.Add(item);
            await db.SaveChangesAsync();
            return item.EquipmentId;
        }

        private static MultipartFormDataContent CreateForm(
            string token,
            long categoryId,
            string itemCode,
            byte[] imageBytes = null,
            string fileName = "photo.png",
            string contentType = "image/png")
        {
            var form = new MultipartFormDataContent();
            AddField(form, "__RequestVerificationToken", token);
            AddField(form, "CategoryId", categoryId.ToString());
            AddField(form, "ItemCode", itemCode);
            AddField(form, "ItemName", "Uploaded item");
            AddField(form, "Location", "Test store");
            AddField(form, "ConditionStatus", DomainValues.ReturnConditions.Good);
            AddField(form, "ItemStatus", DomainValues.EquipmentStatuses.Available);
            AddFile(form, imageBytes, fileName, contentType);
            return form;
        }

        private static MultipartFormDataContent EditForm(
            string token,
            long equipmentId,
            long categoryId,
            string itemCode,
            byte[] imageBytes = null,
            bool removeImage = false,
            string fileName = "photo.png",
            string contentType = "image/png")
        {
            var form = new MultipartFormDataContent();
            AddField(form, "__RequestVerificationToken", token);
            AddField(form, "EquipmentId", equipmentId.ToString());
            AddField(form, "CategoryId", categoryId.ToString());
            AddField(form, "ItemCode", itemCode);
            AddField(form, "ItemName", "Updated item");
            AddField(form, "Location", "Test store");
            AddField(form, "ConditionStatus", DomainValues.ReturnConditions.Good);
            AddField(form, "ItemStatus", DomainValues.EquipmentStatuses.Available);
            if (removeImage)
            {
                AddField(form, "removeImage", "true");
            }

            AddFile(form, imageBytes, fileName, contentType);
            return form;
        }

        private static void AddField(
            MultipartFormDataContent form,
            string name,
            string value)
        {
            form.Add(new StringContent(value), name);
        }

        private static void AddFile(
            MultipartFormDataContent form,
            byte[] imageBytes,
            string fileName,
            string contentType)
        {
            if (imageBytes == null)
            {
                return;
            }

            var file = new ByteArrayContent(imageBytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, "imageFile", fileName);
        }
    }
}
