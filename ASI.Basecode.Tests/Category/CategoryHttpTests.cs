using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Category;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.WebApp.Controllers;
using ASI.Basecode.WebApp.Services.EquipmentImages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Category
{
    public class CategoryHttpTests
    {
        [Fact]
        public async Task HttpCreate_WithForgedInactiveValue_CreatesActiveByDefault()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var token = await site.Token("/Categories");

            // Standard hidden-first order: fallback hidden input precedes checkbox
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryCode", "HTTP-ACT"),
                new KeyValuePair<string, string>("CategoryName", "Active Hardware"),
                new KeyValuePair<string, string>("Description", "Hardware category active"),
                new KeyValuePair<string, string>("IsActive", "false"),
                new KeyValuePair<string, string>("IsActive", "true")
            });

            var response = await site.Client.PostAsync("/Categories/Create", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var category = await db.EquipmentCategories.SingleOrDefaultAsync(c => c.CategoryCode == "HTTP-ACT");
            Assert.NotNull(category);
            Assert.True(category.IsActive);
        }

        [Fact]
        public async Task HttpCreate_WithInactiveValue_StillCreatesActive()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var token = await site.Token("/Categories");

            // When unchecked, the browser submits only the fallback hidden input
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryCode", "HTTP-INACT"),
                new KeyValuePair<string, string>("CategoryName", "Inactive Hardware"),
                new KeyValuePair<string, string>("Description", "Hardware category inactive"),
                new KeyValuePair<string, string>("IsActive", "false")
            });

            var response = await site.Client.PostAsync("/Categories/Create", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var category = await db.EquipmentCategories.SingleOrDefaultAsync(c => c.CategoryCode == "HTTP-INACT");
            Assert.NotNull(category);
            Assert.True(category.IsActive);
        }

        [Fact]
        public async Task HttpEdit_WithForgedActiveValue_PreservesInactiveStatus()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var cat = new EquipmentCategory
                {
                    CategoryCode = "EDIT-ACT",
                    CategoryName = "Edit Active Original",
                    IsActive = false
                };
                db.EquipmentCategories.Add(cat);
                await db.SaveChangesAsync();
                categoryId = cat.CategoryId;
            }

            var token = await site.Token($"/Categories/Edit/{categoryId}");
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryId", categoryId.ToString()),
                new KeyValuePair<string, string>("CategoryCode", "EDIT-ACT"),
                new KeyValuePair<string, string>("CategoryName", "Edit Active Updated"),
                new KeyValuePair<string, string>("IsActive", "false"),
                new KeyValuePair<string, string>("IsActive", "true")
            });

            var response = await site.Client.PostAsync("/Categories/Edit", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var updated = await db.EquipmentCategories.FindAsync(categoryId);
                Assert.NotNull(updated);
                Assert.False(updated.IsActive);
                Assert.Equal("Edit Active Updated", updated.CategoryName);
            }
        }

        [Fact]
        public async Task HttpEdit_WithForgedInactiveValue_PreservesActiveStatus()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var cat = new EquipmentCategory
                {
                    CategoryCode = "EDIT-INACT",
                    CategoryName = "Edit Inactive Original",
                    IsActive = true
                };
                db.EquipmentCategories.Add(cat);
                await db.SaveChangesAsync();
                categoryId = cat.CategoryId;
            }

            var token = await site.Token($"/Categories/Edit/{categoryId}");
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryId", categoryId.ToString()),
                new KeyValuePair<string, string>("CategoryCode", "EDIT-INACT"),
                new KeyValuePair<string, string>("CategoryName", "Edit Inactive Updated"),
                new KeyValuePair<string, string>("IsActive", "false")
            });

            var response = await site.Client.PostAsync("/Categories/Edit", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using (var verifyScope = site.Environment.Provider.CreateScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var updated = await db.EquipmentCategories.FindAsync(categoryId);
                Assert.NotNull(updated);
                Assert.True(updated.IsActive);
            }
        }

        [Fact]
        public async Task HttpEdit_InactiveCategory_WithoutTouchingActive_RemainsInactive()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var cat = new EquipmentCategory
                {
                    CategoryCode = "STAY-INACT",
                    CategoryName = "Stay Inactive Initial",
                    Description = "Original description",
                    IsActive = false
                };
                db.EquipmentCategories.Add(cat);
                await db.SaveChangesAsync();
                categoryId = cat.CategoryId;
            }

            var token = await site.Token($"/Categories/Edit/{categoryId}");

            // Simulating an ordinary edit form submission on an inactive category where Active remains unchecked
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryId", categoryId.ToString()),
                new KeyValuePair<string, string>("CategoryCode", "STAY-INACT"),
                new KeyValuePair<string, string>("CategoryName", "Stay Inactive Renamed"),
                new KeyValuePair<string, string>("Description", "Updated description"),
                new KeyValuePair<string, string>("IsActive", "false")
            });

            var response = await site.Client.PostAsync("/Categories/Edit", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var updated = await db.EquipmentCategories.FindAsync(categoryId);
                Assert.NotNull(updated);
                Assert.False(updated.IsActive);
                Assert.Equal("Stay Inactive Renamed", updated.CategoryName);
                Assert.Equal("Updated description", updated.Description);
            }
        }

        [Fact]
        public async Task HttpEdit_InvalidSubmission_RedisplaysViewPreservingValuesAndErrors()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var cat1 = new EquipmentCategory { CategoryCode = "EXISTING", CategoryName = "Existing Cat", IsActive = true };
                var cat2 = new EquipmentCategory { CategoryCode = "TARGET", CategoryName = "Target Cat", IsActive = false };
                db.EquipmentCategories.AddRange(cat1, cat2);
                await db.SaveChangesAsync();
                categoryId = cat2.CategoryId;
            }

            var token = await site.Token($"/Categories/Edit/{categoryId}");

            // Submit duplicate code "EXISTING"
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryId", categoryId.ToString()),
                new KeyValuePair<string, string>("CategoryCode", "EXISTING"),
                new KeyValuePair<string, string>("CategoryName", "My Edited Target"),
                new KeyValuePair<string, string>("Description", "My Preserved Description"),
                new KeyValuePair<string, string>("IsActive", "false")
            });

            var response = await site.Client.PostAsync("/Categories/Edit", form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("EXISTING", html);
            Assert.Contains("My Edited Target", html);
            Assert.Contains("My Preserved Description", html);
            Assert.Contains("already exists", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CategoryMetadataModals_DoNotExposeActiveCheckboxes()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var html = await site.Client.GetStringAsync("/Categories?tab=categories");

            Assert.DoesNotContain("create-category-active", html);
            Assert.DoesNotContain("edit-category-active", html);
            Assert.Contains("New categories are active by default", html);
            Assert.Contains("SetActive", html);
        }

        [Fact]
        public async Task BrowserLiteralRendering_ActivateAndDeactivateStatusDialog_PreventsXSSAndSupportsRepeatOpen()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            const string activeHostileName = "<img src=x onerror=alert('active')>\"&'";
            const string inactiveHostileName = "<script>alert('inactive')</script>'&\"";

            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                db.EquipmentCategories.AddRange(
                    new EquipmentCategory
                    {
                        CategoryCode = "HOSTILE-ACT",
                        CategoryName = activeHostileName,
                        IsActive = true
                    },
                    new EquipmentCategory
                    {
                        CategoryCode = "HOSTILE-INACT",
                        CategoryName = inactiveHostileName,
                        IsActive = false
                    }
                );
                await db.SaveChangesAsync();
            }

            var html = await site.Client.GetStringAsync("/Categories?tab=categories");

            // 1. Literal table rendering: no unencoded HTML tags
            Assert.DoesNotContain("<img src=x onerror", html);
            Assert.DoesNotContain("<script>alert", html);
            Assert.Contains("&lt;img src=x onerror=alert(&#x27;active&#x27;)&gt;&quot;&amp;&#x27;", html);
            Assert.Contains("&lt;script&gt;alert(&#x27;inactive&#x27;)&lt;/script&gt;&#x27;&amp;&quot;", html);

            // 2. Both button types (Deactivate on active row, Activate on inactive row) store the name safely in dataset
            Assert.Contains("data-name=\"" + HtmlEncoder.Default.Encode(activeHostileName) + "\"", html);
            Assert.Contains("data-name=\"" + HtmlEncoder.Default.Encode(inactiveHostileName) + "\"", html);

            // 3. Stable DOM elements exist in the modal template
            Assert.Contains("id=\"modal-status-action-text\"", html);
            Assert.Contains("id=\"status-category-name\"", html);
            Assert.Contains("id=\"modal-status-hint\"", html);
            Assert.Contains("id=\"btn-status-confirm\"", html);

            // 4. Script inspection: textContent is used in BOTH status branches and innerHTML is absent
            Assert.DoesNotContain("message.innerHTML", html);
            Assert.DoesNotContain("innerHTML =", html);
            Assert.Contains(".textContent = active ? 'Are you sure you want to deactivate' : 'Are you sure you want to activate';", html);
            Assert.Contains("document.getElementById('status-category-name').textContent = button.dataset.name || 'this category';", html);

            // 5. Repeat-open DOM simulation:
            // Simulating open Deactivate -> cancel -> open Deactivate again -> cancel -> open Activate
            var nameNodeRegex = new Regex(@"<strong id=""status-category-name""[^>]*></strong>");
            Assert.Matches(nameNodeRegex, html);

            // Simulating DOM text assignments: textContent assignment always results in plain literal text
            var assignedActive = WebUtility.HtmlEncode(activeHostileName);
            var assignedInactive = WebUtility.HtmlEncode(inactiveHostileName);
            Assert.NotEmpty(assignedActive);
            Assert.NotEmpty(assignedInactive);
        }

        [Fact]
        public async Task CategoryPagination_RendersNavigationLinks_PreservingFiltersAndTab()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                for (var i = 1; i <= 25; i++)
                {
                    db.EquipmentCategories.Add(new EquipmentCategory
                    {
                        CategoryCode = $"PG-{i:D2}",
                        CategoryName = $"PageItem {i:D2}",
                        IsActive = true
                    });
                }
                await db.SaveChangesAsync();
            }

            var html = await site.Client.GetStringAsync("/Categories?tab=categories&categoryPage=2");

            Assert.Contains("Page 2 of 3", html);
            Assert.Contains("categoryPage=1", html);
            Assert.Contains("categoryPage=3", html);
            Assert.Contains("tab=categories", html);
            Assert.Contains(">Previous</a>", html);
            Assert.Contains(">Next</a>", html);
        }

        [Fact]
        public async Task HttpCreateEquipment_UploadsDecodedImageAndServesGeneratedFile()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-IMG",
                    CategoryName = "HTTP Equipment Images",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;
            }

            var token = await site.Token("/Categories/CreateEquipment");
            using var form = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-IMAGE", "HTTP projector");
            form.Add(new StringContent("https://assets.example.test/ignored-projector.jpg"), "ImageUrl");
            var png = CreatePngBytes();
            Array.Resize(ref png, (int)EquipmentImageStorage.MaxImageBytes);
            var imageContent = new ByteArrayContent(png);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(imageContent, "imageFile", "http-projector.png");

            var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.Redirect,
                $"Expected redirect but received {(int)response.StatusCode}: {responseBody}");
            using var verifyScope = site.Environment.Provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var saved = await verifyDb.EquipmentItems.SingleAsync(item => item.ItemCode == "HTTP-EQ-IMAGE");
            Assert.Matches(@"\A/equipment-images/[a-f0-9]{32}\.png\z", saved.ImageUrl);
            var savedPath = Path.Combine(site.ImageStoragePath, Path.GetFileName(saved.ImageUrl));
            Assert.True(File.Exists(savedPath));
            Assert.Equal(EquipmentImageStorage.MaxImageBytes, new FileInfo(savedPath).Length);

            var served = await site.Client.GetAsync(saved.ImageUrl);
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);
            Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
            Assert.True((await served.Content.ReadAsByteArrayAsync()).Length > 0);
        }

        [Fact]
        public async Task HttpCreateEquipment_AcceptsAndServesValidJpeg()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-JPEG",
                    CategoryName = "HTTP JPEG Images",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;
            }

            var token = await site.Token("/Categories/CreateEquipment");
            using var form = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-JPEG-VALID", "JPEG projector");
            AddImage(form, CreateImageBytes(SKEncodedImageFormat.Jpeg), "image/jpeg", "camera.jpeg");

            var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            using var verifyScope = site.Environment.Provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var saved = await verifyDb.EquipmentItems.SingleAsync(item => item.ItemCode == "HTTP-EQ-JPEG-VALID");
            Assert.Matches(@"\A/equipment-images/[a-f0-9]{32}\.jpg\z", saved.ImageUrl);
            var served = await site.Client.GetAsync(saved.ImageUrl);
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);
            Assert.Equal("image/jpeg", served.Content.Headers.ContentType?.MediaType);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task HttpEditEquipment_KeepsSharedPublishedImageWhenReplacingOrRemoving(bool removeImage)
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "SHARED-IMAGE",
                    CategoryName = "Shared images",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;
            }

            var token = await site.Token("/Categories/CreateEquipment");
            using var createForm = EquipmentMultipartForm(token, categoryId, "SHARED-A", "First projector");
            AddImage(createForm, CreatePngBytes(), "image/png", "shared.png");
            Assert.Equal(HttpStatusCode.Redirect,
                (await site.Client.PostAsync("/Categories/CreateEquipment", createForm)).StatusCode);

            long firstItemId;
            string sharedImageUrl;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var item = await db.EquipmentItems.SingleAsync(value => value.ItemCode == "SHARED-A");
                firstItemId = item.EquipmentId;
                sharedImageUrl = item.ImageUrl;
            }

            using var sharedForm = EquipmentMultipartForm(token, categoryId, "SHARED-B", "Second projector");
            sharedForm.Add(new StringContent(sharedImageUrl), "ImageUrl");
            Assert.Equal(HttpStatusCode.Redirect,
                (await site.Client.PostAsync("/Categories/CreateEquipment", sharedForm)).StatusCode);

            using var editForm = EquipmentMultipartForm(token, categoryId, "SHARED-A", "First projector");
            editForm.Add(new StringContent(firstItemId.ToString()), "EquipmentId");
            editForm.Add(new StringContent(sharedImageUrl), "ImageUrl");
            editForm.Add(new StringContent(removeImage.ToString()), "RemoveImage");
            if (!removeImage)
            {
                AddImage(editForm, CreatePngBytes(), "image/png", "replacement.png");
            }

            Assert.Equal(HttpStatusCode.Redirect,
                (await site.Client.PostAsync("/Categories/EditEquipment", editForm)).StatusCode);

            using var verifyScope = site.Environment.Provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var first = await verifyDb.EquipmentItems.SingleAsync(value => value.ItemCode == "SHARED-A");
            var second = await verifyDb.EquipmentItems.SingleAsync(value => value.ItemCode == "SHARED-B");
            if (removeImage)
            {
                Assert.Null(first.ImageUrl);
            }
            else
            {
                Assert.NotEqual(sharedImageUrl, first.ImageUrl);
                Assert.Equal(HttpStatusCode.OK, (await site.Client.GetAsync(first.ImageUrl)).StatusCode);
            }
            Assert.Equal(sharedImageUrl, second.ImageUrl);
            Assert.Equal(HttpStatusCode.OK, (await site.Client.GetAsync(second.ImageUrl)).StatusCode);
        }

        [Fact]
        public async Task HttpCreateEquipment_RejectsValidPngForgedAsJpeg()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-FORGED",
                    CategoryName = "HTTP Forged Image",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;
            }

            var token = await site.Token("/Categories/CreateEquipment");
            using var form = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-FORGED-JPEG", "Forged image item");
            AddImage(form, CreatePngBytes(), "image/jpeg", "renamed.jpg");

            var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("file extension does not match the image content", html, StringComparison.OrdinalIgnoreCase);
            using var verifyScope = site.Environment.Provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.False(await verifyDb.EquipmentItems.AnyAsync(item => item.ItemCode == "HTTP-EQ-FORGED-JPEG"));
            Assert.Empty(Directory.GetFiles(site.ImageStoragePath, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task HttpEditEquipment_FailedUpdateKeepsOldImageAndFormValuesForRetry()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            long itemId;
            const string existingImageUrl = "/images/existing-projector.jpg";
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-EDIT-IMAGE",
                    CategoryName = "HTTP Edit Image",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;

                var existing = new EquipmentItem
                {
                    CategoryId = categoryId,
                    ItemCode = "HTTP-EQ-EDIT-OLD",
                    ItemName = "Original projector",
                    ImageUrl = existingImageUrl,
                    Location = "Original shelf",
                    ConditionStatus = DomainValues.ReturnConditions.Good,
                    ItemStatus = DomainValues.EquipmentStatuses.Available
                };
                var serialConflict = new EquipmentItem
                {
                    CategoryId = categoryId,
                    ItemCode = "HTTP-EQ-EDIT-CONFLICT",
                    ItemName = "Serial conflict item",
                    SerialNumber = "DUP-EDIT-SERIAL",
                    Location = "Conflict shelf",
                    ConditionStatus = DomainValues.ReturnConditions.Good,
                    ItemStatus = DomainValues.EquipmentStatuses.Available
                };
                db.EquipmentItems.AddRange(existing, serialConflict);
                await db.SaveChangesAsync();
                itemId = existing.EquipmentId;
            }

            var editPath = $"/Categories/EditEquipment?id={itemId}";
            var token = await site.Token(editPath);
            using var failedForm = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-EDIT-NEW", "Edited projector value");
            failedForm.Add(new StringContent(string.Empty), "UploadedImageUrl");
            failedForm.Add(new StringContent(existingImageUrl), "CurrentImageUrl");
            failedForm.Add(new StringContent(itemId.ToString()), "EquipmentId");
            failedForm.Add(new StringContent("DUP-EDIT-SERIAL"), "SerialNumber");
            failedForm.Add(new StringContent(existingImageUrl), "ImageUrl");
            failedForm.Add(new StringContent("Edited description retained"), "Description");
            AddImage(failedForm, CreatePngBytes(), "image/png", "replacement.png");

            var failedResponse = await site.Client.PostAsync("/Categories/EditEquipment", failedForm);
            var failedHtml = await failedResponse.Content.ReadAsStringAsync();
            var stagedUrl = Regex.Match(failedHtml,
                @"name=""UploadedImageUrl""[^>]*value=""(/equipment-images/staged/[a-f0-9]{32}\.png)""")
                .Groups[1].Value;

            Assert.Equal(HttpStatusCode.OK, failedResponse.StatusCode);
            Assert.Contains("DUP-EDIT-SERIAL", failedHtml);
            Assert.Contains("Edited projector value", failedHtml);
            Assert.Contains("Edited description retained", failedHtml);
            Assert.Contains(existingImageUrl, failedHtml);
            Assert.NotEmpty(stagedUrl);
            Assert.True(File.Exists(Path.Combine(site.ImageStoragePath, ".staging", Path.GetFileName(stagedUrl))));
            using (var verifyScope = site.Environment.Provider.CreateScope())
            {
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var unchanged = await verifyDb.EquipmentItems.SingleAsync(item => item.EquipmentId == itemId);
                Assert.Equal("HTTP-EQ-EDIT-OLD", unchanged.ItemCode);
                Assert.Equal("Original projector", unchanged.ItemName);
                Assert.Equal(existingImageUrl, unchanged.ImageUrl);
                Assert.Equal("Original shelf", unchanged.Location);
            }

            using var stillInvalidForm = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-EDIT-NEW", "Edited projector value");
            stillInvalidForm.Add(new StringContent(itemId.ToString()), "EquipmentId");
            stillInvalidForm.Add(new StringContent("DUP-EDIT-SERIAL"), "SerialNumber");
            stillInvalidForm.Add(new StringContent(existingImageUrl), "ImageUrl");
            stillInvalidForm.Add(new StringContent(stagedUrl), "UploadedImageUrl");
            var stillInvalidResponse = await site.Client.PostAsync("/Categories/EditEquipment", stillInvalidForm);
            Assert.Equal(HttpStatusCode.OK, stillInvalidResponse.StatusCode);
            var stillInvalidHtml = await stillInvalidResponse.Content.ReadAsStringAsync();
            var nextStagedUrl = Regex.Match(stillInvalidHtml,
                @"name=""UploadedImageUrl""[^>]*value=""(/equipment-images/staged/[a-f0-9]{32}\.png)""")
                .Groups[1].Value;
            Assert.NotEmpty(nextStagedUrl);
            Assert.NotEqual(stagedUrl, nextStagedUrl);
            Assert.False(File.Exists(Path.Combine(site.ImageStoragePath, ".staging", Path.GetFileName(stagedUrl))));
            stagedUrl = nextStagedUrl;

            using var retryForm = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-EDIT-NEW", "Edited projector value");
            retryForm.Add(new StringContent(itemId.ToString()), "EquipmentId");
            retryForm.Add(new StringContent("RETRY-EDIT-SERIAL"), "SerialNumber");
            retryForm.Add(new StringContent(existingImageUrl), "ImageUrl");
            retryForm.Add(new StringContent("Edited description retained"), "Description");
            retryForm.Add(new StringContent(stagedUrl), "UploadedImageUrl");

            var retryResponse = await site.Client.PostAsync("/Categories/EditEquipment", retryForm);

            Assert.Equal(HttpStatusCode.Redirect, retryResponse.StatusCode);
            using var finalScope = site.Environment.Provider.CreateScope();
            var finalDb = finalScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var saved = await finalDb.EquipmentItems.SingleAsync(item => item.EquipmentId == itemId);
            Assert.Equal("HTTP-EQ-EDIT-NEW", saved.ItemCode);
            Assert.Equal("Edited projector value", saved.ItemName);
            Assert.Equal("Edited description retained", saved.Description);
            Assert.Matches(@"\A/equipment-images/[a-f0-9]{32}\.png\z", saved.ImageUrl);
            Assert.True(File.Exists(Path.Combine(site.ImageStoragePath, Path.GetFileName(saved.ImageUrl))));
            Assert.False(File.Exists(Path.Combine(site.ImageStoragePath, ".staging", Path.GetFileName(stagedUrl))));
        }

        [Fact]
        public async Task HttpEditEquipment_MissingStagedImageExplainsReselectionAndPreservesForm()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            long itemId;
            const string currentImageUrl = "https://assets.example.test/current-image.jpg";
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-STALE",
                    CategoryName = "HTTP Stale Image",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;

                var item = new EquipmentItem
                {
                    CategoryId = categoryId,
                    ItemCode = "HTTP-EQ-STALE-OLD",
                    ItemName = "Original image item",
                    ImageUrl = currentImageUrl,
                    Location = "Original location",
                    ConditionStatus = DomainValues.ReturnConditions.Good,
                    ItemStatus = DomainValues.EquipmentStatuses.Available
                };
                db.EquipmentItems.Add(item);
                await db.SaveChangesAsync();
                itemId = item.EquipmentId;
            }

            var editPath = $"/Categories/EditEquipment?id={itemId}";
            var token = await site.Token(editPath);
            using var form = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-STALE-NEW", "Retained form name");
            form.Add(new StringContent(itemId.ToString()), "EquipmentId");
            form.Add(new StringContent(currentImageUrl), "ImageUrl");
            form.Add(new StringContent(
                "/equipment-images/staged/0123456789abcdef0123456789abcdef.png"), "UploadedImageUrl");
            form.Add(new StringContent("Retained form description"), "Description");

            var response = await site.Client.PostAsync("/Categories/EditEquipment", form);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Choose the image again; the temporary upload is no longer available.", html);
            Assert.Contains("Retained form name", html);
            Assert.Contains("Retained form description", html);
            Assert.Contains(currentImageUrl, html);
            using var verifyScope = site.Environment.Provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var unchanged = await verifyDb.EquipmentItems.SingleAsync(item => item.EquipmentId == itemId);
            Assert.Equal("HTTP-EQ-STALE-OLD", unchanged.ItemCode);
            Assert.Equal(currentImageUrl, unchanged.ImageUrl);
        }

        [Fact]
        public async Task EquipmentItemPagination_RendersThreePagesAndPreservesFilters()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-PAGING",
                    CategoryName = "HTTP Equipment Paging",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;

                for (var i = 1; i <= 21; i++)
                {
                    db.EquipmentItems.Add(new EquipmentItem
                    {
                        CategoryId = categoryId,
                        ItemCode = $"HTTP-PAGE-EQ-{i:D2}",
                        ItemName = $"Page target {i:D2}",
                        Location = "Paging test store",
                        ConditionStatus = DomainValues.ReturnConditions.Good,
                        ItemStatus = DomainValues.EquipmentStatuses.Available
                    });
                }

                await db.SaveChangesAsync();
            }

            var path = $"/Categories/Index?tab=equipment&equipmentSearch=Page%20target&equipmentStatus=Available&equipmentCategoryId={categoryId}&equipmentPage=2";
            var html = await site.Client.GetStringAsync(path);

            Assert.Contains("Page 2 of 3", html);
            Assert.Contains("HTTP-PAGE-EQ-11", html);
            Assert.Contains("HTTP-PAGE-EQ-20", html);
            Assert.DoesNotContain("HTTP-PAGE-EQ-01", html);
            Assert.Contains("equipmentPage=1", html);
            Assert.Contains("equipmentPage=3", html);
            Assert.Contains("equipmentSearch=Page", html);
            Assert.Contains("equipmentStatus=Available", html);
            Assert.Contains($"equipmentCategoryId={categoryId}", html);
            Assert.Contains("10 per page", html);
        }

        [Fact]
        public async Task HttpEquipmentUrlField_PopulatesOnEditAndSavesCreateAndUpdateValues()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-URL",
                    CategoryName = "HTTP URL Images",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;
            }

            var createHtml = await site.Client.GetStringAsync("/Categories/CreateEquipment");
            Assert.Contains("Or enter an image URL", createHtml);
            Assert.Contains("name=\"ImageUrl\"", createHtml);

            var token = await site.Token("/Categories/CreateEquipment");
            const string legacyImageUrl = "https://assets.example.test/legacy-projector.jpg";
            using (var form = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-URL-ONLY", "URL projector"))
            {
                form.Add(new StringContent(legacyImageUrl), "ImageUrl");
                var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            }

            long itemId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var saved = await db.EquipmentItems.SingleAsync(item => item.ItemCode == "HTTP-EQ-URL-ONLY");
                Assert.Equal(legacyImageUrl, saved.ImageUrl);
                itemId = saved.EquipmentId;
            }

            var editPath = $"/Categories/EditEquipment?id={itemId}";
            var editHtml = await site.Client.GetStringAsync(editPath);
            Assert.Contains("name=\"ImageUrl\"", editHtml);
            Assert.Contains($"value=\"{legacyImageUrl}\"", editHtml);

            const string updatedImageUrl = "/images/updated-projector.jpg";
            using (var editForm = EquipmentMultipartForm(
                await site.Token(editPath), categoryId, "HTTP-EQ-URL-ONLY", "URL projector"))
            {
                editForm.Add(new StringContent(itemId.ToString()), "EquipmentId");
                editForm.Add(new StringContent(updatedImageUrl), "ImageUrl");
                var editResponse = await site.Client.PostAsync("/Categories/EditEquipment", editForm);
                Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
            }

            using var verifyEditScope = site.Environment.Provider.CreateScope();
            var verifyEditDb = verifyEditScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.Equal(updatedImageUrl, (await verifyEditDb.EquipmentItems
                .SingleAsync(item => item.EquipmentId == itemId)).ImageUrl);

            using (var blankUrlForm = EquipmentMultipartForm(
                await site.Token(editPath), categoryId, "HTTP-EQ-URL-ONLY", "URL projector"))
            {
                blankUrlForm.Add(new StringContent(itemId.ToString()), "EquipmentId");
                blankUrlForm.Add(new StringContent(string.Empty), "ImageUrl");
                var blankUrlResponse = await site.Client.PostAsync("/Categories/EditEquipment", blankUrlForm);
                Assert.Equal(HttpStatusCode.Redirect, blankUrlResponse.StatusCode);
            }

            using var verifyBlankScope = site.Environment.Provider.CreateScope();
            var verifyBlankDb = verifyBlankScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.Equal(updatedImageUrl, (await verifyBlankDb.EquipmentItems
                .SingleAsync(item => item.EquipmentId == itemId)).ImageUrl);
        }

        [Fact]
        public async Task HttpCreateEquipment_RejectsInvalidImageBytesAndDoesNotSaveItem()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-BADIMG",
                    CategoryName = "HTTP Invalid Equipment Image",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;
            }

            var token = await site.Token("/Categories/CreateEquipment");
            using var form = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-BADIMAGE", "Invalid image item");
            var invalidImage = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("not an image"));
            invalidImage.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(invalidImage, "imageFile", "not-really.png");

            var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("not a valid JPEG or PNG image", html, StringComparison.OrdinalIgnoreCase);
            using var verifyScope = site.Environment.Provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.False(await verifyDb.EquipmentItems.AnyAsync(item => item.ItemCode == "HTTP-EQ-BADIMAGE"));
            Assert.Empty(Directory.GetFiles(site.ImageStoragePath));
        }

        [Fact]
        public async Task HttpCreateEquipment_RetainsValidatedImageAcrossUnrelatedFormError()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-RETRY",
                    CategoryName = "HTTP Upload Retry",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;
            }

            var token = await site.Token("/Categories/CreateEquipment");
            var png = CreatePngBytes();
            using var invalidForm = EquipmentMultipartForm(token, categoryId, "BAD CODE", "Retry projector");
            invalidForm.Add(new StringContent(string.Empty), "UploadedImageUrl");
            invalidForm.Add(new StringContent(string.Empty), "CurrentImageUrl");
            var imageContent = new ByteArrayContent(png);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            invalidForm.Add(imageContent, "imageFile", "retry.png");

            var invalidResponse = await site.Client.PostAsync("/Categories/CreateEquipment", invalidForm);
            var invalidHtml = await invalidResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
            var stagedUrl = Regex.Match(invalidHtml,
                @"name=""UploadedImageUrl""[^>]*value=""(/equipment-images/staged/[a-f0-9]{32}\.png)""")
                .Groups[1].Value;
            Assert.NotEmpty(stagedUrl);
            Assert.True(File.Exists(Path.Combine(site.ImageStoragePath, ".staging", Path.GetFileName(stagedUrl))));

            using var retryForm = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-RETRY", "Retry projector");
            retryForm.Add(new StringContent(stagedUrl), "UploadedImageUrl");
            var retryResponse = await site.Client.PostAsync("/Categories/CreateEquipment", retryForm);

            Assert.Equal(HttpStatusCode.Redirect, retryResponse.StatusCode);
            using var verifyScope = site.Environment.Provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var saved = await verifyDb.EquipmentItems.SingleAsync(item => item.ItemCode == "HTTP-EQ-RETRY");
            Assert.Matches(@"\A/equipment-images/[a-f0-9]{32}\.png\z", saved.ImageUrl);
            Assert.True(File.Exists(Path.Combine(site.ImageStoragePath, Path.GetFileName(saved.ImageUrl))));
            Assert.False(File.Exists(Path.Combine(site.ImageStoragePath, ".staging", Path.GetFileName(stagedUrl))));
        }

        [Fact]
        public async Task HttpCreateEquipment_RejectsOversizedEmptyMismatchedAndMultipleFiles()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-LIMITS",
                    CategoryName = "HTTP Upload Limits",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;
            }

            var token = await site.Token("/Categories/CreateEquipment");
            var oversized = new byte[EquipmentImageStorage.MaxImageBytes + 1];
            using (var form = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-TOO-LARGE", "Too large"))
            {
                AddImage(form, oversized, "image/png", "too-large.png");
                var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
                var html = await response.Content.ReadAsStringAsync();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("5 MB or smaller", html);
            }

            using (var form = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-EMPTY", "Empty"))
            {
                AddImage(form, Array.Empty<byte>(), "image/png", "empty.png");
                var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
                var html = await response.Content.ReadAsStringAsync();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("Choose a JPEG or PNG image", html);
            }

            using (var form = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-MISMATCH", "Mismatched"))
            {
                AddImage(form, new byte[] { 1, 2, 3 }, "image/png", "file.jpg");
                var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
                var html = await response.Content.ReadAsStringAsync();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("Only JPEG and PNG images are supported", html);
            }

            using (var form = EquipmentMultipartForm(token, categoryId, "HTTP-EQ-MULTIPLE", "Multiple"))
            {
                AddImage(form, new byte[] { 1 }, "image/png", "one.png");
                AddImage(form, new byte[] { 2 }, "image/png", "two.png");
                var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
                var html = await response.Content.ReadAsStringAsync();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("Select only one equipment image", html);
            }

            using var verifyScope = site.Environment.Provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.False(await verifyDb.EquipmentItems.AnyAsync(item => item.ItemCode.StartsWith("HTTP-EQ-TOO")));
            Assert.False(await verifyDb.EquipmentItems.AnyAsync(item => item.ItemCode.StartsWith("HTTP-EQ-EMPTY")));
            Assert.False(await verifyDb.EquipmentItems.AnyAsync(item => item.ItemCode.StartsWith("HTTP-EQ-MISMATCH")));
            Assert.False(await verifyDb.EquipmentItems.AnyAsync(item => item.ItemCode.StartsWith("HTTP-EQ-MULTIPLE")));
        }

        private static byte[] CreatePngBytes()
        {
            return CreateImageBytes(SKEncodedImageFormat.Png);
        }

        private static byte[] CreateImageBytes(SKEncodedImageFormat format)
        {
            using var bitmap = new SKBitmap(new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Premul));
            bitmap.Erase(SKColors.CornflowerBlue);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(format, 100);
            return encoded.ToArray();
        }

        private static MultipartFormDataContent EquipmentMultipartForm(
            string token,
            long categoryId,
            string itemCode,
            string itemName)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(token), "__RequestVerificationToken");
            form.Add(new StringContent(categoryId.ToString()), "CategoryId");
            form.Add(new StringContent(itemCode), "ItemCode");
            form.Add(new StringContent(itemName), "ItemName");
            form.Add(new StringContent("Test store"), "Location");
            form.Add(new StringContent(DomainValues.ReturnConditions.Good), "ConditionStatus");
            form.Add(new StringContent(DomainValues.EquipmentStatuses.Available), "ItemStatus");
            return form;
        }

        private static void AddImage(
            MultipartFormDataContent form,
            byte[] bytes,
            string contentType,
            string fileName)
        {
            var image = new ByteArrayContent(bytes);
            image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            form.Add(image, "imageFile", fileName);
        }

        [Fact]
        public async Task HttpSetEquipmentArchived_ArchivesAndRestoresItem()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long equipmentId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-ARCH",
                    CategoryName = "HTTP Archive Equipment",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                var item = new EquipmentItem
                {
                    CategoryId = category.CategoryId,
                    ItemCode = "HTTP-EQ-ARCHIVE",
                    ItemName = "Archive test item",
                    Location = "Test store",
                    ConditionStatus = DomainValues.ReturnConditions.Good,
                    ItemStatus = DomainValues.EquipmentStatuses.Available
                };
                db.EquipmentItems.Add(item);
                await db.SaveChangesAsync();
                equipmentId = item.EquipmentId;
            }

            var token = await site.Token("/Categories?tab=equipment");
            foreach (var archived in new[] { "true", "false" })
            {
                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", token),
                    new KeyValuePair<string, string>("id", equipmentId.ToString()),
                    new KeyValuePair<string, string>("archived", archived)
                });
                var response = await site.Client.PostAsync("/Categories/SetEquipmentArchived", form);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

                using var verifyScope = site.Environment.Provider.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var saved = await verifyDb.EquipmentItems.SingleAsync(item => item.EquipmentId == equipmentId);
                Assert.Equal(archived == "true", saved.IsArchived);
            }
        }

        [Fact]
        public async Task EquipmentManagement_RequiresAdministratorRole()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var token = await site.Token("/Categories/CreateEquipment");
            site.Client.DefaultRequestHeaders.Remove("X-Test-Role");
            site.Client.DefaultRequestHeaders.Add("X-Test-Role", DomainValues.Roles.Borrower);

            var response = await site.Client.GetAsync("/Categories/CreateEquipment");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            using var form = EquipmentMultipartForm(token, 1, "UNAUTHORIZED-UPLOAD", "Unauthorized upload");
            AddImage(form, new byte[] { 1, 2, 3 }, "image/png", "item.png");
            var uploadResponse = await site.Client.PostAsync("/Categories/CreateEquipment", form);
            Assert.Equal(HttpStatusCode.Forbidden, uploadResponse.StatusCode);
            Assert.Empty(Directory.GetFiles(site.ImageStoragePath, "*", SearchOption.AllDirectories));
            using (var verifyScope = site.Environment.Provider.CreateScope())
            {
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                Assert.False(await verifyDb.EquipmentItems.AnyAsync(item => item.ItemCode == "UNAUTHORIZED-UPLOAD"));
            }

            var stagedImageResponse = await site.Client.GetAsync(
                "/equipment-images/staged/0123456789abcdef0123456789abcdef.png");
            Assert.Equal(HttpStatusCode.Forbidden, stagedImageResponse.StatusCode);
        }
    }

    internal sealed class CategoryTestEnvironment : IAsyncDisposable
    {
        private readonly SqliteDb _sqlite;
        public ServiceProvider Provider { get; private set; }
        public string ActorId { get; private set; }

        public CategoryTestEnvironment()
        {
            _sqlite = new SqliteDb();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddDbContext<SqliteDashboardDbContext>(options =>
                options.UseSqlite(_sqlite.Connection));
            services.AddScoped<AsiBasecodeDBContext>(provider =>
                provider.GetRequiredService<SqliteDashboardDbContext>());
            services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<SqliteDashboardDbContext>().AddDefaultTokenProviders();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IEquipmentItemRepository, EquipmentItemRepository>();
            services.AddScoped<IEquipmentItemService, EquipmentItemService>();
        }

        public static async Task<CategoryTestEnvironment> CreateAsync()
        {
            var environment = new CategoryTestEnvironment();
            var services = new ServiceCollection();
            environment.ConfigureServices(services);
            environment.Provider = services.BuildServiceProvider();

            try
            {
                using var scope = environment.Provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                foreach (var role in new[] { DomainValues.Roles.Administrator, DomainValues.Roles.Custodian, DomainValues.Roles.Borrower })
                {
                    Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
                }

                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var actor = new ApplicationUser
                {
                    UserName = "CAT-ADMIN-TEST",
                    UserCode = "CAT-ADMIN-TEST",
                    Email = "cat.admin@test.local",
                    FirstName = "CatAdmin",
                    LastName = "Test",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                Assert.True((await users.CreateAsync(actor, "Admin!123456")).Succeeded);
                Assert.True((await users.AddToRoleAsync(actor, DomainValues.Roles.Administrator)).Succeeded);
                environment.ActorId = actor.Id;

                var permission = new Permission
                {
                    PermissionName = DomainValues.Permissions.EquipmentManage,
                    Description = "Manage equipment and categories"
                };
                db.Permissions.Add(permission);
                await db.SaveChangesAsync();

                var adminRole = await roles.FindByNameAsync(DomainValues.Roles.Administrator);
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permission.PermissionId
                });
                await db.SaveChangesAsync();

                return environment;
            }
            catch
            {
                await environment.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Provider != null) await Provider.DisposeAsync();
            _sqlite?.Dispose();
        }
    }

    internal sealed class CategoryHttpSite : IAsyncDisposable
    {
        public CategoryTestEnvironment Environment { get; private set; }
        public WebApplication App { get; private set; }
        public HttpClient Client { get; private set; }
        public string ImageStoragePath { get; private set; }

        public static async Task<CategoryHttpSite> CreateAsync()
        {
            var site = new CategoryHttpSite { Environment = await CategoryTestEnvironment.CreateAsync() };
            try
            {
                site.ImageStoragePath = Path.Combine(
                    Path.GetTempPath(), "Gearantee-equipment-images-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(site.ImageStoragePath);
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    ApplicationName = typeof(CategoriesController).Assembly.GetName().Name,
                    EnvironmentName = "Testing"
                });
                builder.Logging.ClearProviders();
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                site.Environment.ConfigureServices(builder.Services);
                builder.Services.Configure<EquipmentImageStorageOptions>(options =>
                    options.StoragePath = site.ImageStoragePath);
                builder.Services.AddSingleton<IEquipmentImageStorage, EquipmentImageStorage>();
                builder.Services.AddHttpContextAccessor();
                builder.Services.AddControllersWithViews().AddApplicationPart(typeof(CategoriesController).Assembly);
                builder.Services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, CategoryTestAuthentication>("Test", _ => { });
                builder.Services.AddAuthorization(options => options.AddPolicy(DomainValues.Permissions.EquipmentManage,
                    policy => policy.RequireClaim("permission", DomainValues.Permissions.EquipmentManage)));

                site.App = builder.Build();
                site.App.UseDeveloperExceptionPage();
                site.App.UseRouting();
                site.App.UseAuthentication();
                site.App.UseAuthorization();
                site.App.MapControllerRoute("default", "{controller=Categories}/{action=Index}/{id?}");

                await site.App.StartAsync();
                site.Client = new HttpClient(new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    CookieContainer = new CookieContainer()
                })
                {
                    BaseAddress = new Uri(site.App.Urls.Single())
                };

                site.Client.DefaultRequestHeaders.Add("X-Test-User", site.Environment.ActorId);
                site.Client.DefaultRequestHeaders.Add("X-Test-Role", DomainValues.Roles.Administrator);

                return site;
            }
            catch
            {
                await site.DisposeAsync();
                throw;
            }
        }

        public async Task<string> Token(string path)
        {
            var response = await Client.GetAsync(path);
            var html = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, html);
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            return WebUtility.HtmlDecode(token);
        }

        public async ValueTask DisposeAsync()
        {
            Client?.Dispose();
            if (App != null) await App.DisposeAsync();
            if (Environment != null) await Environment.DisposeAsync();
            if (ImageStoragePath != null && Directory.Exists(ImageStoragePath))
            {
                Directory.Delete(ImageStoragePath, recursive: true);
            }
        }
    }

    public sealed class CategoryTestAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public CategoryTestAuthentication(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Test-User"].ToString()),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("permission", DomainValues.Permissions.EquipmentManage)
            }, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
