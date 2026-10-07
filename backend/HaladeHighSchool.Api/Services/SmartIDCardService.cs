using System.Security.Cryptography;
using System.Data;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Services;

public sealed class SmartIDCardService
{
    private readonly ApplicationDbContext _db;
    private readonly SmartIDTokenKeyProtector _tokenKeyProtector;

    public SmartIDCardService(
        ApplicationDbContext db,
        SmartIDTokenKeyProtector tokenKeyProtector)
    {
        _db = db;
        _tokenKeyProtector = tokenKeyProtector;
    }

    public async Task AddCardAsync(
        ApplicationUser user,
        string cardType,
        CancellationToken cancellationToken)
    {
        var cardUidParameter = new SqlParameter("@CardUID", SqlDbType.NVarChar, 100)
        {
            Direction = ParameterDirection.Output
        };
        await _db.Database.ExecuteSqlRawAsync(
            "EXEC dbo.GenerateSmartCardSerial @CardUID OUTPUT",
            new object[] { cardUidParameter },
            cancellationToken);
        var cardUid = cardUidParameter.Value as string
            ?? throw new InvalidOperationException("The Smart ID serial procedure did not return a card UID.");

        var tokenKey = RandomNumberGenerator.GetBytes(32);
        var protectedKey = _tokenKeyProtector.Protect(tokenKey);
        CryptographicOperations.ZeroMemory(tokenKey);

        _db.SmartCards.Add(new SmartCard
        {
            UserId = user.Id,
            CardUID = cardUid,
            QrTokenHash = protectedKey,
            CardType = cardType,
            Status = "ACTIVE",
            IssuedDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddYears(5),
            CreatedAt = DateTime.UtcNow
        });
    }

}
