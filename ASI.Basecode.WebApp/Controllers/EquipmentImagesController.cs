using ASI.Basecode.WebApp.Services.EquipmentImages;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public sealed class EquipmentImagesController : Controller
    {
        private readonly IEquipmentImageStorage _storage;

        public EquipmentImagesController(IEquipmentImageStorage storage)
        {
            _storage = storage;
        }

        [HttpGet("/equipment-images/{fileName}")]
        public IActionResult Get(string fileName)
        {
            return Serve(fileName, staged: false);
        }

        [HttpGet("/equipment-images/staged/{fileName}")]
        [Authorize(
            Roles = DomainValues.Roles.Administrator,
            Policy = DomainValues.Permissions.EquipmentManage)]
        public IActionResult GetStaged(string fileName)
        {
            return Serve(fileName, staged: true);
        }

        private IActionResult Serve(string fileName, bool staged)
        {
            string path;
            try
            {
                path = staged
                    ? _storage.GetSafeStagedFilePath(fileName)
                    : _storage.GetSafeFilePath(fileName);
            }
            catch (ArgumentException)
            {
                return NotFound();
            }

            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return PhysicalFile(path, _storage.GetContentType(fileName));
        }
    }
}
