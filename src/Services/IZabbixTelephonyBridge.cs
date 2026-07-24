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

        /// <summary>
        /// Returns the current persisted state of one child Call Dispatch execution.
        /// Used by the Zabbix runtime to reconcile asynchronous telephone delivery.
        /// </summary>
        /// <param name="dispatchId">Child Call Dispatch execution identifier.</param>
        /// <param name="cancellationToken">Cancellation token for the persistence lookup.</param>
        /// <returns>The child execution when it exists; otherwise <see langword="null"/>.</returns>
        Task<CallDispatchExecution?> GetCallDispatchExecutionAsync(
            Guid dispatchId,
            CancellationToken cancellationToken = default);
    }
}
