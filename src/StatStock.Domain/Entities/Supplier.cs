using System.ComponentModel.DataAnnotations;

namespace StatStock.Domain.Entities;

public class Supplier
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Supplier name is required")]
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Contact person is required")]
    [StringLength(200, ErrorMessage = "Contact person cannot exceed 200 characters")]
    public string Contact { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [StringLength(200, ErrorMessage = "Email cannot exceed 200 characters")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Invalid phone number format")]
    [RegularExpression(@"^[\d\s\-\+\(\)]+$", ErrorMessage = "Phone number can only contain digits, spaces, +, -, and parentheses")]
    [StringLength(50, ErrorMessage = "Phone number cannot exceed 50 characters")]
    public string Phone { get; set; } = string.Empty;
    
    [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
    public string Address { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
