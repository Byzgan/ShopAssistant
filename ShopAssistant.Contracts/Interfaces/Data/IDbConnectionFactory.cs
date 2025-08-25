namespace ShopAssistant.Contracts.Interfaces.Data;

using System.Data;


/// <summary>
/// Defines a factory for creating <see cref="IDbConnection"/> instances
/// for different named connection strings. This abstraction allows infrastructure code
/// to work with multiple databases and providers in a decoupled manner.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates and returns a new <see cref="IDbConnection"/> using the specified connection string name.
    /// </summary>
    /// <param name="connectionStringName">The name of the connection string as configured in the application settings.</param>
    /// <returns>A new, unopened <see cref="IDbConnection"/> instance.</returns>
    IDbConnection CreateConnection(string connectionStringName);
}