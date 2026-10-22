using MediatR;

namespace Otus.PaymentSystem.Identity.Application.Commands.UnblockUser;

/// <summary>Разблокировка пользователя администратором.</summary>
public record UnblockUserCommand(Guid TargetUserId, Guid UnblockedBy) : IRequest;
