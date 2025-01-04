using SPA.Models.NonDBModels;
using SPA.Data;
using SPA.Encryptions;
using SPA.Models;
using SPA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using NuGet.Protocol.Plugins;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SPA.Encryptions;
using SPA.Data;
using SPA;


namespace KeyGen.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly FirstDbContext _firstContext;
        private readonly SecondDbContext _secondContext;

        public LoginController(IConfiguration configuration, FirstDbContext firstContext, SecondDbContext secondContext)
        {
            _configuration = configuration;
            _firstContext = firstContext;
            _secondContext = secondContext;
        }


        private string GenerateToken(UserAuth user)
        {
            var securitykey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securitykey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserId.ToString()), // Assuming UserID is the unique identifier
                new Claim("AutoGenPass", user.AutogenPass.ToString()), // Convert bool to string
            };

            var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Issuer"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(360),
            signingCredentials: credentials
        );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [Authorize]
        [HttpPost("Extend")]
        public IActionResult Extend()
        {
            IActionResult response = Unauthorized();

            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var userauth = _firstContext.UserAuths.FirstOrDefault(i => i.UserId == userId);
                if (userauth != null)
                {
                    var token = GenerateToken(userauth);

                    //_logger.LogEvent($"User-Login Extended", "Login", userauth.UserID);
                    response = Ok(new { token = token, userauth.UserId, userauth.AutogenPass });

                }
            }
            return response;
        }

        //Login api 
        [AllowAnonymous]
        [HttpPost]
        public IActionResult Login([FromBody] MLoginRequest model)
        {
            var userAuth = GetUserAuth(_firstContext, model.Email) ?? GetUserAuth(_secondContext, model.Email);

            if (userAuth == null)
            {
                return NotFound("User not found");
            }

            if (!userAuth.IsActive)
            {
                return Unauthorized("User is inactive");
            }

            string hashedPassword = Sha256Hasher.ComputeSHA256Hash(model.Password);
            Console.WriteLine(hashedPassword);

            if (hashedPassword != userAuth.ua.Password)
            {
                return Unauthorized("Invalid password");
            }

            var token = GenerateToken(userAuth.ua);

            //_logger.LogEvent($"User Logged-in", "Login", userAuth.ua.UserID);
            return Ok(new { token = token, userAuth.ua.UserId, userAuth.ua.AutogenPass, role = userAuth.Role });
        }

        private dynamic GetUserAuth(DbContext context, string email)
        {
            return (from user in context.Set<User>()
                    join ua in context.Set<UserAuth>() on user.UserId equals ua.UserId
                    join ur in context.Set<User>() on user.UserId equals ur.UserId
                    join r in context.Set<Role>() on ur.RoleId equals r.RoleId
                    where user.Email == email
                    select new
                    {
                        ua,
                        user.IsActive,
                        Role = new
                        {
                            r.RoleId,
                            r.RoleName,
                            r.IsActive,
                            r.PermissionList,
                        }
                    }).FirstOrDefault();
        }

        [HttpPut("Forgotpassword")]
        public IActionResult ResetPassword([FromBody] MLoginRequest user)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var users = GetUser(_firstContext, user.Email) ?? GetUser(_secondContext, user.Email);

            if (users == null)
            {
                return NotFound("User not found");
            }

            var userauth = GetUserAuth(_firstContext, users.UserId) ?? GetUserAuth(_secondContext, users.UserId);

            if (userauth == null)
            {
                return NotFound("User Authentication Data Not Found");
            }

            string newPassword = PasswordGenerate.GeneratePassword();
            string hashedPassword = Sha256Hasher.ComputeSHA256Hash(newPassword);

            userauth.ua.Password = hashedPassword; // Access Password through ua
            userauth.ua.AutogenPass = true;

            // Save changes to the context where the user was found
            var context = userauth.Context;
            context.SaveChanges();

            string emailBody = $@"
    <div style=""text-align: center; background-color: #fff; padding: 20px; border-radius: 8px; box-shadow: 0 0 10px rgba(0, 0, 0, 0.1); border: 2px solid black; min-width: 200px; max-width: 300px; width: 100%; margin: 50px auto;"">
        <h2 style=""color: blue;"">New Login Credentials <hr /></h2>
        <p>
            <strong>Username:</strong><br /> {users.Email}
        </p>
        <p>
            <strong>Password:</strong><br /> {newPassword}
        </p>
        <p style=""color: #F00;"">
            Please change the password immediately after login.
        </p>
        <a href=""http://keygen.chandrakala.co.in/"" style=""display: inline-block; padding: 10px 20px; background-color: #007BFF; color: #fff; text-decoration: none; border-radius: 5px; margin-top: 15px;"">Login Here</a>
    </div>";
            string result = new EmailService(_secondContext, _configuration).SendEmail(users.Email, "Reset-Password", emailBody);

            return Ok();
        }

        private User GetUser(DbContext context, string email)
        {

            return context.Set<User>().FirstOrDefault(u => u.Email == email);
        }

        private dynamic GetUserAuth(DbContext context, int userId)
        {
            var auth = context.Set<UserAuth>().FirstOrDefault(ua => ua.UserId == userId);
            if (auth != null)
            {
                Console.WriteLine("Passed from" + $"{context}");
                return new { ua = auth, Context = context };
            }
            return null;
        }


        [HttpPut("Changepassword/{id}")]
        [Authorize]
        public IActionResult ChangePassword(int id, [FromBody] MChangePassword cred)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            string oldHashPass = Sha256Hasher.ComputeSHA256Hash(cred.OldPassword);
            var userauth = GetUserAuthById(_firstContext, id) ?? GetUserAuthById(_secondContext, id);

            if (userauth == null)
            {
                return NotFound("User Authentication Data Not Found");
            }

            if (userauth.ua.Password != oldHashPass)
            {
                return BadRequest("Existing Password Invalid");
            }

            string newPassword = cred.NewPassword;
            string hashedPassword = Sha256Hasher.ComputeSHA256Hash(newPassword);

            userauth.ua.Password = hashedPassword;

            // Save changes to the context where the user was found
            var context = userauth.Context;
            context.SaveChanges();

            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                //_logger.LogEvent("Password-Changed", "Login", userId);
            }

            return Ok();
        }

        private dynamic GetUserAuthById(DbContext context, int userId)
        {
            var auth = context.Set<UserAuth>().FirstOrDefault(ua => ua.UserId == userId);
            if (auth != null)
            {
                return new { ua = auth, Context = context };
            }
            return null;
        }
    }
}
