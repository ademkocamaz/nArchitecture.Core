﻿using System.Collections.Immutable;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NArchitecture.Core.Security.Encryption;
using NArchitecture.Core.Security.Entities;
using NArchitecture.Core.Security.Extensions;

namespace NArchitecture.Core.Security.JWT;

public class JwtHelper<TUserId, TOperationClaimId> : ITokenHelper<TUserId, TOperationClaimId>
{
    public IConfiguration Configuration { get; }
    private readonly TokenOptions _tokenOptions;
    private DateTime _accessTokenExpiration;

    public JwtHelper(IConfiguration configuration)
    {
        Configuration = configuration;
        const string configurationSection = "TokenOptions";
        _tokenOptions =
            Configuration.GetSection(configurationSection).Get<TokenOptions>()
            ?? throw new NoNullAllowedException($"\"{configurationSection}\" section cannot found in configuration.");
    }

    public AccessToken CreateToken(User<TUserId> user, IList<OperationClaim<TOperationClaimId>> operationClaims)
    {
        _accessTokenExpiration = DateTime.Now.AddMinutes(_tokenOptions.AccessTokenExpiration);
        SecurityKey securityKey = SecurityKeyHelper.CreateSecurityKey(_tokenOptions.SecurityKey);
        SigningCredentials signingCredentials = SigningCredentialsHelper.CreateSigningCredentials(securityKey);
        JwtSecurityToken jwt = CreateJwtSecurityToken(_tokenOptions, user, signingCredentials, operationClaims);
        JwtSecurityTokenHandler jwtSecurityTokenHandler = new();
        string? token = jwtSecurityTokenHandler.WriteToken(jwt);

        return new AccessToken { Token = token, ExpirationDate = _accessTokenExpiration };
    }

    public RefreshToken<TUserId> CreateRefreshToken(User<TUserId> user, string ipAddress)
    {
        RefreshToken<TUserId> refreshToken =
            new()
            {
                UserId = user.Id,
                Token = randomRefreshToken(),
                ExpiresDate = DateTime.UtcNow.AddDays(7),
                CreatedByIp = ipAddress
            };

        return refreshToken;
    }

    public JwtSecurityToken CreateJwtSecurityToken(
        TokenOptions tokenOptions,
        User<TUserId> user,
        SigningCredentials signingCredentials,
        IList<OperationClaim<TOperationClaimId>> operationClaims
    )
    {
        JwtSecurityToken jwt =
            new(
                tokenOptions.Issuer,
                tokenOptions.Audience,
                expires: _accessTokenExpiration,
                notBefore: DateTime.Now,
                claims: SetClaims(user, operationClaims),
                signingCredentials: signingCredentials
            );
        return jwt;
    }

    protected virtual IEnumerable<Claim> SetClaims(
        User<TUserId> user,
        IList<OperationClaim<TOperationClaimId>> operationClaims
    )
    {
        List<Claim> claims = [];
        claims.AddNameIdentifier(user!.Id!.ToString()!);
        claims.AddEmail(user.Email);
        claims.AddRoles(operationClaims.Select(c => c.Name).ToArray());
        return claims.ToImmutableList();
    }

    private string randomRefreshToken()
    {
        byte[] numberByte = new byte[32];
        using var random = RandomNumberGenerator.Create();
        random.GetBytes(numberByte);
        return Convert.ToBase64String(numberByte);
    }
}
