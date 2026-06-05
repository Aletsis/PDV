using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Common.Interfaces;

public interface IRealTimeSyncNotifier
{
    Task NotifyEntityChangedAsync(string entityName, CancellationToken cancellationToken);
}
