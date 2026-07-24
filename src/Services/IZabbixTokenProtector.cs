namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Host-provided protection for customer Zabbix API tokens.
    /// Implementations must produce a durable protected value suitable for
    /// database storage and must never log the clear token.
    /// </summary>
    public interface IZabbixTokenProtector
    {
        string Protect(string token);

        string Unprotect(string protectedToken);
    }
}
