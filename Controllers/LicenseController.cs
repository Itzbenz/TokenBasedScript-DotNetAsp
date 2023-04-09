using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TokenBasedScript.Data;
using TokenBasedScript.Models;
using TokenBasedScript.Services;

namespace TokenBasedScript.Controllers
{
    [Authorize(Policy = "LoggedIn")]
    public class LicenseController : Controller
    {
        private readonly MvcContext _context;
        private readonly IGiveUser _giveUser;
        public LicenseController(MvcContext context, IGiveUser giveUser)
        {
            _context = context;
            _giveUser = giveUser;
        }

        // GET: License
        public async Task<IActionResult> Index()
        {
            var user = await _giveUser.GetUser();
            if(user == null) return RedirectToAction("Index", "Home");
            List<License> list;
            if (user.IsAdmin)
            {
                //load user too
                list = await _context.Licenses.Include(x => x.User).ToListAsync();
            }
            else
            {
                list = await _context.Licenses.Where(x => x.User == user).ToListAsync();
            }
            return View(list);
        }

        // GET: License/Details/5
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            
            var license = await _context.Licenses
                .FirstOrDefaultAsync(m => m.Id == id);
            if (license == null)
            {
                return NotFound();
            }

            return View(license);
        }

        // GET: License/Create
        [Authorize(Policy = "AdminOnly")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: License/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create([Bind("Id,Code,ExternalId,Hwid,DeviceName,IpHash,DateModifiedIpHash,DateModifiedHwid,DateCreated,DateModified")] License license)
        {
            if (ModelState.IsValid)
            {
                _context.Add(license);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(license);
        }

        // GET: License/Edit/5
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var license = await _context.Licenses.FindAsync(id);
            if (license == null)
            {
                return NotFound();
            }
            return View(license);
        }

        // POST: License/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Code,ExternalId,Hwid,DeviceName,IpHash,DateModifiedIpHash,DateModifiedHwid,DateCreated,DateModified")] License license)
        {
            if (id != license.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(license);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LicenseExists(license.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(license);
        }

        // GET: License/Delete/5
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var license = await _context.Licenses
                .FirstOrDefaultAsync(m => m.Id == id);
            if (license == null)
            {
                return NotFound();
            }

            return View(license);
        }

        // POST: License/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var license = await _context.Licenses.FindAsync(id);
            _context.Licenses.Remove(license);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Route("/api/v1/validate")]
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Validate(string? hwid, string? code, string? deviceName)
        {
            if(hwid == null || code == null || deviceName == null)
            {
                return StatusCode(400, "Missing parameters");
            }
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "localhost";
            var sha256 = new System.Security.Cryptography.SHA256Managed();
            var ipHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(ip)));
            var license = await _context.Licenses.FirstOrDefaultAsync(x => x.Code == code);
            if (license == null)
            {
                return StatusCode(404, "License not found");
            }
            
            if (license.Hwid != null && license.Hwid != hwid && license.IpHash != ipHash)
            {
                return StatusCode(403, "HWID and IP mismatch");
            }
            
            if(license.Hwid != hwid)
            {
                license.Hwid = hwid;
                license.DateModifiedHwid = DateTime.Now;
                license.DeviceName = deviceName;
                _context.Update(license);
                await _context.SaveChangesAsync();
            }
            
            if(license.IpHash != ipHash)
            {
                license.IpHash = ipHash;
                license.DateModifiedIpHash = DateTime.Now;
                _context.Update(license);
                await _context.SaveChangesAsync();
            }
            
            return Ok(Guid.NewGuid().ToString());
        }

        private bool LicenseExists(int id)
        {
            return _context.Licenses.Any(e => e.Id == id);
        }
    }
}
