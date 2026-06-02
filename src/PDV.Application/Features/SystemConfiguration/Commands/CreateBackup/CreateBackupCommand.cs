using MediatR;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.SystemConfiguration.Commands.CreateBackup;

public record CreateBackupCommand(string? BackupDirectory) : IRequest<string>;

public class CreateBackupCommandHandler : IRequestHandler<CreateBackupCommand, string>
{
    public async Task<string> Handle(CreateBackupCommand request, CancellationToken cancellationToken)
    {
        // 1. Locate db path
        string dbPath = Path.Combine(Directory.GetCurrentDirectory(), "pdv.db");
        if (!File.Exists(dbPath))
        {
            dbPath = Path.Combine(AppContext.BaseDirectory, "pdv.db");
        }

        if (!File.Exists(dbPath))
            throw new FileNotFoundException("No se encontró el archivo de base de datos local 'pdv.db'.");

        // 2. Resolve backup directory
        string backupDir = request.BackupDirectory?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(backupDir))
        {
            backupDir = Path.Combine(Directory.GetCurrentDirectory(), "backups");
        }

        if (!Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        // 3. Generate filename
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupFileName = $"pdv_backup_{timestamp}.db";
        string targetPath = Path.Combine(backupDir, backupFileName);

        // 4. Safe copy
        // For SQLite, we can copy the file directly. File.Copy is fine.
        File.Copy(dbPath, targetPath, overwrite: true);

        return targetPath;
    }
}
