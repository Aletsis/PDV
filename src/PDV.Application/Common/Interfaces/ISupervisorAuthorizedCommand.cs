namespace PDV.Application.Common.Interfaces;

public interface ISupervisorAuthorizedCommand
{
    string? SupervisorUsername { get; init; }
    string? SupervisorPassword { get; init; }
}

public interface ISupervisorAuthorizedTarget
{
    string? AuthorizedByUserId { get; set; }
}
