using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using reportmangerv2.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace reportmangerv2.Controllers
{
[Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = _userManager.Users.ToList().Where(u=>u.Id!=User.FindFirstValue(ClaimTypes.NameIdentifier ));
            var adminUserIds = (await _userManager.GetUsersInRoleAsync("Admin"))
                .Select(admin => admin.Id)
                .ToHashSet();

            ViewBag.ErrorMessage=TempData["ErrorMessage"];
            ViewBag.SuccessMessage=TempData["SuccessMessage"];
            ViewBag.AdminUserIds = adminUserIds;
            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> Activate(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsActive = true;
                await _userManager.UpdateAsync(user);
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Deactivate(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
        
            if (user != null && !(user.Id==User.FindFirstValue(ClaimTypes.NameIdentifier )))
            {
                user.IsActive = false;
                await _userManager.UpdateAsync(user);
            }
            TempData["ErrorMessage"]="Can't Deactivate your Account";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, "NewPassword123!");
                if (!resetResult.Succeeded)
                {
                    TempData["ErrorMessage"] = string.Join(" ", resetResult.Errors.Select(error => error.Description));
                    return RedirectToAction("Index");
                }

                user.MustChangePassword = true;
                await _userManager.UpdateAsync(user);
       
            ViewBag.MessageSuccess="Password Reset Succeeded";
            TempData["SuccessMessage"]="Password Reset Succeeded";
            return RedirectToAction("Index");
            }
            TempData["ErrorMessage"]="User Not Found";
            return RedirectToAction("Index");
        }
        //set user as an administrator
        [HttpPost]
        public async Task<IActionResult> SetUserAsAdmin(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.AddToRoleAsync(user, "Admin");
                TempData["SuccessMessage"]="User set as Admin";
                return RedirectToAction("Index");
            }
            TempData["ErrorMessage"]="User Not Found";
            return RedirectToAction("Index");
        }
    }
}
