using MediatR;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.SystemConfiguration.Commands.RestoreBackup;

public record RestoreBackupCommand(string BackupFilePath) : IRequest;

public class RestoreBackupCommandHandler : IRequestHandler<RestoreBackupCommand>
{
    public Task Handle(RestoreBackupCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BackupFilePath) || !File.Exists(request.BackupFilePath))
            throw new FileNotFoundException("El archivo de respaldo seleccionado no existe.");

        // 1. Resolve active DB path
        string dbPath = Path.Combine(Directory.GetCurrentDirectory(), "pdv.db");
        if (!File.Exists(dbPath))
        {
            dbPath = Path.Combine(AppContext.BaseDirectory, "pdv.db");
        }

        // 2. Clear SQLite connections pool to release file locks!
        // Using reflection to call Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools()
        // so we don't need a direct dependency on Microsoft.Data.Sqlite in this project
        // if it isn't referenced, though it is likely referenced.
        try
        {
            var sqliteConnType = Type.GetType("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite");
            if (sqliteConnType != null)
            {
                var clearMethod = sqliteConnType.GetMethod("ClearAllPools", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (clearMethod != null)
                {
                    clearMethod.Invoke(null, null);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Nota: No se pudo limpiar la piscina de conexiones SQLite via reflexión: {ex.Message}");
        }

        // Give SQLite a millisecond to release locks
        Thread.Sleep(200);

        // 3. Create a safety backup of the active database before overwriting, in case anything goes wrong!
        if (File.Exists(dbPath))
        {
            string safetyPath = dbPath + ".safety_before_restore";
            try
            {
                File.Copy(dbPath, safetyPath, overwrite: true);
            }
            catch (Exception ex)
            {
                throw new IOException($"No se pudo realizar una copia de seguridad temporal antes de restaurar: {ex.Message}");
            }
        }

        // 4. Overwrite active db with backup
        try
        {
            File.Copy(request.BackupFilePath, dbPath, overwrite: true);
        }
        catch (Exception ex)
        {
            throw new IOException($"Error crítico al sobreescribir la base de datos con el respaldo: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}
