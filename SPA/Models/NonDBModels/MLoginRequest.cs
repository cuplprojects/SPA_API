using System.ComponentModel;

namespace SPA.Models.NonDBModels
{
    public class MLoginRequest
    {
        public string Email { get; set; }
        [PasswordPropertyText]
        public string Password { get; set; }

    }
}
