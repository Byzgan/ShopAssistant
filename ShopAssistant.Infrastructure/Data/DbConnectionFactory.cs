namespace ShopAssistant.Infrastructure.Data;

using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ShopAssistant.Contracts.Interfaces.Data;

/// <summary>
/// Default implementation of <see cref="IDbConnectionFactory"/> for creating database connections.
/// Currently supports Microsoft SQL Server.
/// </summary>
public class DbConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public IDbConnection CreateConnection(string connectionStringName)
    {
        var connectionString = _configuration.GetConnectionString(connectionStringName) ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' not found in configuration.");
        
        return new SqlConnection(connectionString);
    }
}