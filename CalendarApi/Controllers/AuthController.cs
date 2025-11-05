using CalendarApi.Contracts;
using CalendarApi.Dtos;
using CalendarApi.Models;
using CalendarApi.Services;
using CalendarApi.Stores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Session;

namespace CalendarApi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService authService;
        private readonly QrCodeService qrCodeService;
        private readonly SessionStore sessionStore;
        private readonly SignalRTokenService tokenService;
        private readonly ILogger<AuthController> logger;
        private readonly IConfiguration config;

        public AuthController(IAuthService authService, QrCodeService qrCodeService, ILogger<AuthController> logger, SessionStore sessionStore, SignalRTokenService tokenService, IConfiguration config)
        {
            this.authService = authService;
            this.qrCodeService = qrCodeService;
            this.logger = logger;
            this.sessionStore = sessionStore;
            this.tokenService = tokenService;
            this.config = config;
        }
        [HttpPost]
        public async Task<IActionResult> CreateSession()
        {
            var session = await sessionStore.CreateSessionAsync();

            return Ok(new { session.SessionId });
        }

        [HttpGet("qr")]
        public async Task<IActionResult> GetQrCode(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return BadRequest("Missing sessionId");
            var authUrl = authService.GetAuthorizationUrl(sessionId);
            var qrBytes = qrCodeService.GenerateQrCode(authUrl);

            return File(qrBytes, "image/png");
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return BadRequest("Invalid OAuth callback parameters.");
            try
            {
                var session = await authService.ExchangeCodeForTokenAsync(state, code);
                if (session == null)
                {
                    logger.LogWarning("Session not found or expired for state {State}", state);
                    return Unauthorized("Session expired or invalid.");
                }
                   
                var frontendUrl = config["Urls:Frontend"];
                logger.LogInformation("OAuth callback successful for session {SessionId}, redirecting to frontend.", session.SessionId);
                return Redirect($"{frontendUrl}/select-calendar?session={session.SessionId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during OAuth callback for session {SessionId}", state);
                return StatusCode(500, "Internal server error during authentication.");
            }
        }

        /// <summary>
        /// Returns the current state of a session (for polling).
        /// </summary>
        [HttpGet("session/{sessionId}")]
        public async Task<IActionResult> GetSessionState(string sessionId)
        {
            var (found, session) = await sessionStore.TryGetSessionAsync(sessionId);
            if (!found || session?.State == SessionState.Expired)
            {
                return Unauthorized();
            }

            return Ok(new
            {
                session.SessionId,
                session.State,
                session.UserName,
                session.TokenExpiresAt
            });
        }
        [HttpGet("signalr-token")]
        public async Task<IActionResult> GetSignalRToken([FromQuery] string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return BadRequest("Missing sessionId");
            var (found, session) = await sessionStore.TryGetSessionAsync(sessionId);
            if (!found || session?.State == SessionState.Expired)
            {
                return Unauthorized();
            }

            // Optionally verify session.State is acceptable (pending/authenticated)
            // e.g. if(session.State == SessionState.Expired) return BadRequest("Session expired");

            // Create token valid for short period (e.g. 2 minutes)
            var token = tokenService.CreateToken(sessionId, TimeSpan.FromMinutes(2));
            return Ok(new { token });
        }
    }
}
