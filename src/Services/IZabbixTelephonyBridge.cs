using Sufficit.Telephony;
using Sufficit.Telephony.CallDispatch;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Narrow integration boundary between the Zabbix package and the Sufficit
    /// telephony runtime. The host implements this bridge without coupling the
    /// gateway package to Sufficit.Standard.
    /// </summary>
    public interface IZabbixTelephonyBridge
    {
        bool IsTollFreeIdentifier(string identifier);

        Task<DirectInwardDialing?> GetIdentifierAsync(
            string identifier,
            CancellationToken cancellationToken = default);

        Task<CallDispatchConfiguration?> GetCallDispatchConfigurationAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<CallDispatchStartResult> StartCallDispatchAsync(
            CallDispatchRequest request,
            CancellationToken cancellationToken = default);
    }
}
