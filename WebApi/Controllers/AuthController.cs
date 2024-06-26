﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebApi.Data;
using WebApi.Models;


namespace WebApi.Controllers
{
    [ApiController]
    [Route("/auth")]
    public class AuthController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration, DataContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.InitialChar == request.initial);

            if (user == null)
            {
                return Unauthorized(new { Message = "invalid username" });
            }

            if (!BCrypt.Net.BCrypt.Verify(request.password, user.Password))
            {
                return Unauthorized(new { Message = "invalid password" });
            }

            if (user.IsActive == false)
            {
                return Unauthorized(new { Message = "Your account is inactive." });
            }

            var token = GenerateJwtToken(user);

            return Ok(new
            {
                Message = "Success",
                Data = new
                {
                    id = user.Id,
                    Token = token,
                    name = user.Name,
                    initials = user.InitialChar,
                    is_admin = user.IsAdmin,
                    is_active = user.IsActive,
                }
            });
        }

        // PUT: api/Users/5
        [Authorize]
        [HttpPut("password")]
        public async Task<IActionResult> UpdatePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                if (identity == null)
                {
                    return BadRequest(new { Message = "Invalid token." });
                }

                var userInitial = identity.FindFirst(ClaimTypes.Name)?.Value;
                var user = await _context.Users.FirstOrDefaultAsync(u => u.InitialChar == userInitial);

                if (user == null)
                {
                    return NotFound(new { Message = "User not found." });
                }

                if (!BCrypt.Net.BCrypt.Verify(request.old_password, user.Password))
                {
                    return Unauthorized(new { Message = "Old password is incorrect." });
                }

                if (request.new_password != request.confirm_new_password)
                {
                    return BadRequest(new { Message = "New passwords do not match." });
                }

                user.Password = BCrypt.Net.BCrypt.HashPassword(request.new_password);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Message = "Success",
                    Data = new
                    {
                        id = user.Id,
                        name = user.Name,
                        initials = user.InitialChar,
                        is_admin = user.IsAdmin,
                        is_active = user.IsActive,
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return StatusCode(500, new { Message = "Internal Server Error", Data = ex.Message });
            }
        }

        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.InitialChar),
                new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
            };

            var token = new JwtSecurityToken(
                _configuration["JwtSettings:Issuer"],
                _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:ExpirationInMinutes"])),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string initial { get; set; }
        public string password { get; set; }
    }
    public class ChangePasswordRequest
    {
        public string old_password { get; set; }
        public string new_password { get; set; }
        public string confirm_new_password { get; set;}
    }
}
