using System.ComponentModel;

namespace SPA.Models.NonDBModels
{
    public class MChangePassword
    {
        [PasswordPropertyText]
        public string OldPassword { get; set; }

        [PasswordPropertyText]
        public string NewPassword { get; set; }
    }
}
