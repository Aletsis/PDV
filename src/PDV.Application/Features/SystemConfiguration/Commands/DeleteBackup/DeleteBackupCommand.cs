using MediatR;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.SystemConfiguration.Commands.DeleteBackup;

public record DeleteBackupCommand(string BackupFilePath) : IRequest;

public class DeleteBackupCommandHandler : IRequestHandler<DeleteBackupCommand>
{
    public Task Handle(DeleteBackupCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BackupFilePath) || !File.Exists(request.BackupFilePath))
            throw new FileNotFoundException("El archivo de respaldo seleccionado no existe.");

        File.Delete(request.BackupFilePath);

        return Task.CompletedTask;
    }
}
