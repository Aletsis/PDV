using MediatR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.SystemConfiguration.Queries.ListBackups;

public record ListBackupsQuery(string? BackupDirectory) : IRequest<List<BackupDto>>;

public class BackupDto
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
}

public class ListBackupsQueryHandler : IRequestHandler<ListBackupsQuery, List<BackupDto>>
{
    public Task<List<BackupDto>> Handle(ListBackupsQuery request, CancellationToken cancellationToken)
    {
        string backupDir = request.BackupDirectory?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(backupDir))
        {
            backupDir = Path.Combine(Directory.GetCurrentDirectory(), "backups");
        }

        if (!Directory.Exists(backupDir))
        {
            return Task.FromResult(new List<BackupDto>());
        }

        var dirInfo = new DirectoryInfo(backupDir);
        var files = dirInfo.GetFiles("*.db")
            .Select(f => new BackupDto
            {
                FileName = f.Name,
                FullPath = f.FullName,
                CreatedAt = f.CreationTime,
                SizeBytes = f.Length
            })
            .OrderByDescending(f => f.CreatedAt)
            .ToList();

        return Task.FromResult(files);
    }
}
