using MediatR;

namespace Otus.PaymentSystem.Identity.Application.Commands.BlockUser;

/// <summary>Блокировка пользователя администратором.</summary>
public record BlockUserCommand(Guid TargetUserId, Guid BlockedBy) : IRequest;
