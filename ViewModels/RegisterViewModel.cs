using System;
using System.ComponentModel.DataAnnotations;

namespace reportmangerv2.ViewModels;

public class RegisterViewModel
{
    public required string Name { get; set; }
    [EmailAddress]
    [Required]
    //ends with @egyptpost.org
   // [RegularExpression(@"^[a-zA-Z0-9._%+-]+@egyptpost\.org$", ErrorMessage = "The email must be a valid email address ending with @egyptpost.org.")]

    public required string Email { get; set; }
    [DataType(DataType.Password)]
    [Required]
    [Display(Name = "New password")]
    [MinLength(6, ErrorMessage = "The new password must be at least 6 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,}$", ErrorMessage = "The new password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character.")]

    public required string Password { get; set; }
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare("Password",
    ErrorMessage = "The password and confirmation password do not match.")]
    public required string ConfirmPassword { get; set; }

}
