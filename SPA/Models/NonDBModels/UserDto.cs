namespace SPA.Models.NonDBModels
{
    public class UserDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
        public int TenantId { get; set; }
        public string? ProfilePicturePath { get; set; }
    }
}
