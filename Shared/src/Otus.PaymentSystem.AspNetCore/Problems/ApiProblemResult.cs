using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Otus.PaymentSystem.AspNetCore.Problems;

/// <summary>IResult, который пишет ProblemDetails в едином формате.</summary>
public sealed class ApiProblemResult(ProblemDetails problem) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext) => ApiProblems.WriteAsync(httpContext, problem);
}
