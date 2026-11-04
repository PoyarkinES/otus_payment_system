using MediatR;
using Otus.PaymentSystem.Identity.Application.Commands.Auth;
using Otus.PaymentSystem.Identity.Application.Interfaces;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Domain.Errors;
using Otus.PaymentSystem.Identity.Domain.Exceptions;

namespace Otus.PaymentSystem.Identity.Application.Commands.Refresh
{
    public class RefreshCommandHandler(
        IRefreshTokenFactory factory,
        IRefreshTokenRepository repository,
        IUserRepository users,
        ITokenService tokens)
        : IRequestHandler<RefreshCommand, AuthTokens>
    {

        public async Task<AuthTokens> Handle(RefreshCommand request, CancellationToken cancellationToken)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            string hash = factory.Hash(request.RefreshToken);
            RefreshToken token = await repository.GetByHashAsync(hash, cancellationToken)
                                 ?? throw new DomainException("Сессия недействительна.", DomainError.Unauthorized);

            if (token.RevokedAt is not null)
            {
                // Reuse: предъявлен токен, который уже был заменён ротацией.
                if (token.WasRotated)
                    await RevokeChainAsync(token, cancellationToken);   // отзыв + SaveChanges внутри

                throw new DomainException("Сессия недействительна.", DomainError.Unauthorized);
            }

            token.EnsureActive(now);

            User user = await users.GetByIdAsync(token.UserId, cancellationToken)
                        ?? throw new DomainException("Сессия недействительна.", DomainError.Unauthorized);

            if (user.Status == UserStatus.Blocked)
            {
                token.Revoke();
                await repository.SaveChangesAsync(cancellationToken);
                throw new DomainException("Пользователь заблокирован.", DomainError.Unauthorized);
            }

            TokenResult access = tokens.GenerateToken(user);
            IssuedRefreshToken issued = factory.Issue();
            RefreshToken next = RefreshToken.Issues(user.Id, issued.Hash, issued.ExpiresAt);

            token.Revoke(next.Id);
            await repository.AddAsync(next, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);

            return new AuthTokens(access.AccessToken, access.ExpiresAt, issued.Raw, issued.ExpiresAt);
        }

        private async Task RevokeChainAsync(RefreshToken reused, CancellationToken ct)
        {
            RefreshToken current = reused;
            for (var step = 0; step < 100 && current.ReplacedByTokenId is Guid nextId; step++)
            {
                RefreshToken? next = await repository.GetByIdAsync(nextId, ct);
                if (next is null)
                    break;

                next.Revoke();
                current = next;
            }

            await repository.SaveChangesAsync(ct);
        }
    }
}
