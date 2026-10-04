namespace Oism.Identity.Application.Auth.Login;

// Identifier là email hoặc số điện thoại.
public sealed record LoginCommand(string Identifier, string Password);
